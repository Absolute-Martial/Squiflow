using Application.Tenancy;
using Npgsql;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    public async Task<OrderWorkSourceReference?> ReadWorkSourceInTransactionAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, TenantContext context, Guid orderId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfEqual(context.AccountId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(orderId, Guid.Empty);
        await using var session = await OrderTenantDbSession.BorrowAsync(connection, transaction, context.TenantId,
            cancellationToken).ConfigureAwait(false);
        // Keep the header and lines coherent when the caller's transaction uses ReadCommitted.
        await using (var orderLock = session.CreateCommand(OrderSql.LockOrderForCommit))
        {
            orderLock.Parameters.AddWithValue("tenant_id", context.TenantId);
            orderLock.Parameters.AddWithValue("order_id", orderId);
            await orderLock.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
        var order = await FindOrderAsync(session, context.TenantId, orderId, cancellationToken).ConfigureAwait(false);
        if (order is null) return null;
        if (order.TenantId != context.TenantId || order.OrderId != orderId)
            throw new InvalidOperationException("Orders returned another resource's work source.");
        var count = order.Lines.Count;
        var line = count == 1 ? order.Lines[0] : null;
        return new(order.TenantId, order.OrderId, order.Revision, order.CustomerContext?.ProgramId ?? Guid.Empty,
            order.State == OrderDraftState.Committed, count, line?.Position ?? 0, line?.Quantity ?? 0m,
            line?.CommercialFacts?.Catalog);
    }
}
