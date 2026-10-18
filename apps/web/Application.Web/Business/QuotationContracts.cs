using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Web.Business;

public sealed record QuotationLine([property: JsonRequired] int Position, string Description, [property: JsonRequired] decimal Quantity,
    string UnitCode, [property: JsonRequired] decimal UnitPrice, [property: JsonRequired] decimal LineTotal,
    Guid? ItemId, Guid? UnitId, Guid? PriceRevisionId, long? PolicyRevision, QuotationOverride? Override);
public sealed record QuotationOverride([property: JsonRequired] decimal UnitPrice, string Reason, [property: JsonRequired] bool BeyondPolicy);
public sealed record QuotationOffer(string Mode, string Summary, string CurrencyCode, [property: JsonRequired] DateTimeOffset ValidUntil,
    [property: JsonRequired] decimal Total, string? Terms, Guid? CustomerId, OrderCustomer? CustomerContext,
    [property: JsonRequired] bool WholesaleApplicable, QuotationLine[] Lines);
public sealed record QuotationIssued([property: JsonRequired] Guid RevisionId, [property: JsonRequired] long RevisionNumber,
    [property: JsonRequired] Guid IssuedByAccountId, [property: JsonRequired] DateTimeOffset IssuedAt, QuotationOffer Offer);
public sealed record QuotationRecord([property: JsonRequired] Guid QuotationId, [property: JsonRequired] Guid TenantId,
    [property: JsonRequired] long Version, long? Number, [property: JsonRequired] Guid CreatedByAccountId,
    [property: JsonRequired] DateTimeOffset CreatedAt, QuotationOffer? Draft, QuotationIssued? CurrentIssued,
    QuotationResponseFact? CurrentResponse, QuotationConversion? Conversion);
public sealed record QuotationResponseFact([property: JsonRequired] Guid QuotationId, [property: JsonRequired] Guid IssuedRevisionId,
    string Kind, [property: JsonRequired] Guid RecordedByAccountId, [property: JsonRequired] DateTimeOffset RecordedAt,
    string Evidence, string? CustomerClaim);
public sealed record QuotationOrderIdentity([property: JsonRequired] Guid OrderId, [property: JsonRequired] long Revision);
public sealed record QuotationConversion([property: JsonRequired] Guid QuotationId, [property: JsonRequired] Guid IssuedRevisionId,
    [property: JsonRequired] Guid ConvertedByAccountId, [property: JsonRequired] DateTimeOffset ConvertedAt, QuotationOrderIdentity OriginalOrder);
public sealed record QuotationCommandReceipt([property: JsonRequired] Guid QuotationId, [property: JsonRequired] Guid TenantId,
    [property: JsonRequired] long Version, QuotationResponseFact? Response, QuotationConversion? Conversion);
public sealed record QuotationHistory(QuotationIssued[] Items, long? NextAfterRevision);
public sealed record QuotationDraftInput(string Mode, string Summary, string CurrencyCode, string ValidUntil,
    QuotationLineInput[] Lines, long? ExpectedVersion, string? Terms, Guid? CustomerId, OrderCustomer? CustomerContext, bool WholesaleApplicable);
public sealed record QuotationLineInput(decimal Quantity, string? Description = null, string? UnitCode = null,
    decimal? UnitPrice = null, Guid? ItemId = null, Guid? UnitId = null, long? ConversionRevision = null, decimal? OverridePrice = null, string? OverrideReason = null);

