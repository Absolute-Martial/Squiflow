using Application.Catalog;
using Application.Customers;
using Application.Pricing;
using Application.Tenancy;
using System.Text.Json.Serialization;

namespace Application.Quotations;

public enum QuotationPriceMode { Manual, Catalog }
public enum QuotationCapability { Create, Edit, View, Issue, ManualPricing, CatalogView, PricingView, Override, OverrideBeyondPolicy, Respond, Expire, Convert, OrderCreate }
public enum QuotationCommandStatus { Created, Revised, Issued, Replayed, NotFound, RevisionConflict, IdempotencyKeyConflict, NoDraft, CommercialFactsConflict, ValidityConflict, Accepted, Rejected, Expired, Converted, AlreadyLinked, Superseded, AlreadyResponded, NotAccepted, AcceptedFamily }
public sealed record QuotationLineInput(decimal Quantity, string? Description = null, string? UnitCode = null,
    decimal? UnitPrice = null, Guid? ItemId = null, Guid? UnitId = null, long? ConversionRevision = null,
    decimal? OverridePrice = null, string? OverrideReason = null);
public sealed record QuotationDraftRequest(QuotationPriceMode Mode, string Summary, string CurrencyCode,
    DateTimeOffset ValidUntil, IReadOnlyList<QuotationLineInput> Lines, string? Terms = null,
    Guid? CustomerId = null, CustomerOrderContext? CustomerContext = null, bool WholesaleApplicable = false);
public sealed record QuotationLineFacts(int Position, string Description, decimal Quantity, string UnitCode,
    decimal UnitPrice, decimal LineTotal, CatalogLineFacts? Catalog = null, RetainedPriceSelection? PriceSelection = null, PriceRevision? PublishedPrice = null);
public sealed record QuotationDraftFacts(QuotationPriceMode Mode, string Summary, string CurrencyCode,
    DateTimeOffset ValidUntil, IReadOnlyList<QuotationLineFacts> Lines, decimal Total, string? Terms,
    Guid? CustomerId, CustomerOrderContext? CustomerContext, bool WholesaleApplicable);
public sealed record QuotationIssuedFacts(Guid QuotationId, Guid RevisionId, Guid TenantId, long Number,
    long RevisionNumber, Guid IssuedByAccountId, DateTimeOffset IssuedAt, QuotationDraftFacts Offer);
public sealed record QuotationSnapshot(Guid QuotationId, Guid TenantId, Guid CreatedByAccountId,
    DateTimeOffset CreatedAt, long Version, long? Number, QuotationDraftFacts? Draft,
    QuotationIssuedFacts? CurrentIssued,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] QuotationResponseFacts? CurrentResponse = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] QuotationConversionFacts? Conversion = null);
public sealed record QuotationCommandResult(QuotationCommandStatus Status, QuotationSnapshot? Quotation);
public sealed record QuotationIssuedPage(IReadOnlyList<QuotationIssuedFacts> Items, long? NextAfterRevision);
public sealed class QuotationValidationException(string code, string message) : ArgumentException(message)
{ public string Code { get; } = code; }
public sealed class QuotationCapabilityDeniedException : Exception
{ public QuotationCapabilityDeniedException() : base("The current quotation operation is not permitted.") { } }
public interface IQuotationAuthority
{
    Task<bool> CheckAsync(TenantContext context, QuotationCapability permission, CancellationToken cancellationToken);
}
// The exact outbound checks a retained offer needs, resolved by the caller BEFORE the
// issue effect transaction takes the shared commercial-publication pin. A comparison that
// sees any other offer shape than the one its authority was resolved for must fail closed.
public sealed record QuotationFrozenAuthority(bool ManualPricing, bool CatalogView, bool PricingView,
    bool Override, bool OverrideBeyondPolicy)
{
    public static QuotationFrozenAuthority RequiredFor(QuotationDraftFacts offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var manual = offer.Mode == QuotationPriceMode.Manual;
        var overridden = offer.Lines.Any(line => line.PriceSelection?.Explanation.Override is not null);
        return new(manual, !manual, !manual, overridden,
            offer.Lines.Any(line => line.PriceSelection?.Explanation.Override is { BeyondPolicy: true }));
    }
}
public interface IQuotationStore
{
    Task<QuotationCommandResult?> FindReceiptAsync(TenantContext context, string operation, string key, string fingerprint, CancellationToken ct);
    Task<QuotationCommandResult> CreateDraftAsync(TenantContext context, QuotationDraftFacts draft, string key, string fingerprint, CancellationToken ct);
    Task<QuotationCommandResult> ReviseDraftAsync(TenantContext context, Guid id, long expectedVersion, QuotationDraftFacts draft,
        string key, string fingerprint, CancellationToken ct);
    Task<QuotationCommandResult> IssueAsync(TenantContext context, Guid id, long expectedVersion, string key, string fingerprint,
        Func<QuotationDraftFacts, CancellationToken, Task<bool>> validateCurrentFacts, CancellationToken ct);
    Task<QuotationSnapshot?> FindAsync(TenantContext context, Guid id, CancellationToken ct);
    Task<QuotationIssuedPage> ListIssuedAsync(TenantContext context, Guid id, long afterRevision, int limit, CancellationToken ct);
}
