using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.CoreApi.Authorization;
using Application.Fulfillment;
using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class FulfillmentRoutes
{
    internal const int MaximumRequestBodyBytes = 8192;
    private const string OrderPath = "/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/fulfillment";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 4 };

    internal static WebApplication MapFulfillmentRoutes(this WebApplication app)
    {
        Protect(app.MapPost(OrderPath + "/start", (Guid tenantId, Guid orderId, HttpContext http, IFulfillmentStore store) =>
            CommandAsync(tenantId, orderId, http, store, FulfillmentCommandKind.Start)), EndpointAccess.OrderFulfillmentWorkRecord)
            .Accepts<StartPayload>("application/json").Produces<FulfillmentCommandResponse>();
        Protect(app.MapPost(OrderPath + "/complete", (Guid tenantId, Guid orderId, HttpContext http, IFulfillmentStore store) =>
            CommandAsync(tenantId, orderId, http, store, FulfillmentCommandKind.Complete)), EndpointAccess.OrderFulfillmentWorkRecord)
            .Accepts<QuantityPayload>("application/json").Produces<FulfillmentCommandResponse>();
        Protect(app.MapPost(OrderPath + "/handoffs", (Guid tenantId, Guid orderId, HttpContext http, IFulfillmentStore store) =>
            CommandAsync(tenantId, orderId, http, store, FulfillmentCommandKind.Handoff)), EndpointAccess.OrderFulfillmentHandoffRecord)
            .Accepts<HandoffPayload>("application/json").Produces<FulfillmentCommandResponse>();
        Protect(app.MapPost(OrderPath + "/dispositions", (Guid tenantId, Guid orderId, HttpContext http, IFulfillmentStore store) =>
            CommandAsync(tenantId, orderId, http, store, FulfillmentCommandKind.Block)), EndpointAccess.OrderFulfillmentWorkRecord)
            .Accepts<DispositionPayload>("application/json").Produces<FulfillmentCommandResponse>();
        Protect(app.MapPost(OrderPath + "/resolve", (Guid tenantId, Guid orderId, HttpContext http, IFulfillmentStore store) =>
            CommandAsync(tenantId, orderId, http, store, FulfillmentCommandKind.Resolve)), EndpointAccess.OrderFulfillmentWorkRecord)
            .Accepts<ResolvePayload>("application/json").Produces<FulfillmentCommandResponse>();
        Protect(app.MapPost(OrderPath + "/corrections", (Guid tenantId, Guid orderId, HttpContext http, IFulfillmentStore store) =>
            CommandAsync(tenantId, orderId, http, store, FulfillmentCommandKind.Correct)), EndpointAccess.OrderFulfillmentCorrect)
            .Accepts<CorrectionPayload>("application/json").Produces<FulfillmentCommandResponse>();

        Protect(app.MapGet(OrderPath, FindAsync), EndpointAccess.OrderFulfillmentView).Produces<FulfillmentDetailResponse>();
        Protect(app.MapGet(OrderPath + "/history", HistoryAsync), EndpointAccess.OrderFulfillmentView).Produces<FulfillmentHistoryResponse>();
        Protect(app.MapGet("/api/v1/tenants/{tenantId:guid}/fulfillment", ListAsync), EndpointAccess.OrderFulfillmentView)
            .Produces<FulfillmentPageResponse>();
        return app;
    }

    private static RouteHandlerBuilder Protect(RouteHandlerBuilder route, EndpointAccess access) => route
        .WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access).WithTags("Fulfillment").RequireAuthorization()
        .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBodyBytes))
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
        .ProducesProblem(413).ProducesProblem(429).ProducesProblem(500).ProducesProblem(503).ProducesProblem(504);

    private static async Task<IResult> CommandAsync(Guid tenantId, Guid orderId, HttpContext http,
        IFulfillmentStore store, FulfillmentCommandKind command)
    {
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, http);
        if (command == FulfillmentCommandKind.Correct)
        {
            var failure = http.RequestServices.GetRequiredService<HighRiskActionAdmission>()
                .Require(http.User, HighRiskAction.CorrectOrderFulfillment);
            if (failure is not null) return failure;
        }
        if (!TenantOrderEndpoint.TryGetIdempotencyKey(http.Request.Headers, out var key))
            return Invalid("idempotency_key_invalid");

        try
        {
            key = FulfillmentRules.NormalizeIdempotencyKey(key);
            FulfillmentMutation mutation;
            long? expectedOrderRevision = null;
            if (command == FulfillmentCommandKind.Start)
            {
                var body = await ReadPayloadAsync<StartPayload>(http).ConfigureAwait(false);
                if (body.ExpectedRevision < 0 || body.ExpectedOrderRevision < 1) return Invalid("fulfillment_request_invalid");
                expectedOrderRevision = body.ExpectedOrderRevision;
                mutation = new(command, body.ExpectedRevision);
            }
            else if (command == FulfillmentCommandKind.Complete)
            {
                var body = await ReadPayloadAsync<QuantityPayload>(http).ConfigureAwait(false);
                if (body.ExpectedRevision < 1) return Invalid("fulfillment_request_invalid");
                mutation = new(command, body.ExpectedRevision, Quantity: body.Quantity);
            }
            else if (command == FulfillmentCommandKind.Handoff)
            {
                var body = await ReadPayloadAsync<HandoffPayload>(http).ConfigureAwait(false);
                if (body.ExpectedRevision < 1) return Invalid("fulfillment_request_invalid");
                mutation = new(command, body.ExpectedRevision, Quantity: body.Quantity, Recipient: body.Recipient);
            }
            else if (command == FulfillmentCommandKind.Block)
            {
                var body = await ReadPayloadAsync<DispositionPayload>(http).ConfigureAwait(false);
                if (body.ExpectedRevision < 1 || !TryEnum(body.Disposition, out FulfillmentDispositionKind disposition))
                    return Invalid("fulfillment_request_invalid");
                mutation = new(command, body.ExpectedRevision, Quantity: body.Quantity, Reason: body.Reason, Disposition: disposition);
            }
            else if (command == FulfillmentCommandKind.Resolve)
            {
                var body = await ReadPayloadAsync<ResolvePayload>(http).ConfigureAwait(false);
                if (body.ExpectedRevision < 1) return Invalid("fulfillment_request_invalid");
                mutation = new(command, body.ExpectedRevision, Reason: body.Reason);
            }
            else
            {
                var body = await ReadPayloadAsync<CorrectionPayload>(http).ConfigureAwait(false);
                if (body.ExpectedRevision < 1 || !Guid.TryParseExact(body.TargetEventId, "D", out var targetEventId) || targetEventId == Guid.Empty)
                    return Invalid("fulfillment_request_invalid");
                FulfillmentEventFacts? replacement = null;
                if (body.Replacement is { } facts)
                {
                    if (!TryEnum(facts.Kind, out FulfillmentEventKind kind)) return Invalid("fulfillment_request_invalid");
                    FulfillmentDispositionKind? disposition = null;
                    if (facts.Disposition is not null)
                    {
                        if (!TryEnum(facts.Disposition, out FulfillmentDispositionKind parsed)) return Invalid("fulfillment_request_invalid");
                        disposition = parsed;
                    }
                    replacement = new(kind, facts.Quantity, facts.Recipient, disposition);
                }
                mutation = new(command, body.ExpectedRevision, Reason: body.Reason, TargetEventId: targetEventId, Replacement: replacement);
            }

            var result = await store.ExecuteAsync(context, orderId, mutation, expectedOrderRevision, key, http.RequestAborted)
                .ConfigureAwait(false);
            if (result.Status == FulfillmentStoreStatus.TransitionRejected && result.Rejection is
                FulfillmentMutationStatus.InputInvalid or FulfillmentMutationStatus.QuantityInvalid or FulfillmentMutationStatus.TextInvalid)
                return Invalid(RejectionCode(result.Rejection));
            if (result.Status is not (FulfillmentStoreStatus.Applied or FulfillmentStoreStatus.Replayed))
                return Outcome(result.Status, result.Rejection);
            var work = result.Work ?? throw new InvalidOperationException("Fulfillment command returned no retained work projection.");
            if (work.TenantId != tenantId || work.OrderId != orderId || work.Source.TenantId != tenantId ||
                work.Source.OrderId != orderId || work.WorkId == Guid.Empty)
                throw new InvalidOperationException("Fulfillment command returned another resource's projection.");
            if (result.Status == FulfillmentStoreStatus.Replayed) http.Response.Headers.Append("Idempotency-Replayed", "true");
            return Results.Ok(new FulfillmentCommandResponse(work.WorkId.ToString("D"), work.OrderId.ToString("D"),
                work.Revision, work.Stage.ToString()));
        }
        catch (FulfillmentPayloadTooLargeException)
        {
            return TooLarge();
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge();
        }
        catch (JsonException)
        {
            return Invalid("fulfillment_request_invalid");
        }
        catch (FulfillmentValidationException error)
        {
            return Invalid(error.Code);
        }
        catch (ArgumentException)
        {
            return Invalid("fulfillment_request_invalid");
        }
        catch (FulfillmentAccessDeniedException)
        {
            return Outcome(FulfillmentStoreStatus.Forbidden);
        }
        catch (AuthorizationProviderUnavailableException)
        {
            return Unavailable();
        }
    }

    private static async Task<IResult> FindAsync(Guid tenantId, Guid orderId, HttpContext http, IFulfillmentStore store)
    {
        try
        {
            var work = await store.FindAsync(TenantRequestAccess.RequireResolvedContext(tenantId, http), orderId, http.RequestAborted)
                .ConfigureAwait(false);
            if (work is null) return Outcome(FulfillmentStoreStatus.NotFound);
            if (work.TenantId != tenantId || work.OrderId != orderId || work.Source.TenantId != tenantId || work.Source.OrderId != orderId)
                throw new InvalidOperationException("Fulfillment query returned another resource.");
            return Results.Ok(Detail(work));
        }
        catch (FulfillmentAccessDeniedException) { return Outcome(FulfillmentStoreStatus.Forbidden); }
        catch (FulfillmentValidationException error) { return Invalid(error.Code); }
        catch (AuthorizationProviderUnavailableException) { return Unavailable(); }
    }

    private static async Task<IResult> HistoryAsync(Guid tenantId, Guid orderId, HttpContext http, IFulfillmentStore store)
    {
        if (!TryQuery(http.Request.Query, ["limit", "beforeRevision"], 10, 10, out var limit)) return Invalid("fulfillment_page_invalid");
        long? before = null;
        if (http.Request.Query.TryGetValue("beforeRevision", out var rawBefore))
        {
            if (!long.TryParse(rawBefore, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
                return Invalid("fulfillment_page_invalid");
            before = parsed;
        }
        try
        {
            var page = await store.HistoryAsync(TenantRequestAccess.RequireResolvedContext(tenantId, http), orderId,
                new(limit, before), http.RequestAborted).ConfigureAwait(false);
            if (page is null) return Outcome(FulfillmentStoreStatus.NotFound);
            if (page.Items.Count > limit) throw new InvalidOperationException("Fulfillment history exceeded its requested bound.");
            return Results.Ok(new FulfillmentHistoryResponse(page.ObservedRevision, page.Items.Select(item => Event(item.Event, item.IsActive)).ToArray(),
                page.NextRevision));
        }
        catch (FulfillmentAccessDeniedException) { return Outcome(FulfillmentStoreStatus.Forbidden); }
        catch (FulfillmentValidationException error) { return Invalid(error.Code); }
        catch (AuthorizationProviderUnavailableException) { return Unavailable(); }
    }

    private static async Task<IResult> ListAsync(Guid tenantId, HttpContext http, IFulfillmentStore store)
    {
        if (!TryQuery(http.Request.Query, ["limit", "after"], 50, 25, out var limit)) return Invalid("fulfillment_page_invalid");
        FulfillmentCursor? after = null;
        if (http.Request.Query.TryGetValue("after", out var rawAfter) && !TryCursor(rawAfter, tenantId, out after))
            return Invalid("fulfillment_cursor_invalid");
        try
        {
            var page = await store.ListAsync(TenantRequestAccess.RequireResolvedContext(tenantId, http), new(limit, after), http.RequestAborted)
                .ConfigureAwait(false);
            if (page.Items.Count > limit) throw new InvalidOperationException("Fulfillment page exceeded its requested bound.");
            return Results.Ok(new FulfillmentPageResponse(page.Items.Select(Item).ToArray(),
                page.NextCursor is null ? null : EncodeCursor(tenantId, page.NextCursor)));
        }
        catch (FulfillmentAccessDeniedException) { return Outcome(FulfillmentStoreStatus.Forbidden); }
        catch (FulfillmentValidationException error) { return Invalid(error.Code); }
        catch (AuthorizationProviderUnavailableException) { return Unavailable(); }
    }

    private static FulfillmentDetailResponse Detail(FulfillmentSnapshot work) => new(
        work.WorkId.ToString("D"), work.OrderId.ToString("D"), work.Revision, work.Stage.ToString(), work.Started,
        work.Source.OrderedQuantity, work.CompletedQuantity, work.HandedOffQuantity, work.Source.UnitCode, work.Source.UnitPrecision,
        work.ActiveDispositionId?.ToString("D"), work.ActiveDispositionKind?.ToString(), work.CreatedAt, work.UpdatedAt);

    private static FulfillmentHistoryEventResponse Event(FulfillmentEvent entry, bool isActive) => new(
        entry.EventId.ToString("D"), entry.Revision, entry.ActorAccountId.ToString("D"), entry.RecordedAt,
        entry.AuthorizationRevision, entry.Kind.ToString(), entry.Quantity, entry.Recipient, entry.Reason,
        entry.Disposition?.ToString(), entry.TargetEventId?.ToString("D"),
        entry.Replacement is null ? null : new FulfillmentReplacementResponse(entry.Replacement.Kind.ToString(), entry.Replacement.Quantity,
            entry.Replacement.Recipient, entry.Replacement.Disposition?.ToString()), isActive);

    private static FulfillmentListItemResponse Item(FulfillmentListItem item) => new(
        item.WorkId.ToString("D"), item.OrderId.ToString("D"), item.Revision, item.Stage.ToString(),
        item.OrderedQuantity, item.CompletedQuantity, item.HandedOffQuantity, item.UnitCode, item.CreatedAt);

    private static async Task<T> ReadPayloadAsync<T>(HttpContext http)
    {
        if (!http.Request.HasJsonContentType()) throw new JsonException();
        if (http.Request.ContentLength > MaximumRequestBodyBytes) throw new FulfillmentPayloadTooLargeException();
        var buffer = new byte[MaximumRequestBodyBytes + 1];
        var count = await http.Request.Body.ReadAtLeastAsync(buffer, buffer.Length, false, http.RequestAborted).ConfigureAwait(false);
        if (count > MaximumRequestBodyBytes) throw new FulfillmentPayloadTooLargeException();
        using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 4 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        ValidateUniquePropertyNames(document.RootElement);
        return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, count), JsonOptions) ?? throw new JsonException();
    }

    private static void ValidateUniquePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException();
                ValidateUniquePropertyNames(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) ValidateUniquePropertyNames(item);
        }
    }

    private static bool TryQuery(IQueryCollection query, string[] names, int maximum, int defaultLimit, out int limit)
    {
        limit = defaultLimit;
        if (query.Any(pair => pair.Value.Count != 1 || !names.Contains(pair.Key, StringComparer.Ordinal))) return false;
        return !query.TryGetValue("limit", out var raw) || int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit >= 1 && limit <= maximum;
    }

    private static string EncodeCursor(Guid tenantId, FulfillmentCursor cursor) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        string.Create(CultureInfo.InvariantCulture, $"{tenantId:N}:{cursor.CreatedAt.UtcTicks}:{cursor.OrderId:N}")))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryCursor(string? raw, Guid tenantId, out FulfillmentCursor? cursor)
    {
        cursor = null;
        if (raw is null || raw.Length is < 1 or > 160 || raw.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
            return false;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(raw.Replace('-', '+').Replace('_', '/')
                .PadRight((raw.Length + 3) / 4 * 4, '='))).Split(':');
            if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var cursorTenant) || cursorTenant != tenantId ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParseExact(parts[2], "N", out var orderId) || orderId == Guid.Empty) return false;
            cursor = new(new DateTimeOffset(ticks, TimeSpan.Zero), orderId);
            return true;
        }
        catch (Exception error) when (error is FormatException or ArgumentOutOfRangeException) { return false; }
    }

    private static bool TryEnum<T>(string? value, out T parsed) where T : struct, Enum =>
        Enum.TryParse(value, false, out parsed) && Enum.IsDefined(parsed) && string.Equals(Enum.GetName(parsed), value, StringComparison.Ordinal);

    private static IResult Invalid(string code) => Results.Problem(statusCode: 400, title: "The fulfillment request is invalid.",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private static IResult TooLarge() => Results.Problem(statusCode: 413, title: "Fulfillment request exceeds the supported size.",
        extensions: new Dictionary<string, object?> { ["code"] = "request_too_large" });

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Fulfillment authority is temporarily unavailable.",
        extensions: new Dictionary<string, object?> { ["code"] = "authorization_unavailable" });

    private static IResult Outcome(FulfillmentStoreStatus status, FulfillmentMutationStatus? rejection = null) => Results.Problem(
        statusCode: status switch
        {
            FulfillmentStoreStatus.Forbidden => 403,
            FulfillmentStoreStatus.NotFound => 404,
            FulfillmentStoreStatus.TransitionRejected when rejection == FulfillmentMutationStatus.InvalidProjection => 500,
            _ => 409,
        },
        title: "The fulfillment operation cannot continue in its current state.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = status switch
            {
                FulfillmentStoreStatus.Forbidden => "fulfillment_denied",
                FulfillmentStoreStatus.NotFound => "fulfillment_not_found",
                FulfillmentStoreStatus.IdempotencyConflict => "idempotency_key_conflict",
                FulfillmentStoreStatus.OrderRevisionConflict => "order_revision_conflict",
                FulfillmentStoreStatus.TransitionRejected => RejectionCode(rejection),
                _ => "fulfillment_conflict",
            }
        });

    private static string RejectionCode(FulfillmentMutationStatus? status) => status switch
    {
        FulfillmentMutationStatus.RevisionConflict => "fulfillment_revision_conflict",
        FulfillmentMutationStatus.AlreadyStarted => "fulfillment_already_started",
        FulfillmentMutationStatus.NotStarted => "fulfillment_not_started",
        FulfillmentMutationStatus.DispositionActive => "fulfillment_disposition_active",
        FulfillmentMutationStatus.DispositionMissing => "fulfillment_disposition_missing",
        FulfillmentMutationStatus.QuantityInvalid => "fulfillment_quantity_invalid",
        FulfillmentMutationStatus.TextInvalid => "fulfillment_text_invalid",
        FulfillmentMutationStatus.CorrectionTargetInvalid => "fulfillment_correction_target_invalid",
        FulfillmentMutationStatus.CorrectionHasDependents => "fulfillment_correction_has_dependents",
        FulfillmentMutationStatus.SourceIneligible => "fulfillment_source_ineligible",
        FulfillmentMutationStatus.InvalidProjection => "fulfillment_projection_invalid",
        FulfillmentMutationStatus.InputInvalid => "fulfillment_request_invalid",
        _ => "fulfillment_conflict",
    };

    private sealed class FulfillmentPayloadTooLargeException : Exception { }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record StartPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] long ExpectedOrderRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record QuantityPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] decimal Quantity);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record HandoffPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] decimal Quantity,
    [property: JsonRequired] string Recipient);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DispositionPayload([property: JsonRequired] long ExpectedRevision,
    [property: JsonRequired] string Disposition, [property: JsonRequired] string Reason, decimal? Quantity = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ResolvePayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CorrectionPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] string TargetEventId,
    [property: JsonRequired] string Reason, FulfillmentReplacementPayload? Replacement = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record FulfillmentReplacementPayload([property: JsonRequired] string Kind, decimal? Quantity = null,
    string? Recipient = null, string? Disposition = null);

