using Application.Tenancy;

namespace Application.Fulfillment;

public enum FulfillmentStoreStatus
{
    Applied = 1, Replayed = 2, NotFound = 3, Forbidden = 4,
    IdempotencyConflict = 5, OrderRevisionConflict = 6, TransitionRejected = 7,
}

public sealed record FulfillmentCommandResult(FulfillmentStoreStatus Status, FulfillmentSnapshot? Work = null,
    FulfillmentMutationStatus? Rejection = null);
public sealed record FulfillmentCursor(DateTimeOffset CreatedAt, Guid OrderId);
public sealed record FulfillmentListRequest(int Limit = 25, FulfillmentCursor? After = null);
public sealed record FulfillmentListItem(Guid WorkId, Guid OrderId, long Revision, FulfillmentStage Stage,
    decimal OrderedQuantity, decimal CompletedQuantity, decimal HandedOffQuantity, string UnitCode, DateTimeOffset CreatedAt);
public sealed record FulfillmentPage(IReadOnlyList<FulfillmentListItem> Items, FulfillmentCursor? NextCursor);
public sealed record FulfillmentHistoryRequest(int Limit = 10, long? BeforeRevision = null);
public sealed record FulfillmentHistoryItem(FulfillmentEvent Event, bool IsActive);
public sealed record FulfillmentHistoryPage(long ObservedRevision, IReadOnlyList<FulfillmentHistoryItem> Items, long? NextRevision);

public interface IFulfillmentStore
{
    Task<FulfillmentCommandResult> ExecuteAsync(TenantContext context, Guid orderId, FulfillmentMutation mutation,
        long? expectedOrderRevision, string idempotencyKey, CancellationToken cancellationToken);
    Task<FulfillmentSnapshot?> FindAsync(TenantContext context, Guid orderId, CancellationToken cancellationToken);
    Task<FulfillmentPage> ListAsync(TenantContext context, FulfillmentListRequest request, CancellationToken cancellationToken);
    Task<FulfillmentHistoryPage?> HistoryAsync(TenantContext context, Guid orderId, FulfillmentHistoryRequest request, CancellationToken cancellationToken);
}

public sealed class FulfillmentAccessDeniedException : Exception
{
    public FulfillmentAccessDeniedException() : base("Current fulfillment authority is required.") { }
}

public sealed class FulfillmentValidationException(string code) : ArgumentException("Fulfillment request is invalid.")
{
    public string Code { get; } = code;
}
