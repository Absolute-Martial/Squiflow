using System.Text.Json.Serialization;

namespace Application.Web.Business;

public sealed record OrderCustomer([property: JsonRequired] Guid OrganizationId, Guid? ProgramId);
public sealed record OrderOrigin([property: JsonRequired] Guid QuotationId, [property: JsonRequired] Guid IssuedRevisionId,
    [property: JsonRequired] long Number, [property: JsonRequired] long RevisionNumber);
public sealed record OrderConversion(Guid SourceUnitId, Guid TargetUnitId, long Revision);
public sealed record OrderCatalog(Guid ItemId, Guid UnitId, OrderConversion Conversion);
public sealed record OrderSelectionContext(Guid? CustomerId, bool WholesaleApplicable);
public sealed record OrderExplanation(OrderSelectionContext Context);
public sealed record OrderSelection(OrderExplanation Explanation);
public sealed record OrderCommercial(OrderCatalog Catalog, OrderSelection PriceSelection);
public sealed record OrderLine([property: JsonRequired] int Position, string Description, [property: JsonRequired] decimal Quantity,
    string UnitCode, [property: JsonRequired] decimal UnitPrice, [property: JsonRequired] decimal LineTotal, OrderCommercial? CommercialFacts = null);
public sealed record OrderRecord([property: JsonRequired] Guid OrderId, string Summary, string CurrencyCode,
    [property: JsonRequired] decimal Total, [property: JsonRequired] long Revision, [property: JsonRequired] DateTimeOffset CreatedAt,
    OrderLine[] Lines, string State, DateTimeOffset? AbandonedAt, OrderCustomer? CustomerContext, DateTimeOffset? CommittedAt, OrderOrigin? QuotationOrigin);
public sealed record OrderSummary([property: JsonRequired] Guid OrderId, string Summary, string CurrencyCode,
    [property: JsonRequired] decimal Total, [property: JsonRequired] long Revision, [property: JsonRequired] DateTimeOffset CreatedAt,
    string State, DateTimeOffset? AbandonedAt, OrderCustomer? CustomerContext, DateTimeOffset? CommittedAt, OrderOrigin? QuotationOrigin);
public sealed record OrderPage(OrderSummary[] Items, string? NextCursor);
public sealed record OrderTransition([property: JsonRequired] Guid OrderId, string State, [property: JsonRequired] long Revision,
    DateTimeOffset? AbandonedAt, DateTimeOffset? CommittedAt);
public sealed record OrderAction(string Action, [property: JsonRequired] bool Available, string? UnavailabilityCode);
public sealed record OrderActions([property: JsonRequired] Guid OrderId, [property: JsonRequired] long ObservedRevision, OrderAction[] Actions);
public sealed record OrderHistoryEntry(string Change, [property: JsonRequired] Guid ChangedByAccountId,
    [property: JsonRequired] DateTimeOffset RecordedAt, OrderRecord Order);
public sealed record OrderHistory([property: JsonRequired] long CurrentRevision, OrderHistoryEntry[] Items, long? NextBeforeRevision);

public static class OrderContracts
{
    public static bool Summary(OrderSummary row, Guid? id = null) => row.OrderId != Guid.Empty && (!id.HasValue || row.OrderId == id) &&
        BusinessValidation.Text(row.Summary, 200) && CatalogPricingContracts.Currency(row.CurrencyCode) && CatalogPricingContracts.Money(row.Total) &&
        row.Revision > 0 && row.CreatedAt != default && row.State is "draft" or "abandoned" or "committed" &&
        (row.State == "abandoned" ? row.AbandonedAt.HasValue : row.AbandonedAt is null) &&
        (row.State == "committed" ? row.CommittedAt.HasValue : row.CommittedAt is null) &&
        (row.CustomerContext is null || row.CustomerContext.OrganizationId != Guid.Empty && row.CustomerContext.ProgramId != Guid.Empty) &&
        (row.QuotationOrigin is null || row.QuotationOrigin.QuotationId != Guid.Empty && row.QuotationOrigin.IssuedRevisionId != Guid.Empty && row.QuotationOrigin.Number > 0 && row.QuotationOrigin.RevisionNumber > 0);

    public static bool Record(OrderRecord row, Guid? id = null) => Summary(new(row.OrderId, row.Summary, row.CurrencyCode, row.Total,
        row.Revision, row.CreatedAt, row.State, row.AbandonedAt, row.CustomerContext, row.CommittedAt, row.QuotationOrigin), id) &&
        row.Lines is { Length: > 0 and <= 100 } && row.Lines.Select((line, index) => line is not null && line.Position == index + 1 &&
            BusinessValidation.Text(line.Description, 300) && BusinessValidation.Text(line.UnitCode, 64) && line.Quantity > 0 &&
            CatalogPricingContracts.Money(line.UnitPrice) && CatalogPricingContracts.Money(line.LineTotal) &&
            (line.CommercialFacts is null || line.CommercialFacts is { Catalog: { ItemId: var itemId, UnitId: var unitId, Conversion: { } conversion }, PriceSelection.Explanation.Context: { } } &&
                itemId != Guid.Empty && unitId != Guid.Empty && conversion.SourceUnitId != Guid.Empty && conversion.TargetUnitId != Guid.Empty && conversion.Revision >= 0)).All(valid => valid);

    public static string Reason(string? code) => code switch
    {
        "permission_required" => "This operation requires its own permission.",
        "order_already_abandoned" => "The draft was abandoned.",
        "order_already_committed" => "The order is committed.",
        "order_quotation_bound" => "This order preserves its accepted quotation; ordinary replacement is unavailable.",
        "order_profile_unavailable" => "An authoritative order profile is unavailable.",
        "order_program_reference_required" => "A customer program reference is required before commitment.",
        "order_approval_required" => "Independent approval is required before commitment.",
        "order_approval_stale" => "The order changed; obtain approval for the current revision.",
        null => "Available when checked; the service checks again on save.",
        _ => "The operation is currently unavailable.",
    };
}
