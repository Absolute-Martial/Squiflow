using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    public async Task<ReviseOrderDraftResult> ReviseAsync(
        TenantContext tenantContext,
        ReviseOrderDraftRequest request,
        OrderDraftIntent intent,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        await using var session = await OrderTenantDbSession
            .OpenAsync(dataSource, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var receipt = await FindReceiptAsync(
            session, tenantContext.TenantId, tenantContext.AccountId,
            ReviseOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ToExistingReviseResult(receipt, fingerprint);
        }

        await using (var update = session.CreateCommand(OrderSql.TryReviseOrder))
        {
            update.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            update.Parameters.AddWithValue("order_id", request.OrderId);
            update.Parameters.AddWithValue("expected_revision", request.ExpectedRevision);
            update.Parameters.AddWithValue("summary", intent.Summary);
            update.Parameters.AddWithValue("currency_code", intent.CurrencyCode);
            update.Parameters.AddWithValue("total", intent.Total);
            update.Parameters.Add(new NpgsqlParameter("customer_organization_id", NpgsqlDbType.Uuid)
            {
                Value = (object?)intent.CustomerContext?.OrganizationId ?? DBNull.Value,
            });
            update.Parameters.Add(new NpgsqlParameter("customer_program_id", NpgsqlDbType.Uuid)
            {
                Value = (object?)intent.CustomerContext?.ProgramId ?? DBNull.Value,
            });
            if (await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                // The conditional update may have waited for a command using the same receipt key.
                receipt = await FindReceiptAsync(
                    session, tenantContext.TenantId, tenantContext.AccountId,
                    ReviseOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
                if (receipt is not null)
                {
                    await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return ToExistingReviseResult(receipt, fingerprint);
                }

                var current = await FindOrderStateAsync(
                    session, tenantContext.TenantId, request.OrderId, cancellationToken)
                    .ConfigureAwait(false);
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                var status = current is null
                    ? ReviseOrderDraftStatus.NotFound
                    : OrderDraftLifecycle.AssessRevise(
                        current.Value.State, current.Value.Revision, request.ExpectedRevision);
                if (status == ReviseOrderDraftStatus.Revised)
                {
                    throw new InvalidOperationException("The draft remained eligible after its conditional update failed.");
                }

                return new ReviseOrderDraftResult(status, null);
            }
        }

        var headerWithOldLines = await FindOrderAsync(
            session, tenantContext.TenantId, request.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The revised order disappeared before line replacement.");
        var order = headerWithOldLines with { Lines = intent.Lines };
        await using (var delete = session.CreateCommand(OrderSql.DeleteLines))
        {
            delete.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            delete.Parameters.AddWithValue("order_id", request.OrderId);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertLinesAsync(session, order, cancellationToken).ConfigureAwait(false);
        if (await TryInsertReceiptAsync(
            session, tenantContext.AccountId, ReviseOperation, idempotencyKey,
            fingerprint, order, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ReviseOrderDraftResult(ReviseOrderDraftStatus.Revised, order);
        }

        receipt = await FindReceiptAsync(
            session, tenantContext.TenantId, tenantContext.AccountId,
            ReviseOperation, idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The order command receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ToExistingReviseResult(receipt, fingerprint);
    }
}
