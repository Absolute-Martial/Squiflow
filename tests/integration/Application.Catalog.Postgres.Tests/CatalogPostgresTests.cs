using Application.Catalog;
using Application.Catalog.Postgres;
using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Application.Catalog.Postgres.Migrations;
using Npgsql;
using Xunit;

namespace Application.Catalog.Postgres.Tests;

public sealed class CatalogPostgresTests : CatalogPostgresTestDatabase
{
    [Fact]
    public async Task MigrationMatchesModelAndCreatesForcedTenantPolicies()
    {
        await ApplyCatalogSchemaAsync();

        await using var context = CatalogPostgresMigrations.CreateContext(ConnectionString);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Null(new CatalogFoundation().TargetModel.FindEntityType("Application.Catalog.Postgres.CatalogItemRow")!.FindProperty("Availability"));
        Assert.NotNull(new CatalogConversionsAndAvailability().TargetModel.FindEntityType("Application.Catalog.Postgres.CatalogItemRow")!.FindProperty("Availability"));

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.relname, c.relrowsecurity, c.relforcerowsecurity
            FROM pg_class AS c
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            WHERE n.nspname = 'catalog' AND c.relkind = 'r'
            ORDER BY c.relname
            """;
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        var tables = new Dictionary<string, (bool Rls, bool Forced)>(StringComparer.Ordinal);
        while (await reader.ReadAsync(CancellationToken.None))
            tables.Add(reader.GetString(0), (reader.GetBoolean(1), reader.GetBoolean(2)));

        Assert.Equal((true, true), tables["units"]);
        Assert.Equal((true, true), tables["items"]);
        Assert.Equal((true, true), tables["command_receipts"]);
        Assert.Equal((true, true), tables["unit_conversions"]);
    }

    [Fact]
    public async Task CatalogKeepsIdentityAcrossRenameRetirementAndCallerScopedReplay()
    {
        await ApplyCatalogSchemaAsync();
        var accountId = Guid.CreateVersion7();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await SeedAuthorityAsync(accountId, tenantA, tenantB);
        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresCatalogStore(dataSource, new FixedTimeProvider());
        var contextA = await CreateContextAsync(tenantA, accountId);
        var contextB = await CreateContextAsync(tenantB, accountId);

        var unit = await new CreateCatalogUnit(store).ExecuteAsync(
            contextA, new CreateCatalogUnitRequest("ea", "Each", 2), "unit-key", CancellationToken.None);
        var unitSnapshot = Assert.IsType<CatalogUnitSnapshot>(unit.Unit);
        Assert.Equal(CreateCatalogUnitStatus.Created, unit.Status);
        Assert.Equal(CreateCatalogUnitStatus.Replayed, (await new CreateCatalogUnit(store).ExecuteAsync(
            contextA, new CreateCatalogUnitRequest("EA", "Each", 2), "unit-key", CancellationToken.None)).Status);
        Assert.Equal(CreateCatalogUnitStatus.IdempotencyKeyConflict, (await new CreateCatalogUnit(store).ExecuteAsync(
            contextA, new CreateCatalogUnitRequest("EA", "Different", 2), "unit-key", CancellationToken.None)).Status);
        var renamedUnit = await new RenameCatalogUnit(store).ExecuteAsync(
            contextA, new RenameCatalogUnitRequest(unitSnapshot.UnitId, 1, "Each (display)"),
            "unit-rename-key", CancellationToken.None);
        Assert.Equal(RenameCatalogUnitStatus.Renamed, renamedUnit.Status);
        Assert.Equal(2, renamedUnit.Unit?.Revision);

        var item = await new CreateCatalogItem(store).ExecuteAsync(
            contextA,
            new CreateCatalogItemRequest("P-1", "Service-shaped product", null, CatalogItemKind.Product,
                unitSnapshot.UnitId, CatalogStockMode.NonStock),
            "item-key",
            CancellationToken.None);
        var itemSnapshot = Assert.IsType<CatalogItemSnapshot>(item.Item);
        Assert.Equal(CreateCatalogItemStatus.Created, item.Status);

        var facts = await new SelectCatalogLineFacts(store).ExecuteAsync(
            contextA, new CatalogLineSelection(itemSnapshot.ItemId, unitSnapshot.UnitId, 1.25m), CancellationToken.None);
        Assert.Equal(CatalogLineFactsStatus.Available, facts.Status);
        Assert.Equal(itemSnapshot.Name, facts.Facts?.ItemName);
        Assert.Equal(1.25m, facts.Facts?.Quantity);
        Assert.Equal(1, facts.Facts?.Conversion.Revision);
        Assert.Equal(2, facts.Facts?.UnitRevision);

        await using (var mutation = new NpgsqlConnection(runtimeConnectionString))
        {
            await mutation.OpenAsync(CancellationToken.None);
            await SetTenantAsync(mutation, tenantA);
            await using var command = mutation.CreateCommand();
            command.CommandText = "UPDATE catalog.units SET code = 'EA2', precision = 3 WHERE tenant_id = @tenant_id AND id = @unit_id";
            command.Parameters.AddWithValue("tenant_id", tenantA);
            command.Parameters.AddWithValue("unit_id", unitSnapshot.UnitId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(CancellationToken.None));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }

        var renamed = await new RenameCatalogItem(store).ExecuteAsync(
            contextA, new RenameCatalogItemRequest(itemSnapshot.ItemId, 1, "Renamed product", "History stays explainable"),
            "rename-key", CancellationToken.None);
        Assert.Equal(RenameCatalogItemStatus.Renamed, renamed.Status);
        Assert.Equal(2, renamed.Item?.Revision);
        var renamedReplay = await new RenameCatalogItem(store).ExecuteAsync(
            contextA, new RenameCatalogItemRequest(itemSnapshot.ItemId, 1, "Renamed product", "History stays explainable"),
            "rename-key", CancellationToken.None);
        Assert.Equal(RenameCatalogItemStatus.Replayed, renamedReplay.Status);
        Assert.Equal(2, renamedReplay.Item?.Revision);
        Assert.Equal(RenameCatalogItemStatus.RevisionConflict, (await new RenameCatalogItem(store).ExecuteAsync(
            contextA, new RenameCatalogItemRequest(itemSnapshot.ItemId, 1, "Stale", null), "stale-key", CancellationToken.None)).Status);

        var retired = await new RetireCatalogItem(store).ExecuteAsync(
            contextA, new RetireCatalogItemRequest(itemSnapshot.ItemId, 2), "retire-key", CancellationToken.None);
        Assert.Equal(RetireCatalogItemStatus.Retired, retired.Status);
        Assert.Equal(RetireCatalogItemStatus.Replayed, (await new RetireCatalogItem(store).ExecuteAsync(
            contextA, new RetireCatalogItemRequest(itemSnapshot.ItemId, 2), "retire-key", CancellationToken.None)).Status);
        Assert.Equal(CatalogEntityStatus.Retired, (await new GetCatalogItem(store).ExecuteAsync(
            contextA, itemSnapshot.ItemId, CancellationToken.None))?.Status);
        Assert.Equal(CatalogLineFactsStatus.ItemRetired, (await new SelectCatalogLineFacts(store).ExecuteAsync(
            contextA, new CatalogLineSelection(itemSnapshot.ItemId, unitSnapshot.UnitId, 1m), CancellationToken.None)).Status);
        Assert.Null(await new GetCatalogItem(store).ExecuteAsync(contextB, itemSnapshot.ItemId, CancellationToken.None));

        var units = await new ListCatalogUnits(store).ExecuteAsync(
            contextA, new ListCatalogUnitsRequest(10, null), CancellationToken.None);
        Assert.Contains(units.Items, candidate => candidate.UnitId == unitSnapshot.UnitId);
    }

    [Fact]
    public async Task RlsBlocksMissingOrForeignTenantContextAndRetiredUnitsRemainReadable()
    {
        await ApplyCatalogSchemaAsync();
        var accountId = Guid.CreateVersion7();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await SeedAuthorityAsync(accountId, tenantA, tenantB);
        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresCatalogStore(dataSource, new FixedTimeProvider());
        var contextA = await CreateContextAsync(tenantA, accountId);
        var unit = (await new CreateCatalogUnit(store).ExecuteAsync(
            contextA, new CreateCatalogUnitRequest("HR", "Hour", 0), "unit-key", CancellationToken.None)).Unit!;
        Assert.Equal(RetireCatalogUnitStatus.Retired, (await new RetireCatalogUnit(store).ExecuteAsync(
            contextA, new RetireCatalogUnitRequest(unit.UnitId, 1), "retire-key", CancellationToken.None)).Status);
        Assert.Equal(CatalogEntityStatus.Retired, (await new GetCatalogUnit(store).ExecuteAsync(
            contextA, unit.UnitId, CancellationToken.None))?.Status);
        Assert.Equal(CreateCatalogItemStatus.BaseUnitRetired, (await new CreateCatalogItem(store).ExecuteAsync(
            contextA, new CreateCatalogItemRequest(null, "Cannot use retired unit", null, CatalogItemKind.Service,
                unit.UnitId, CatalogStockMode.PreciseStock), "item-key", CancellationToken.None)).Status);

        await using (var reused = await dataSource.OpenConnectionAsync())
        {
            await using var contextValue = reused.CreateCommand();
            contextValue.CommandText = "SELECT NULLIF(current_setting('app.current_tenant', true), '')";
            Assert.IsType<DBNull>(await contextValue.ExecuteScalarAsync());
        }

        await using var connection = new NpgsqlConnection(runtimeConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using (var missing = connection.CreateCommand())
        {
            missing.CommandText = "SELECT count(*) FROM catalog.units";
            Assert.Equal(0L, (long)(await missing.ExecuteScalarAsync(CancellationToken.None))!);
        }
        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
                INSERT INTO catalog.units(tenant_id,id,code,name,precision,status,revision,created_by_account_id,created_at)
                VALUES(@tenant,@id,'FOREIGN','Foreign',0,'active',1,@actor,'2026-10-06T12:00:00Z')
                """;
            insert.Parameters.AddWithValue("tenant", tenantA); insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("actor", accountId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(CancellationToken.None));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
            await SetTenantAsync(connection, tenantB);
            var foreignInsert = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(CancellationToken.None));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, foreignInsert.SqlState);
        }
        await SetTenantAsync(connection, tenantB);
        await using var foreign = connection.CreateCommand();
        foreign.CommandText = "SELECT count(*) FROM catalog.units";
        Assert.Equal(0L, (long)(await foreign.ExecuteScalarAsync(CancellationToken.None))!);
    }

    private static async Task<TenantContext> CreateContextAsync(Guid tenantId, Guid accountId) =>
        (await new ResolveTenantContext(new ActiveMembershipDirectory())
            .ExecuteAsync(accountId, tenantId, CancellationToken.None))!;

    private static async Task SetTenantAsync(NpgsqlConnection connection, Guid tenantId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        command.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
