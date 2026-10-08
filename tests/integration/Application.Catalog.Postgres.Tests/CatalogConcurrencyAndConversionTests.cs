using Application.Catalog;
using Application.Tenancy;
using Npgsql;
using Xunit;

namespace Application.Catalog.Postgres.Tests;

public sealed class CatalogConcurrencyAndConversionTests : CatalogPostgresTestDatabase
{
    [Fact]
    public async Task SameKeyCreatesConvergeBeforeCodeConflictsAndDoNotLeaveExtraRows()
    {
        await ApplyCatalogSchemaAsync();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityAsync(actor, tenant);
        await using var source = NpgsqlDataSource.Create(await CreateRuntimeRoleAsync());
        var store = new PostgresCatalogStore(source);
        var context = await ContextAsync(tenant, actor);
        var units = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => new CreateCatalogUnit(store)
            .ExecuteAsync(context, new("EA", "Each", 2), "same-unit", CancellationToken.None)));
        Assert.Single(units, result => result.Status == CreateCatalogUnitStatus.Created);
        Assert.Equal(7, units.Count(result => result.Status == CreateCatalogUnitStatus.Replayed));
        Assert.Single(units.Select(result => result.Unit!.UnitId).Distinct());
        var unit = units[0].Unit!;
        var items = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => new CreateCatalogItem(store).ExecuteAsync(context,
            new("SKU", "Item", null, CatalogItemKind.Service, unit.UnitId, CatalogStockMode.NonStock), "same-item", CancellationToken.None)));
        Assert.Single(items, result => result.Status == CreateCatalogItemStatus.Created);
        Assert.Equal(7, items.Count(result => result.Status == CreateCatalogItemStatus.Replayed));
        Assert.Single(items.Select(result => result.Item!.ItemId).Distinct());
        Assert.Equal(CreateCatalogItemStatus.IdempotencyKeyConflict, (await new CreateCatalogItem(store).ExecuteAsync(context,
            new("SKU", "Changed", null, CatalogItemKind.Service, unit.UnitId, CatalogStockMode.NonStock), "same-item", CancellationToken.None)).Status);
        Assert.Equal(CreateCatalogItemStatus.CodeConflict, (await new CreateCatalogItem(store).ExecuteAsync(context,
            new("SKU", "Other", null, CatalogItemKind.Service, unit.UnitId, CatalogStockMode.NonStock), "different-key", CancellationToken.None)).Status);
    }

    [Fact]
    public async Task VersionedConversionsAvailabilityAndHistoricalReceiptsRemainExactAfterPublicationAndRename()
    {
        await ApplyCatalogSchemaAsync();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var otherTenant = Guid.NewGuid();
        await SeedAuthorityAsync(actor, tenant, otherTenant);
        await using var source = NpgsqlDataSource.Create(await CreateRuntimeRoleAsync());
        var store = new PostgresCatalogStore(source);
        var context = await ContextAsync(tenant, actor);
        var alternate = (await new CreateCatalogUnit(store).ExecuteAsync(context, new("ALT", "Alternate", 2), "alt", CancellationToken.None)).Unit!;
        var basis = (await new CreateCatalogUnit(store).ExecuteAsync(context, new("BASE", "Base", 2), "base", CancellationToken.None)).Unit!;
        var item = (await new CreateCatalogItem(store).ExecuteAsync(context, new(null, "Service", null, CatalogItemKind.Service,
            basis.UnitId, CatalogStockMode.AvailabilityOnly), "item", CancellationToken.None)).Item!;
        Assert.Equal(CatalogAvailability.Unavailable, item.Availability);
        var publish = new PublishCatalogConversion(store);
        var firstRequest = new PublishCatalogConversionRequest(alternate.UnitId, basis.UnitId, 0, 1m, 8m);
        var published = await publish.ExecuteAsync(context, firstRequest, "publish", CancellationToken.None);
        Assert.Equal(PublishCatalogConversionStatus.Published, published.Status);
        Assert.Equal(PublishCatalogConversionStatus.Replayed, (await publish.ExecuteAsync(context, firstRequest, "publish", CancellationToken.None)).Status);
        Assert.Equal(PublishCatalogConversionStatus.IdempotencyKeyConflict, (await publish.ExecuteAsync(context,
            firstRequest with { Denominator = 3m }, "publish", CancellationToken.None)).Status);
        Assert.Equal(PublishCatalogConversionStatus.RevisionConflict, (await publish.ExecuteAsync(context,
            firstRequest, "stale", CancellationToken.None)).Status);
        var selection = new CatalogLineSelection(item.ItemId, alternate.UnitId, 1m, 1);
        Assert.Equal(CatalogLineFactsStatus.ItemUnavailable, (await new SelectCatalogLineFacts(store).ExecuteAsync(context, selection, CancellationToken.None)).Status);
        var available = new ChangeCatalogAvailability(store);
        var changed = await available.ExecuteAsync(context, new(item.ItemId, 1, CatalogAvailability.Available), "availability", CancellationToken.None);
        Assert.Equal(ChangeCatalogAvailabilityStatus.Changed, changed.Status);
        var facts = (await new SelectCatalogLineFacts(store).ExecuteAsync(context, selection, CancellationToken.None)).Facts!;
        Assert.Equal(0.12m, facts.BaseQuantity);
        Assert.Equal(1m, facts.Quantity);
        var concurrent = await Task.WhenAll(
            publish.ExecuteAsync(context, firstRequest with { ExpectedRevision = 1, Denominator = 2 }, "publish2", CancellationToken.None),
            publish.ExecuteAsync(context, firstRequest with { ExpectedRevision = 1, Denominator = 3 }, "publish3", CancellationToken.None));
        Assert.Single(concurrent, result => result.Status == PublishCatalogConversionStatus.Published);
        Assert.Single(concurrent, result => result.Status == PublishCatalogConversionStatus.RevisionConflict);
        await new RenameCatalogUnit(store).ExecuteAsync(context, new(alternate.UnitId, 1, "Renamed"), "rename", CancellationToken.None);
        await available.ExecuteAsync(context, new(item.ItemId, 2, CatalogAvailability.Unavailable), "unavailable", CancellationToken.None);
        var replay = await available.ExecuteAsync(context, new(item.ItemId, 1, CatalogAvailability.Available), "availability", CancellationToken.None);
        Assert.Equal(ChangeCatalogAvailabilityStatus.Replayed, replay.Status);
        Assert.Equal(2, replay.Item?.Revision);
        Assert.Equal(CatalogAvailability.Available, replay.Item?.Availability);
        Assert.Equal("Alternate", facts.UnitName);
        Assert.Equal(1, facts.Conversion.Revision);
        Assert.Equal(0.12m, facts.BaseQuantity);
        Assert.Equal(8m, (await new GetCatalogConversion(store).ExecuteAsync(context, alternate.UnitId, basis.UnitId, 1, CancellationToken.None))?.Denominator);
        Assert.Null(await new GetCatalogConversion(store).ExecuteAsync(await ContextAsync(otherTenant, actor), alternate.UnitId, basis.UnitId, 1, CancellationToken.None));
        var nonstock = (await new CreateCatalogItem(store).ExecuteAsync(context, new(null, "No inventory promise", null,
            CatalogItemKind.Product, basis.UnitId, CatalogStockMode.NonStock), "nonstock", CancellationToken.None)).Item!;
        Assert.Equal(ChangeCatalogAvailabilityStatus.StockModeMismatch, (await available.ExecuteAsync(context,
            new(nonstock.ItemId, 1, CatalogAvailability.Available), "not-availability", CancellationToken.None)).Status);
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var overwrite = admin.CreateCommand();
        overwrite.CommandText = "UPDATE catalog.unit_conversions SET numerator = 2 WHERE tenant_id = @tenant";
        overwrite.Parameters.AddWithValue("tenant", tenant);
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => overwrite.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, immutable.SqlState);
    }

    [Fact]
    public async Task RetirementWinsBeforeBlockedCreationAndSelectionInsteadOfAdmittingStaleUnits()
    {
        await ApplyCatalogSchemaAsync();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityAsync(actor, tenant);
        await using var source = NpgsqlDataSource.Create(await CreateRuntimeRoleAsync());
        var store = new PostgresCatalogStore(source);
        var context = await ContextAsync(tenant, actor);
        var unit = (await new CreateCatalogUnit(store).ExecuteAsync(context, new("EA", "Each", 0), "unit", CancellationToken.None)).Unit!;
        var item = (await new CreateCatalogItem(store).ExecuteAsync(context, new(null, "Existing", null, CatalogItemKind.Product,
            unit.UnitId, CatalogStockMode.NonStock), "existing", CancellationToken.None)).Item!;
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var retirement = await admin.BeginTransactionAsync();
        await using (var update = new NpgsqlCommand("""
            UPDATE catalog.units SET status = 'retired', revision = revision + 1,
                retired_at = '2026-10-06T12:00:00Z', retired_by_account_id = @actor
            WHERE tenant_id = @tenant AND id = @id
            """, admin, retirement))
        {
            update.Parameters.AddWithValue("actor", actor); update.Parameters.AddWithValue("tenant", tenant); update.Parameters.AddWithValue("id", unit.UnitId);
            await update.ExecuteNonQueryAsync();
        }
        var create = new CreateCatalogItem(store).ExecuteAsync(context, new(null, "New", null, CatalogItemKind.Product,
            unit.UnitId, CatalogStockMode.NonStock), "new", CancellationToken.None);
        var select = new SelectCatalogLineFacts(store).ExecuteAsync(context, new(item.ItemId, unit.UnitId, 1), CancellationToken.None);
        await WaitForBlockedCatalogReadsAsync(2);
        await retirement.CommitAsync();
        Assert.Equal(CreateCatalogItemStatus.BaseUnitRetired, (await create.WaitAsync(TimeSpan.FromSeconds(15))).Status);
        Assert.Equal(CatalogLineFactsStatus.UnitRetired, (await select.WaitAsync(TimeSpan.FromSeconds(15))).Status);
    }

    [Fact]
    public async Task FailedReceiptRollsBackMasterEffectAndNewStoreReplaysDurableOriginalSnapshot()
    {
        await ApplyCatalogSchemaAsync();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityAsync(actor, tenant);
        var runtime = await CreateRuntimeRoleAsync();
        var context = await ContextAsync(tenant, actor);
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using (var fail = admin.CreateCommand())
        {
            fail.CommandText = """
                CREATE FUNCTION catalog.reject_test_receipt() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN RAISE EXCEPTION 'Synthetic atomic-receipt rejection' USING ERRCODE = '23514'; END $body$;
                CREATE TRIGGER reject_test_receipt BEFORE INSERT ON catalog.command_receipts
                    FOR EACH ROW EXECUTE FUNCTION catalog.reject_test_receipt();
                """;
            await fail.ExecuteNonQueryAsync();
        }
        await using (var source = NpgsqlDataSource.Create(runtime))
        {
            var command = new CreateCatalogUnit(new PostgresCatalogStore(source));
            await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteAsync(context, new("EA", "Each", 0), "unit", CancellationToken.None));
            Assert.Empty((await new ListCatalogUnits(new PostgresCatalogStore(source)).ExecuteAsync(context, new(10, null), CancellationToken.None)).Items);
        }
        await using (var release = admin.CreateCommand())
        {
            release.CommandText = "DROP TRIGGER reject_test_receipt ON catalog.command_receipts; DROP FUNCTION catalog.reject_test_receipt();";
            await release.ExecuteNonQueryAsync();
        }
        CatalogUnitSnapshot original;
        await using (var source = NpgsqlDataSource.Create(runtime))
        {
            var store = new PostgresCatalogStore(source);
            original = (await new CreateCatalogUnit(store).ExecuteAsync(context, new("EA", "Each", 0), "unit", CancellationToken.None)).Unit!;
            await new RenameCatalogUnit(store).ExecuteAsync(context, new(original.UnitId, 1, "Later display"), "rename", CancellationToken.None);
        }
        await using (var reopened = NpgsqlDataSource.Create(runtime))
        {
            var replay = await new CreateCatalogUnit(new PostgresCatalogStore(reopened)).ExecuteAsync(context, new("EA", "Each", 0), "unit", CancellationToken.None);
            Assert.Equal(CreateCatalogUnitStatus.Replayed, replay.Status);
            Assert.Equal(original, replay.Unit);
        }
    }

    [Fact]
    public async Task DirectStoreBrowseAndReadsValidateBeforeOpeningProvider()
    {
        await using var source = NpgsqlDataSource.Create("Host=unused.example.test;Database=unused;Timeout=1");
        var store = new PostgresCatalogStore(source);
        var context = await ContextAsync(Guid.NewGuid(), Guid.NewGuid());
        await Assert.ThrowsAsync<CatalogValidationException>(() => store.FindItemAsync(context, Guid.Empty, CancellationToken.None));
        await Assert.ThrowsAsync<CatalogValidationException>(() => store.FindUnitAsync(context, Guid.Empty, CancellationToken.None));
        await Assert.ThrowsAsync<CatalogValidationException>(() => store.ListItemsAsync(context, new(0, null), CancellationToken.None));
        await Assert.ThrowsAsync<CatalogValidationException>(() => store.ListUnitsAsync(context, new(51, null), CancellationToken.None));
        await Assert.ThrowsAsync<CatalogValidationException>(() => store.ListUnitsAsync(context, new(1, new(default, Guid.NewGuid())), CancellationToken.None));
    }

    private async Task WaitForBlockedCatalogReadsAsync(int expected)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(budget.Token);
        while (true)
        {
            await using var query = connection.CreateCommand();
            query.CommandText = "SELECT count(*) FROM pg_stat_activity WHERE usename LIKE 'catalog_runtime_%' AND wait_event_type = 'Lock' AND query LIKE '%FOR SHARE%'";
            if ((long)(await query.ExecuteScalarAsync(budget.Token))! >= expected) return;
            await Task.Yield();
        }
    }

    private static async Task<TenantContext> ContextAsync(Guid tenant, Guid actor) =>
        (await new ResolveTenantContext(new Memberships()).ExecuteAsync(actor, tenant, CancellationToken.None))!;

    private sealed class Memberships : ITenantMembershipDirectory
    {
        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TenantMembership>>([]);
    }
}
