using Application.Catalog;
using Application.Customers;
using Application.Pricing;
using Application.Tenancy;

namespace Application.Quotations;

public enum QuotationPriceMode { Manual, Catalog }
public enum QuotationCapability { Create, Edit, View, Issue, ManualPricing, CatalogView, PricingView, Override, OverrideBeyondPolicy }
public enum QuotationCommandStatus { Created, Revised, Issued, Replayed, NotFound, RevisionConflict, IdempotencyKeyConflict, NoDraft, CommercialFactsConflict, ValidityConflict }
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
    QuotationIssuedFacts? CurrentIssued);
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
