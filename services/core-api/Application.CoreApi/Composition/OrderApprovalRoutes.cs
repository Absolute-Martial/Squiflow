using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.CoreApi.Authorization;
using Application.Orders;
using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class OrderApprovalRoutes
{
    internal const int MaximumRequestBodyBytes = 8192;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 3 };
    private const string Requests = "/api/v1/tenants/{tenantId:guid}/order-approvals";

    internal static WebApplication MapOrderApprovalRoutes(this WebApplication app)
    {
        Protect(app.MapPost("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/approvals",
            (Guid tenantId, Guid orderId, HttpContext http, IOrderApprovalStore store) =>
                CommandAsync(tenantId, orderId, http, store, ApprovalCommand.Submit)), EndpointAccess.OrderApprovalSubmit)
            .Accepts<SubmitApprovalPayload>("application/json").Produces<OrderApprovalCommandResponse>();

        Protect(app.MapGet(Requests, ListAsync), EndpointAccess.OrderApprovalView).Produces<OrderApprovalPageResponse>();
        Protect(app.MapGet(Requests + "/{requestId:guid}", FindAsync), EndpointAccess.OrderApprovalView).Produces<OrderApprovalDetailResponse>();
        Protect(app.MapGet(Requests + "/{requestId:guid}/history", HistoryAsync), EndpointAccess.OrderApprovalView).Produces<OrderApprovalHistoryResponse>();
        foreach (var (name, command, access) in new[]
        {
            ("approve", ApprovalCommand.Approve, EndpointAccess.OrderApprovalDecide),
            ("reject", ApprovalCommand.Reject, EndpointAccess.OrderApprovalDecide),
            ("reassign", ApprovalCommand.Reassign, EndpointAccess.OrderApprovalRecover),
            ("cancel", ApprovalCommand.Cancel, EndpointAccess.OrderApprovalCancel),
            ("recover-cancel", ApprovalCommand.RecoverCancel, EndpointAccess.OrderApprovalRecover),
        })
        {
            var route = Protect(app.MapPost(Requests + "/{requestId:guid}/" + name,
                (Guid tenantId, Guid requestId, HttpContext http, IOrderApprovalStore store) =>
                    CommandAsync(tenantId, requestId, http, store, command)), access)
                .Produces<OrderApprovalCommandResponse>();
            if (command == ApprovalCommand.Reassign) route.Accepts<ReassignApprovalPayload>("application/json");
            else if (command is ApprovalCommand.Cancel or ApprovalCommand.RecoverCancel) route.Accepts<CancelApprovalPayload>("application/json");
            else route.Accepts<DecideApprovalPayload>("application/json");
        }
        return app;
    }

    private static RouteHandlerBuilder Protect(RouteHandlerBuilder route, EndpointAccess access) => route
        .WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access)
        .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBodyBytes)).WithTags("Orders").RequireAuthorization()
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
        .ProducesProblem(409).ProducesProblem(413).ProducesProblem(429).ProducesProblem(500)
        .ProducesProblem(503).ProducesProblem(504);

    private static async Task<IResult> CommandAsync(Guid tenantId, Guid id, HttpContext http,
        IOrderApprovalStore store, ApprovalCommand command)
    {
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, http);
        if (command is ApprovalCommand.Reassign or ApprovalCommand.RecoverCancel)
        {
            var failure = http.RequestServices.GetRequiredService<HighRiskActionAdmission>()
                .Require(http.User, HighRiskAction.RecoverOrderApproval);
            if (failure is not null) return failure;
        }
        if (!TenantOrderEndpoint.TryGetIdempotencyKey(http.Request.Headers, out var key))
            return Invalid("idempotency_key_invalid");
        try
        {
            key = OrderApprovalRules.NormalizeKey(key!);
            OrderApprovalResult result;
            if (command == ApprovalCommand.Submit)
            {
                var body = await ReadPayloadAsync<SubmitApprovalPayload>(http).ConfigureAwait(false);
                result = await store.SubmitApprovalAsync(context,
                    new(id, body.ExpectedOrderRevision, body.AssignedAccountId, body.ExpectedPendingRequestRevision), key, http.RequestAborted).ConfigureAwait(false);
            }
            else if (command == ApprovalCommand.Reassign)
            {
                var body = await ReadPayloadAsync<ReassignApprovalPayload>(http).ConfigureAwait(false);
                result = await store.ReassignApprovalAsync(context,
                    new(id, body.ExpectedRequestRevision, body.AssignedAccountId, body.Reason), key, http.RequestAborted).ConfigureAwait(false);
            }
            else if (command is ApprovalCommand.Cancel or ApprovalCommand.RecoverCancel)
            {
                var body = await ReadPayloadAsync<CancelApprovalPayload>(http).ConfigureAwait(false);
                result = await store.CancelApprovalAsync(context,
                    new(id, body.ExpectedRequestRevision, body.Reason, command == ApprovalCommand.RecoverCancel), key, http.RequestAborted).ConfigureAwait(false);
            }
            else
            {
                var body = await ReadPayloadAsync<DecideApprovalPayload>(http).ConfigureAwait(false);
                result = await store.DecideApprovalAsync(context,
                    new(id, body.ExpectedRequestRevision, command == ApprovalCommand.Approve ? OrderApprovalDecision.Approve : OrderApprovalDecision.Reject, body.Reason), key, http.RequestAborted).ConfigureAwait(false);
            }
            if (result.Status is not (OrderApprovalStatus.Applied or OrderApprovalStatus.Replayed)) return Outcome(result.Status);
            var request = result.Request ?? throw new InvalidOperationException("Approval command returned no retained request.");
            if (request.TenantId != tenantId || (command == ApprovalCommand.Submit ? request.OrderId != id : request.RequestId != id))
                throw new InvalidOperationException("Approval command returned another resource's evidence.");
            if (result.Status == OrderApprovalStatus.Replayed) http.Response.Headers.Append("Idempotency-Replayed", "true");
            return Results.Ok(new OrderApprovalCommandResponse(request.RequestId, request.OrderId, request.Subject.Order.Revision, request.Revision, request.State));
        }
        catch (ApprovalPayloadTooLargeException) { return Results.Problem(statusCode: 413, title: "Approval request exceeds the supported size.", extensions: new Dictionary<string, object?> { ["code"] = "request_too_large" }); }
        catch (BadHttpRequestException error) when (error.StatusCode == 413) { return Results.Problem(statusCode: 413, title: "Approval request exceeds the supported size.", extensions: new Dictionary<string, object?> { ["code"] = "request_too_large" }); }
        catch (JsonException) { return Invalid("approval_request_invalid"); }
        catch (OrderDraftValidationException error) { return Invalid(error.Code); }
        catch (ArgumentException) { return Invalid("approval_request_invalid"); }
        catch (OrderApprovalAccessDeniedException) { return Outcome(OrderApprovalStatus.Forbidden); }
        catch (AuthorizationProviderUnavailableException) { return Unavailable(); }
    }

    private static async Task<IResult> ListAsync(Guid tenantId, HttpContext http, IOrderApprovalStore store)
    {
        if (!TryQuery(http.Request.Query, ["limit", "after", "recovery"], out var limit)) return Invalid("approval_page_invalid");
        var recovery = false;
        if (http.Request.Query.TryGetValue("recovery", out var rawRecovery) && !bool.TryParse(rawRecovery, out recovery)) return Invalid("approval_page_invalid");
        OrderApprovalCursor? after = null;
        if (http.Request.Query.TryGetValue("after", out var rawAfter) && !TryCursor(rawAfter, tenantId, out after)) return Invalid("approval_cursor_invalid");
        try
        {
            var page = await store.ListApprovalsAsync(TenantRequestAccess.RequireResolvedContext(tenantId, http), new(limit, after, recovery), http.RequestAborted).ConfigureAwait(false);
            return Results.Ok(new OrderApprovalPageResponse(page.Items, page.NextCursor is null ? null : EncodeCursor(tenantId, page.NextCursor)));
        }
        catch (OrderApprovalAccessDeniedException) { return Outcome(OrderApprovalStatus.Forbidden); }
        catch (OrderDraftValidationException error) { return Invalid(error.Code); }
        catch (AuthorizationProviderUnavailableException) { return Unavailable(); }
    }

    private static async Task<IResult> FindAsync(Guid tenantId, Guid requestId, HttpContext http, IOrderApprovalStore store)
    {
        try
        {
            var request = await store.FindApprovalAsync(TenantRequestAccess.RequireResolvedContext(tenantId, http), requestId, http.RequestAborted).ConfigureAwait(false);
            if (request is null) return Outcome(OrderApprovalStatus.NotFound);
            if (request.TenantId != tenantId || request.RequestId != requestId) throw new InvalidOperationException("Approval query returned another resource.");
            return Results.Ok(Detail(request));
        }
        catch (OrderApprovalAccessDeniedException) { return Outcome(OrderApprovalStatus.Forbidden); }
        catch (OrderDraftValidationException error) { return Invalid(error.Code); }
        catch (AuthorizationProviderUnavailableException) { return Unavailable(); }
    }

    private static async Task<IResult> HistoryAsync(Guid tenantId, Guid requestId, HttpContext http, IOrderApprovalStore store)
    {
        if (!TryQuery(http.Request.Query, ["limit", "beforeRevision"], out var limit, 10, 10)) return Invalid("approval_page_invalid");
        long? before = null;
        if (http.Request.Query.TryGetValue("beforeRevision", out var raw))
        {
            if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1) return Invalid("approval_page_invalid");
            before = value;
        }
        try
        {
            var page = await store.ListApprovalHistoryAsync(TenantRequestAccess.RequireResolvedContext(tenantId, http), requestId,
                new(limit, before), http.RequestAborted).ConfigureAwait(false);
            return page is null ? Outcome(OrderApprovalStatus.NotFound) : Results.Ok(new OrderApprovalHistoryResponse(page.CurrentRevision, page.Items.Select(Detail).ToArray(), page.NextRevision));
        }
        catch (OrderApprovalAccessDeniedException) { return Outcome(OrderApprovalStatus.Forbidden); }
        catch (OrderDraftValidationException error) { return Invalid(error.Code); }
        catch (AuthorizationProviderUnavailableException) { return Unavailable(); }
    }

    private static OrderApprovalDetailResponse Detail(OrderApprovalSnapshot request) => new(
        request.RequestId, request.OrderId, request.RequestedByAccountId, request.AssignedAccountId, request.Revision,
        request.State, request.CreatedAt, request.UpdatedAt,
        new(request.Subject.SchemaVersion, TenantOrderEndpoint.ToResponse(request.Subject.Order), request.Subject.RevisionAuthorAccountId, request.Subject.Fingerprint),
        request.LastActorAccountId, request.AuthorizationRevision, request.Change, request.Reason);

    private static async Task<T> ReadPayloadAsync<T>(HttpContext http)
    {
        if (!http.Request.HasJsonContentType()) throw new JsonException();
        if (http.Request.ContentLength > MaximumRequestBodyBytes) throw new ApprovalPayloadTooLargeException();
        var buffer = new byte[MaximumRequestBodyBytes + 1];
        var count = await http.Request.Body.ReadAtLeastAsync(buffer, buffer.Length, false, http.RequestAborted).ConfigureAwait(false);
        if (count > MaximumRequestBodyBytes) throw new ApprovalPayloadTooLargeException();
        using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 3 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
            if (!names.Add(property.Name)) throw new JsonException();
        return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, count), JsonOptions) ?? throw new JsonException();
    }

    private static bool TryQuery(IQueryCollection query, string[] names, out int limit, int maximumLimit = 50, int defaultLimit = 25)
    {
        limit = defaultLimit;
        if (query.Any(pair => pair.Value.Count != 1 || !names.Contains(pair.Key, StringComparer.Ordinal))) return false;
        return !query.TryGetValue("limit", out var raw) || int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit >= 1 && limit <= maximumLimit;
    }

    private static string EncodeCursor(Guid tenant, OrderApprovalCursor cursor) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        string.Create(CultureInfo.InvariantCulture, $"{tenant:N}:{cursor.CreatedAt.UtcTicks}:{cursor.RequestId:N}"))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryCursor(string? raw, Guid tenant, out OrderApprovalCursor? cursor)
    {
        cursor = null;
        if (raw is null || raw.Length is < 1 or > 160 || raw.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) return false;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(raw.Replace('-', '+').Replace('_', '/').PadRight((raw.Length + 3) / 4 * 4, '='))).Split(':');
            if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var cursorTenant) || cursorTenant != tenant ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParseExact(parts[2], "N", out var requestId) || requestId == Guid.Empty) return false;
            cursor = new(new DateTimeOffset(ticks, TimeSpan.Zero), requestId);
            return true;
        }
        catch (Exception error) when (error is FormatException or ArgumentOutOfRangeException) { return false; }
    }

    private static IResult Invalid(string code) => Results.Problem(statusCode: 400, title: "The approval request is invalid.", extensions: new Dictionary<string, object?> { ["code"] = code });
    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Approval authority is temporarily unavailable.", extensions: new Dictionary<string, object?> { ["code"] = "authorization_unavailable" });
    private static IResult Outcome(OrderApprovalStatus status) => Results.Problem(
        statusCode: status == OrderApprovalStatus.Forbidden ? 403 : status == OrderApprovalStatus.NotFound ? 404 : status == OrderApprovalStatus.ProfileUnavailable ? 503 : 409,
        title: "The approval operation cannot continue in its current state.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = status switch
            {
                OrderApprovalStatus.Forbidden => "order_approval_denied",
                OrderApprovalStatus.NotFound => "order_approval_not_found",
                OrderApprovalStatus.RevisionConflict => "approval_revision_conflict",
                OrderApprovalStatus.IdempotencyKeyConflict => "idempotency_key_conflict",
                OrderApprovalStatus.IneligibleApprover => "approval_assignee_unavailable",
                OrderApprovalStatus.SelfApprovalForbidden => "approval_self_approval_forbidden",
                OrderApprovalStatus.SubjectStale => "approval_subject_stale",
                OrderApprovalStatus.PendingRequestExists => "approval_pending_request_exists",
                OrderApprovalStatus.TerminalRequest => "approval_request_terminal",
                OrderApprovalStatus.OrderNotDraft => "order_not_draft",
                OrderApprovalStatus.ProfileUnavailable => "order_profile_unavailable",
                OrderApprovalStatus.ProgramReferenceRequired => "order_program_reference_required",
                OrderApprovalStatus.CommercialFactsConflict => "order_commercial_facts_conflict",
                _ => throw new InvalidOperationException("Unsupported approval operation outcome."),
            }
        });

    private enum ApprovalCommand { Submit, Approve, Reject, Reassign, Cancel, RecoverCancel }
    private sealed class ApprovalPayloadTooLargeException : Exception { }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SubmitApprovalPayload([property: JsonRequired] long ExpectedOrderRevision,
    [property: JsonRequired] Guid AssignedAccountId, long? ExpectedPendingRequestRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DecideApprovalPayload([property: JsonRequired] long ExpectedRequestRevision, string? Reason = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ReassignApprovalPayload([property: JsonRequired] long ExpectedRequestRevision,
    [property: JsonRequired] Guid AssignedAccountId, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CancelApprovalPayload([property: JsonRequired] long ExpectedRequestRevision, [property: JsonRequired] string Reason);
internal sealed record OrderApprovalCommandResponse(Guid RequestId, Guid OrderId, long OrderRevision, long RequestRevision, OrderApprovalState State);
internal sealed record OrderApprovalPageResponse(IReadOnlyList<OrderApprovalListItem> Items, string? NextCursor);

internal sealed record OrderApprovalSubjectResponse(int SchemaVersion, OrderDraftResponse Order, Guid RevisionAuthorAccountId, string Fingerprint);
internal sealed record OrderApprovalDetailResponse(Guid RequestId, Guid OrderId, Guid RequestedByAccountId, Guid AssignedAccountId,
    long Revision, OrderApprovalState State, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, OrderApprovalSubjectResponse Subject,
    Guid LastActorAccountId, long AuthorizationRevision, OrderApprovalChange Change, string? Reason);
internal sealed record OrderApprovalHistoryResponse(long CurrentRevision, IReadOnlyList<OrderApprovalDetailResponse> Items, long? NextRevision);
