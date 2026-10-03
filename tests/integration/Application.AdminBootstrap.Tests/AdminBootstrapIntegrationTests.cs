using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.AdminBootstrap;
using Application.IdentityAccess;
using Application.PlatformAdministration;
using Application.PlatformAdministration.Postgres;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;
using OpenFga.Sdk.Model;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.AdminBootstrap.Tests;

public sealed class AdminBootstrapIntegrationTests : IAsyncLifetime
{
    private const ushort OpenFgaPort = 8080;
    private const string RuntimeRole = "application_admin_bootstrap_test";
    private const string RuntimePassword = "local-admin-bootstrap-test-only";

    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithDatabase("application_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private readonly IContainer _openFga =
        new ContainerBuilder(new DockerImage(repository: "openfga/openfga", tag: "v1.21.0"))
            .WithCommand("run", "--playground-enabled=false")
            .WithPortBinding(OpenFgaPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(OpenFgaPort).ForPath("/healthz")))
            .Build();

    private string ConnectionString => _database.GetConnectionString();

    [Fact]
    public async Task StoreMigrationOwnsOneBootstrapAndRetainsAuthoritativeAudit()
    {
        await ResetBootstrapRowsAsync();
        await using var context = PlatformAdministrationPostgresMigrations.CreateContext(ConnectionString);
        Assert.False(context.Database.HasPendingModelChanges());

        var store = new PostgresPlatformAdminBootstrapStore(context);
        var intent = Intent();
        var prepared = await store.PrepareAsync(
            intent,
            new DateTimeOffset(2026, 10, 2, 16, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        var retry = await store.PrepareAsync(
            intent,
            new DateTimeOffset(2026, 10, 2, 16, 1, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.False(prepared.Existing);
        Assert.True(retry.Existing);
        Assert.Equal(prepared.Snapshot, retry.Snapshot);
        Assert.Equal(1, await context.Principals.CountAsync());
        Assert.Equal(1, await context.AdminDevices.CountAsync());
        Assert.Equal(1, await context.BootstrapStates.CountAsync());
        Assert.Equal(1, await context.AuditEvents.CountAsync());

        var conflicting = Intent(
            deviceName: "different-admin-device",
            idempotencyKey: intent.IdempotencyKey,
            fingerprint: new string('B', 64));
        await Assert.ThrowsAsync<PlatformAdminBootstrapConflictException>(() =>
            store.PrepareAsync(conflicting, DateTimeOffset.UtcNow, CancellationToken.None));

        var completed = await store.CompleteAsync(
            prepared.Snapshot.BootstrapId,
            new DateTimeOffset(2026, 10, 2, 16, 2, 0, TimeSpan.Zero),
            CancellationToken.None);
        var completedRetry = await store.CompleteAsync(
            prepared.Snapshot.BootstrapId,
            new DateTimeOffset(2026, 10, 2, 16, 3, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(PlatformAdminBootstrapStatus.Completed, completed.Status);
        Assert.Equal(completed, completedRetry);
        Assert.Equal(2, await context.AuditEvents.CountAsync());
        Assert.Equal(
            [PlatformAdminAuditEventKind.BootstrapPrepared, PlatformAdminAuditEventKind.BootstrapCompleted],
            await context.AuditEvents.OrderBy(row => row.OccurredAt).Select(row => row.EventKind).ToArrayAsync());
    }

    [Fact]
    public async Task LeastPrivilegeBootstrapRoleCanPrepareAndCompleteButCannotRewriteAuthority()
    {
        await ResetBootstrapRowsAsync();
        await ProvisionBootstrapRuntimeRoleAsync();

        var runtimeConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = RuntimeRole,
            Password = RuntimePassword,
        }.ConnectionString;
        await using var context = PlatformAdministrationPostgresMigrations.CreateContext(runtimeConnectionString);
        var store = new PostgresPlatformAdminBootstrapStore(context);

        var prepared = await store.PrepareAsync(Intent(), DateTimeOffset.UtcNow, CancellationToken.None);
        var completed = await store.CompleteAsync(
            prepared.Snapshot.BootstrapId,
            DateTimeOffset.UtcNow.AddSeconds(1),
            CancellationToken.None);
        Assert.Equal(PlatformAdminBootstrapStatus.Completed, completed.Status);

        await using var runtime = new NpgsqlConnection(runtimeConnectionString);
        await runtime.OpenAsync();
        await using var forbidden = new NpgsqlCommand(
            "UPDATE platform_administration.principals SET availability = 2",
            runtime);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => forbidden.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);

        await using var delete = new NpgsqlCommand(
            "DELETE FROM platform_administration.audit_events",
            runtime);
        denied = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    [Fact]
    public async Task PendingBootstrapResumesThroughRealOpenFgaAndDuplicateRetryIsSafe()
    {
        await ResetBootstrapRowsAsync();
        var (client, configuration) = await CreatePlatformAuthorizationAsync();
        using (client)
        {
            await using var context = PlatformAdministrationPostgresMigrations.CreateContext(ConnectionString);
            var store = new PostgresPlatformAdminBootstrapStore(context);
            var intent = Intent();
            var pending = await store.PrepareAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.Equal(PlatformAdminBootstrapStatus.PendingAuthorization, pending.Snapshot.Status);

            var provisioner = new OpenFgaInitialPlatformAdministratorProvisioner(client, configuration);
            var coordinator = new PlatformAdminBootstrapCoordinator(store, provisioner, TimeProvider.System);

            var resumed = await coordinator.ExecuteAsync(intent, CancellationToken.None);
            var replayed = await coordinator.ExecuteAsync(intent, CancellationToken.None);

            Assert.Equal(PlatformAdminBootstrapExecutionKind.Resumed, resumed.ExecutionKind);
            Assert.Equal(PlatformAdminBootstrapExecutionKind.Replayed, replayed.ExecutionKind);
            Assert.Equal(PlatformAdminBootstrapStatus.Completed, replayed.Snapshot.Status);

            var check = await client.Check(
                new ClientCheckRequest
                {
                    User = $"user:{resumed.Snapshot.PrincipalId:N}",
                    Relation = "can_access_admin",
                    Object = "platform:root",
                },
                new ClientCheckOptions
                {
                    StoreId = configuration.StoreId,
                    AuthorizationModelId = configuration.AuthorizationModelId,
                    Consistency = ConsistencyPreference.HIGHERCONSISTENCY,
                },
                CancellationToken.None);
            Assert.True(check.Allowed is true);
            Assert.Equal(2, await context.AuditEvents.CountAsync());
        }
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _openFga.StartAsync());
        await using var context = PlatformAdministrationPostgresMigrations.CreateContext(ConnectionString);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _openFga.DisposeAsync();
        await _database.DisposeAsync();
    }

    private static PlatformAdminBootstrapIntent Intent(
        string deviceName = "admin-laptop",
        string idempotencyKey = "bootstrap-2026-10-02",
        string? fingerprint = null) =>
        PlatformAdminBootstrapIntent.Create(
            ExternalIdentity.Create("https://identity.example.test", "admin-subject"),
            AdminDeviceCertificateFingerprint.Create(fingerprint ?? new string('A', 64)),
            deviceName,
            idempotencyKey);

    private async Task ResetBootstrapRowsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            TRUNCATE TABLE
                platform_administration.audit_events,
                platform_administration.bootstrap_state,
                platform_administration.admin_devices,
                platform_administration.principals
            RESTART IDENTITY CASCADE;
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ProvisionBootstrapRuntimeRoleAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using (var create = new NpgsqlCommand(
            $"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{RuntimeRole}') THEN
                    CREATE ROLE {RuntimeRole} LOGIN PASSWORD '{RuntimePassword}'
                        NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                END IF;
            END $$;
            """,
            connection))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using (var setRole = new NpgsqlCommand(
            "SELECT set_config('app.provision_admin_bootstrap_role', @role, false)",
            connection))
        {
            setRole.Parameters.AddWithValue("role", RuntimeRole);
            await setRole.ExecuteScalarAsync();
        }

        var root = FindRepositoryRoot();
        var script = await File.ReadAllTextAsync(
            Path.Combine(root, "deploy", "database", "grant-admin-bootstrap.sql"));
        await using var grant = new NpgsqlCommand(script, connection);
        await grant.ExecuteNonQueryAsync();
    }

    private async Task<(OpenFgaClient Client, PlatformOpenFgaConfiguration Configuration)>
        CreatePlatformAuthorizationAsync()
    {
        var apiUrl = $"http://127.0.0.1:{_openFga.GetMappedPublicPort(OpenFgaPort)}";
        using var administrativeClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        using var storeResponse = await administrativeClient.PostAsJsonAsync(
            "/stores",
            new { name = "platform-admin-bootstrap-tests" });
        storeResponse.EnsureSuccessStatusCode();
        using var storeBody = JsonDocument.Parse(await storeResponse.Content.ReadAsStringAsync());
        var storeId = storeBody.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("OpenFGA did not return a store ID.");

        var modelJson = await File.ReadAllTextAsync(Path.Combine(
            FindRepositoryRoot(),
            "infrastructure",
            "authorization",
            "openfga",
            "platform-authorization-model.json"));
        using var modelContent = new StringContent(modelJson, Encoding.UTF8, "application/json");
        using var modelResponse = await administrativeClient.PostAsync(
            $"/stores/{storeId}/authorization-models",
            modelContent);
        modelResponse.EnsureSuccessStatusCode();
        using var modelBody = JsonDocument.Parse(await modelResponse.Content.ReadAsStringAsync());
        var modelId = modelBody.RootElement.GetProperty("authorization_model_id").GetString()
            ?? throw new InvalidOperationException("OpenFGA did not return an authorization model ID.");

        var configuration = new PlatformOpenFgaConfiguration(
            new Uri(apiUrl),
            storeId,
            modelId,
            TimeSpan.FromSeconds(5),
            1,
            100,
            new Credentials { Method = CredentialsMethod.None });
        return (new OpenFgaClient(configuration.ToClientConfiguration()), configuration);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Application.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Cannot locate Application.slnx from test output.");
    }
}
