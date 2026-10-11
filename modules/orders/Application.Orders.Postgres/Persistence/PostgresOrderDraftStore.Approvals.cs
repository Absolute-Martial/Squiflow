using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    private static readonly JsonSerializerOptions ApprovalJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<OrderApprovalResult> SubmitApprovalAsync(TenantContext context,
        SubmitOrderApprovalRequest request, string key, CancellationToken ct)
    {
        var authority = await ResolveApprovalAuthorityAsync(context, ct).ConfigureAwait(false);
        if (!MayExecuteApproval(authority, "submit")) return new(OrderApprovalStatus.Forbidden, null);
        ArgumentNullException.ThrowIfNull(request);
        OrderApprovalRules.ValidateIdentity(request.OrderId, request.ExpectedOrderRevision);
        if (request.AssignedAccountId == Guid.Empty || request.ExpectedPendingRequestRevision is < 1 or long.MaxValue)
            throw new OrderDraftValidationException("approval_request_invalid", "Approval assignment or pending revision is invalid.");
        key = OrderApprovalRules.NormalizeKey(key);
        var fingerprint = OrderApprovalRules.Fingerprint(request);
        await using var session = await OrderTenantDbSession.OpenAsync(dataSource, context.TenantId, ct).ConfigureAwait(false);
        await LockApprovalOrderAsync(session, context.TenantId, request.OrderId, ct).ConfigureAwait(false);
        authority = await ResolveApprovalAuthorityAsync(context, ct).ConfigureAwait(false);
        if (!MayExecuteApproval(authority, "submit")) return new(OrderApprovalStatus.Forbidden, null);
        var replay = await FindApprovalReplayAsync(session, context, "submit", key, fingerprint, ct).ConfigureAwait(false);
        if (replay is not null)
        {
            if (replay.Request is not null)
                await RequireRetainedProgramPolicyAsync(session, context.TenantId, replay.Request.Subject.Order, ct).ConfigureAwait(false);
            await session.CommitAsync(ct).ConfigureAwait(false);
            return replay;
        }
        var order = await FindOrderAsync(session, context.TenantId, request.OrderId, ct).ConfigureAwait(false);
        if (order is null) return new(OrderApprovalStatus.NotFound, null);
        if (order.State != OrderDraftState.Draft) return new(OrderApprovalStatus.OrderNotDraft, null);
        if (order.Revision != request.ExpectedOrderRevision) return new(OrderApprovalStatus.RevisionConflict, null);
        if (order.ProgramPolicy is null || profilePolicySource is null)
            return new(OrderApprovalStatus.ProfileUnavailable, null);
        if (OrderProgramReference.IsMissing(order)) return new(OrderApprovalStatus.ProgramReferenceRequired, null);
        if (!await ApprovalCommercialFactsCompatibleAsync(session, context, order, ct).ConfigureAwait(false))
            return new(OrderApprovalStatus.CommercialFactsConflict, null);
        var author = await ReadApprovalRevisionAuthorAsync(session, order, ct).ConfigureAwait(false);
        if (request.AssignedAccountId == context.AccountId || request.AssignedAccountId == author)
            return new(OrderApprovalStatus.SelfApprovalForbidden, null);
        if (!await IsEligibleApproverAsync(context, request.AssignedAccountId, ct).ConfigureAwait(false))
            return new(OrderApprovalStatus.IneligibleApprover, null);
        var pending = await ReadApprovalAsync(session, OrderSql.FindPendingApproval, context.TenantId,
            "order_id", order.OrderId, ct).ConfigureAwait(false);
        if (pending is not null)
        {
            if (OrderApprovalRules.Matches(pending.Subject, order)) return new(OrderApprovalStatus.PendingRequestExists, null);
            if (request.ExpectedPendingRequestRevision != pending.Revision) return new(OrderApprovalStatus.RevisionConflict, null);
        }
        else if (request.ExpectedPendingRequestRevision is not null)
            return new(OrderApprovalStatus.RevisionConflict, null);
        var now = UtcNow();
        if (pending is not null)
        {
            var superseded = pending with
            {
                Revision = pending.Revision + 1,
                State = OrderApprovalState.Superseded,
                UpdatedAt = now,
                LastActorAccountId = context.AccountId,
                AuthorizationRevision = authority.AuthorizationRevision,
                Change = OrderApprovalChange.Superseded,
                Reason = null
            };
            await UpdateApprovalAsync(session, superseded, pending.Revision, ct).ConfigureAwait(false);
            if (!await InsertApprovalEventAsync(session, superseded, "supersede", key, fingerprint, ct).ConfigureAwait(false))
            {
                await session.RollbackAsync(ct).ConfigureAwait(false);
                return new(OrderApprovalStatus.IdempotencyKeyConflict, null);
            }
        }
        var submitted = new OrderApprovalSnapshot(Guid.CreateVersion7(), context.TenantId, order.OrderId,
            context.AccountId, request.AssignedAccountId, 1, OrderApprovalState.Pending, now, now,
            OrderApprovalRules.CreateSubject(order, author), context.AccountId, authority.AuthorizationRevision,
            OrderApprovalChange.Submitted, null);
        await InsertApprovalAsync(session, submitted, ct).ConfigureAwait(false);
        if (!await InsertApprovalEventAsync(session, submitted, "submit", key, fingerprint, ct).ConfigureAwait(false))
        {
            await session.RollbackAsync(ct).ConfigureAwait(false);
            return new(OrderApprovalStatus.IdempotencyKeyConflict, null);
        }
        await session.CommitAsync(ct).ConfigureAwait(false);
        return new(OrderApprovalStatus.Applied, submitted);
    }

    public async Task<OrderApprovalResult> DecideApprovalAsync(TenantContext context,
        DecideOrderApprovalRequest request, string key, CancellationToken ct)
    {
        var authority = await ResolveApprovalAuthorityAsync(context, ct).ConfigureAwait(false);
        if (!MayExecuteApproval(authority, "approve")) return new(OrderApprovalStatus.Forbidden, null);
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Decision)) throw new OrderDraftValidationException("approval_decision_invalid", "Approval decision is invalid.");
        var reason = OrderApprovalRules.NormalizeReason(request.Reason, request.Decision == OrderApprovalDecision.Reject);
        return await ChangeApprovalAsync(context, request.RequestId, request.ExpectedRequestRevision,
            request.Decision == OrderApprovalDecision.Approve ? "approve" : "reject", null,
            reason.Length == 0 ? null : reason, key, ct).ConfigureAwait(false);
    }

    public async Task<OrderApprovalResult> ReassignApprovalAsync(TenantContext context,
        ReassignOrderApprovalRequest request, string key, CancellationToken ct)
    {
        var authority = await ResolveApprovalAuthorityAsync(context, ct).ConfigureAwait(false);
        if (!MayExecuteApproval(authority, "reassign")) return new(OrderApprovalStatus.Forbidden, null);
        ArgumentNullException.ThrowIfNull(request);
        if (request.AssignedAccountId == Guid.Empty) throw new OrderDraftValidationException("approval_assignment_invalid", "Approval assignment is invalid.");
        return await ChangeApprovalAsync(context, request.RequestId, request.ExpectedRequestRevision,
            "reassign", request.AssignedAccountId, OrderApprovalRules.NormalizeReason(request.Reason, true), key, ct).ConfigureAwait(false);
    }

    public async Task<OrderApprovalResult> CancelApprovalAsync(TenantContext context,
        CancelOrderApprovalRequest request, string key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operation = request.Recover ? "recover-cancel" : "cancel";
        var authority = await ResolveApprovalAuthorityAsync(context, ct).ConfigureAwait(false);
        if (!MayExecuteApproval(authority, operation)) return new(OrderApprovalStatus.Forbidden, null);
        return await ChangeApprovalAsync(context, request.RequestId, request.ExpectedRequestRevision,
            operation, null, OrderApprovalRules.NormalizeReason(request.Reason, true), key, ct).ConfigureAwait(false);
    }

    private async Task<OrderApprovalResult> ChangeApprovalAsync(TenantContext context, Guid requestId,
        long expectedRevision, string operation, Guid? assignedId, string? reason, string key, CancellationToken ct)
    {
        OrderApprovalRules.ValidateIdentity(requestId, expectedRevision);
        key = OrderApprovalRules.NormalizeKey(key);
        var fingerprint = OrderApprovalRules.Fingerprint(new { requestId, expectedRevision, operation, assignedId, reason });
        await using var session = await OrderTenantDbSession.OpenAsync(dataSource, context.TenantId, ct).ConfigureAwait(false);
        var current = await ReadApprovalAsync(session, OrderSql.FindApproval, context.TenantId,
            "request_id", requestId, ct).ConfigureAwait(false);
        if (current is null) return new(OrderApprovalStatus.NotFound, null);
        await LockApprovalOrderAsync(session, context.TenantId, current.OrderId, ct).ConfigureAwait(false);
        var authority = await ResolveApprovalAuthorityAsync(context, ct).ConfigureAwait(false);
        if (!MayExecuteApproval(authority, operation)) return new(OrderApprovalStatus.Forbidden, null);
        current = await ReadApprovalAsync(session, OrderSql.FindApproval, context.TenantId,
            "request_id", requestId, ct).ConfigureAwait(false) ?? throw InvalidApproval();
        if ((operation is "approve" or "reject") && (context.AccountId != current.AssignedAccountId ||
            !OrderApprovalRules.IsIndependent(current, context.AccountId))) return new(OrderApprovalStatus.SelfApprovalForbidden, null);
        if (operation == "cancel" && context.AccountId != current.RequestedByAccountId)
            return new(OrderApprovalStatus.Forbidden, null);
        var replay = await FindApprovalReplayAsync(session, context, operation, key, fingerprint, ct).ConfigureAwait(false);
        if (replay is not null)
        {
            if (replay.Request is not null)
                await RequireRetainedProgramPolicyAsync(session, context.TenantId, replay.Request.Subject.Order, ct).ConfigureAwait(false);
            await session.CommitAsync(ct).ConfigureAwait(false);
            return replay;
        }
        if (current.Revision != expectedRevision) return new(OrderApprovalStatus.RevisionConflict, null);
        if (current.State != OrderApprovalState.Pending) return new(OrderApprovalStatus.TerminalRequest, null);
        var order = await FindOrderAsync(session, context.TenantId, current.OrderId, ct).ConfigureAwait(false) ?? throw InvalidApproval();
        if (operation is "approve" or "reject")
        {
            if (!await IsEligibleApproverAsync(context, context.AccountId, ct).ConfigureAwait(false))
                return new(OrderApprovalStatus.IneligibleApprover, null);
            if (order.State != OrderDraftState.Draft || !OrderApprovalRules.Matches(current.Subject, order))
                return new(OrderApprovalStatus.SubjectStale, null);
            if (await ReadApprovalRevisionAuthorAsync(session, order, ct).ConfigureAwait(false) != current.Subject.RevisionAuthorAccountId)
                throw InvalidApproval();
            if (!await ApprovalCommercialFactsCompatibleAsync(session, context, order, ct).ConfigureAwait(false))
                return new(OrderApprovalStatus.CommercialFactsConflict, null);
        }
        if (assignedId is { } target)
        {
            if (!OrderApprovalRules.IsIndependent(current, target)) return new(OrderApprovalStatus.SelfApprovalForbidden, null);
            if (!await IsEligibleApproverAsync(context, target, ct).ConfigureAwait(false))
                return new(OrderApprovalStatus.IneligibleApprover, null);
        }
        var changed = current with
        {
            Revision = current.Revision + 1,
            AssignedAccountId = assignedId ?? current.AssignedAccountId,
            State = operation switch
            {
                "approve" => OrderApprovalState.Approved,
                "reject" => OrderApprovalState.Rejected,
                "cancel" or "recover-cancel" => OrderApprovalState.Canceled,
                _ => OrderApprovalState.Pending
            },
            Change = operation switch
            {
                "approve" => OrderApprovalChange.Approved,
                "reject" => OrderApprovalChange.Rejected,
                "cancel" or "recover-cancel" => OrderApprovalChange.Canceled,
                _ => OrderApprovalChange.Reassigned
            },
            UpdatedAt = UtcNow(),
            LastActorAccountId = context.AccountId,
            AuthorizationRevision = authority.AuthorizationRevision,
            Reason = reason,
        };
        await UpdateApprovalAsync(session, changed, expectedRevision, ct).ConfigureAwait(false);
        if (!await InsertApprovalEventAsync(session, changed, operation, key, fingerprint, ct).ConfigureAwait(false))
        {
            await session.RollbackAsync(ct).ConfigureAwait(false);
            return new(OrderApprovalStatus.IdempotencyKeyConflict, null);
        }
        await session.CommitAsync(ct).ConfigureAwait(false);
        return new(OrderApprovalStatus.Applied, changed);
    }

    private async Task<OrderApprovalAuthority> ResolveApprovalAuthorityAsync(TenantContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (approvalAuthority is null) return new(0, false, false, false, false, false, false);
        var authority = await approvalAuthority.ResolveAsync(context, ct).ConfigureAwait(false);
        if (authority.AuthorizationRevision < 1) return new(0, false, false, false, false, false, false);
        return authority;
    }

    private static bool MayExecuteApproval(OrderApprovalAuthority authority, string operation) => operation switch
    {
        "submit" => authority.CanSubmit && authority.CanView && authority.CanViewOrder,
        "cancel" => authority.CanSubmit,
        "approve" or "reject" => authority.CanDecide && authority.CanView && authority.CanViewOrder,
        "reassign" or "recover-cancel" => authority.MayRecover && authority.CanViewOrder,
        _ => false,
    };

    private Task<bool> IsEligibleApproverAsync(TenantContext context, Guid account, CancellationToken ct) =>
        approvalAuthority?.IsEligibleApproverAsync(context, account, ct) ?? Task.FromResult(false);

    private static async Task LockApprovalOrderAsync(OrderTenantDbSession session, Guid tenantId, Guid orderId, CancellationToken ct)
    {
        await using var command = session.CreateCommand(OrderSql.LockOrderForCommit);
        command.Parameters.AddWithValue("tenant_id", tenantId); command.Parameters.AddWithValue("order_id", orderId);
        await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    private async Task<bool> ApprovalCommercialFactsCompatibleAsync(OrderTenantDbSession session,
        TenantContext context, OrderDraftSnapshot order, CancellationToken ct)
    {
        if (order.QuotationOrigin is null && !order.Lines.Any(line => line.CommercialFacts is not null)) return true;
        if (commercialCommitGuard is null) return false;
        if (order.QuotationOrigin is null)
        {
            await using var pin = session.CreateCommand(OrderSql.PinCommercialPublication);
            pin.Parameters.AddWithValue("tenant_id", context.TenantId);
            await pin.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        return await commercialCommitGuard.IsCompatibleAsync(context, order, ct).ConfigureAwait(false);
    }

    private static async Task<Guid> ReadApprovalRevisionAuthorAsync(OrderTenantDbSession session,
        OrderDraftSnapshot order, CancellationToken ct)
    {
        await using var command = session.CreateCommand(OrderSql.ReadApprovalRevisionAuthor);
        command.Parameters.AddWithValue("tenant_id", order.TenantId); command.Parameters.AddWithValue("order_id", order.OrderId);
        command.Parameters.AddWithValue("revision", order.Revision);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) throw InvalidApproval();
        var retained = ReadReceiptSnapshot(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(3), reader.GetString(4)), reader.GetString(5));
        var author = reader.GetGuid(2);
        // Earlier receipt writers retained sub-microsecond ticks that the relational header cannot store.
        retained = retained with { CreatedAt = DatabaseTimestamp(retained.CreatedAt) };
        if (retained.ProgramPolicy is null && order.ProgramPolicy is { RequireApprovalForProgramOrders: false })
            retained = retained with { ProgramPolicy = order.ProgramPolicy };
        if (author == Guid.Empty || !OrderApprovalRules.Matches(new(1, retained, author, string.Empty), order) ||
            retained.Revision == 1 && author != retained.CreatedByAccountId ||
            await reader.ReadAsync(ct).ConfigureAwait(false)) throw InvalidApproval();
        return author;
    }

    private static async Task InsertApprovalAsync(OrderTenantDbSession session, OrderApprovalSnapshot request, CancellationToken ct)
    {
        OrderApprovalRules.RequireValidStored(request);
        await using var command = session.CreateCommand(OrderSql.InsertApproval);
        AddApprovalMutationParameters(command, request);
        command.Parameters.AddWithValue("order_id", request.OrderId); command.Parameters.AddWithValue("requester_id", request.RequestedByAccountId);
        command.Parameters.AddWithValue("revision_author_id", request.Subject.RevisionAuthorAccountId);
        command.Parameters.AddWithValue("order_revision", request.Subject.Order.Revision);
        command.Parameters.AddWithValue("subject", JsonSerializer.Serialize(request.Subject, ApprovalJsonOptions));
        command.Parameters.AddWithValue("subject_fingerprint", request.Subject.Fingerprint);
        command.Parameters.AddWithValue("created_at", request.CreatedAt);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task UpdateApprovalAsync(OrderTenantDbSession session, OrderApprovalSnapshot request, long expectedRevision, CancellationToken ct)
    {
        OrderApprovalRules.RequireValidStored(request);
        await using var command = session.CreateCommand(OrderSql.UpdateApproval);
        AddApprovalMutationParameters(command, request);
        command.Parameters.AddWithValue("expected_revision", expectedRevision);
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1) throw InvalidApproval();
    }

    private static void AddApprovalMutationParameters(NpgsqlCommand command, OrderApprovalSnapshot request)
    {
        command.Parameters.AddWithValue("tenant_id", request.TenantId); command.Parameters.AddWithValue("request_id", request.RequestId);
        command.Parameters.AddWithValue("assigned_id", request.AssignedAccountId); command.Parameters.AddWithValue("revision", request.Revision);
        command.Parameters.AddWithValue("state", ApprovalState(request.State)); command.Parameters.AddWithValue("updated_at", request.UpdatedAt);
    }

    private static async Task<bool> InsertApprovalEventAsync(OrderTenantDbSession session, OrderApprovalSnapshot request,
        string operation, string key, string fingerprint, CancellationToken ct)
    {
        await using var command = session.CreateCommand(OrderSql.InsertApprovalEvent);
        command.Parameters.AddWithValue("tenant_id", request.TenantId); command.Parameters.AddWithValue("request_id", request.RequestId);
        command.Parameters.AddWithValue("revision", request.Revision); command.Parameters.AddWithValue("actor_id", request.LastActorAccountId);
        command.Parameters.AddWithValue("authorization_revision", request.AuthorizationRevision); command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("key", key); command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("response_json", JsonSerializer.Serialize(new ApprovalEnvelope(1, operation, request), ApprovalJsonOptions));
        command.Parameters.AddWithValue("updated_at", request.UpdatedAt);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null;
    }

    private static async Task<OrderApprovalSnapshot?> ReadApprovalAsync(OrderTenantDbSession session, string sql,
        Guid tenantId, string identityParameter, Guid identity, CancellationToken ct)
    {
        await using var command = session.CreateCommand(sql);
        command.Parameters.AddWithValue("tenant_id", tenantId); command.Parameters.AddWithValue(identityParameter, identity);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var request = ReadApprovalEnvelope(reader.GetString(reader.GetOrdinal("response_json")), tenantId);
        if (request.RequestId != reader.GetGuid(reader.GetOrdinal("request_id")) || request.OrderId != reader.GetGuid(reader.GetOrdinal("order_id")) ||
            request.RequestedByAccountId != reader.GetGuid(reader.GetOrdinal("requester_id")) || request.AssignedAccountId != reader.GetGuid(reader.GetOrdinal("assigned_id")) ||
            request.Revision != reader.GetInt64(reader.GetOrdinal("revision")) || ApprovalState(request.State) != reader.GetString(reader.GetOrdinal("state")) ||
            request.Subject.RevisionAuthorAccountId != reader.GetGuid(reader.GetOrdinal("revision_author_id")) ||
            request.Subject.Order.Revision != reader.GetInt64(reader.GetOrdinal("order_revision")) ||
            request.Subject.Fingerprint != reader.GetString(reader.GetOrdinal("subject_fingerprint")) ||
            request.CreatedAt != reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")) ||
            request.UpdatedAt != reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at"))) throw InvalidApproval();
        ValidateApprovalEventColumns(reader, request);
        var subject = JsonSerializer.Deserialize<OrderApprovalSubject>(reader.GetString(reader.GetOrdinal("subject")), ApprovalJsonOptions);
        if (subject is null || JsonSerializer.Serialize(subject, ApprovalJsonOptions) != JsonSerializer.Serialize(request.Subject, ApprovalJsonOptions))
            throw InvalidApproval();
        return request;
    }

    private static async Task<OrderApprovalResult?> FindApprovalReplayAsync(OrderTenantDbSession session, TenantContext context,
        string operation, string key, string fingerprint, CancellationToken ct)
    {
        await using var command = session.CreateCommand(OrderSql.FindApprovalReceipt);
        command.Parameters.AddWithValue("tenant_id", context.TenantId); command.Parameters.AddWithValue("actor_id", context.AccountId);
        command.Parameters.AddWithValue("operation", operation); command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        if (reader.GetString(reader.GetOrdinal("fingerprint")) != fingerprint) return new(OrderApprovalStatus.IdempotencyKeyConflict, null);
        var request = ReadApprovalEnvelope(reader.GetString(reader.GetOrdinal("response_json")), context.TenantId);
        ValidateApprovalEventColumns(reader, request);
        if (request.RequestId != reader.GetGuid(reader.GetOrdinal("request_id")) || request.Revision != reader.GetInt64(reader.GetOrdinal("request_revision")))
            throw InvalidApproval();
        return new(OrderApprovalStatus.Replayed, request);
    }

    private static void ValidateApprovalEventColumns(NpgsqlDataReader reader, OrderApprovalSnapshot request)
    {
        if (request.LastActorAccountId != reader.GetGuid(reader.GetOrdinal("actor_id")) ||
            request.AuthorizationRevision != reader.GetInt64(reader.GetOrdinal("authorization_revision")) ||
            request.UpdatedAt != reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("recorded_at")) ||
            !ApprovalOperationMatches(reader.GetString(reader.GetOrdinal("operation")), request.Change)) throw InvalidApproval();
    }

    private static OrderApprovalSnapshot ReadApprovalEnvelope(string json, Guid tenantId)
    {
        try
        {
            if (json.Length > 4 * 1024 * 1024) throw InvalidApproval();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var schema) || !schema.TryGetInt32(out var version) || version != 1 ||
                !root.TryGetProperty("operation", out var operation) || operation.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object) throw InvalidApproval();
            foreach (var property in new[] { "requestId", "tenantId", "orderId", "requestedByAccountId", "assignedAccountId", "revision", "state",
                "createdAt", "updatedAt", "subject", "lastActorAccountId", "authorizationRevision", "change", "reason" })
                if (!payload.TryGetProperty(property, out _)) throw InvalidApproval();
            var subject = payload.GetProperty("subject");
            if (subject.ValueKind != JsonValueKind.Object || !subject.TryGetProperty("schemaVersion", out _) || !subject.TryGetProperty("revisionAuthorAccountId", out _) ||
                !subject.TryGetProperty("fingerprint", out _) || !subject.TryGetProperty("order", out var order) || order.ValueKind != JsonValueKind.Object ||
                !order.TryGetProperty("programPolicy", out var policy) || policy.ValueKind != JsonValueKind.Object ||
                !policy.TryGetProperty("requireApprovalForProgramOrders", out var required) || required.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw InvalidApproval();
            foreach (var property in new[] { "orderId", "tenantId", "createdByAccountId", "summary", "currencyCode", "total", "revision", "createdAt", "lines", "state",
                "abandonedAt", "abandonedByAccountId", "customerContext", "committedAt", "committedByAccountId" })
                if (!order.TryGetProperty(property, out _)) throw InvalidApproval();
            if (!policy.TryGetProperty("profileId", out _) || !policy.TryGetProperty("policyRevisionId", out _) ||
                !policy.TryGetProperty("requireReferenceForProgramOrders", out var referenceRequired) ||
                referenceRequired.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw InvalidApproval();
            var envelope = root.Deserialize<ApprovalEnvelope>(ApprovalJsonOptions) ?? throw InvalidApproval();
            var request = envelope.Payload ?? throw InvalidApproval();
            OrderApprovalRules.RequireValidStored(request);
            if (request.TenantId != tenantId || !ApprovalOperationMatches(envelope.Operation, request.Change)) throw InvalidApproval();
            return request;
        }
        catch (JsonException) { throw InvalidApproval(); }
        catch (OrderDraftValidationException) { throw InvalidApproval(); }
    }

    private static bool ApprovalOperationMatches(string operation, OrderApprovalChange change) => (operation, change) switch
    {
        ("submit", OrderApprovalChange.Submitted) or ("approve", OrderApprovalChange.Approved) or ("reject", OrderApprovalChange.Rejected) or
        ("reassign", OrderApprovalChange.Reassigned) or ("cancel" or "recover-cancel", OrderApprovalChange.Canceled) or ("supersede", OrderApprovalChange.Superseded) => true,
        _ => false,
    };

    private static string ApprovalState(OrderApprovalState state) => state switch
    {
        OrderApprovalState.Pending => "pending",
        OrderApprovalState.Approved => "approved",
        OrderApprovalState.Rejected => "rejected",
        OrderApprovalState.Canceled => "canceled",
        OrderApprovalState.Superseded => "superseded",
        _ => throw InvalidApproval(),
    };

    private static InvalidOperationException InvalidApproval() => new("The retained Order approval evidence is invalid or unsupported.");
    private sealed record ApprovalEnvelope(int SchemaVersion, string Operation, OrderApprovalSnapshot Payload);
}
