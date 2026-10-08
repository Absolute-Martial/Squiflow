using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    private const string ProgramReferenceOperation = "set-order-program-reference";

    private static async Task<(OrderProgramPolicyFacts Policy, string? Reference, bool HasLegacyAssignment)?> ReadProgramPolicyAsync(
        OrderTenantDbSession session, Guid tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(OrderSql.ReadProgramOrderPolicy);
        command.Parameters.AddWithValue("tenant_id", tenantId); command.Parameters.AddWithValue("order_id", orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (new(reader.GetGuid(0), reader.GetGuid(1), reader.GetBoolean(2)), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4)) : null;
    }

    private static async Task<(bool Found, OrderProgramPolicyFacts? Policy)> ReadCreationProgramPolicyAsync(OrderTenantDbSession session,
        Guid tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(OrderSql.ReadCreationReceipt);
        command.Parameters.AddWithValue("tenant_id", tenantId); command.Parameters.AddWithValue("order_id", orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var found = false;
        OrderProgramPolicyFacts? policy = null;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var receipt = new OrderCommandReceipt(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3));
            var order = ReadReceiptSnapshot(receipt, reader.GetString(4));
            if (found && order.ProgramPolicy != policy) throw InvalidReceipt();
            found = true;
            policy = order.ProgramPolicy;
        }
        return (found, policy);
    }

    private async Task RequireRetainedProgramPolicyAsync(OrderTenantDbSession session, Guid tenantId,
        OrderDraftSnapshot order, CancellationToken cancellationToken)
    {
        if (order.ProgramPolicy is not { } policy)
        {
            await using var command = session.CreateCommand(OrderSql.ReadCreationPolicyPresence);
            command.Parameters.AddWithValue("tenant_id", tenantId); command.Parameters.AddWithValue("order_id", order.OrderId);
            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true)
                throw new InvalidOperationException("The Order lost its immutable creation policy binding.");
            return;
        }
        var creation = await ReadCreationProgramPolicyAsync(session, tenantId, order.OrderId, cancellationToken).ConfigureAwait(false);
        var creationPolicy = creation.Policy;
        var retainedMetadata = await ReadProgramPolicyAsync(session, tenantId, order.OrderId, cancellationToken).ConfigureAwait(false);
        if (!creation.Found || retainedMetadata?.Policy != policy || creationPolicy is not null && creationPolicy != policy || profilePolicySource is null ||
            !await profilePolicySource.IsRetainedCompatibleAsync(session.Connection, session.Transaction, tenantId, policy, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("The pinned Order policy does not match its retained profile and immutable binding.");
        if (creationPolicy is null && (retainedMetadata?.HasLegacyAssignment != true ||
            await profilePolicySource.ResolveLegacyBaselineAsync(session.Connection, session.Transaction,
                tenantId, cancellationToken).ConfigureAwait(false) != policy))
            throw new InvalidOperationException("The legacy Order policy does not match its explicitly selected baseline.");
    }

    private static async Task InsertProgramPolicyAsync(OrderTenantDbSession session, OrderDraftSnapshot order, CancellationToken cancellationToken)
    {
        if (order.ProgramPolicy is not { } policy) return;
        OrderProgramReference.RequireValidStored(policy, order.ExternalProgramReference);
        await using var command = session.CreateCommand(OrderSql.InsertProgramOrderPolicy);
        command.Parameters.AddWithValue("tenant_id", order.TenantId); command.Parameters.AddWithValue("order_id", order.OrderId);
        command.Parameters.AddWithValue("profile_id", policy.ProfileId); command.Parameters.AddWithValue("policy_revision_id", policy.PolicyRevisionId);
        command.Parameters.AddWithValue("require_reference", policy.RequireReferenceForProgramOrders);
        command.Parameters.Add(new NpgsqlParameter("external_reference", NpgsqlDbType.Text) { Value = (object?)order.ExternalProgramReference ?? DBNull.Value });
        command.Parameters.AddWithValue("bound_at", order.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SetOrderProgramReferenceResult> SetProgramReferenceAsync(TenantContext tenantContext,
        SetOrderProgramReferenceRequest request, string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext); ArgumentNullException.ThrowIfNull(request);
        if (request.OrderId == Guid.Empty || request.ExpectedRevision < 1 || request.ExpectedRevision == long.MaxValue)
            throw new ArgumentException("The Order reference request is invalid.");
        idempotencyKey = OrderProgramReference.NormalizeIdempotencyKey(idempotencyKey);
        var reference = OrderProgramReference.Normalize(request.ExternalProgramReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        await using var session = await OrderTenantDbSession.OpenAsync(dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        await using (var orderLock = session.CreateCommand(OrderSql.LockOrderForCommit))
        {
            orderLock.Parameters.AddWithValue("tenant_id", tenantContext.TenantId); orderLock.Parameters.AddWithValue("order_id", request.OrderId);
            await orderLock.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
        var receipt = await FindReceiptAsync(session, tenantContext.TenantId, tenantContext.AccountId, ProgramReferenceOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            var retained = ReadReceiptSnapshot(receipt, ProgramReferenceOperation);
            if (receipt.Fingerprint != fingerprint) return new(SetOrderProgramReferenceStatus.IdempotencyKeyConflict, null);
            await RequireRetainedProgramPolicyAsync(session, tenantContext.TenantId, retained, cancellationToken).ConfigureAwait(false);
            await RequireAcceptedQuotationFactsAsync(tenantContext, retained, cancellationToken).ConfigureAwait(false);
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(SetOrderProgramReferenceStatus.Replayed, retained);
        }
        var order = await FindOrderAsync(session, tenantContext.TenantId, request.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null) return new(SetOrderProgramReferenceStatus.NotFound, null);
        if (order.State == OrderDraftState.Committed) return new(SetOrderProgramReferenceStatus.AlreadyCommitted, null);
        if (order.State == OrderDraftState.Abandoned) return new(SetOrderProgramReferenceStatus.AlreadyAbandoned, null);
        if (order.Revision != request.ExpectedRevision) return new(SetOrderProgramReferenceStatus.RevisionConflict, null);
        if (order.ProgramPolicy is null || profilePolicySource is null ||
            !await profilePolicySource.IsRetainedCompatibleAsync(session.Connection, session.Transaction, tenantContext.TenantId, order.ProgramPolicy, cancellationToken).ConfigureAwait(false))
            return new(SetOrderProgramReferenceStatus.ProfileUnavailable, null);
        await RequireAcceptedQuotationFactsAsync(tenantContext, order, cancellationToken).ConfigureAwait(false);
        await using (var update = session.CreateCommand(OrderSql.AdvanceReferenceRevision))
        {
            update.Parameters.AddWithValue("tenant_id", tenantContext.TenantId); update.Parameters.AddWithValue("order_id", order.OrderId);
            update.Parameters.AddWithValue("expected_revision", order.Revision);
            if (await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1) throw new InvalidOperationException("The locked Order reference revision changed.");
        }
        await using (var update = session.CreateCommand(OrderSql.UpdateProgramReference))
        {
            update.Parameters.AddWithValue("tenant_id", tenantContext.TenantId); update.Parameters.AddWithValue("order_id", order.OrderId);
            update.Parameters.Add(new NpgsqlParameter("external_reference", NpgsqlDbType.Text) { Value = (object?)reference ?? DBNull.Value });
            if (await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1) throw new InvalidOperationException("The locked Order policy disappeared.");
        }
        order = order with { Revision = order.Revision + 1, ExternalProgramReference = reference };
        if (!await TryInsertReceiptAsync(session, tenantContext.AccountId, ProgramReferenceOperation, idempotencyKey, fingerprint, order, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            receipt = await FindReceiptAsync(session, tenantContext.TenantId, tenantContext.AccountId, ProgramReferenceOperation, idempotencyKey, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The Order reference receipt disappeared after a conflict.");
            await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
            // Another Order won this actor-scoped idempotencyKey. The losing metadata and revision are rolled back.
            return new(SetOrderProgramReferenceStatus.IdempotencyKeyConflict, null);
        }
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(SetOrderProgramReferenceStatus.Updated, order);
    }
}
