using System.Text.Json.Serialization;

namespace Application.Web.Business;

public sealed record PurchasingReceipt([property: JsonRequired] Guid Id, [property: JsonRequired] long Revision);
public sealed record SupplierRecord([property: JsonRequired] Guid SupplierId, [property: JsonRequired] long Revision, string Name,
    string? Contact, string Status, [property: JsonRequired] DateTimeOffset CreatedAt, [property: JsonRequired] DateTimeOffset UpdatedAt);
public sealed record PurchaseOrderLink([property: JsonRequired] Guid OrderId, [property: JsonRequired] int LinePosition, [property: JsonRequired] long OrderRevision);
public sealed record PurchaseReceiptRecord([property: JsonRequired] Guid EntryId, [property: JsonRequired] Guid EffectiveEventId,
    [property: JsonRequired] decimal Quantity, [property: JsonRequired] decimal Cost, [property: JsonRequired] bool IsVoided);
public sealed record SupplierPaymentRecord([property: JsonRequired] Guid EntryId, [property: JsonRequired] Guid EffectiveEventId,
    [property: JsonRequired] Guid ReceiptId, [property: JsonRequired] decimal Amount, string Method, string? Reference, [property: JsonRequired] bool IsVoided);
public sealed record SupplierRefundRecord([property: JsonRequired] Guid EntryId, [property: JsonRequired] Guid EffectiveEventId,
    [property: JsonRequired] Guid PaymentId, [property: JsonRequired] decimal Amount, string Method, string? Reference, [property: JsonRequired] bool IsVoided);
public sealed record PurchaseRecord([property: JsonRequired] Guid PurchaseId, [property: JsonRequired] long Revision, SupplierRecord Supplier,
    [property: JsonRequired] Guid UnitId, string UnitCode, [property: JsonRequired] int UnitPrecision, [property: JsonRequired] long UnitRevision,
    PurchaseOrderLink? Order, string Description, [property: JsonRequired] decimal RequestedQuantity, [property: JsonRequired] decimal ReceivedQuantity,
    [property: JsonRequired] decimal RemainingQuantity, decimal? CancelledQuantity, [property: JsonRequired] decimal UnfulfilledQuantity, string Currency,
    [property: JsonRequired] decimal TotalCost, [property: JsonRequired] decimal PaidAmount, [property: JsonRequired] decimal RefundedAmount,
    [property: JsonRequired] decimal OutstandingPayable, PurchaseReceiptRecord[] Receipts, SupplierPaymentRecord[] Payments,
    SupplierRefundRecord[] Refunds, [property: JsonRequired] DateTimeOffset CreatedAt, [property: JsonRequired] DateTimeOffset UpdatedAt);
public sealed record SupplierBalanceRecord([property: JsonRequired] Guid SupplierId, string Currency, [property: JsonRequired] decimal TotalCost,
    [property: JsonRequired] decimal PaidAmount, [property: JsonRequired] decimal RefundedAmount, [property: JsonRequired] decimal OutstandingPayable);
public sealed record PurchasingFactRecord(string Kind, decimal? Quantity, decimal? Amount, Guid? ReceiptId, Guid? PaymentId, string? Method, string? Reference);
public sealed record PurchaseEventRecord([property: JsonRequired] Guid EventId, [property: JsonRequired] long Revision,
    [property: JsonRequired] Guid ActorAccountId, [property: JsonRequired] DateTimeOffset RecordedAt, [property: JsonRequired] long AuthorizationRevision,
    string Kind, PurchasingFactRecord? Facts, string? Reason, Guid? TargetEventId, PurchasingFactRecord? Replacement);
public sealed record SupplierEventRecord([property: JsonRequired] Guid EventId, [property: JsonRequired] long Revision,
    [property: JsonRequired] Guid ActorAccountId, [property: JsonRequired] DateTimeOffset RecordedAt,
    [property: JsonRequired] long AuthorizationRevision, string Kind, SupplierRecord Supplier);

