using Application.Catalog;
using Application.CoreApi.Authorization;
using Application.Tenancy;

namespace Application.CoreApi.Tests;

// Deliberate host seams only; actual catalog durability/races are tested against PostgreSQL.
internal sealed class CatalogHostAuthorization : ITenantCatalogAuthorization
{
    internal bool CanRead { get; set; }
    internal bool CanManage { get; set; }
    internal bool Unavailable { get; set; }
    internal int ReadChecks { get; private set; }
    internal int ManageChecks { get; private set; }

    public Task<bool> CanViewAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken)
    {
        ReadChecks++;
        return Decision(CanRead, cancellationToken);
    }

    public Task<bool> CanManageAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken)
    {
        ManageChecks++;
        return Decision(CanManage, cancellationToken);
    }

    private Task<bool> Decision(bool allowed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable) throw new AuthorizationProviderUnavailableException("catalog-authority-private-canary", new InvalidOperationException());
        return Task.FromResult(allowed);
    }
}

internal sealed class CatalogHostStore : ICatalogStore, ICatalogConversionStore, ICatalogAvailabilityStore
{
    internal int Calls { get; private set; }
    internal bool Fail { get; set; }
    internal CatalogUnitSnapshot? Unit { get; set; }
    internal CatalogItemSnapshot? Item { get; set; }
    internal CatalogConversionSnapshot? Conversion { get; set; }
    internal CatalogUnitPage? UnitPage { get; set; }
    internal CatalogItemPage? ItemPage { get; set; }
    internal CatalogLineFactsResult Selection { get; set; } = new(CatalogLineFactsStatus.ItemNotFound, null);
    internal CreateCatalogUnitStatus UnitCreateStatus { get; set; } = CreateCatalogUnitStatus.Created;
    internal CatalogLineSelection? LastSelection { get; private set; }
    internal ListCatalogUnitsRequest? LastUnitPage { get; private set; }

    public Task<CreateCatalogUnitResult> CreateUnitAsync(TenantContext tenantContext, CatalogUnitIntent intent,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        Unit ??= new(Guid.NewGuid(), tenantContext.TenantId, intent.Code, intent.Name, intent.Precision,
            CatalogEntityStatus.Active, 1, tenantContext.AccountId, Now);
        return Task.FromResult(new CreateCatalogUnitResult(UnitCreateStatus, Unit));
    }

    public Task<CreateCatalogItemResult> CreateItemAsync(TenantContext tenantContext, CatalogItemIntent intent,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        Item ??= new(Guid.NewGuid(), tenantContext.TenantId, intent.Code, intent.Name, intent.Description, intent.Kind,
            CatalogEntityStatus.Active, intent.BaseUnitId, intent.StockMode, 1, tenantContext.AccountId, Now,
            Availability: intent.StockMode == CatalogStockMode.AvailabilityOnly ? CatalogAvailability.Unavailable : null);
        return Task.FromResult(new CreateCatalogItemResult(CreateCatalogItemStatus.Created, Item));
    }

    public Task<CatalogUnitSnapshot?> FindUnitAsync(TenantContext tenantContext, Guid unitId, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(Unit?.TenantId == tenantContext.TenantId && Unit.UnitId == unitId ? Unit : null);
    }

    public Task<CatalogItemSnapshot?> FindItemAsync(TenantContext tenantContext, Guid itemId, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(Item?.TenantId == tenantContext.TenantId && Item.ItemId == itemId ? Item : null);
    }

    public Task<CatalogUnitPage> ListUnitsAsync(TenantContext tenantContext, ListCatalogUnitsRequest request, CancellationToken cancellationToken)
    {
        Observe(cancellationToken); LastUnitPage = request;
        return Task.FromResult(UnitPage ?? new CatalogUnitPage([], null));
    }

    public Task<CatalogItemPage> ListItemsAsync(TenantContext tenantContext, ListCatalogItemsRequest request, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(ItemPage ?? new CatalogItemPage([], null));
    }

    public Task<RenameCatalogUnitResult> RenameUnitAsync(TenantContext tenantContext, RenameCatalogUnitRequest request,
        string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(new RenameCatalogUnitResult(Unit is null ? RenameCatalogUnitStatus.NotFound : RenameCatalogUnitStatus.Renamed, Unit));
    }

    public Task<RenameCatalogItemResult> RenameItemAsync(TenantContext tenantContext, RenameCatalogItemRequest request,
        string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(new RenameCatalogItemResult(Item is null ? RenameCatalogItemStatus.NotFound : RenameCatalogItemStatus.Renamed, Item));
    }

    public Task<RetireCatalogUnitResult> RetireUnitAsync(TenantContext tenantContext, RetireCatalogUnitRequest request,
        string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(new RetireCatalogUnitResult(Unit is null ? RetireCatalogUnitStatus.NotFound : RetireCatalogUnitStatus.Retired, Unit));
    }

    public Task<RetireCatalogItemResult> RetireItemAsync(TenantContext tenantContext, RetireCatalogItemRequest request,
        string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(new RetireCatalogItemResult(Item is null ? RetireCatalogItemStatus.NotFound : RetireCatalogItemStatus.Retired, Item));
    }

    public Task<CatalogLineFactsResult> SelectLineFactsAsync(TenantContext tenantContext, CatalogLineSelection selection, CancellationToken cancellationToken)
    {
        Observe(cancellationToken); LastSelection = selection;
        return Task.FromResult(Selection);
    }

    public Task<PublishCatalogConversionResult> PublishConversionAsync(TenantContext context, CatalogConversionIntent intent,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        Conversion ??= new(context.TenantId, intent.SourceUnitId, intent.TargetUnitId, intent.ExpectedRevision + 1,
            intent.Numerator, intent.Denominator, context.AccountId, Now);
        return Task.FromResult(new PublishCatalogConversionResult(PublishCatalogConversionStatus.Published, Conversion));
    }

    public Task<CatalogConversionSnapshot?> FindConversionAsync(TenantContext context, Guid sourceUnitId, Guid targetUnitId,
        long revision, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(Conversion?.TenantId == context.TenantId && Conversion.SourceUnitId == sourceUnitId &&
            Conversion.TargetUnitId == targetUnitId && Conversion.Revision == revision ? Conversion : null);
    }

    public Task<ChangeCatalogAvailabilityResult> ChangeAvailabilityAsync(TenantContext context, ChangeCatalogAvailabilityRequest request,
        string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(new ChangeCatalogAvailabilityResult(Item is null ? ChangeCatalogAvailabilityStatus.NotFound : ChangeCatalogAvailabilityStatus.Changed, Item));
    }

    private static DateTimeOffset Now => new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private void Observe(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); Calls++;
        if (Fail) throw new InvalidOperationException("catalog-store-private-canary");
    }
}
