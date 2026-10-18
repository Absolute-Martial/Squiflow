extern alias DatabaseMigrator;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using DatabaseMigrator::Application.DatabaseMigrator;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenFga.Sdk.Client;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.CoreApi.Tests;

[Collection("Authorization reconciliation native")]
public sealed partial class TenantAuthorizationReconciliationNativeTests(AuthorizationReconciliationFixture fixture)
    : IClassFixture<AuthorizationReconciliationFixture>
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task TwoExecutorsSerializeAcrossSourcesAndTerminalReplayCannotRestoreALaterRevoke(int poolSize)
    {
        var scenario = await fixture.SeedAsync();
        await using var proxy = await AuthorizationWriteProxy.StartAsync(fixture.ApiUri, holdBefore: true);
        await using var firstSource = fixture.Source(poolSize, "auth-first-" + scenario.TenantId.ToString("N"));
        var secondName = "auth-second-" + scenario.TenantId.ToString("N");
        await using var secondSource = fixture.Source(poolSize, secondName);
        var configuration = fixture.Configuration(proxy.Uri);
        using var client = new OpenFgaTenantAuthorizationAdministrationClient(configuration);
        var provider = new OpenFgaTenantAuthorizationAdministrationProvider(client, configuration);
        var first = new TenantAuthorizationReconciler(new PostgresTenantAuthorizationAdministrationStore(firstSource), provider, TimeProvider.System)
            .ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default);
        await proxy.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = new TenantAuthorizationReconciler(new PostgresTenantAuthorizationAdministrationStore(secondSource), provider, TimeProvider.System)
            .ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default);
        try
        {
            await fixture.WaitForLockWaitAsync(secondName);
            Assert.False(second.IsCompleted);
            Assert.Equal(1, proxy.Writes);
        }
        finally { proxy.Release.TrySetResult(); }
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.All(results, result => Assert.Equal(TenantAuthorizationProposalStatus.Applied, result!.Status));
        Assert.Equal(1, proxy.Writes);
        var store = new PostgresTenantAuthorizationAdministrationStore(secondSource);
        Assert.Equal(2, await store.GetAuthorizationRevisionAsync(scenario.TenantId, default));
        var revoke = await store.ProposeAsync(TenantAuthorizationActor.Create(scenario.OwnerId),
            TenantAuthorizationProposalIntent.PermissionChange(TenantAuthorizationProposalKind.RevokePermission,
                scenario.TenantId, scenario.TargetId, "invoices.issue", 2, "revoke"), DateTimeOffset.UtcNow, default);
        Assert.Equal(TenantAuthorizationProposalResultStatus.Created, revoke.Status);
        var reconciler = new TenantAuthorizationReconciler(store, provider, TimeProvider.System);
        Assert.Equal(TenantAuthorizationProposalStatus.Applied,
            (await reconciler.ReconcileAsync(scenario.TenantId, revoke.Proposal!.ProposalId, default))!.Status);
        Assert.False(await fixture.AllowedAsync(scenario));
        Assert.Equal(TenantAuthorizationProposalStatus.Applied,
            (await reconciler.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default))!.Status);
        Assert.Equal(2, proxy.Writes);
        Assert.False(await fixture.AllowedAsync(scenario));
        Assert.Equal(3, await store.GetAuthorizationRevisionAsync(scenario.TenantId, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealWriteThenLocalCompletionFailureRecoversAfterRestartWithoutRewritingAndHoldsOnAuthorityLoss(bool suspend)
    {
        var scenario = await fixture.SeedAsync();
        await using var proxy = await AuthorizationWriteProxy.StartAsync(fixture.ApiUri);
        await fixture.SqlAsync($"""
            CREATE FUNCTION tenancy.test_completion_failure_{scenario.TenantId:N}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN IF NEW.tenant_id='{scenario.TenantId:D}'::uuid AND NEW.reason='provider_state_observed'
              THEN RAISE EXCEPTION 'Synthetic local authorization completion failure'; END IF; RETURN NEW; END; $body$;
            CREATE TRIGGER test_completion_failure_{scenario.TenantId:N} BEFORE INSERT ON tenancy.tenant_authorization_events
              FOR EACH ROW EXECUTE FUNCTION tenancy.test_completion_failure_{scenario.TenantId:N}();
            """);
        var configuration = fixture.Configuration(proxy.Uri);
        using var client = new OpenFgaTenantAuthorizationAdministrationClient(configuration);
        var provider = new OpenFgaTenantAuthorizationAdministrationProvider(client, configuration);
        try
        {
            await using var source = fixture.Source(1);
            var reconciler = new TenantAuthorizationReconciler(new PostgresTenantAuthorizationAdministrationStore(source), provider, TimeProvider.System);
            await Assert.ThrowsAsync<PostgresException>(() => reconciler.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default));
            Assert.True(await fixture.AllowedAsync(scenario));
            var retained = await new PostgresTenantAuthorizationAdministrationStore(source).FindProposalAsync(scenario.TenantId, scenario.Proposal.ProposalId, default);
            Assert.Equal(TenantAuthorizationExecution.Observed, retained!.FailureCode);
            Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, retained.Status);
            Assert.Equal(1, await new PostgresTenantAuthorizationAdministrationStore(source).GetAuthorizationRevisionAsync(scenario.TenantId, default));
        }
        finally
        {
            await fixture.SqlAsync($"DROP TRIGGER test_completion_failure_{scenario.TenantId:N} ON tenancy.tenant_authorization_events; DROP FUNCTION tenancy.test_completion_failure_{scenario.TenantId:N}();");
        }
        await using var restartedSource = fixture.Source(1);
        var store = new PostgresTenantAuthorizationAdministrationStore(restartedSource);
        var restarted = new TenantAuthorizationReconciler(store, provider, TimeProvider.System);
        if (suspend)
        {
            await fixture.SetTenantActiveAsync(scenario.TenantId, false);
            Assert.Equal(TenantAuthorizationProposalStatus.Uncertain,
                (await restarted.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default))!.Status);
            Assert.Equal(TenantAuthorizationProposalStatus.Uncertain,
                (await store.MarkFailedAsync(scenario.TenantId, scenario.Proposal.ProposalId, "delegator_authority_revoked", DateTimeOffset.UtcNow, default))!.Status);
            Assert.Equal(TenantAuthorizationExecution.Observed,
                (await store.MarkUncertainAsync(scenario.TenantId, scenario.Proposal.ProposalId, "other_failure", DateTimeOffset.UtcNow, default))!.FailureCode);
            Assert.Equal(TenantAuthorizationProposalStatus.Uncertain,
                (await store.CompleteAsync(scenario.TenantId, scenario.Proposal.ProposalId, DateTimeOffset.UtcNow, default))!.Status);
            await fixture.AssertSlotHeldAsync(scenario);
            await fixture.SetTenantActiveAsync(scenario.TenantId, true);
        }
        Assert.Equal(TenantAuthorizationProposalStatus.Applied,
            (await restarted.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default))!.Status);
        Assert.Equal(TenantAuthorizationProposalStatus.Applied,
            (await restarted.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default))!.Status);
        Assert.Equal(1, proxy.Writes);
        Assert.Equal(2, await store.GetAuthorizationRevisionAsync(scenario.TenantId, default));
        Assert.Equal(1, await fixture.CountAsync("SELECT count(*) FROM tenancy.tenant_authorization_events WHERE tenant_id=@tenant AND reason='provider_state_observed'", scenario.TenantId));
        Assert.Equal(1, await fixture.CountAsync("SELECT count(*) FROM tenancy.tenant_permission_grants WHERE tenant_id=@tenant AND is_active", scenario.TenantId));
    }

    [Fact]
    public async Task RealAppliedWriteWithLostHttpResponseRemainsQuarantinedAcrossRestartAndAuthorityLoss()
    {
        var scenario = await fixture.SeedAsync();
        await using var proxy = await AuthorizationWriteProxy.StartAsync(fixture.ApiUri, holdAfter: true);
        await using var source = fixture.Source(1);
        var configuration = fixture.Configuration(proxy.Uri, seconds: 1);
        using var client = new OpenFgaTenantAuthorizationAdministrationClient(configuration);
        var provider = new OpenFgaTenantAuthorizationAdministrationProvider(client, configuration);
        var pending = new TenantAuthorizationReconciler(new PostgresTenantAuthorizationAdministrationStore(source), provider, TimeProvider.System)
            .ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default);
        await proxy.WriteForwarded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(await fixture.AllowedAsync(scenario));
        var unknown = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, unknown!.Status);
        Assert.Equal(TenantAuthorizationExecution.UnresolvedPrefix + "provider_timeout", unknown.FailureCode);
        await using var restartedSource = fixture.Source(1);
        var store = new PostgresTenantAuthorizationAdministrationStore(restartedSource);
        var restarted = new TenantAuthorizationReconciler(store, provider, TimeProvider.System);
        foreach (var active in new[] { true, false, true })
        {
            await fixture.SetTenantActiveAsync(scenario.TenantId, active);
            Assert.Equal(TenantAuthorizationProposalStatus.Uncertain,
                (await restarted.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default))!.Status);
        }
        await fixture.AssertSlotHeldAsync(scenario);
        proxy.Release.TrySetResult();
        await proxy.ResponseFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // The test knows the proxy drained; the application has no durable proof of that fact.
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain,
            (await restarted.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default))!.Status);
        Assert.Equal(1, proxy.Writes);
        Assert.Equal(1, await store.GetAuthorizationRevisionAsync(scenario.TenantId, default));
        Assert.True(await fixture.AllowedAsync(scenario));
    }

    [Fact]
    public async Task LostPostgresSessionCannotFenceAnInflightRemoteWriteAndReplacementCannotOvertakeIt()
    {
        var scenario = await fixture.SeedAsync();
        await using var proxy = await AuthorizationWriteProxy.StartAsync(fixture.ApiUri, holdBefore: true);
        var name = "auth-lost-" + scenario.TenantId.ToString("N");
        await using var source = fixture.Source(1, name);
        var configuration = fixture.Configuration(proxy.Uri, seconds: 30);
        using var client = new OpenFgaTenantAuthorizationAdministrationClient(configuration);
        var provider = new OpenFgaTenantAuthorizationAdministrationProvider(client, configuration);
        var pending = new TenantAuthorizationReconciler(new PostgresTenantAuthorizationAdministrationStore(source), provider, TimeProvider.System)
            .ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default);
        await proxy.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.TerminateAsync(name);
        await using var replacement = fixture.Source(1);
        var store = new PostgresTenantAuthorizationAdministrationStore(replacement);
        var retry = new TenantAuthorizationReconciler(store, provider, TimeProvider.System);
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain,
            (await retry.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default))!.Status);
        await fixture.AssertSlotHeldAsync(scenario);
        Assert.Equal(1, proxy.Writes);
        proxy.Release.TrySetResult();
        await Assert.ThrowsAnyAsync<NpgsqlException>(() => pending.WaitAsync(TimeSpan.FromSeconds(20)));
        Assert.True(await fixture.AllowedAsync(scenario));
        var held = await store.MarkFailedAsync(scenario.TenantId, scenario.Proposal.ProposalId, "provider_rejected", DateTimeOffset.UtcNow, default);
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, held!.Status);
        Assert.Equal(TenantAuthorizationExecution.InFlight, held.FailureCode);
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain,
            (await store.CompleteAsync(scenario.TenantId, scenario.Proposal.ProposalId, DateTimeOffset.UtcNow, default))!.Status);
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain,
            (await retry.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default))!.Status);
        await fixture.AssertSlotHeldAsync(scenario);
        Assert.Equal(1, proxy.Writes);
        Assert.Equal(1, await store.GetAuthorizationRevisionAsync(scenario.TenantId, default));
    }

    [Fact]
    public async Task FreshAtomicModelRejectionHasNoEffectAndTerminalReplayDoesNotWrite()
    {
        var scenario = await fixture.SeedAsync();
        await using var proxy = await AuthorizationWriteProxy.StartAsync(fixture.ApiUri);
        await using var source = fixture.Source(1);
        var configuration = fixture.Configuration(proxy.Uri, pin: "01ARZ3NDEKTSV4RRFFQ69G5FAW");
        using var client = new OpenFgaTenantAuthorizationAdministrationClient(configuration);
        var provider = new OpenFgaTenantAuthorizationAdministrationProvider(client, configuration);
        var store = new PostgresTenantAuthorizationAdministrationStore(source);
        var reconciler = new TenantAuthorizationReconciler(store, provider, TimeProvider.System);
        var rejected = await reconciler.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default);
        Assert.Equal(TenantAuthorizationProposalStatus.Failed, rejected!.Status);
        Assert.Equal("authorization_model_rejected", rejected.FailureCode);
        Assert.False(await fixture.AllowedAsync(scenario));
        await reconciler.ReconcileAsync(scenario.TenantId, scenario.Proposal.ProposalId, default);
        Assert.Equal(1, proxy.Writes);
        Assert.Equal(1, await store.GetAuthorizationRevisionAsync(scenario.TenantId, default));
        var successor = await store.ProposeAsync(TenantAuthorizationActor.Create(scenario.OwnerId),
            TenantAuthorizationProposalIntent.PermissionChange(TenantAuthorizationProposalKind.GrantPermission,
                scenario.TenantId, scenario.TargetId, "invoices.view", 1, "after-rejection"), DateTimeOffset.UtcNow, default);
        Assert.Equal(TenantAuthorizationProposalResultStatus.Created, successor.Status);
    }

    [Fact]
    public async Task CancelledLockWaitReturnsItsPoolSlotAndDoesNotDispatch()
    {
        var scenario = await fixture.SeedAsync();
        await using var holderSource = fixture.Source(1);
        var holder = await new PostgresTenantAuthorizationAdministrationStore(holderSource).OpenAsync(scenario.TenantId, default);
        var name = "auth-wait-" + scenario.TenantId.ToString("N");
        await using var waiterSource = fixture.Source(1, name);
        var waiter = new PostgresTenantAuthorizationAdministrationStore(waiterSource);
        using var cancellation = new CancellationTokenSource();
        var pending = waiter.OpenAsync(scenario.TenantId, cancellation.Token).AsTask();
        try
        {
            await fixture.WaitForLockWaitAsync(name);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        finally { await holder.DisposeAsync(); }
        await using var recovered = await waiter.OpenAsync(scenario.TenantId, default);
        Assert.Equal(0, (await recovered.FindAsync(scenario.Proposal.ProposalId, default))!.AttemptCount);
    }
}

