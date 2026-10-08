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
            var replayResult = await ToExistingCommitResultAsync(session, tenantContext, receipt, fingerprint, cancellationToken).ConfigureAwait(false);
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return replayResult;
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
            var replayResult = await ToExistingCommitResultAsync(session, tenantContext, receipt, fingerprint, cancellationToken).ConfigureAwait(false);
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return replayResult;
        }
        var beforeCommit = await FindOrderAsync(session, tenantContext.TenantId, request.OrderId, cancellationToken).ConfigureAwait(false);
        if (beforeCommit is { State: OrderDraftState.Draft } && beforeCommit.Revision == request.ExpectedRevision &&
            (beforeCommit.QuotationOrigin is not null || beforeCommit.Lines.Any(line => line.CommercialFacts is not null)))
        {
            // Missing composition fails closed; manual paths do not depend on this port.
            if (commercialCommitGuard is null)
                return new(CommitOrderDraftStatus.CommercialFactsConflict, null);

            // Same backend/transaction as the effect and receipt: backend loss cannot
            // release publication protection while leaving this commit alive.
            // Catalog/Pricing writers pin exclusively before their own row locks and
            // never lock Orders rows. Public comparison queries acquire no second pin.
            if (beforeCommit.QuotationOrigin is null)
                await using (var pin = session.CreateCommand(OrderSql.PinCommercialPublication))
                {
                    pin.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
                    await pin.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            cancellationToken.ThrowIfCancellationRequested();
            if (!await commercialCommitGuard.IsCompatibleAsync(tenantContext, beforeCommit, cancellationToken).ConfigureAwait(false))
                return new(CommitOrderDraftStatus.CommercialFactsConflict, null);
        }

        if (beforeCommit is { State: OrderDraftState.Draft } && beforeCommit.Revision == request.ExpectedRevision)
        {
            if (profilePolicySource is not null && (beforeCommit.ProgramPolicy is null ||
                !await profilePolicySource.IsRetainedCompatibleAsync(session.Connection, session.Transaction,
                    tenantContext.TenantId, beforeCommit.ProgramPolicy, cancellationToken).ConfigureAwait(false)))
                return new(CommitOrderDraftStatus.ProfileUnavailable, null);
            if (OrderProgramReference.IsMissing(beforeCommit))
                return new(CommitOrderDraftStatus.ProgramReferenceRequired, null);
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
                var replayResult = await ToExistingCommitResultAsync(session, tenantContext, receipt, fingerprint, cancellationToken).ConfigureAwait(false);
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return replayResult;
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
        var conflictResult = await ToExistingCommitResultAsync(session, tenantContext, receipt, fingerprint, cancellationToken).ConfigureAwait(false);
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return conflictResult;
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
