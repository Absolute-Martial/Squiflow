using System.Text;
using System.Threading.Channels;
using Application.CoreApi.Authorization;
using Application.CoreApi.ImportExecution;
using Application.CoreApi.Storage;
using Application.Customers;
using Application.Customers.Postgres;
using Application.IdentityAccess.Postgres;
using Application.ObjectStorage;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class CustomerImportAutonomousPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
        .WithDatabase("application_import_executor_tests").WithUsername("postgres")
        .WithPassword("local-import-executor-test-only").Build();
    private readonly Guid _tenant = Guid.NewGuid(), _account = Guid.NewGuid();
    private string ConnectionString => _database.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await using (var identity = IdentityAccessPostgresMigrations.CreateContext(ConnectionString)) await identity.Database.MigrateAsync();
        await using (var tenancy = TenancyPostgresMigrations.CreateContext(ConnectionString)) await tenancy.Database.MigrateAsync();
        await using (var customers = CustomersPostgresMigrations.CreateContext(ConnectionString)) await customers.Database.MigrateAsync();
        await SqlAsync("""
            INSERT INTO identity_access.accounts(id,availability,created_at) VALUES(@account,1,clock_timestamp());
            INSERT INTO tenancy.tenants(id,display_name,availability,created_at) VALUES(@tenant,'Executor fixture',1,clock_timestamp());
            INSERT INTO tenancy.memberships(tenant_id,account_id,availability,created_at,revision,activated_at)
              VALUES(@tenant,@account,1,clock_timestamp(),1,clock_timestamp());
            INSERT INTO tenancy.tenant_authorization_state(tenant_id,revision,updated_at) VALUES(@tenant,12,clock_timestamp());
            CREATE ROLE application_import_executor_runtime LOGIN NOSUPERUSER NOBYPASSRLS PASSWORD 'local-import-runtime-test-only';
            GRANT USAGE ON SCHEMA customers,identity_access,tenancy TO application_import_executor_runtime;
            GRANT SELECT ON identity_access.accounts,tenancy.tenants,tenancy.memberships,tenancy.tenant_authorization_state
              TO application_import_executor_runtime;
            GRANT SELECT,INSERT ON customers.imports,customers.import_rows,customers.import_work,customers.individuals
              TO application_import_executor_runtime;
            GRANT UPDATE(requires_decision,duplicate_evidence,decision,mapping_customer_id,status,customer_id,error_code,error_message,processed_at,attempts)
              ON customers.import_rows TO application_import_executor_runtime;
            GRANT UPDATE(status,completed_at,last_error,authorization_revision,generation,worker_id,lease_expires_at,next_attempt_at)
              ON customers.import_work TO application_import_executor_runtime;
            GRANT SELECT,INSERT ON customers.object_storage_usage,customers.import_source_objects,customers.object_storage_reservations
              TO application_import_executor_runtime;
            GRANT UPDATE(source_object_key) ON customers.imports TO application_import_executor_runtime;
            GRANT UPDATE(reserved_bytes,retained_bytes,updated_at) ON customers.object_storage_usage TO application_import_executor_runtime;
            GRANT UPDATE(state,retention,expires_at,failure_code,retirement_generation,retirement_lease_id,retirement_lease_expires_at)
              ON customers.import_source_objects TO application_import_executor_runtime;
            GRANT UPDATE(state,updated_at) ON customers.object_storage_reservations TO application_import_executor_runtime;
            GRANT EXECUTE ON FUNCTION customers.discover_runnable_import_tenants(uuid,integer) TO application_import_executor_runtime;
            """);
    }

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutonomousHostRecoversExpiredRetainedWorkUsingRealCurrentAuthorityAndCommittedRowResults(bool lastRowAlreadyCommitted)
    {
        var authorization = new ExecutorAuthorization(); var observation = new WorkObservation();
        var (configuration, database) = Configuration();
        await using var source = database.CreateDataSource();
        using var host = BuildHost(configuration, database, source, authorization, observation);
        var (context, importId) = await AcceptFixtureAsync(host.Services);
        var prior = new PostgresCustomerStore(source);
        var killedClaim = (await prior.ClaimImportAsync(_tenant, Guid.NewGuid(), RunCustomerImportBatch.ClaimLease, CancellationToken.None))!;
        Assert.Equal(CustomerImportRowExecutionStatus.Processed, await prior.ProcessNextImportRowAsync(killedClaim, context, CancellationToken.None));
        if (lastRowAlreadyCommitted)
            Assert.Equal(CustomerImportRowExecutionStatus.Processed, await prior.ProcessNextImportRowAsync(killedClaim, context, CancellationToken.None));
        await SqlAsync("UPDATE customers.import_work SET lease_expires_at=clock_timestamp()-interval '1 second' WHERE tenant_id=@tenant");
        try
        {
            await host.StartAsync();
            await observation.Releases.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
            var completed = (await prior.ReadImportSummaryAsync(context, importId, CancellationToken.None))!;
            Assert.Equal(CustomerImportWorkStatus.Completed, completed.Work!.Status);
            Assert.Equal(2, completed.Imported); Assert.Equal(0, completed.Pending);
            var rows = await prior.ReadImportRowsPageAsync(context, importId, 0, 50, CancellationToken.None);
            Assert.All(rows.Items, row => { Assert.Equal(CustomerImportRowStatus.Imported, row.Status); Assert.Equal(row.RowId, row.CustomerId); });
            Assert.Equal(2, rows.Items.Select(row => row.CustomerId).Distinct().Count());
            await Assert.ThrowsAsync<CustomerImportClaimLostException>(() => prior.ProcessNextImportRowAsync(killedClaim, context, CancellationToken.None));
            Assert.True(host.Services.GetRequiredService<CustomerImportExecutionState>().IsAcceptingWork);
        }
        finally { await StopHostAsync(host); }
        Assert.False(host.Services.GetRequiredService<CustomerImportExecutionState>().IsAcceptingWork);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("tenant")]
    [InlineData("membership")]
    [InlineData("revision")]
    [InlineData("missing_revision")]
    [InlineData("permission")]
    [InlineData("provider")]
    public async Task AutonomousExecutorRechecksAuthorityAfterEachRealRowCommitAndRetainsUnprocessedRows(string change)
    {
        var authorization = new ExecutorAuthorization(); var observation = new WorkObservation();
        var (configuration, database) = Configuration();
        await using var source = database.CreateDataSource();
        observation.AfterCommit = change switch
        {
            "account" => () => SqlAsync("UPDATE identity_access.accounts SET availability=2 WHERE id=@account"),
            "tenant" => () => SqlAsync("UPDATE tenancy.tenants SET availability=2,revision=revision+1,suspended_at=clock_timestamp() WHERE id=@tenant"),
            "membership" => () => SqlAsync("UPDATE tenancy.memberships SET availability=2,revision=revision+1,suspended_at=clock_timestamp() WHERE tenant_id=@tenant AND account_id=@account"),
            "revision" => () => SqlAsync("UPDATE tenancy.tenant_authorization_state SET revision=revision+1 WHERE tenant_id=@tenant"),
            "missing_revision" => () => SqlAsync("DELETE FROM tenancy.tenant_authorization_state WHERE tenant_id=@tenant"),
            "permission" => () => { authorization.Allowed = false; return Task.CompletedTask; }
            ,
            _ => () => { authorization.Unavailable = true; return Task.CompletedTask; }
            ,
        };
        using var host = BuildHost(configuration, database, source, authorization, observation);
        var (context, importId) = await AcceptFixtureAsync(host.Services);
        try
        {
            await host.StartAsync();
            var denied = await observation.Releases.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(change != "provider", denied);
            var store = new PostgresCustomerStore(source);
            var retained = (await store.ReadImportSummaryAsync(context, importId, CancellationToken.None))!;
            Assert.Equal(1, retained.Imported); Assert.Equal(1, retained.Pending);
            Assert.Equal(change == "provider" ? CustomerImportWorkStatus.Accepted : CustomerImportWorkStatus.Failed, retained.Work!.Status);
            Assert.Equal(change == "provider" ? null : "authority_changed", retained.Work.LastError);
            var rows = await store.ReadImportRowsPageAsync(context, importId, 0, 50, CancellationToken.None);
            Assert.Equal(CustomerImportRowStatus.Pending, rows.Items[1].Status); Assert.Null(rows.Items[1].CustomerId);
            Assert.Equal(0, rows.Items[1].Attempts);
        }
        finally { await StopHostAsync(host); }
    }

    [Fact]
    public async Task AutonomousHostDiscoversExpiredSourceOnlyTenantAndRetiresWithControlledDeleteOutcome()
    {
        var authorization = new ExecutorAuthorization();
        var observation = new WorkObservation();
        var objectStore = new ControlledObjectStore();
        var retirementObservation = new RetirementObservation();
        var (configuration, database) = Configuration(objectStorageEnabled: true);
        await using var source = database.CreateDataSource();
        using var host = BuildHost(configuration, database, source, authorization, observation, objectStore, retirementObservation);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = (await scope.ServiceProvider.GetRequiredService<ResolveTenantContext>()
                .ExecuteAsync(_account, _tenant, CancellationToken.None))!;
            var result = await scope.ServiceProvider.GetRequiredService<CreateCustomerImport>().ExecuteAsync(context,
                new MemoryStream(Encoding.UTF8.GetBytes("name\nExpired source only\n")), Guid.NewGuid(),
                "expired-source-only", CancellationToken.None);
            Assert.True(result.Created);
        }
        await SqlAsync("UPDATE customers.import_source_objects SET expires_at=clock_timestamp()-interval '1 second' WHERE tenant_id=@tenant");

        try
        {
            await host.StartAsync();
            await objectStore.Deletes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
            await retirementObservation.Completions.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
            await using var verify = new NpgsqlConnection(ConnectionString);
            await verify.OpenAsync();
            await using var command = verify.CreateCommand();
            command.CommandText = """
                SELECT source.state, usage.retained_bytes
                FROM customers.import_source_objects source
                JOIN customers.object_storage_usage usage
                  ON usage.tenant_id=source.tenant_id AND usage.provider_scope=source.provider_scope
                WHERE source.tenant_id=@tenant
                """;
            command.Parameters.AddWithValue("tenant", _tenant);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal((int)CustomerImportSourceState.Retired, reader.GetInt32(0));
            Assert.Equal(0L, reader.GetInt64(1));
            Assert.False(await reader.ReadAsync());
        }
        finally { await StopHostAsync(host); }
    }

    private async Task<(TenantContext Context, Guid ImportId)> AcceptFixtureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = (await scope.ServiceProvider.GetRequiredService<ResolveTenantContext>()
            .ExecuteAsync(_account, _tenant, CancellationToken.None))!;
        var plan = await scope.ServiceProvider.GetRequiredService<CreateCustomerImport>().ExecuteAsync(context,
            new MemoryStream(Encoding.UTF8.GetBytes("name\nFirst Executor Fixture\nSecond Executor Fixture\n")), Guid.NewGuid(), "plan", CancellationToken.None);
        var accepted = await scope.ServiceProvider.GetRequiredService<ExecuteCustomerImport>().ExecuteAsync(context,
            new(plan.ImportId), "accept", CancellationToken.None);
        Assert.Equal(CustomerImportWorkStatus.Accepted, accepted.Work.Status);
        return (context, plan.ImportId);
    }

    private static IHost BuildHost(IConfiguration configuration, RuntimeDatabaseConfiguration database, NpgsqlDataSource source,
        ExecutorAuthorization authorization, WorkObservation observation, ControlledObjectStore? objectStore = null,
        RetirementObservation? retirementObservation = null)
    {
        var builder = Host.CreateApplicationBuilder(); builder.Logging.ClearProviders();
        builder.Services.AddSingleton(source);
        builder.Services.AddIdentityAccessPostgres(); builder.Services.AddTenancyPostgres(); builder.Services.AddCustomersPostgres();
        builder.Services.AddObjectStorage(HuggingFaceObjectStoreConfiguration.From(configuration));
        builder.Services.AddSingleton<ITenantCustomerAuthorization>(authorization);
        builder.Services.AddCustomerImportExecution(configuration, database);
        builder.Services.Replace(ServiceDescriptor.Scoped<ICustomerImportWorkStore>(_ => new ObservedWorkStore(source, observation)));
        if (objectStore is not null)
            builder.Services.Replace(ServiceDescriptor.Singleton<IObjectStore>(objectStore));
        if (retirementObservation is not null)
            builder.Services.Replace(ServiceDescriptor.Scoped<ICustomerImportSourceMaintenanceStore>(_ =>
                new ObservedRetirementStore(source, retirementObservation)));
        return builder.Build();
    }

    private (IConfiguration Configuration, RuntimeDatabaseConfiguration Database) Configuration(bool objectStorageEnabled = false)
    {
        var runtime = new NpgsqlConnectionStringBuilder(ConnectionString)
        { Username = "application_import_executor_runtime", Password = "local-import-runtime-test-only" }.ConnectionString;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PrimaryDatabase"] = runtime,
            ["Database:ConnectionMode"] = "Direct",
            ["Database:MaximumPoolSize"] = "2",
            ["Database:MinimumPoolSize"] = "0",
            ["Database:ConnectionIdleLifetimeSeconds"] = "300",
            ["Database:ConnectionPruningIntervalSeconds"] = "10",
            ["Database:ConnectionLifetimeSeconds"] = "3600",
            ["Database:CommandTimeoutSeconds"] = "5",
            ["CustomerImports:Execution:Enabled"] = "true",
            ["CustomerImports:Execution:PollIntervalSeconds"] = "300",
            ["CustomerImports:Execution:TenantPageSize"] = "10",
            ["CustomerImports:Execution:BatchRows"] = "10",
            ["CustomerImports:Execution:OperationTimeoutSeconds"] = "10",
            ["HostOptions:ShutdownTimeout"] = "00:00:20",
        }).Build();
        if (objectStorageEnabled)
        {
            var values = new Dictionary<string, string?>
            {
                ["ObjectStorage:Enabled"] = "true",
                ["ObjectStorage:Endpoint"] = "https://s3.hf.co",
                ["ObjectStorage:Namespace"] = "test-namespace",
                ["ObjectStorage:Bucket"] = "test-bucket",
                ["ObjectStorage:AccessKeyId"] = "test-access-key",
                ["ObjectStorage:SecretAccessKey"] = "test-secret-key",
                ["ObjectStorage:ProviderScope"] = "test/provider",
                ["ObjectStorage:MaximumRetainedBytes"] = "1000000",
                ["ObjectStorage:RequestTimeoutSeconds"] = "30",
            };
            configuration = new ConfigurationBuilder().AddConfiguration(configuration).AddInMemoryCollection(values).Build();
        }
        return (configuration, RuntimeDatabaseConfiguration.From(configuration));
    }

    private async Task SqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        command.Parameters.AddWithValue("tenant", _tenant); command.Parameters.AddWithValue("account", _account);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task StopHostAsync(IHost host)
    {
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(20)); await host.StopAsync(shutdown.Token);
    }

    private sealed class WorkObservation
    {
        internal readonly Channel<bool> Releases = Channel.CreateBounded<bool>(10);
        internal Func<Task>? AfterCommit;
    }

    private sealed class RetirementObservation
    {
        internal readonly Channel<bool> Completions = Channel.CreateBounded<bool>(10);
    }

    private sealed class ObservedWorkStore(NpgsqlDataSource source, WorkObservation observation) : ICustomerImportWorkStore
    {
        private readonly PostgresCustomerStore _store = new(source);
        public Task<CustomerImportClaim?> ClaimImportAsync(Guid tenant, Guid worker, TimeSpan lease, CancellationToken ct) =>
            _store.ClaimImportAsync(tenant, worker, lease, ct);
        public async Task<CustomerImportRowExecutionStatus> ProcessNextImportRowAsync(CustomerImportClaim claim, TenantContext context, CancellationToken ct)
        {
            var committed = await _store.ProcessNextImportRowAsync(claim, context, ct);
            if (committed == CustomerImportRowExecutionStatus.Processed && observation.AfterCommit is not null)
                await observation.AfterCommit();
            return committed;
        }
        public async Task ReleaseImportClaimAsync(CustomerImportClaim claim, bool denied, CancellationToken ct)
        {
            await _store.ReleaseImportClaimAsync(claim, denied, ct); observation.Releases.Writer.TryWrite(denied);
        }
    }

    private sealed class ObservedRetirementStore(NpgsqlDataSource source, RetirementObservation observation)
        : ICustomerImportSourceMaintenanceStore
    {
        private readonly PostgresCustomerStore _store = new(source);

        public Task<CustomerImportSourceRetirementLease?> ClaimExpiredSourceRetirementAsync(
            Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken) =>
            _store.ClaimExpiredSourceRetirementAsync(tenantId, now, cancellationToken);

        public async Task CompleteSourceRetirementAsync(Guid tenantId, CustomerImportSourceRetirementLease lease,
            bool deleted, string? failureCode, CancellationToken cancellationToken)
        {
            await _store.CompleteSourceRetirementAsync(tenantId, lease, deleted, failureCode, cancellationToken);
            observation.Completions.Writer.TryWrite(true);
        }
    }

    private sealed class ExecutorAuthorization : ITenantCustomerAuthorization
    {
        internal bool Allowed = true, Unavailable;
        public Task<bool> CanImportCustomersAsync(Guid a, Guid t, CancellationToken c)
        {
            c.ThrowIfCancellationRequested();
            if (Unavailable) throw new AuthorizationProviderUnavailableException("Synthetic unavailable.", new HttpRequestException());
            return Task.FromResult(Allowed);
        }
        public Task<bool> CanCreateOrganizationAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanViewOrganizationsAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanCreateProgramAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanViewProgramsAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanCreateIndividualAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanViewIndividualsAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanChangeIndividualAvailabilityAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanEditIndividualContactAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanViewRepresentativesAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanManageRepresentativesAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanResolveCustomerDuplicatesAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanConsolidateCustomerDuplicatesAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
    }

    private sealed class ControlledObjectStore : IObjectStore
    {
        internal readonly Channel<bool> Deletes = Channel.CreateBounded<bool>(10);

        public Task<ObjectStorePutResult> PutAsync(ObjectStorePutRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ObjectStorePutResult(ObjectStorePutOutcome.Created));

        public Task<ObjectStoreReadResult> OpenReadAsync(ObjectStoreReadRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ObjectStoreReadResult(ObjectStoreReadOutcome.Unavailable));

        public Task<ObjectStoreMetadataResult> GetMetadataAsync(ObjectStoreKey key, CancellationToken cancellationToken) =>
            Task.FromResult(new ObjectStoreMetadataResult(ObjectStoreMetadataOutcome.Unavailable));

        public Task<ObjectStoreDeleteResult> DeleteAsync(ObjectStoreKey key, CancellationToken cancellationToken)
        {
            Deletes.Writer.TryWrite(true);
            return Task.FromResult(new ObjectStoreDeleteResult(ObjectStoreDeleteOutcome.Deleted));
        }
    }
}
