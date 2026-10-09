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

        // Lock before reading lines: revision/abandon/commit cannot change the
        // retained content while commercial facts are revalidated under the transaction's pin.
        await using (var orderLock = session.CreateCommand(OrderSql.LockOrderForCommit))
        {
            orderLock.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            orderLock.Parameters.AddWithValue("order_id", request.OrderId);
            await orderLock.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
        // Another same-key winner may have committed while this row lock waited.
        receipt = await FindReceiptAsync(session, tenantContext.TenantId, tenantContext.AccountId,
            CommitOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ToExistingCommitResult(receipt, fingerprint);
        }
        var beforeCommit = await FindOrderAsync(session, tenantContext.TenantId, request.OrderId, cancellationToken).ConfigureAwait(false);
        if (beforeCommit is { State: OrderDraftState.Draft } && beforeCommit.Revision == request.ExpectedRevision &&
            beforeCommit.Lines.Any(line => line.CommercialFacts is not null))
        {
            // Missing composition fails closed; manual paths do not depend on this port.
            if (commercialCommitGuard is null)
                return new(CommitOrderDraftStatus.CommercialFactsConflict, null);

            // External current authority is resolved BEFORE the publication pin. It is
            // not Catalog/Pricing state, so the pin does not protect it, and a slow or
            // degraded authorization provider must never hold tenant-wide publication
            // exclusion for the length of an external call.
            var currentAuthority = await commercialCommitGuard
                .ResolveCurrentAuthorityAsync(tenantContext, beforeCommit, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // The pin belongs to THIS Orders transaction, so PostgreSQL releases it at the
            // same COMMIT/ROLLBACK as the header update and receipt: losing this backend
            // cannot leave an effect without its publication protection. It does NOT make
            // the comparison share that transaction. The guard's comparison queries run on
            // separate pooled connections in their own ordinary read transactions; what
            // makes their result valid for the effect is this lock-mediated exclusion —
            // no Catalog/Pricing publication can obtain the exclusive pin, and therefore
            // no selectable fact can change between the comparison and the commit.
            // Catalog/Pricing writers pin exclusively before their own row locks and
            // never lock Orders rows. Public comparison queries acquire no second pin.
            await using (var pin = session.CreateCommand(OrderSql.PinCommercialPublication))
            {
                pin.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
                await pin.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!await commercialCommitGuard.IsCompatibleAsync(
                    tenantContext, beforeCommit, currentAuthority, cancellationToken).ConfigureAwait(false))
                return new(CommitOrderDraftStatus.CommercialFactsConflict, null);
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