public static class PurchasingContracts
{
    public static bool Receipt(PurchasingReceipt value, Guid id, long revision) => value.Id == id && value.Revision == revision;
    public static bool Supplier(SupplierRecord value, Guid? id = null) => value.SupplierId != Guid.Empty && (!id.HasValue || value.SupplierId == id) && value.Revision > 0 &&
        OperationsContracts.Text(value.Name, 128) && OperationsContracts.Text(value.Contact, 512, false) && value.Status is "Active" or "Retired" && value.CreatedAt != default && value.UpdatedAt != default;
    public static bool Method(string? value) => value is "Cash" or "Bank";
    public static bool Purchase(PurchaseRecord value, Guid? id = null) => value.PurchaseId != Guid.Empty && (!id.HasValue || value.PurchaseId == id) && value.Revision > 0 &&
        value.Supplier is not null && Supplier(value.Supplier) && value.UnitId != Guid.Empty && value.UnitRevision > 0 && OperationsContracts.Text(value.UnitCode, 64) &&
        OperationsContracts.Quantity(value.RequestedQuantity, value.UnitPrecision) && OperationsContracts.Text(value.Description, 512) &&
        value.ReceivedQuantity >= 0 && value.ReceivedQuantity <= value.RequestedQuantity && value.RemainingQuantity >= 0 && value.RemainingQuantity <= value.RequestedQuantity &&
        value.UnfulfilledQuantity >= 0 && value.UnfulfilledQuantity <= value.RequestedQuantity && (!value.CancelledQuantity.HasValue || value.CancelledQuantity >= 0 && value.CancelledQuantity <= value.RequestedQuantity) &&
        value.Currency == "NPR" && OperationsContracts.Aggregate(value.TotalCost) && OperationsContracts.Aggregate(value.PaidAmount) && OperationsContracts.Aggregate(value.RefundedAmount) && OperationsContracts.Aggregate(value.OutstandingPayable) &&
        value.CreatedAt != default && value.UpdatedAt != default && (value.Order is null || value.Order.OrderId != Guid.Empty && value.Order.LinePosition > 0 && value.Order.OrderRevision > 0) &&
        value.Receipts is { Length: <= 128 } && value.Payments is { Length: <= 128 } && value.Refunds is { Length: <= 128 } && value.Receipts.Length + value.Payments.Length + value.Refunds.Length <= 128 &&
        value.Receipts.All(x => x is not null && x.EntryId != Guid.Empty && x.EffectiveEventId != Guid.Empty && OperationsContracts.Quantity(x.Quantity, value.UnitPrecision) && OperationsContracts.Money(x.Cost, true)) &&
        value.Payments.All(x => x is not null && x.EntryId != Guid.Empty && x.EffectiveEventId != Guid.Empty && x.ReceiptId != Guid.Empty && OperationsContracts.Money(x.Amount, true) && Method(x.Method) && OperationsContracts.Text(x.Reference, 128, false)) &&
        value.Refunds.All(x => x is not null && x.EntryId != Guid.Empty && x.EffectiveEventId != Guid.Empty && x.PaymentId != Guid.Empty && OperationsContracts.Money(x.Amount, true) && Method(x.Method) && OperationsContracts.Text(x.Reference, 128, false));
    public static bool Balance(SupplierBalanceRecord value, Guid supplier) => value.SupplierId == supplier && value.Currency == "NPR" &&
        OperationsContracts.Aggregate(value.TotalCost) && OperationsContracts.Aggregate(value.PaidAmount) && OperationsContracts.Aggregate(value.RefundedAmount) && OperationsContracts.Aggregate(value.OutstandingPayable);
    public static bool Event(PurchaseEventRecord value) => value.EventId != Guid.Empty && value.Revision > 0 && value.ActorAccountId != Guid.Empty && value.RecordedAt != default && value.AuthorizationRevision > 0 &&
        value.Kind is "Requested" or "Receipt" or "Payment" or "Refund" or "Correction" or "Cancellation" && OperationsContracts.Text(value.Reason, 512, false) && value.TargetEventId != Guid.Empty &&
        (value.Facts is null || Fact(value.Facts)) && (value.Replacement is null || Fact(value.Replacement));
    public static bool Fact(PurchasingFactRecord value) => value.Kind is "Requested" or "Receipt" or "Payment" or "Refund" or "Cancellation" &&
        (!value.Quantity.HasValue || OperationsContracts.Quantity(value.Quantity.Value)) && (!value.Amount.HasValue || OperationsContracts.Money(value.Amount.Value, true)) &&
        value.ReceiptId != Guid.Empty && value.PaymentId != Guid.Empty && (value.Method is null || Method(value.Method)) && OperationsContracts.Text(value.Reference, 128, false);
    public static bool SupplierEvent(SupplierEventRecord value, Guid supplier) => value.EventId != Guid.Empty && value.Revision > 0 && value.ActorAccountId != Guid.Empty && value.RecordedAt != default &&
        value.AuthorizationRevision > 0 && value.Kind is "Created" or "Revised" or "Retired" && value.Supplier is not null && Supplier(value.Supplier, supplier);
}
