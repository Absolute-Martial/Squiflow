using System.Net;
using Application.Catalog;
using Application.Tenancy;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class PricingReferenceReaderTests
{
    [Fact]
    public async Task RetiredReferencesPreserveSavedRetriesAndHistoryButRefuseNewEffects()
    {
        using var fixture = new PricingHostFixture(realReferences: true);
        fixture.Permissions.Edit = true;
        fixture.Permissions.Publish = true;
        fixture.Permissions.View = true;
        using var created = await fixture.PostAsync("drafts", fixture.DraftBody(), "saved-draft");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var originalDraft = await created.Content.ReadAsStringAsync();
        using var json = System.Text.Json.JsonDocument.Parse(originalDraft);
        var revisionId = json.RootElement.GetProperty("revisionId").GetGuid();
        using var published = await fixture.PostAsync($"revisions/{revisionId:D}/publish", new { }, "saved-publication");
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var originalPublication = await published.Content.ReadAsStringAsync();
        fixture.Catalog.Item = fixture.Catalog.Item with { Status = CatalogEntityStatus.Retired };
        fixture.Catalog.Unit = fixture.Catalog.Unit with { Status = CatalogEntityStatus.Retired };
        using var draftReplay = await fixture.PostAsync("drafts", fixture.DraftBody(), "saved-draft");
        Assert.Equal(originalDraft, await draftReplay.Content.ReadAsStringAsync());
        Assert.Equal("true", draftReplay.Headers.GetValues("Idempotency-Replayed").Single());
        using var publicationReplay = await fixture.PostAsync($"revisions/{revisionId:D}/publish", new { }, "saved-publication");
        Assert.Equal(originalPublication, await publicationReplay.Content.ReadAsStringAsync());
        using var history = await fixture.PostAsync("history", new { itemId = fixture.Item, unitId = fixture.Unit, currencyCode = "USD" });
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var historyJson = System.Text.Json.JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Single(historyJson.RootElement.GetProperty("items").EnumerateArray());
        using var fresh = await fixture.PostAsync("drafts", fixture.DraftBody(), "new-draft");
        Assert.Equal(HttpStatusCode.BadRequest, fresh.StatusCode);
        Assert.Equal(2, fixture.Store.Effects);
        fixture.Permissions.Edit = false;
        using var revoked = await fixture.PostAsync("drafts", fixture.DraftBody(), "saved-draft");
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }

    [Fact]
    public async Task AdapterValidatesTenantOwnedActiveCompatibleUnitsBeforeDraftEffects()
    {
        using var fixture = new PricingHostFixture(realReferences: true);
        fixture.Permissions.Edit = true;
        fixture.Catalog.Unit = fixture.Catalog.Unit with { TenantId = Guid.NewGuid() };
        using var foreign = await fixture.PostAsync("drafts", fixture.DraftBody(), "foreign");
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(0, fixture.Store.Effects);
        fixture.Catalog.Unit = fixture.Catalog.Unit with { TenantId = fixture.Tenant, Status = CatalogEntityStatus.Retired };
        using var retired = await fixture.PostAsync("drafts", fixture.DraftBody(), "retired");
        Assert.Equal(HttpStatusCode.BadRequest, retired.StatusCode);
        Assert.Equal(0, fixture.Store.Effects);
        fixture.Catalog.Unit = fixture.Catalog.Unit with { Status = CatalogEntityStatus.Active };
        using var created = await fixture.PostAsync("drafts", fixture.DraftBody(), "valid");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(1, fixture.Store.Effects);
        Assert.Equal(0, fixture.Catalog.ArithmeticCalls);
    }

    [Fact]
    public async Task NonBaseUnitRequiresPinnedCatalogConversionButNeverConvertsPriceOrQuantity()
    {
        using var fixture = new PricingHostFixture(realReferences: true);
        fixture.Permissions.Edit = true;
        var baseUnit = Guid.NewGuid();
        fixture.Catalog.Item = fixture.Catalog.Item with { BaseUnitId = baseUnit };
        fixture.Catalog.TargetUnit = fixture.Catalog.Unit with { UnitId = baseUnit, Code = "BASE" };
        fixture.Catalog.Conversion = new(fixture.Tenant, fixture.Unit, baseUnit, 3, 12, 1, fixture.Account, fixture.Now);
        using var missing = await fixture.PostAsync("drafts", fixture.DraftBody(), "missing-conversion");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var wrong = await fixture.PostAsync("drafts", fixture.DraftBody(conversionRevision: 2), "wrong-conversion");
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(0, fixture.Store.Effects);
        using var compatible = await fixture.PostAsync("drafts", fixture.DraftBody(conversionRevision: 3), "compatible");
        Assert.Equal(HttpStatusCode.Created, compatible.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await compatible.Content.ReadAsStringAsync());
        Assert.Equal(20, json.RootElement.GetProperty("baseUnitPrice").GetDecimal());
        Assert.Equal(3, json.RootElement.GetProperty("conversionRevision").GetInt64());
        Assert.Equal(0, fixture.Catalog.ArithmeticCalls);
    }
}

