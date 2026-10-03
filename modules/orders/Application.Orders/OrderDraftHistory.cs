using Application.Tenancy;

namespace Application.Orders;

public enum OrderDraftChange
{
    Created = 1,
    Revised = 2,
    Abandoned = 3,
    Committed = 4,
}

public sealed record OrderDraftHistoryEntry(
    OrderDraftChange Change,
    Guid ChangedByAccountId,
    DateTimeOffset RecordedAt,
    OrderDraftSnapshot Order);

public sealed record OrderDraftHistoryPage(
    long CurrentRevision,
    IReadOnlyList<OrderDraftHistoryEntry> Items,
    long? NextBeforeRevision);

public sealed record GetOrderDraftHistoryRequest(
    Guid OrderId,
    int Limit = 5,
    long? BeforeRevision = null)
{
    public void Validate()
    {
        if (OrderId == Guid.Empty)
            throw new OrderDraftValidationException("order_id_invalid", "Order identity cannot be empty.");
        // History includes full priced snapshots, unlike the lighter draft-header browse.
        if (Limit is < 1 or > 10)
            throw new OrderDraftValidationException("limit_invalid", "History page size must be between 1 and 10.");
        if (BeforeRevision is <= 0)
            throw new OrderDraftValidationException("history_revision_invalid", "Before revision must be a positive integer.");
    }
}

public interface IOrderDraftHistoryStore
{
    Task<OrderDraftHistoryPage?> ListHistoryAsync(
        TenantContext tenantContext,
        GetOrderDraftHistoryRequest request,
        CancellationToken cancellationToken);
}

public sealed class GetOrderDraftHistory(IOrderDraftHistoryStore store)
{
    public Task<OrderDraftHistoryPage?> ExecuteAsync(
        TenantContext tenantContext,
        GetOrderDraftHistoryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        return store.ListHistoryAsync(tenantContext, request, cancellationToken);
    }
}
