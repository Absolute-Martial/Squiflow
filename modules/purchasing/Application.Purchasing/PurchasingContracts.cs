using Application.Tenancy;

namespace Application.Purchasing;

public sealed record PurchasingAuthority(long AuthorizationRevision, bool CanView, bool CanManageSupplier,
    bool CanRecordRequest, bool CanRecordReceipt, bool CanRecordPayment, bool CanCorrect,
    bool IsCurrentInitialOwner, bool HasRecentAuthentication);

public interface IPurchasingAuthorityReader
{
    Task<PurchasingAuthority> ResolveAsync(TenantContext context, CancellationToken cancellationToken);
}

public sealed record PurchasingUnitFacts(Guid TenantId, Guid UnitId, string UnitCode, int UnitPrecision,
    long UnitRevision, bool IsActive);

public sealed record PurchasingOrderLink(Guid OrderId, int LinePosition, long OrderRevision);

public sealed record PurchasingOrderFacts(Guid TenantId, Guid OrderId, int LinePosition, long OrderRevision,
    bool IsCommitted);

public sealed record PurchasingSourceFacts(PurchasingUnitFacts Unit, PurchasingOrderFacts? Order = null);

public enum SupplierCommandKind { Create = 1, Revise = 2, Retire = 3 }
public enum PurchasingCommandKind { Request = 1, Receive = 2, Pay = 3, Refund = 4, Correct = 5, Cancel = 6 }
public enum PurchasingEventKind { Requested = 1, Receipt = 2, Payment = 3, Refund = 4, Correction = 5, Cancellation = 6 }
public enum SupplierEventKind { Created = 1, Revised = 2, Retired = 3 }
public enum SupplierStatus { Active = 1, Retired = 2 }
public enum SupplierPaymentMethod { Cash = 1, Bank = 2 }

public enum PurchasingMutationStatus
{
    Applied = 1, InputInvalid = 2, TextInvalid = 3, QuantityInvalid = 4, AmountInvalid = 5,
    SourceIneligible = 6, RevisionConflict = 7, AlreadyExists = 8, NotFound = 9, SupplierRetired = 10,
    RequestCancelled = 11, OverReceipt = 12, OverPayment = 13, OverRefund = 14, DependencyInvalid = 15,
    CorrectionTargetInvalid = 16, CorrectionHasDependents = 17, InvalidProjection = 18,
    ResourceLimit = 19, AlreadyRetired = 20, NoRemainingQuantity = 21,
}

public sealed record SupplierMutation(SupplierCommandKind Kind, long ExpectedRevision,
    string? Name = null, string? Contact = null);

public sealed record SupplierSnapshot(Guid SupplierId, Guid TenantId, long Revision, string Name, string? Contact,
    SupplierStatus Status, Guid CreatedByAccountId, DateTimeOffset CreatedAt, Guid UpdatedByAccountId,
    DateTimeOffset UpdatedAt, int SchemaVersion = 1);

public sealed record SupplierEvent(Guid EventId, long Revision, Guid ActorAccountId, DateTimeOffset RecordedAt,
    long AuthorizationRevision, SupplierEventKind Kind, SupplierSnapshot Supplier, int SchemaVersion = 1);

public sealed record PurchasingFact(PurchasingEventKind Kind, decimal? Quantity = null, decimal? Amount = null,
    Guid? ReceiptId = null, Guid? PaymentId = null, SupplierPaymentMethod? Method = null, string? Reference = null);

public sealed record PurchasingMutation(PurchasingCommandKind Kind, long ExpectedRevision,
    Guid? SupplierId = null, Guid? UnitId = null, PurchasingOrderLink? Order = null, string? Description = null,
    decimal? Quantity = null, decimal? Amount = null, Guid? ReceiptId = null, Guid? PaymentId = null,
    SupplierPaymentMethod? Method = null, string? Reference = null, string? Reason = null,
    Guid? TargetEventId = null, PurchasingFact? Replacement = null);

public sealed record PurchasingReceipt(Guid EntryId, Guid EffectiveEventId, decimal Quantity, decimal Cost,
    bool IsVoided = false);
public sealed record PurchasingPayment(Guid EntryId, Guid EffectiveEventId, Guid ReceiptId, decimal Amount,
    SupplierPaymentMethod Method, string? Reference = null, bool IsVoided = false);
public sealed record PurchasingRefund(Guid EntryId, Guid EffectiveEventId, Guid PaymentId, decimal Amount,
    SupplierPaymentMethod Method, string? Reference = null, bool IsVoided = false);

public sealed record PurchaseSnapshot(Guid PurchaseId, Guid TenantId, long Revision, SupplierSnapshot Supplier,
    PurchasingUnitFacts Unit, PurchasingOrderLink? Order, string Description, decimal RequestedQuantity,
    IReadOnlyList<PurchasingReceipt> Receipts, IReadOnlyList<PurchasingPayment> Payments,
    IReadOnlyList<PurchasingRefund> Refunds, decimal? CancelledQuantity,
    Guid CreatedByAccountId, DateTimeOffset CreatedAt, Guid UpdatedByAccountId, DateTimeOffset UpdatedAt,
    int SchemaVersion = 1)
{
    public decimal ReceivedQuantity => Receipts.Where(x => !x.IsVoided).Sum(x => x.Quantity);
    public decimal TotalCost => Receipts.Where(x => !x.IsVoided).Sum(x => x.Cost);
    public decimal PaidAmount => Payments.Where(x => !x.IsVoided).Sum(x => x.Amount);
    public decimal RefundedAmount => Refunds.Where(x => !x.IsVoided).Sum(x => x.Amount);
    public decimal OutstandingPayable => TotalCost - PaidAmount + RefundedAmount;
    public bool IsCancelled => CancelledQuantity is not null;
    public decimal UnfulfilledQuantity => RequestedQuantity - ReceivedQuantity - (CancelledQuantity ?? 0m);
    public decimal RemainingQuantity => CancelledQuantity is not null ? 0m : RequestedQuantity - ReceivedQuantity;
}

public sealed record PurchasingEvent(Guid EventId, long Revision, Guid ActorAccountId, DateTimeOffset RecordedAt,
    long AuthorizationRevision, PurchasingEventKind Kind, PurchasingFact? Facts = null, string? Reason = null,
    Guid? TargetEventId = null, PurchasingFact? Replacement = null, int SchemaVersion = 1);

public sealed record SupplierTransitionResult(PurchasingMutationStatus Status,
    SupplierSnapshot? Snapshot = null, SupplierEvent? Event = null);
public sealed record PurchasingTransitionResult(PurchasingMutationStatus Status,
    PurchaseSnapshot? Snapshot = null, PurchasingEvent? Event = null);
