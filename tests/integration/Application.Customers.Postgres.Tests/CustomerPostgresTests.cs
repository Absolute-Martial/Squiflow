using Application.Customers;
using Application.Customers.Postgres;
using Application.IdentityAccess.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.Customers.Postgres.Tests;

public sealed class CustomerPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("application_tests")
        .WithUsername("postgres")
        .WithPassword("local-integration-test-only")
        .Build();

    private string ConnectionString => _database.GetConnectionString();

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task MigrationCreatesForcedTenantPoliciesAndRequiresContextForWrites()
    {
        await MigrateAsync();
        var (tenantId, accountId) = await SeedAsync();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        foreach (var table in new[] { "organizations", "programs", "organization_receipts", "program_receipts" })
        {
            await using var policy = connection.CreateCommand();
            policy.CommandText = """
                SELECT relrowsecurity AND relforcerowsecurity FROM pg_class
                WHERE oid = @table_name::regclass
                """;
            policy.Parameters.AddWithValue("table_name", $"customers.{table}");
            Assert.Equal(true, await policy.ExecuteScalarAsync());
        }

        var runtime = await CreateRestrictedRoleAsync();
        await using var restricted = new NpgsqlConnection(runtime);
        await restricted.OpenAsync();
        await using (var insert = restricted.CreateCommand())
        {
            insert.CommandText = """
                INSERT INTO customers.organizations
                    (tenant_id, id, created_by_account_id, display_name, created_at)
                VALUES (@tenant_id, @id, @account_id, 'Blocked', now())
                """;
            insert.Parameters.AddWithValue("tenant_id", tenantId);
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("account_id", accountId);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }

        await using (var forbidden = restricted.CreateCommand())
        {
            forbidden.CommandText = "DELETE FROM customers.organizations";
            var denied = await Assert.ThrowsAsync<PostgresException>(() => forbidden.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }

        await using (var forbidden = restricted.CreateCommand())
        {
            forbidden.CommandText = "UPDATE customers.organizations SET display_name = 'Changed'";
            var denied = await Assert.ThrowsAsync<PostgresException>(() => forbidden.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
    }

    [Fact]
    public async Task CreateReplayConflictAndConcurrentSameKeyPersistOneOrganization()
    {
        await MigrateAsync();
        var (tenantId, accountId) = await SeedAsync();
        var context = await ResolveContextAsync(tenantId, accountId);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime);
        var store = new PostgresCustomerStore(source);
        var create = new CreateCustomerOrganization(store);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            create.ExecuteAsync(context, new("  Acme  "), "create-acme", CancellationToken.None)));
        Assert.Single(attempts, item => item.Status == CreateCustomerOrganizationStatus.Created);
        Assert.Equal(7, attempts.Count(item => item.Status == CreateCustomerOrganizationStatus.Replayed));
        var original = attempts[0].Organization;
        Assert.NotNull(original);
        Assert.All(attempts, item => Assert.Equal(original.OrganizationId, item.Organization?.OrganizationId));
        Assert.Equal("Acme", original.DisplayName);

        var changed = await create.ExecuteAsync(context, new("Other"), "create-acme", CancellationToken.None);
        Assert.Equal(CreateCustomerOrganizationStatus.IdempotencyKeyConflict, changed.Status);
        Assert.Null(changed.Organization);
        Assert.Equal(1L, await CountAsync("customers.organizations"));
        Assert.Equal(1L, await CountAsync("customers.organization_receipts"));
    }

    [Fact]
    public async Task ProgramParentAndTenantBoundariesHoldForQueriesAndReceipts()
    {
        await MigrateAsync();
        var (tenantA, accountA) = await SeedAsync();
        var (tenantB, accountB) = await SeedAsync();
        var contextA = await ResolveContextAsync(tenantA, accountA);
        var contextB = await ResolveContextAsync(tenantB, accountB);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime);
        var store = new PostgresCustomerStore(source);
        var createOrganization = new CreateCustomerOrganization(store);
        var createProgram = new CreateCustomerProgram(store);
        var organizationA = (await createOrganization.ExecuteAsync(
            contextA, new("Customer A"), "same-key", CancellationToken.None)).Organization!;
        var organizationB = (await createOrganization.ExecuteAsync(
            contextB, new("Customer B"), "same-key", CancellationToken.None)).Organization!;
        Assert.NotEqual(organizationA.OrganizationId, organizationB.OrganizationId);
        var missingParent = await createProgram.ExecuteAsync(contextB,
            new(organizationA.OrganizationId, "Program"), "program-key", CancellationToken.None);
        Assert.Equal(CreateCustomerProgramStatus.ParentNotFound, missingParent.Status);
        var program = (await createProgram.ExecuteAsync(contextA,
            new(organizationA.OrganizationId, "Program"), "program-key", CancellationToken.None)).Program!;
        var replay = await createProgram.ExecuteAsync(contextA,
            new(organizationA.OrganizationId, "Program"), "program-key", CancellationToken.None);
        Assert.Equal(CreateCustomerProgramStatus.Replayed, replay.Status);
        Assert.Equal(program.ProgramId, replay.Program?.ProgramId);
        var changedParent = await createProgram.ExecuteAsync(contextA,
            new(organizationB.OrganizationId, "Program"), "program-key", CancellationToken.None);
        Assert.Equal(CreateCustomerProgramStatus.IdempotencyKeyConflict, changedParent.Status);
        Assert.Null(await store.FindOrganizationAsync(contextB, organizationA.OrganizationId, CancellationToken.None));
        Assert.Null(await store.FindProgramAsync(contextB, program.ProgramId, CancellationToken.None));
        Assert.Null(await store.ResolveOrderContextAsync(
            contextA, organizationB.OrganizationId, null, CancellationToken.None));
        Assert.Null(await store.ResolveOrderContextAsync(
            contextA, organizationB.OrganizationId, program.ProgramId, CancellationToken.None));
        Assert.Equal(new(organizationA.OrganizationId, program.ProgramId), await store.ResolveOrderContextAsync(
            contextA, organizationA.OrganizationId, program.ProgramId, CancellationToken.None));
    }

    [Fact]
    public async Task BrowseIsBoundedAndScopedToTenantAndParent()
    {
        await MigrateAsync();
        var (tenantId, accountId) = await SeedAsync();
        var context = await ResolveContextAsync(tenantId, accountId);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime);
        var store = new PostgresCustomerStore(source);
        var createOrganization = new CreateCustomerOrganization(store);
        var createProgram = new CreateCustomerProgram(store);
        var first = (await createOrganization.ExecuteAsync(context, new("First"), "first", CancellationToken.None)).Organization!;
        var second = (await createOrganization.ExecuteAsync(context, new("Second"), "second", CancellationToken.None)).Organization!;
        var page = await store.ListOrganizationsAsync(context, new(1, null), CancellationToken.None);
        Assert.Single(page.Items);
        Assert.NotNull(page.NextCursor);
        var next = await store.ListOrganizationsAsync(context, new(1, page.NextCursor), CancellationToken.None);
        Assert.Single(next.Items);
        Assert.NotEqual(page.Items[0].OrganizationId, next.Items[0].OrganizationId);
        Assert.Null(next.NextCursor);
        await createProgram.ExecuteAsync(context, new(first.OrganizationId, "P1"), "p1", CancellationToken.None);
        await createProgram.ExecuteAsync(context, new(second.OrganizationId, "P2"), "p2", CancellationToken.None);
        var firstPrograms = await store.ListProgramsAsync(context, new(first.OrganizationId, 50, null), CancellationToken.None);
        Assert.Single(firstPrograms.Items);
        Assert.Equal(first.OrganizationId, firstPrograms.Items[0].OrganizationId);
    }

    [Fact]
    public async Task ReceiptsAreScopedByCallerAndOperationAndProgramRacePersistsOneRow()
    {
        await MigrateAsync();
        var (tenantId, accountA) = await SeedAsync();
        var accountB = Guid.NewGuid();
        await SeedAdditionalAccountAsync(tenantId, accountB);
        var contextA = await ResolveContextAsync(tenantId, accountA);
        var contextB = await ResolveContextAsync(tenantId, accountB);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime);
        var store = new PostgresCustomerStore(source);
        var createOrganization = new CreateCustomerOrganization(store);
        var createProgram = new CreateCustomerProgram(store);

        var organizationA = (await createOrganization.ExecuteAsync(
            contextA, new("A"), "shared", CancellationToken.None)).Organization!;
        var organizationB = (await createOrganization.ExecuteAsync(
            contextB, new("B"), "shared", CancellationToken.None)).Organization!;
        Assert.NotEqual(organizationA.OrganizationId, organizationB.OrganizationId);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            createProgram.ExecuteAsync(contextA,
                new(organizationA.OrganizationId, "Program"), "shared", CancellationToken.None)));
        Assert.Single(results, result => result.Status == CreateCustomerProgramStatus.Created);
        Assert.Equal(7, results.Count(result => result.Status == CreateCustomerProgramStatus.Replayed));
        Assert.All(results, result => Assert.Equal(results[0].Program?.ProgramId, result.Program?.ProgramId));
        Assert.Equal(1L, await CountAsync("customers.programs"));
        Assert.Equal(1L, await CountAsync("customers.program_receipts"));
    }

    [Fact]
    public async Task RestrictedRoleCannotCrossTenantOrMutateImmutableCustomers()
    {
        await MigrateAsync();
        var (tenantA, accountA) = await SeedAsync();
        var (tenantB, _) = await SeedAsync();
        var contextA = await ResolveContextAsync(tenantA, accountA);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime);
        var store = new PostgresCustomerStore(source);
        var organization = (await new CreateCustomerOrganization(store).ExecuteAsync(
            contextA, new("A"), "key", CancellationToken.None)).Organization!;
        await using var connection = new NpgsqlConnection(runtime);
        await connection.OpenAsync();

        await using (var setWrongTenant = connection.CreateCommand())
        {
            setWrongTenant.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
            setWrongTenant.Parameters.AddWithValue("tenant_id", tenantB.ToString("D"));
            await setWrongTenant.ExecuteNonQueryAsync();
        }

        await using (var read = connection.CreateCommand())
        {
            read.CommandText = "SELECT count(*) FROM customers.organizations WHERE id = @id";
            read.Parameters.AddWithValue("id", organization.OrganizationId);
            Assert.Equal(0L, await read.ExecuteScalarAsync());
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
                INSERT INTO customers.organizations
                    (tenant_id, id, created_by_account_id, display_name, created_at)
                VALUES (@tenant_id, @id, @account_id, 'Wrong tenant', now())
                """;
            insert.Parameters.AddWithValue("tenant_id", tenantA);
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("account_id", accountA);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }

        await using (var forbidden = connection.CreateCommand())
        {
            forbidden.CommandText = "CREATE TABLE customers.runtime_ddl (id integer)";
            var denied = await Assert.ThrowsAsync<PostgresException>(() => forbidden.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
    }

    [Fact]
    public async Task RollingBackCustomersRefusesToDropExternalForeignKeys()
    {
        await MigrateAsync();
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE public.customer_dependency_fixture (
                    tenant_id uuid NOT NULL,
                    organization_id uuid NOT NULL,
                    program_id uuid NOT NULL,
                    CONSTRAINT fk_fixture_program FOREIGN KEY (tenant_id, organization_id, program_id)
                        REFERENCES customers.programs(tenant_id, organization_id, id)
                )
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using var customers = CustomersPostgresMigrations.CreateContext(ConnectionString);
        var rejected = await Assert.ThrowsAsync<PostgresException>(() => customers.Database.MigrateAsync("0"));
        Assert.Equal(PostgresErrorCodes.DependentObjectsStillExist, rejected.SqlState);
        Assert.Equal(1L, await CountAsync("pg_catalog.pg_class WHERE oid = 'customers.programs'::regclass"));
    }

    private async Task MigrateAsync()
    {
        await using var identity = IdentityAccessPostgresMigrations.CreateContext(ConnectionString);
        await identity.Database.MigrateAsync();
        await using var tenancy = TenancyPostgresMigrations.CreateContext(ConnectionString);
        await tenancy.Database.MigrateAsync();
        await using var customers = CustomersPostgresMigrations.CreateContext(ConnectionString);
        await customers.Database.MigrateAsync();
    }

    private async Task<(Guid TenantId, Guid AccountId)> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO identity_access.accounts(id, availability, created_at) VALUES (@account_id, 1, now());
            INSERT INTO tenancy.tenants(id, display_name, availability, created_at)
                VALUES (@tenant_id, 'Fixture tenant', 1, now());
            INSERT INTO tenancy.memberships(tenant_id, account_id, availability, created_at)
                VALUES (@tenant_id, @account_id, 1, now());
            """;
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        await command.ExecuteNonQueryAsync();
        return (tenantId, accountId);
    }

    private async Task<TenantContext> ResolveContextAsync(Guid tenantId, Guid accountId)
    {
        var options = new DbContextOptionsBuilder<TenancyDbContext>();
        PostgresTenancyOptions.Configure(options, ConnectionString);
        await using var db = new TenancyDbContext(options.Options);
        return await new ResolveTenantContext(new PostgresTenantMembershipDirectory(db))
            .ExecuteAsync(accountId, tenantId, CancellationToken.None)
            ?? throw new InvalidOperationException("Fixture membership was not resolved.");
    }

    private async Task SeedAdditionalAccountAsync(Guid tenantId, Guid accountId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO identity_access.accounts(id, availability, created_at) VALUES (@account_id, 1, now());
            INSERT INTO tenancy.memberships(tenant_id, account_id, availability, created_at)
                VALUES (@tenant_id, @account_id, 1, now());
            """;
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string> CreateRestrictedRoleAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE ROLE application_customers_runtime LOGIN PASSWORD 'local-runtime-test-only';
            GRANT CONNECT ON DATABASE application_tests TO application_customers_runtime;
            GRANT USAGE ON SCHEMA customers TO application_customers_runtime;
            GRANT SELECT, INSERT ON customers.organizations, customers.programs,
                customers.organization_receipts, customers.program_receipts TO application_customers_runtime;
            """;
        await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "application_customers_runtime",
            Password = "local-runtime-test-only",
        }.ConnectionString;
    }

    private async Task<long> CountAsync(string qualifiedTable)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM {qualifiedTable}";
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException());
    }
}