public sealed record AuthorizationReconciliationScenario(Guid TenantId, Guid OwnerId, Guid TargetId, TenantAuthorizationProposal Proposal);

public sealed class AuthorizationReconciliationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
        .WithDatabase("authorization_reconciliation_tests").WithUsername("postgres").WithPassword("local-tests-only").Build();
    private readonly IContainer server = new ContainerBuilder(new DockerImage(repository: "openfga/openfga", tag: "v1.21.0"))
        .WithCommand("run", "--playground-enabled=false").WithPortBinding(8080, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/healthz"))).Build();
    private string storeId = "";
    private string modelId = "";
    public Uri ApiUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await new MigrationRunner(database.GetConnectionString(), 0x61A721).ApplyAsync(TimeSpan.FromSeconds(10), default);
        await server.StartAsync();
        ApiUri = new Uri($"http://127.0.0.1:{server.GetMappedPublicPort(8080)}");
        using var admin = new HttpClient { BaseAddress = ApiUri };
        using var created = await admin.PostAsJsonAsync("/stores", new { name = "authorization-reconciliation-tests" });
        created.EnsureSuccessStatusCode();
        using var store = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        storeId = store.RootElement.GetProperty("id").GetString()!;
        var model = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "OpenFga/tenant-authorization-model.json"));
        using var published = await admin.PostAsync($"/stores/{storeId}/authorization-models", new StringContent(model, Encoding.UTF8, "application/json"));
        published.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await published.Content.ReadAsStringAsync());
        modelId = document.RootElement.GetProperty("authorization_model_id").GetString()!;
        await using var owner = NpgsqlDataSource.Create(database.GetConnectionString());
        await using var connection = await owner.OpenConnectionAsync();
        await using (var createRole = new NpgsqlCommand("CREATE ROLE authorization_test_runtime LOGIN PASSWORD 'local-runtime-tests-only'; SELECT set_config('app.provision_runtime_role','authorization_test_runtime',false);", connection))
            await createRole.ExecuteNonQueryAsync();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Application.slnx"))) directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Source root missing.");
        await using var grants = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "deploy/database/grant-core-api-runtime.sql")), connection);
        await grants.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() { await server.DisposeAsync(); await database.DisposeAsync(); }

    internal NpgsqlDataSource Source(int pool, string? name = null, bool owner = false) => NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(database.GetConnectionString())
    {
        MaxPoolSize = pool,
        ApplicationName = name ?? "authorization-reconciliation",
        Timeout = 10,
        CommandTimeout = 10,
        Username = owner ? "postgres" : "authorization_test_runtime",
        Password = owner ? "local-tests-only" : "local-runtime-tests-only",
    }.ConnectionString);

    internal OpenFgaAuthorizationConfiguration Configuration(Uri uri, int seconds = 5, string? pin = null) => OpenFgaAuthorizationConfiguration.From(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authorization:OpenFga:ApiUrl"] = uri.AbsoluteUri,
            ["Authorization:OpenFga:StoreId"] = storeId,
            ["Authorization:OpenFga:AuthorizationModelId"] = pin ?? modelId,
            ["Authorization:OpenFga:RequestTimeoutSeconds"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Authorization:OpenFga:MaximumRetries"] = "3",
            ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
            ["Authorization:OpenFga:CredentialMethod"] = "None",
        }).Build());

    internal async Task<AuthorizationReconciliationScenario> SeedAsync()
    {
        var tenant = Guid.NewGuid(); var owner = Guid.NewGuid(); var target = Guid.NewGuid();
        await using var source = Source(1, owner: true);
        await using (var command = source.CreateCommand("""
            INSERT INTO identity_access.accounts(id,availability,created_at) VALUES(@owner,1,now()),(@target,1,now());
            INSERT INTO tenancy.tenants(id,display_name,availability,created_at) VALUES(@tenant,'Authorization test',1,now());
            INSERT INTO tenancy.memberships(tenant_id,account_id,availability,created_at,revision,is_initial_owner,activated_at)
            VALUES(@tenant,@owner,1,now(),1,true,now()),(@tenant,@target,1,now(),1,false,now());
            """))
        {
            command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("owner", owner); command.Parameters.AddWithValue("target", target);
            await command.ExecuteNonQueryAsync();
        }
        await using var runtime = Source(1);
        var result = await new PostgresTenantAuthorizationAdministrationStore(runtime).ProposeAsync(TenantAuthorizationActor.Create(owner),
            TenantAuthorizationProposalIntent.PermissionChange(TenantAuthorizationProposalKind.GrantPermission, tenant, target, "invoices.issue", 1, "grant"), DateTimeOffset.UtcNow, default);
        Assert.Equal(TenantAuthorizationProposalResultStatus.Created, result.Status);
        return new(tenant, owner, target, result.Proposal!);
    }

    internal async Task<bool> AllowedAsync(AuthorizationReconciliationScenario scenario)
    {
        using var client = new HttpClient { BaseAddress = ApiUri };
        using var response = await client.PostAsJsonAsync($"/stores/{storeId}/check", new
        { authorization_model_id = modelId, tuple_key = new { user = $"user:{scenario.TargetId:N}", relation = "invoice_issuer", @object = $"tenant:{scenario.TenantId:N}" }, consistency = "HIGHER_CONSISTENCY" });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("allowed").GetBoolean();
    }

    internal async Task AssertSlotHeldAsync(AuthorizationReconciliationScenario scenario)
    {
        await using var source = Source(1);
        var store = new PostgresTenantAuthorizationAdministrationStore(source);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM tenancy.tenant_authorization_proposals WHERE tenant_id=@tenant AND status IN (1,4)", scenario.TenantId));
        if (!await store.IsInitialOwnerAsync(scenario.TenantId, scenario.OwnerId, default)) return;
        var competing = await store.ProposeAsync(TenantAuthorizationActor.Create(scenario.OwnerId),
            TenantAuthorizationProposalIntent.PermissionChange(TenantAuthorizationProposalKind.GrantPermission, scenario.TenantId,
                scenario.TargetId, "invoices.view", 1, Guid.NewGuid().ToString("N")), DateTimeOffset.UtcNow, default);
        Assert.Equal(TenantAuthorizationProposalResultStatus.AuthorizationRevisionConflict, competing.Status);
    }

    internal Task SetTenantActiveAsync(Guid tenantId, bool active) => SqlAsync(
        "UPDATE tenancy.tenants SET availability=@availability WHERE id=@tenant", ("tenant", tenantId), ("availability", (short)(active ? 1 : 2)));
    internal async Task SqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var source = Source(1, owner: true);
        await using var command = source.CreateCommand(sql);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
    internal async Task<long> CountAsync(string sql, Guid tenantId)
    {
        await using var source = Source(1, owner: true); await using var command = source.CreateCommand(sql);
        command.Parameters.AddWithValue("tenant", tenantId); return (long)(await command.ExecuteScalarAsync())!;
    }
    internal Task TerminateAsync(string name) => SqlAsync("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name=@name", ("name", name));
    internal async Task WaitForLockWaitAsync(string name)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var source = Source(1, owner: true);
        while (true)
        {
            await using var command = source.CreateCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name=@name AND wait_event='advisory')");
            command.Parameters.AddWithValue("name", name);
            if (await command.ExecuteScalarAsync(timeout.Token) is true) return;
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }
}

