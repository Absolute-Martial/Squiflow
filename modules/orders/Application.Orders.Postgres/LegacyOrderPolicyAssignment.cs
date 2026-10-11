using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Application.Orders.Postgres;

public enum LegacyOrderPolicyAssignmentStatus { Assigned, Replayed, NotFound, RevisionConflict, NotDraft, AlreadyBound, IdempotencyKeyConflict, ProfileUnavailable }
public sealed record LegacyOrderPolicyAssignmentResult(LegacyOrderPolicyAssignmentStatus Status, Guid OrderId, long? ObservedRevision = null);

public sealed partial class PostgresOrderDraftStore
{
    // A control-plane assignment records its actor/device evidence without rewriting old business receipts or Order revisions.
    public async Task<LegacyOrderPolicyAssignmentResult> AssignLegacyPolicyAsync(Guid tenantId, Guid orderId, long expectedRevision,
        Guid principalId, Guid deviceId, string key, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || orderId == Guid.Empty || principalId == Guid.Empty || deviceId == Guid.Empty || expectedRevision < 1)
            throw new ArgumentException("The legacy Order policy request is invalid.");
        key = OrderProgramReference.NormalizeIdempotencyKey(key);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(
            "order-profile-baseline/v1:", deviceId.ToString("D"), ":", orderId.ToString("D"), ":", expectedRevision.ToString(CultureInfo.InvariantCulture))))).ToLowerInvariant();
        await using var session = await OrderTenantDbSession.OpenAsync(dataSource, tenantId, ct).ConfigureAwait(false);
        await using (var orderLock = session.CreateCommand(OrderSql.LockOrderForCommit))
        {
            orderLock.Parameters.AddWithValue("tenant_id", tenantId); orderLock.Parameters.AddWithValue("order_id", orderId);
            await orderLock.ExecuteScalarAsync(ct).ConfigureAwait(false);
        }
        (Guid OrderId, string Fingerprint, Guid DeviceId, long Revision, OrderProgramPolicyFacts Policy)? retained = null;
        await using (var replay = session.CreateCommand(OrderSql.ReadBaselineAssignment))
        {
            replay.Parameters.AddWithValue("tenant_id", tenantId); replay.Parameters.AddWithValue("principal_id", principalId); replay.Parameters.AddWithValue("key", key);
            await using var reader = await replay.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
                retained = (reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetInt64(3),
                    new(reader.GetGuid(4), reader.GetGuid(5), reader.GetBoolean(6)));
        }
        if (retained is { } original)
        {
            if (original.OrderId != orderId || original.Fingerprint != fingerprint || original.DeviceId != deviceId || original.Revision != expectedRevision)
                return new(LegacyOrderPolicyAssignmentStatus.IdempotencyKeyConflict, orderId);
            if (original.Policy.RequireReferenceForProgramOrders || profilePolicySource is null ||
                !await profilePolicySource.IsRetainedCompatibleAsync(session.Connection, session.Transaction, tenantId, original.Policy, ct).ConfigureAwait(false))
                throw new InvalidOperationException("The retained legacy Order profile assignment is invalid.");
            return new(LegacyOrderPolicyAssignmentStatus.Replayed, orderId, original.Revision);
        }
        var state = await FindOrderStateAsync(session, tenantId, orderId, ct).ConfigureAwait(false);
        if (state is null) return new(LegacyOrderPolicyAssignmentStatus.NotFound, orderId);
        if (state.Value.State != OrderDraftState.Draft) return new(LegacyOrderPolicyAssignmentStatus.NotDraft, orderId, state.Value.Revision);
        if (state.Value.Revision != expectedRevision) return new(LegacyOrderPolicyAssignmentStatus.RevisionConflict, orderId, state.Value.Revision);
        if (await ReadProgramPolicyAsync(session, tenantId, orderId, ct).ConfigureAwait(false) is not null)
            return new(LegacyOrderPolicyAssignmentStatus.AlreadyBound, orderId, state.Value.Revision);
        await using (var eligibility = session.CreateCommand(OrderSql.ReadLegacyPolicyEligibility))
        {
            eligibility.Parameters.AddWithValue("tenant_id", tenantId); eligibility.Parameters.AddWithValue("order_id", orderId);
            if (await eligibility.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true)
                return new(LegacyOrderPolicyAssignmentStatus.AlreadyBound, orderId, state.Value.Revision);
        }
        var baseline = profilePolicySource is null ? null : await profilePolicySource.ResolveLegacyBaselineAsync(
            session.Connection, session.Transaction, tenantId, ct).ConfigureAwait(false);
        if (baseline is null || baseline.RequireReferenceForProgramOrders)
            return new(LegacyOrderPolicyAssignmentStatus.ProfileUnavailable, orderId);
        await using (var insert = session.CreateCommand(OrderSql.InsertBaselineAssignment))
        {
            insert.Parameters.AddWithValue("tenant_id", tenantId); insert.Parameters.AddWithValue("order_id", orderId);
            insert.Parameters.AddWithValue("profile_id", baseline.ProfileId); insert.Parameters.AddWithValue("policy_revision_id", baseline.PolicyRevisionId);
            insert.Parameters.AddWithValue("principal_id", principalId); insert.Parameters.AddWithValue("device_id", deviceId);
            insert.Parameters.AddWithValue("key", key); insert.Parameters.AddWithValue("fingerprint", fingerprint);
            insert.Parameters.AddWithValue("expected_revision", expectedRevision); insert.Parameters.AddWithValue("bound_at", _timeProvider.GetUtcNow());
            if (await insert.ExecuteScalarAsync(ct).ConfigureAwait(false) is null)
                return new(LegacyOrderPolicyAssignmentStatus.IdempotencyKeyConflict, orderId);
        }
        await session.CommitAsync(ct).ConfigureAwait(false);
        return new(LegacyOrderPolicyAssignmentStatus.Assigned, orderId, expectedRevision);
    }
}