// Public Catalog ports under control, not a replacement Pricing implementation.
internal sealed class PricingCatalogReferences : ICatalogStore, ICatalogConversionStore
{
    internal CatalogItemSnapshot Item { get; set; }
    internal CatalogUnitSnapshot Unit { get; set; }
    internal CatalogUnitSnapshot? TargetUnit { get; set; }
    internal CatalogConversionSnapshot? Conversion { get; set; }
    internal int ArithmeticCalls { get; private set; }
    internal PricingCatalogReferences(Guid tenant, Guid actor, Guid item, Guid unit, DateTimeOffset now)
    {
        Item = new(item, tenant, "ITEM", "Item", null, CatalogItemKind.Product, CatalogEntityStatus.Active, unit, CatalogStockMode.NonStock, 1, actor, now);
        Unit = new(unit, tenant, "EA", "Each", 0, CatalogEntityStatus.Active, 1, actor, now);
    }
    public Task<CatalogItemSnapshot?> FindItemAsync(TenantContext tenantContext, Guid itemId, CancellationToken ct) =>
        Task.FromResult(Item.ItemId == itemId && Item.TenantId == tenantContext.TenantId ? Item : null);
    public Task<CatalogUnitSnapshot?> FindUnitAsync(TenantContext tenantContext, Guid unitId, CancellationToken ct)
    {
        var unit = Unit.UnitId == unitId ? Unit : TargetUnit?.UnitId == unitId ? TargetUnit : null;
        return Task.FromResult(unit?.TenantId == tenantContext.TenantId ? unit : null);
    }
    public Task<CatalogConversionSnapshot?> FindConversionAsync(TenantContext tenantContext, Guid sourceUnitId, Guid targetUnitId, long revision, CancellationToken ct) =>
        Task.FromResult(Conversion?.TenantId == tenantContext.TenantId && Conversion.SourceUnitId == sourceUnitId &&
            Conversion.TargetUnitId == targetUnitId && Conversion.Revision == revision ? Conversion : null);
    public Task<CatalogLineFactsResult> SelectLineFactsAsync(TenantContext tenantContext, CatalogLineSelection selection, CancellationToken ct)
    { ArithmeticCalls++; throw new InvalidOperationException("Pricing must not run Catalog quantity arithmetic."); }
    public Task<CreateCatalogUnitResult> CreateUnitAsync(TenantContext tenantContext, CatalogUnitIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
    public Task<CreateCatalogItemResult> CreateItemAsync(TenantContext tenantContext, CatalogItemIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
    public Task<CatalogUnitPage> ListUnitsAsync(TenantContext tenantContext, ListCatalogUnitsRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<CatalogItemPage> ListItemsAsync(TenantContext tenantContext, ListCatalogItemsRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<RenameCatalogUnitResult> RenameUnitAsync(TenantContext tenantContext, RenameCatalogUnitRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
    public Task<RenameCatalogItemResult> RenameItemAsync(TenantContext tenantContext, RenameCatalogItemRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
    public Task<RetireCatalogUnitResult> RetireUnitAsync(TenantContext tenantContext, RetireCatalogUnitRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
    public Task<RetireCatalogItemResult> RetireItemAsync(TenantContext tenantContext, RetireCatalogItemRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
    public Task<PublishCatalogConversionResult> PublishConversionAsync(TenantContext tenantContext, CatalogConversionIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
}
