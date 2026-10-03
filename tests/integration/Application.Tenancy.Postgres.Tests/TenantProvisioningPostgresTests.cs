using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Application.Tenancy.Postgres.Tests;

public sealed class TenantProvisioningPostgresTests : PostgresTestDatabase
{
    [Fact]
    public async Task ConcurrentSameKeyCreatesOneActiveTenantAndReplaysExactFacts()
    {
        await MigrateAsync();
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantProvisioningStore(source);
        var actor = Actor();
        var intent = TenantProvisioningIntent.Create("  Cafe\u0301 Tenant  ", "  request-key  ");
        var timestamp = new DateTimeOffset(2026, 10, 3, 12, 1, 2, TimeSpan.Zero).AddTicks(1234567);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            store.ProvisionAsync(actor, intent, timestamp, CancellationToken.None)));
        Assert.Single(results, result => result.Status == ProvisionTenantStatus.Created);
        Assert.Equal(7, results.Count(result => result.Status == ProvisionTenantStatus.Replayed));
        Assert.All(results, result => Assert.Equal(results[0].Tenant, result.Tenant));
        var tenant = Assert.IsType<TenantProvisioningSnapshot>(results[0].Tenant);
        Assert.Equal("Café Tenant", tenant.DisplayName);
        Assert.Equal(TenantAvailability.Active, tenant.Availability);
        Assert.Equal(timestamp.AddTicks(-7), tenant.ActivatedAt);
        Assert.Equal(actor.PrincipalId, tenant.ProvisionedByPrincipalId);
        Assert.Equal(actor.DeviceId, tenant.ProvisionedByDeviceId);
        Assert.Equal(1L, await CountAsync("tenancy.tenants"));
        Assert.Equal(1L, await CountAsync("tenancy.tenant_provisioning_receipts"));

        var changed = await store.ProvisionAsync(actor,
            TenantProvisioningIntent.Create("Other Tenant", "request-key"), timestamp,
            CancellationToken.None);
        Assert.Equal(ProvisionTenantStatus.IdempotencyKeyConflict, changed.Status);
        Assert.Null(changed.Tenant);
        Assert.Equal(1L, await CountAsync("tenancy.tenants"));
    }

    [Fact]
    public async Task SameKeyBelongsIndependentlyToEachPrincipal()
    {
        await MigrateAsync();
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantProvisioningStore(source);
        var intent = TenantProvisioningIntent.Create("Tenant", "shared");
        var timestamp = DateTimeOffset.UnixEpoch;

        var first = await store.ProvisionAsync(Actor(), intent, timestamp, CancellationToken.None);
        var second = await store.ProvisionAsync(Actor(), intent, timestamp, CancellationToken.None);
        Assert.Equal(ProvisionTenantStatus.Created, first.Status);
        Assert.Equal(ProvisionTenantStatus.Created, second.Status);
        Assert.NotEqual(first.Tenant!.TenantId, second.Tenant!.TenantId);
        Assert.Equal(2L, await CountAsync("tenancy.tenants"));
        Assert.Equal(2L, await CountAsync("tenancy.tenant_provisioning_receipts"));
    }

    [Fact]
    public async Task ReceiptWriteFailureRollsBackTheTenant()
    {
        await MigrateAsync();
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE FUNCTION public.reject_provisioning_receipt() RETURNS trigger
                LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture receipt failure'; END $$;
                CREATE TRIGGER reject_provisioning_receipt
                    BEFORE INSERT ON tenancy.tenant_provisioning_receipts
                    FOR EACH ROW EXECUTE FUNCTION public.reject_provisioning_receipt();
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantProvisioningStore(source);
        var error = await Assert.ThrowsAsync<PostgresException>(() => store.ProvisionAsync(
            Actor(), TenantProvisioningIntent.Create("Tenant", "failed"), DateTimeOffset.UnixEpoch,
            CancellationToken.None));
        Assert.Contains("fixture receipt failure", error.MessageText, StringComparison.Ordinal);
        Assert.Equal(0L, await CountAsync("tenancy.tenants"));
        Assert.Equal(0L, await CountAsync("tenancy.tenant_provisioning_receipts"));
    }

    [Fact]
    public async Task MigrationCannotDiscardRetainedProvisioningReceipt()
    {
        await MigrateAsync();
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantProvisioningStore(source);
        var result = await store.ProvisionAsync(Actor(),
            TenantProvisioningIntent.Create("Retained", "retained"), DateTimeOffset.UnixEpoch,
            CancellationToken.None);
        await using var db = CreateContext();

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync("0"));
        Assert.Contains("receipts exist", error.MessageText, StringComparison.Ordinal);
        Assert.Equal(1L, await CountAsync("tenancy.tenants"));
        Assert.Equal(1L, await CountAsync("tenancy.tenant_provisioning_receipts"));
        // EF rolls back each migration separately: a later empty lifecycle migration
        // may be removed before the provisioning migration refuses data loss.
        Assert.Contains("202610030002_TenantProvisioning", await db.Database.GetAppliedMigrationsAsync());
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var replay = await store.ProvisionAsync(
            TenantProvisioningActor.Create(result.Tenant!.ProvisionedByPrincipalId,
                result.Tenant.ProvisionedByDeviceId),
            TenantProvisioningIntent.Create("Retained", "retained"), DateTimeOffset.UnixEpoch,
            CancellationToken.None);
        Assert.Equal(ProvisionTenantStatus.Replayed, replay.Status);
        Assert.Equal(result.Tenant, replay.Tenant);
    }

    [Fact]
    public void IntentRejectsMalformedTextAndNormalizesTheDisplayName()
    {
        var decomposed = TenantProvisioningIntent.Create(" Cafe\u0301 ", " key ");
        var composed = TenantProvisioningIntent.Create("Café", "key");
        Assert.Equal("Café", decomposed.DisplayName);
        Assert.Equal(composed.Fingerprint, decomposed.Fingerprint);
        Assert.Equal("key", decomposed.IdempotencyKey);
        Assert.Throws<ArgumentException>(() => TenantProvisioningIntent.Create("Name\0", "key"));
        Assert.Throws<ArgumentException>(() => TenantProvisioningIntent.Create("Name", "key\n"));
        Assert.Throws<ArgumentException>(() => TenantProvisioningIntent.Create("\ud800", "key"));
        Assert.Throws<ArgumentException>(() => TenantProvisioningIntent.Create("Name", "\ud800"));
    }

    private async Task MigrateAsync() => await CreateMigrationRunner()
        .ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

    private async Task<long> CountAsync(string table)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM {table}";
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException());
    }

    private static TenantProvisioningActor Actor() =>
        TenantProvisioningActor.Create(Guid.NewGuid(), Guid.NewGuid());
}