public static class QuotationContracts
{
    public static bool Offer(QuotationOffer row) => row.Mode is "manual" or "catalog" && BusinessValidation.Text(row.Summary, 200) &&
        CatalogPricingContracts.Currency(row.CurrencyCode) && row.ValidUntil != default && row.ValidUntil.Offset == TimeSpan.Zero &&
        CatalogPricingContracts.Money(row.Total) && BusinessValidation.Text(row.Terms, 2000, false) && row.CustomerId != Guid.Empty &&
        (row.CustomerContext is null || row.CustomerContext.OrganizationId != Guid.Empty && row.CustomerContext.ProgramId != Guid.Empty) &&
        row.Lines is { Length: > 0 and <= 100 } && row.Lines.Select((line, index) => line is not null && line.Position == index + 1 &&
            BusinessValidation.Text(line.Description, 300) && BusinessValidation.Text(line.UnitCode, 64) && line.Quantity > 0 && CatalogPricingContracts.Money(line.Quantity) &&
            CatalogPricingContracts.Money(line.UnitPrice) && CatalogPricingContracts.Money(line.LineTotal) &&
            (row.Mode == "manual" ? line.ItemId is null && line.UnitId is null && line.Override is null : line.ItemId.HasValue && line.ItemId != Guid.Empty && line.UnitId.HasValue && line.UnitId != Guid.Empty && line.PolicyRevision > 0) &&
            (line.Override is null || CatalogPricingContracts.Money(line.Override.UnitPrice) && BusinessValidation.Text(line.Override.Reason, 500))).All(valid => valid);
    public static bool Issued(QuotationIssued row) => row.RevisionId != Guid.Empty && row.RevisionNumber > 0 && row.IssuedByAccountId != Guid.Empty &&
        row.IssuedAt != default && row.IssuedAt.Offset == TimeSpan.Zero && row.Offer is not null && Offer(row.Offer) && row.Offer.ValidUntil > row.IssuedAt;
    public static bool Response(QuotationResponseFact row, Guid quote, Guid? revision = null) => row.QuotationId == quote && row.QuotationId != Guid.Empty &&
        row.IssuedRevisionId != Guid.Empty && (!revision.HasValue || row.IssuedRevisionId == revision) && row.Kind is "accepted" or "rejected" or "expired" &&
        row.RecordedByAccountId != Guid.Empty && row.RecordedAt != default && row.RecordedAt.Offset == TimeSpan.Zero && BusinessValidation.Text(row.Evidence, 2000) &&
        (row.Kind == "expired" ? row.CustomerClaim is null : BusinessValidation.Text(row.CustomerClaim, 300));
    public static bool Conversion(QuotationConversion row, Guid quote, Guid? revision = null) => row.QuotationId == quote && quote != Guid.Empty && row.IssuedRevisionId != Guid.Empty &&
        (!revision.HasValue || row.IssuedRevisionId == revision) && row.ConvertedByAccountId != Guid.Empty && row.ConvertedAt != default &&
        row.OriginalOrder is { } order && order.OrderId != Guid.Empty && order.Revision > 0;
    public static bool Record(QuotationRecord row, Guid tenant, Guid? quote = null) => row.QuotationId != Guid.Empty && row.TenantId == tenant &&
        (!quote.HasValue || row.QuotationId == quote) && row.Version > 0 && row.CreatedByAccountId != Guid.Empty && row.CreatedAt != default &&
        (row.Draft is null || Offer(row.Draft)) && (row.CurrentIssued is null || Issued(row.CurrentIssued)) &&
        (row.CurrentIssued is null ? row.Number is null : row.Number > 0) && (row.Draft is not null || row.CurrentIssued is not null) &&
        (row.CurrentResponse is null || row.CurrentIssued is not null && Response(row.CurrentResponse, row.QuotationId, row.CurrentIssued.RevisionId)) &&
        (row.Conversion is null || row.CurrentResponse is { Kind: "accepted" } && Conversion(row.Conversion, row.QuotationId, row.CurrentIssued?.RevisionId));
    public static bool Command(QuotationCommandReceipt row, Guid tenant, Guid quote, Guid revision, string action) => row.QuotationId == quote && row.TenantId == tenant && row.Version > 0 &&
        (row.Response is null || Response(row.Response, quote, revision)) && (row.Conversion is null || Conversion(row.Conversion, quote, revision)) &&
        (action == "convert" ? row.Conversion is not null : row.Response?.Kind == (action switch { "accept" => "accepted", "reject" => "rejected", _ => "expired" }));
    public static bool Instant(string? text, out DateTimeOffset value)
    {
        value = default;
        if (text is not { Length: <= 64 } || !(text.EndsWith('Z') || text.Length >= 6 && text[^6] is '+' or '-' && text[^3] == ':')) return false;
        try { value = JsonSerializer.Deserialize<DateTimeOffset>(JsonSerializer.Serialize(text)); }
        catch (JsonException) { return false; }
        return value != default && value.Ticks % 10 == 0;
    }
}