internal sealed record FulfillmentCommandResponse(string WorkId, string OrderId, long Revision, string Stage);
internal sealed record FulfillmentDetailResponse(string WorkId, string OrderId, long Revision, string Stage, bool Started,
    decimal OrderedQuantity, decimal CompletedQuantity, decimal HandedOffQuantity, string UnitCode, int UnitPrecision,
    string? ActiveDispositionId, string? ActiveDispositionKind, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
internal sealed record FulfillmentReplacementResponse(string Kind, decimal? Quantity, string? Recipient, string? Disposition);
internal sealed record FulfillmentHistoryEventResponse(string EventId, long Revision, string ActorAccountId, DateTimeOffset RecordedAt,
    long AuthorizationRevision, string Kind, decimal? Quantity, string? Recipient, string? Reason, string? Disposition,
    string? TargetEventId, FulfillmentReplacementResponse? Replacement, bool IsActive);
internal sealed record FulfillmentHistoryResponse(long ObservedRevision, IReadOnlyList<FulfillmentHistoryEventResponse> Items, long? NextRevision);
internal sealed record FulfillmentListItemResponse(string WorkId, string OrderId, long Revision, string Stage,
    decimal OrderedQuantity, decimal CompletedQuantity, decimal HandedOffQuantity, string UnitCode, DateTimeOffset CreatedAt);
internal sealed record FulfillmentPageResponse(IReadOnlyList<FulfillmentListItemResponse> Items, string? NextCursor);
