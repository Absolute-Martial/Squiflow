using Application.Tenancy;
using Npgsql;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    public async Task<OrderFinancialSourceFacts?> ReadFinancialSourceInTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantContext context,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(context);
        if (orderId == Guid.Empty || context.AccountId == Guid.Empty)
            throw new ArgumentException("Order and account identities cannot be empty.");

        await using var session = await OrderTenantDbSession.BorrowAsync(connection, transaction, context.TenantId,
            cancellationToken).ConfigureAwait(false);
        // The retained header, lines, policy and approval evidence must form one coherent source.
        await using (var orderLock = session.CreateCommand(OrderSql.LockOrderForCommit))
        {
            orderLock.Parameters.AddWithValue("tenant_id", context.TenantId);
            orderLock.Parameters.AddWithValue("order_id", orderId);
            await orderLock.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
        var order = await FindOrderAsync(session, context.TenantId, orderId, cancellationToken).ConfigureAwait(false);
        if (order is null) return null;
        if (order.TenantId != context.TenantId || order.OrderId != orderId)
            throw new InvalidOperationException("Orders returned another resource's financial source.");

        // Commitment owns price/quotation admission. This read validates retained facts and never
        // asks the current quotation, catalog or pricing state to admit those prices again.
        if (order.QuotationOrigin is { } origin)
            new AcceptedQuotationOrder(origin, order.Summary, order.CurrencyCode, order.Total, order.Lines,
                order.CustomerContext).RequireValid(context.TenantId);
        var lines = Array.AsReadOnly(order.Lines.Select(line => new OrderFinancialSourceLine(line.Position,
            line.Description, line.Quantity, line.UnitCode, line.UnitPrice, line.LineTotal)).ToArray());
        return new(order.TenantId, order.OrderId, order.Revision, order.State == OrderDraftState.Committed,
            order.CurrencyCode, order.CustomerContext, lines, order.Total);
    }
}