internal sealed class AuthorizationWriteProxy : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly HttpClient upstream;
    private int writes;
    private int readbackRejection;
    private int rejectedReadbacks;
    internal Uri Uri { get; }
    internal int Writes => Volatile.Read(ref writes);
    internal int ReadbackRejection { get => Volatile.Read(ref readbackRejection); set => Volatile.Write(ref readbackRejection, value); }
    internal int RejectedReadbacks => Volatile.Read(ref rejectedReadbacks);
    internal TaskCompletionSource WriteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource WriteForwarded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ResponseFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private AuthorizationWriteProxy(WebApplication app, HttpClient upstream, Uri uri) { this.app = app; this.upstream = upstream; Uri = uri; }
    internal static async Task<AuthorizationWriteProxy> StartAsync(Uri target, bool holdBefore = false, bool holdAfter = false)
    {
        var builder = WebApplication.CreateSlimBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var upstream = new HttpClient { BaseAddress = target, Timeout = TimeSpan.FromSeconds(20) };
        AuthorizationWriteProxy? proxy = null;
        app.Run(async context =>
        {
            var current = proxy!;
            var write = context.Request.Path.Value!.EndsWith("/write", StringComparison.Ordinal);
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            if (write)
            {
                Interlocked.Increment(ref current.writes); current.WriteEntered.TrySetResult();
                if (holdBefore) await current.Release.Task.WaitAsync(budget.Token);
            }
            else if (current.ReadbackRejection is 400 or 404)
            {
                var status = current.ReadbackRejection;
                Interlocked.Increment(ref current.rejectedReadbacks);
                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = status == 400 ? "validation_error" : "store_id_not_found",
                    message = "Synthetic readback endpoint rejection after completed write"
                }, cancellationToken: budget.Token);
                return;
            }
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.Path + context.Request.QueryString);
            if (context.Request.ContentLength is > 0 || context.Request.Headers.TransferEncoding.Count > 0)
            {
                using var buffer = new MemoryStream(); await context.Request.Body.CopyToAsync(buffer, budget.Token);
                request.Content = new ByteArrayContent(buffer.ToArray()); request.Content.Headers.ContentType = new("application/json");
            }
            using var response = await upstream.SendAsync(request, budget.Token);
            if (write)
            {
                if (response.IsSuccessStatusCode) current.WriteForwarded.TrySetResult();
                if (holdAfter) await current.Release.Task.WaitAsync(budget.Token);
            }
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = "application/json";
            try { await response.Content.CopyToAsync(context.Response.Body, budget.Token); }
            finally { if (write) current.ResponseFinished.TrySetResult(); }
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        proxy = new(app, upstream, new Uri(address));
        return proxy;
    }
    public async ValueTask DisposeAsync()
    {
        Release.TrySetResult();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await app.StopAsync(timeout.Token); }
        finally { await app.DisposeAsync(); upstream.Dispose(); }
    }
}
