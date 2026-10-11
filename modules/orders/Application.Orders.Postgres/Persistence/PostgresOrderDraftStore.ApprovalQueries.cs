using System.Text.Json;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    public async Task<CommitOrderDraftStatus> AssessAdmissionAsync(TenantContext context, Guid orderId, long expectedOrderRevision, CancellationToken ct)
    {
        var authority = await ResolveApprovalAuthorityAsync(context, ct).ConfigureAwait(false);
        if (!authority.CanViewOrder) throw new OrderApprovalAccessDeniedException();
        OrderApprovalRules.ValidateIdentity(orderId, expectedOrderRevision);
        await using var session = await OrderTenantDbSession.OpenSnapshotAsync(dataSource, context.TenantId, ct).ConfigureAwait(false);
        var order = await FindOrderAsync(session, context.TenantId, orderId, ct).ConfigureAwait(false);
        if (order is null) return CommitOrderDraftStatus.NotFound;
        if (order.Revision != expectedOrderRevision) return CommitOrderDraftStatus.RevisionConflict;
        if (order.State != OrderDraftState.Draft) return OrderDraftLifecycle.AssessCommit(order.State, order.Revision, expectedOrderRevision);
        var result = await AssessApprovalAdmissionAsync(session, order, ct).ConfigureAwait(false);
        await session.CommitAsync(ct).ConfigureAwait(false);
        return result.Status;
    }

    public async Task<OrderApprovalSnapshot?> FindApprovalAsync(TenantContext context, Guid requestId, CancellationToken ct)
    {
        var authority = await ResolveApprovalAuthorityAsync(context, ct).ConfigureAwait(false);
        if (!authority.CanView || !authority.CanViewOrder) throw new OrderApprovalAccessDeniedException();
        if (requestId == Guid.Empty) throw new OrderDraftValidationException("approval_request_invalid", "Approval identity is invalid.");
        await using var session = await OrderTenantDbSession.OpenSnapshotAsync(dataSource, context.TenantId, ct).ConfigureAwait(false);
        var request = await ReadApprovalAsync(session, OrderSql.FindApproval, context.TenantId, "request_id", requestId, ct).ConfigureAwait(false);
        if (request is not null)
        {
            RequireApprovalVisibility(context, authority, request);
            await RequireRetainedProgramPolicyAsync(session, context.TenantId, request.Subject.Order, ct).ConfigureAwait(false);
            await RequireAcceptedQuotationFactsAsync(context, request.Subject.Order, ct).ConfigureAwait(false);
        }
        await session.CommitAsync(ct).ConfigureAwait(false);
        return request;
    }

    public async Task<OrderApprovalPage> ListApprovalsAsync(TenantContext context, ListOrderApprovalsRequest request, CancellationToken ct)
    {
        var authority = await ResolveApprovalAuthorityAsync(context, ct).ConfigureAwait(false);
        if (!authority.CanView) throw new OrderApprovalAccessDeniedException();
        ArgumentNullException.ThrowIfNull(request);
        if (request.Recovery && !MayDiscoverRecovery(authority)) throw new OrderApprovalAccessDeniedException();
        ValidateApprovalLimit(request.Limit);
        if (request.After is { } after && (after.RequestId == Guid.Empty || after.CreatedAt == default || after.CreatedAt.Offset != TimeSpan.Zero))
            throw new OrderDraftValidationException("approval_cursor_invalid", "Approval cursor is invalid.");
        await using var session = await OrderTenantDbSession.OpenSnapshotAsync(dataSource, context.TenantId, ct).ConfigureAwait(false);
        var items = new List<OrderApprovalListItem>(request.Limit + 1);
        await using (var command = session.CreateCommand(OrderSql.ListApprovals))
        {
            command.Parameters.AddWithValue("tenant_id", context.TenantId); command.Parameters.AddWithValue("actor_id", context.AccountId);
            command.Parameters.AddWithValue("recovery", request.Recovery); command.Parameters.AddWithValue("limit", request.Limit + 1);
            command.Parameters.Add(new NpgsqlParameter("after_created_at", NpgsqlDbType.TimestampTz) { Value = (object?)request.After?.CreatedAt ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter("after_id", NpgsqlDbType.Uuid) { Value = (object?)request.After?.RequestId ?? DBNull.Value });
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (reader.GetString(4) != "pending") throw InvalidApproval();
                var item = new OrderApprovalListItem(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetInt64(3),
                    OrderApprovalState.Pending, reader.GetGuid(5), reader.GetGuid(6), reader.GetFieldValue<DateTimeOffset>(7), reader.GetBoolean(8), false);
                if (item.RequestId == Guid.Empty || item.OrderId == Guid.Empty || item.OrderRevision < 1 || item.RequestRevision < 1 ||
                    item.RequestedByAccountId == Guid.Empty || item.AssignedAccountId == Guid.Empty || item.CreatedAt == default ||
                    !request.Recovery && item.RequestedByAccountId != context.AccountId && item.AssignedAccountId != context.AccountId)
                    throw InvalidApproval();
                items.Add(item);
            }
        }
        var hasMore = items.Count > request.Limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        for (var index = 0; index < items.Count; index++)
            items[index] = items[index] with { AssigneeUnavailable = !await IsEligibleApproverAsync(context, items[index].AssignedAccountId, ct).ConfigureAwait(false) };
        await session.CommitAsync(ct).ConfigureAwait(false);
        return new(items.AsReadOnly(), hasMore ? new(items[^1].CreatedAt, items[^1].RequestId) : null);
    }

    public async Task<OrderApprovalHistoryPage?> ListApprovalHistoryAsync(TenantContext context, Guid requestId,
        ListOrderApprovalHistoryRequest request, CancellationToken ct)
    {
        var authority = await ResolveApprovalAuthorityAsync(context, ct).ConfigureAwait(false);
        if (!authority.CanView || !authority.CanViewOrder) throw new OrderApprovalAccessDeniedException();
        ArgumentNullException.ThrowIfNull(request);
        if (requestId == Guid.Empty || request.BeforeRevision is < 2)
            throw new OrderDraftValidationException("approval_history_invalid", "Approval history request is invalid.");
        ValidateApprovalLimit(request.Limit, 10);
        await using var session = await OrderTenantDbSession.OpenSnapshotAsync(dataSource, context.TenantId, ct).ConfigureAwait(false);
        var current = await ReadApprovalAsync(session, OrderSql.FindApproval, context.TenantId, "request_id", requestId, ct).ConfigureAwait(false);
        if (current is null) return null;
        RequireApprovalVisibility(context, authority, current);
        await RequireRetainedProgramPolicyAsync(session, context.TenantId, current.Subject.Order, ct).ConfigureAwait(false);
        var items = new List<OrderApprovalSnapshot>(request.Limit + 1);
        await using (var command = session.CreateCommand(OrderSql.ListApprovalHistory))
        {
            command.Parameters.AddWithValue("tenant_id", context.TenantId); command.Parameters.AddWithValue("request_id", requestId);
            command.Parameters.AddWithValue("limit", request.Limit + 1);
            command.Parameters.Add(new NpgsqlParameter("before_revision", NpgsqlDbType.Bigint) { Value = (object?)request.BeforeRevision ?? DBNull.Value });
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var item = ReadApprovalEnvelope(reader.GetString(reader.GetOrdinal("response_json")), context.TenantId);
                ValidateApprovalEventColumns(reader, item);
                if (item.RequestId != requestId || item.Revision != reader.GetInt64(reader.GetOrdinal("request_revision")) ||
                    item.RequestedByAccountId != current.RequestedByAccountId || item.CreatedAt != current.CreatedAt ||
                    JsonSerializer.Serialize(item.Subject, ApprovalJsonOptions) != JsonSerializer.Serialize(current.Subject, ApprovalJsonOptions)) throw InvalidApproval();
                items.Add(item);
            }
        }
        var expected = Math.Min(current.Revision, (request.BeforeRevision ?? (current.Revision + 1)) - 1);
        foreach (var item in items)
        {
            if (item.Revision != expected--) throw InvalidApproval();
        }
        if (items.Count <= request.Limit && expected != 0) throw InvalidApproval();
        var hasMore = items.Count > request.Limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        await session.CommitAsync(ct).ConfigureAwait(false);
        return new(current.Revision, items.AsReadOnly(), hasMore ? items[^1].Revision : null);
    }

    private static bool MayDiscoverRecovery(OrderApprovalAuthority authority) =>
        authority.CanView && authority.CanRecover && authority.IsCurrentInitialOwner;

    private static void RequireApprovalVisibility(TenantContext context, OrderApprovalAuthority authority, OrderApprovalSnapshot request)
    {
        if (request.RequestedByAccountId != context.AccountId && request.AssignedAccountId != context.AccountId && !MayDiscoverRecovery(authority))
            throw new OrderApprovalAccessDeniedException();
    }

    private static void ValidateApprovalLimit(int limit, int maximum = 50)
    {
        if (limit < 1 || limit > maximum) throw new OrderDraftValidationException("approval_page_size_invalid", "Approval page size is invalid.");
    }
}
