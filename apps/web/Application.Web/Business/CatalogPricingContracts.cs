using System.Text.Json.Serialization;

namespace Application.Web.Business;

public sealed record CatalogUnit([property: JsonRequired] Guid UnitId, string Code, string Name, [property: JsonRequired] int Precision,
    string Status, [property: JsonRequired] long Revision, [property: JsonRequired] DateTimeOffset CreatedAt, DateTimeOffset? RetiredAt);
public sealed record CatalogItem([property: JsonRequired] Guid ItemId, string? Code, string Name, string? Description, string Kind,
    string Status, [property: JsonRequired] Guid BaseUnitId, string StockMode, string? Availability, [property: JsonRequired] long Revision,
    [property: JsonRequired] DateTimeOffset CreatedAt, DateTimeOffset? RetiredAt, DateTimeOffset? AvailabilityChangedAt);
public sealed record CatalogUnitPage(CatalogUnit[] Items, string? NextCursor);
public sealed record CatalogItemPage(CatalogItem[] Items, string? NextCursor);
public sealed record CatalogChange([property: JsonRequired] Guid Id, [property: JsonRequired] long Revision, string Status, DateTimeOffset? ChangedAt);
public sealed record CatalogConversion([property: JsonRequired] Guid SourceUnitId, [property: JsonRequired] Guid TargetUnitId,
    [property: JsonRequired] long Revision, [property: JsonRequired] decimal Numerator, [property: JsonRequired] decimal Denominator,
    [property: JsonRequired] DateTimeOffset PublishedAt);
public sealed record PriceRevision([property: JsonRequired] Guid PriceId, [property: JsonRequired] Guid RevisionId,
    [property: JsonRequired] long Revision, [property: JsonRequired] Guid ItemId, [property: JsonRequired] Guid UnitId,
    string UnitCode, string CurrencyCode, string Scope, Guid? TargetId, [property: JsonRequired] decimal BaseUnitPrice,
    [property: JsonRequired] DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, string State, [property: JsonRequired] DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt, DateTimeOffset? RetiredAt, long? ConversionRevision);
public sealed record PriceHistory(PriceRevision[] Items, long? AfterRevision);
public sealed record PriceCandidate([property: JsonRequired] Guid RevisionId, [property: JsonRequired] long Revision, string Scope,
    [property: JsonRequired] DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, decimal? BaseUnitPrice);
public sealed record PriceResolution(string Status, decimal? UnitPrice, Guid? PriceId, Guid? RevisionId, long? Revision,
    string? Source, [property: JsonRequired] long PolicyRevision, [property: JsonRequired] DateTimeOffset EvaluatedAt,
    string Why, PriceCandidate[] Candidates);
public sealed record OverridePolicy([property: JsonRequired] long PolicyRevision, [property: JsonRequired] decimal MinimumUnitPrice,
    [property: JsonRequired] decimal MaximumUnitPrice, decimal? MaximumDecreasePercent, decimal? MaximumIncreasePercent,
    [property: JsonRequired] int EnvelopeSemanticsVersion);
public sealed record PricingPolicy([property: JsonRequired] Guid TenantId, OverridePolicy Policy,
    [property: JsonRequired] Guid CreatedByAccountId, [property: JsonRequired] DateTimeOffset CreatedAt);

public static class CatalogPricingContracts
{
    public static bool Unit(CatalogUnit value, Guid? id = null) => value.UnitId != Guid.Empty &&
        (!id.HasValue || value.UnitId == id) && BusinessValidation.Text(value.Code, 64) && BusinessValidation.Text(value.Name, 200) &&
        value.Precision is >= 0 and <= 9 && value.Status is "active" or "retired" && value.Revision > 0 && value.CreatedAt != default;

    public static bool Item(CatalogItem value, Guid? id = null) => value.ItemId != Guid.Empty && (!id.HasValue || value.ItemId == id) &&
        value.BaseUnitId != Guid.Empty && BusinessValidation.Text(value.Code, 64, false) && BusinessValidation.Text(value.Name, 200) &&
        BusinessValidation.Text(value.Description, 1000, false) && value.Kind is "product" or "service" && value.Status is "active" or "retired" &&
        value.Revision > 0 && value.CreatedAt != default && value.StockMode is "preciseStock" or "availabilityOnly" or "nonStock" &&
        (value.StockMode == "availabilityOnly" ? value.Availability is "available" or "unavailable" : value.Availability is null);

    public static bool Price(PriceRevision value, Guid? revisionId = null) => value.PriceId != Guid.Empty && value.RevisionId != Guid.Empty &&
        (!revisionId.HasValue || value.RevisionId == revisionId) && value.ItemId != Guid.Empty && value.UnitId != Guid.Empty &&
        value.Revision > 0 && BusinessValidation.Text(value.UnitCode, 64) && Currency(value.CurrencyCode) &&
        value.Scope is "Default" or "Wholesale" or "Customer" or "Program" or "Organization" &&
        (value.Scope is "Default" or "Wholesale" ? value.TargetId is null : value.TargetId.HasValue && value.TargetId != Guid.Empty) &&
        Money(value.BaseUnitPrice) && value.ValidFrom != default && value.ValidFrom.Offset == TimeSpan.Zero &&
        (!value.ValidTo.HasValue || value.ValidTo > value.ValidFrom) && value.CreatedAt != default &&
        value.State is "Draft" or "Published" or "Superseded" or "Retired" && (!value.ConversionRevision.HasValue || value.ConversionRevision > 0);

    public static bool Resolution(PriceResolution value) => value.Status is "PriceResolved" or "PriceMissing" or "PriceExpired" or "PriceConflict" or "PriceOverrideRequired" &&
        value.EvaluatedAt != default && value.PolicyRevision >= 0 && BusinessValidation.Text(value.Why, 2000) &&
        (value.Status == "PriceResolved" ? value.UnitPrice.HasValue && Money(value.UnitPrice.Value) : value.UnitPrice is null) &&
        value.Candidates is { Length: <= 64 } && value.Candidates.All(row => row is not null && row.RevisionId != Guid.Empty && row.Revision > 0 &&
            row.Scope is "Default" or "Wholesale" or "Customer" or "Program" or "Organization" or "CommittedQuotation" or "CommittedAgreement" &&
            row.ValidFrom != default && (!row.ValidTo.HasValue || row.ValidTo > row.ValidFrom) && (!row.BaseUnitPrice.HasValue || Money(row.BaseUnitPrice.Value))) &&
        value.Candidates.Select(row => row.RevisionId).Distinct().Count() == value.Candidates.Length;

    public static bool Policy(PricingPolicy value, Guid tenant) => value.TenantId == tenant && value.CreatedByAccountId != Guid.Empty &&
        value.CreatedAt != default && value.Policy is { } policy && policy.PolicyRevision > 0 && policy.EnvelopeSemanticsVersion is 1 or 2 && Money(policy.MinimumUnitPrice) && Money(policy.MaximumUnitPrice) &&
        policy.MaximumUnitPrice >= policy.MinimumUnitPrice && (!policy.MaximumDecreasePercent.HasValue || policy.MaximumDecreasePercent is >= 0 and <= 100) &&
        (!policy.MaximumIncreasePercent.HasValue || policy.MaximumIncreasePercent is >= 0 and <= 10000);

    public static bool Currency(string? value) => value is { Length: 3 } && value.All(c => c is >= 'A' and <= 'Z');
    public static bool Money(decimal value) => value is >= 0 and <= 999_999_999_999_999.9999m && decimal.Round(value, 4) == value;
}
