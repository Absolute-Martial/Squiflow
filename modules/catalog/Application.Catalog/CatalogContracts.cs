namespace Application.Catalog;

public enum CatalogItemKind
{
    Product = 1,
    Service = 2,
}

public enum CatalogEntityStatus
{
    Active = 1,
    Retired = 2,
}

public enum CatalogStockMode
{
    PreciseStock = 1,
    AvailabilityOnly = 2,
    NonStock = 3,
}

public enum CatalogAvailability
{
    Available = 1,
    Unavailable = 2,
}

public sealed record CatalogUnitSnapshot(
    Guid UnitId,
    Guid TenantId,
    string Code,
    string Name,
    int Precision,
    CatalogEntityStatus Status,
    long Revision,
    Guid CreatedByAccountId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RetiredAt = null,
    Guid? RetiredByAccountId = null);

public sealed record CatalogItemSnapshot(
    Guid ItemId,
    Guid TenantId,
    string? Code,
    string Name,
    string? Description,
    CatalogItemKind Kind,
    CatalogEntityStatus Status,
    Guid BaseUnitId,
    CatalogStockMode StockMode,
    long Revision,
    Guid CreatedByAccountId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RetiredAt = null,
    Guid? RetiredByAccountId = null,
    CatalogAvailability? Availability = null,
    DateTimeOffset? AvailabilityChangedAt = null,
    Guid? AvailabilityChangedByAccountId = null);

public sealed record CatalogUnitPageCursor(DateTimeOffset CreatedAt, Guid UnitId);

public sealed record CatalogItemPageCursor(DateTimeOffset CreatedAt, Guid ItemId);

public sealed record ListCatalogUnitsRequest(int Limit, CatalogUnitPageCursor? After);

public sealed record ListCatalogItemsRequest(int Limit, CatalogItemPageCursor? After);

public sealed record CatalogUnitPage(
    IReadOnlyList<CatalogUnitSnapshot> Items,
    CatalogUnitPageCursor? NextCursor);

public sealed record CatalogItemPage(
    IReadOnlyList<CatalogItemSnapshot> Items,
    CatalogItemPageCursor? NextCursor);

public sealed record CreateCatalogUnitRequest(string Code, string Name, int Precision);

public sealed record CreateCatalogItemRequest(
    string? Code,
    string Name,
    string? Description,
    CatalogItemKind Kind,
    Guid BaseUnitId,
    CatalogStockMode StockMode);

public sealed record RenameCatalogUnitRequest(
    Guid UnitId,
    long ExpectedRevision,
    string Name);

public sealed record RenameCatalogItemRequest(
    Guid ItemId,
    long ExpectedRevision,
    string Name,
    string? Description);

public sealed record RetireCatalogUnitRequest(Guid UnitId, long ExpectedRevision);

public sealed record RetireCatalogItemRequest(Guid ItemId, long ExpectedRevision);

public sealed record CatalogLineSelection(Guid ItemId, Guid UnitId, decimal Quantity, long? ConversionRevision = null);

// Conversion is a direct, explicitly selected source/base-unit ratio, never an
// inferred graph path. Retained lines carry the exact revision and ratio used.
public sealed record CatalogConversionFacts(
    Guid SourceUnitId,
    Guid TargetUnitId,
    long Revision,
    decimal Numerator,
    decimal Denominator);

public sealed record CatalogLineFacts(
    Guid ItemId,
    string? ItemCode,
    Guid UnitId,
    string ItemName,
    string UnitCode,
    string UnitName,
    decimal Quantity,
    int UnitPrecision,
    long ItemRevision,
    long UnitRevision,
    CatalogConversionFacts Conversion,
    string? BaseUnitCode = null,
    string? BaseUnitName = null,
    int? BaseUnitPrecision = null,
    long? BaseUnitRevision = null,
    decimal? BaseQuantity = null,
    int QuantityArithmeticVersion = 1,
    string QuantityRounding = "toEven");

public enum CreateCatalogUnitStatus
{
    Created = 1,
    Replayed = 2,
    CodeConflict = 3,
    IdempotencyKeyConflict = 4,
}

public enum CreateCatalogItemStatus
{
    Created = 1,
    Replayed = 2,
    CodeConflict = 3,
    BaseUnitNotFound = 4,
    BaseUnitRetired = 5,
    IdempotencyKeyConflict = 6,
}

public enum RenameCatalogUnitStatus
{
    Renamed = 1,
    Replayed = 2,
    NotFound = 3,
    RevisionConflict = 4,
    AlreadyRetired = 5,
    IdempotencyKeyConflict = 6,
}

public enum RenameCatalogItemStatus
{
    Renamed = 1,
    Replayed = 2,
    NotFound = 3,
    RevisionConflict = 4,
    AlreadyRetired = 5,
    IdempotencyKeyConflict = 6,
}

public enum RetireCatalogUnitStatus
{
    Retired = 1,
    Replayed = 2,
    NotFound = 3,
    RevisionConflict = 4,
    AlreadyRetired = 5,
    IdempotencyKeyConflict = 6,
}

public enum RetireCatalogItemStatus
{
    Retired = 1,
    Replayed = 2,
    NotFound = 3,
    RevisionConflict = 4,
    AlreadyRetired = 5,
    IdempotencyKeyConflict = 6,
}

public enum CatalogLineFactsStatus
{
    Available = 1,
    ItemNotFound = 2,
    UnitNotFound = 3,
    ItemRetired = 4,
    UnitRetired = 5,
    UnitMismatch = 6,
    QuantityInvalid = 7,
    ItemUnavailable = 8,
    ConversionNotFound = 9,
    ConversionRevisionRequired = 10,
}

public sealed record CreateCatalogUnitResult(
    CreateCatalogUnitStatus Status,
    CatalogUnitSnapshot? Unit);

public sealed record CreateCatalogItemResult(
    CreateCatalogItemStatus Status,
    CatalogItemSnapshot? Item);

public sealed record RenameCatalogUnitResult(
    RenameCatalogUnitStatus Status,
    CatalogUnitSnapshot? Unit);

public sealed record RenameCatalogItemResult(
    RenameCatalogItemStatus Status,
    CatalogItemSnapshot? Item);

public sealed record RetireCatalogUnitResult(
    RetireCatalogUnitStatus Status,
    CatalogUnitSnapshot? Unit);

public sealed record RetireCatalogItemResult(
    RetireCatalogItemStatus Status,
    CatalogItemSnapshot? Item);

public sealed record CatalogLineFactsResult(
    CatalogLineFactsStatus Status,
    CatalogLineFacts? Facts);

public sealed class CatalogValidationException(string code, string message) : ArgumentException(message)
{
    public string Code { get; } = code;
}
