using Application.Tenancy;

namespace Application.Purchasing;

public enum PurchasingStoreStatus
{
    Applied = 1, Replayed = 2, NotFound = 3, Forbidden = 4, IdempotencyConflict = 5, TransitionRejected = 6,
}

public sealed record SupplierCommandResult(PurchasingStoreStatus Status, SupplierSnapshot? Supplier = null,
    PurchasingMutationStatus? Rejection = null);
public sealed record PurchasingCommandResult(PurchasingStoreStatus Status, PurchaseSnapshot? Purchase = null,
    PurchasingMutationStatus? Rejection = null);
public sealed record PurchasingCursor(DateTimeOffset CreatedAt, Guid Id);
public sealed record PurchasingListRequest(int Limit = 25, PurchasingCursor? After = null);
public sealed record SupplierPage(IReadOnlyList<SupplierSnapshot> Items, PurchasingCursor? NextCursor);
public sealed record PurchasingPage(IReadOnlyList<PurchaseSnapshot> Items, PurchasingCursor? NextCursor);
public sealed record PurchasingHistoryRequest(int Limit = 10, long? BeforeRevision = null);
public sealed record PurchasingHistoryPage(long ObservedRevision, IReadOnlyList<PurchasingEvent> Items, long? NextRevision);
public sealed record SupplierHistoryPage(long ObservedRevision, IReadOnlyList<SupplierEvent> Items, long? NextRevision);
public sealed record SupplierBalance(Guid SupplierId, string Currency, decimal TotalCost, decimal PaidAmount,
    decimal RefundedAmount, decimal OutstandingPayable);

public interface IPurchasingStore
{
    Task<SupplierCommandResult> ExecuteSupplierAsync(TenantContext context, Guid supplierId, SupplierMutation mutation,
        string idempotencyKey, CancellationToken cancellationToken);
    Task<PurchasingCommandResult> ExecutePurchaseAsync(TenantContext context, Guid purchaseId, PurchasingMutation mutation,
        string idempotencyKey, CancellationToken cancellationToken);
    Task<SupplierSnapshot?> FindSupplierAsync(TenantContext context, Guid supplierId, CancellationToken cancellationToken);
    Task<PurchaseSnapshot?> FindPurchaseAsync(TenantContext context, Guid purchaseId, CancellationToken cancellationToken);
    Task<SupplierPage> ListSuppliersAsync(TenantContext context, PurchasingListRequest request, CancellationToken cancellationToken);
    Task<PurchasingPage> ListPurchasesAsync(TenantContext context, PurchasingListRequest request, CancellationToken cancellationToken);
    Task<PurchasingHistoryPage?> HistoryAsync(TenantContext context, Guid purchaseId,
        PurchasingHistoryRequest request, CancellationToken cancellationToken);
    Task<SupplierHistoryPage?> SupplierHistoryAsync(TenantContext context, Guid supplierId,
        PurchasingHistoryRequest request, CancellationToken cancellationToken);
    Task<SupplierBalance?> SupplierBalanceAsync(TenantContext context, Guid supplierId, CancellationToken cancellationToken);
}

public sealed class PurchasingAccessDeniedException : Exception
{
    public PurchasingAccessDeniedException() : base("Current purchasing authority is required.") { }
}

public sealed class PurchasingValidationException(string code) : ArgumentException("Purchasing request is invalid.")
{
    public string Code { get; } = code;
}
