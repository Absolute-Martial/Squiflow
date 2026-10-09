using System.Text.Json;
using Application.Tenancy;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    private static async Task<(CommitOrderDraftStatus Status, OrderApprovalEvidence? Evidence)> AssessApprovalAdmissionAsync(
        OrderTenantDbSession session, OrderDraftSnapshot order, CancellationToken ct)
    {
        if (!OrderApprovalRules.IsRequired(order)) return (CommitOrderDraftStatus.Committed, null);
        var approved = await ReadApprovalAsync(session, OrderSql.FindApprovedOrderSubject, order.TenantId, "order_id", order.OrderId, ct).ConfigureAwait(false);
        if (approved is null) return (CommitOrderDraftStatus.ApprovalRequired, null);
        if (!OrderApprovalRules.Matches(approved.Subject, order)) return (CommitOrderDraftStatus.ApprovalStale, null);
        if (await ReadApprovalRevisionAuthorAsync(session, order, ct).ConfigureAwait(false) != approved.Subject.RevisionAuthorAccountId)
            throw InvalidApproval();
        return (CommitOrderDraftStatus.Committed, new(approved.RequestId, approved.Revision, order.Revision,
            approved.Subject.Fingerprint, approved.LastActorAccountId, approved.UpdatedAt, approved.AuthorizationRevision));
    }

    private static async Task InsertApprovalCommitEvidenceAsync(OrderTenantDbSession session, Guid tenantId, Guid orderId,
        OrderApprovalEvidence evidence, CancellationToken ct)
    {
        await using var command = session.CreateCommand(OrderSql.InsertApprovalCommitEvidence);
        command.Parameters.AddWithValue("tenant_id", tenantId); command.Parameters.AddWithValue("order_id", orderId);
        command.Parameters.AddWithValue("request_id", evidence.RequestId); command.Parameters.AddWithValue("request_revision", evidence.RequestRevision);
        command.Parameters.AddWithValue("order_revision", evidence.OrderRevision); command.Parameters.AddWithValue("subject_fingerprint", evidence.SubjectFingerprint);
        command.Parameters.AddWithValue("response_json", JsonSerializer.Serialize(evidence, ApprovalJsonOptions));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<OrderApprovalEvidence?> ReadApprovalCommitEvidenceAsync(OrderTenantDbSession session,
        OrderDraftSnapshot order, CancellationToken ct)
    {
        await using var command = session.CreateCommand(OrderSql.ReadApprovalCommitEvidence);
        command.Parameters.AddWithValue("tenant_id", order.TenantId); command.Parameters.AddWithValue("order_id", order.OrderId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (order.State == OrderDraftState.Committed && OrderApprovalRules.IsRequired(order)) throw InvalidApproval();
            return null;
        }
        try
        {
            var evidence = JsonSerializer.Deserialize<OrderApprovalEvidence>(reader.GetString(0), ApprovalJsonOptions) ?? throw InvalidApproval();
            var approved = ReadApprovalEnvelope(reader.GetString(5), order.TenantId);
            var original = order with
            {
                Revision = order.Revision - 1,
                State = OrderDraftState.Draft,
                CommittedAt = null,
                CommittedByAccountId = null,
                ApprovalEvidence = null
            };
            if (evidence.RequestId != reader.GetGuid(1) || evidence.RequestRevision != reader.GetInt64(2) || evidence.OrderRevision != reader.GetInt64(3) ||
                evidence.SubjectFingerprint != reader.GetString(4) || approved.State != OrderApprovalState.Approved ||
                approved.RequestId != evidence.RequestId || approved.Revision != evidence.RequestRevision ||
                approved.Subject.Fingerprint != evidence.SubjectFingerprint || approved.LastActorAccountId != evidence.ApprovedByAccountId ||
                approved.UpdatedAt != evidence.ApprovedAt || approved.AuthorizationRevision != evidence.AuthorizationRevision ||
                !OrderApprovalRules.Matches(approved.Subject, original)) throw InvalidApproval();
            OrderApprovalRules.RequireValidEvidence(order with { ApprovalEvidence = evidence });
            return evidence;
        }
        catch (JsonException) { throw InvalidApproval(); }
    }

    private static async Task RequireRetainedApprovalCommitAsync(OrderTenantDbSession session, OrderDraftSnapshot order, CancellationToken ct)
    {
        var evidence = await ReadApprovalCommitEvidenceAsync(session, order, ct).ConfigureAwait(false);
        if (evidence != order.ApprovalEvidence) throw InvalidApproval();
    }
}
