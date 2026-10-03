using Application.Tenancy;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    public async Task<CommitOrderDraftResult> CommitAsync(
        TenantContext tenantContext,
        CommitOrderDraftRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        await using var session = await OrderTenantDbSession
            .OpenAsync(dataSource, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var receipt = await FindReceiptAsync(
            session, tenantContext.TenantId, tenantContext.AccountId,
            CommitOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ToExistingCommitResult(receipt, fingerprint);
        }

        var committedAt = _timeProvider.GetUtcNow();
        var changed = await TryCommitOrderAsync(
            session, tenantContext, request, committedAt, cancellationToken)
            .ConfigureAwait(false);
        if (!changed)
        {
            // A concurrent winner may have committed while the conditional UPDATE waited.
            receipt = await FindReceiptAsync(
                session, tenantContext.TenantId, tenantContext.AccountId,
                CommitOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (receipt is not null)
            {
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return ToExistingCommitResult(receipt, fingerprint);
            }

            var current = await FindOrderStateAsync(
                session, tenantContext.TenantId, request.OrderId, cancellationToken)
                .ConfigureAwait(false);
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            var status = current is null
                ? CommitOrderDraftStatus.NotFound
                : OrderDraftLifecycle.AssessCommit(
                    current.Value.State, current.Value.Revision, request.ExpectedRevision);
            if (status == CommitOrderDraftStatus.Committed)
            {
                throw new InvalidOperationException("The draft remained eligible after its conditional update failed.");
            }

            return new CommitOrderDraftResult(
                status, null);
        }

        var order = await FindOrderAsync(
            session, tenantContext.TenantId, request.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The committed order disappeared before receipt creation.");
        if (await TryInsertReceiptAsync(
            session, tenantContext.AccountId, CommitOperation,
            idempotencyKey, fingerprint, order, committedAt, cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new CommitOrderDraftResult(CommitOrderDraftStatus.Committed, order);
        }

        receipt = await FindReceiptAsync(
            session, tenantContext.TenantId, tenantContext.AccountId,
            CommitOperation, idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The order command receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ToExistingCommitResult(receipt, fingerprint);
    }

    private static async Task<bool> TryCommitOrderAsync(
        OrderTenantDbSession session,
        TenantContext tenantContext,
        CommitOrderDraftRequest request,
        DateTimeOffset committedAt,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(OrderSql.TryCommitOrder);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.AddWithValue("order_id", request.OrderId);
        command.Parameters.AddWithValue("expected_revision", request.ExpectedRevision);
        command.Parameters.AddWithValue("committed_at", committedAt);
        command.Parameters.AddWithValue("account_id", tenantContext.AccountId);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

}
