using Application.Catalog;
using Application.Tenancy;
using Xunit;

namespace Application.Catalog.Tests;

public sealed class CatalogContractTests
{
    [Fact]
    public void UnitIntentNormalizesCodeAndRetainsMathematicalPrecision()
    {
        var intent = CatalogUnitIntent.Create(new CreateCatalogUnitRequest(" kg ", "Kilogram", 3));

        Assert.Equal("KG", intent.Code);
        Assert.Equal("Kilogram", intent.Name);
        Assert.Equal(3, intent.Precision);
        Assert.NotEqual(
            intent.Fingerprint,
            CatalogUnitIntent.Create(new CreateCatalogUnitRequest("KG", "Kilogram", 2)).Fingerprint);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void UnitPrecisionIsBounded(int precision)
    {
        var exception = Assert.Throws<CatalogValidationException>(() =>
            CatalogUnitIntent.Create(new CreateCatalogUnitRequest("EA", "Each", precision)));

        Assert.Equal("precision_invalid", exception.Code);
    }

    [Fact]
    public void ProductAndServiceDoNotSelectStockMode()
    {
        var unitId = Guid.NewGuid();
        var product = CatalogItemIntent.Create(new CreateCatalogItemRequest(
            "P-1", "A service-shaped product", null, CatalogItemKind.Product, unitId, CatalogStockMode.NonStock));
        var service = CatalogItemIntent.Create(new CreateCatalogItemRequest(
            "S-1", "A stock-tracked service", null, CatalogItemKind.Service, unitId, CatalogStockMode.PreciseStock));

        Assert.Equal(CatalogItemKind.Product, product.Kind);
        Assert.Equal(CatalogStockMode.NonStock, product.StockMode);
        Assert.Equal(CatalogItemKind.Service, service.Kind);
        Assert.Equal(CatalogStockMode.PreciseStock, service.StockMode);
    }

    [Fact]
    public async Task RenameCommandNormalizesDisplayFieldsAndCarriesExpectedRevision()
    {
        var store = new RecordingCatalogStore();
        var command = new RenameCatalogItem(store);
        var context = await CreateTenantContextAsync();
        var itemId = Guid.NewGuid();

        await command.ExecuteAsync(
            context,
            new RenameCatalogItemRequest(itemId, 4, "  Renamed  ", "  Description  "),
            "rename-key",
            CancellationToken.None);

        Assert.Equal(itemId, store.ItemRequest?.ItemId);
        Assert.Equal(4, store.ItemRequest?.ExpectedRevision);
        Assert.Equal("Renamed", store.ItemRequest?.Name);
        Assert.Equal("Description", store.ItemRequest?.Description);
        Assert.Equal("rename-key", store.IdempotencyKey);
        Assert.NotNull(store.Fingerprint);
    }

    [Fact]
    public void LineFactsRetainIdentityAndConversionFactsWithoutReinterpretingQuantity()
    {
        var itemId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var facts = new CatalogLineFacts(
            itemId, "PANEL", unitId, "Printed panel", "EA", "Each", 1.25m, 2, 7, 9,
            new CatalogConversionFacts(unitId, unitId, 9, 1m, 1m),
            QuantityArithmetic.Version1, QuantityArithmetic.Version1Rounding);

        Assert.Equal(itemId, facts.ItemId);
        Assert.Equal("PANEL", facts.ItemCode);
        Assert.Equal("Printed panel", facts.ItemName);
        Assert.Equal("Each", facts.UnitName);
        Assert.Equal(1.25m, facts.Quantity);
        Assert.Equal(2, facts.UnitPrecision);
        Assert.Equal(9, facts.Conversion.Revision);
        Assert.Equal(1m, facts.Conversion.Numerator);
        Assert.Equal(1m, facts.Conversion.Denominator);
    }

    private static async Task<TenantContext> CreateTenantContextAsync() =>
        (await new ResolveTenantContext(new ActiveMembershipDirectory())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None))!;

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
            Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class RecordingCatalogStore : ICatalogStore
    {
        public RenameCatalogItemRequest? ItemRequest { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? Fingerprint { get; private set; }

        public Task<CreateCatalogUnitResult> CreateUnitAsync(TenantContext tenantContext, CatalogUnitIntent intent, string idempotencyKey, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<CreateCatalogItemResult> CreateItemAsync(TenantContext tenantContext, CatalogItemIntent intent, string idempotencyKey, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<CatalogUnitSnapshot?> FindUnitAsync(TenantContext tenantContext, Guid unitId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<CatalogItemSnapshot?> FindItemAsync(TenantContext tenantContext, Guid itemId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<CatalogUnitPage> ListUnitsAsync(TenantContext tenantContext, ListCatalogUnitsRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<CatalogItemPage> ListItemsAsync(TenantContext tenantContext, ListCatalogItemsRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<RenameCatalogUnitResult> RenameUnitAsync(TenantContext tenantContext, RenameCatalogUnitRequest request, string idempotencyKey, string fingerprint, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<RenameCatalogItemResult> RenameItemAsync(TenantContext tenantContext, RenameCatalogItemRequest request, string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
        {
            ItemRequest = request;
            IdempotencyKey = idempotencyKey;
            Fingerprint = fingerprint;
            return Task.FromResult(new RenameCatalogItemResult(RenameCatalogItemStatus.Renamed, null));
        }

        public Task<RetireCatalogUnitResult> RetireUnitAsync(TenantContext tenantContext, RetireCatalogUnitRequest request, string idempotencyKey, string fingerprint, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<RetireCatalogItemResult> RetireItemAsync(TenantContext tenantContext, RetireCatalogItemRequest request, string idempotencyKey, string fingerprint, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<CatalogLineFactsResult> SelectLineFactsAsync(TenantContext tenantContext, CatalogLineSelection selection, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
