using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Primitives;
using Application.CoreApi.Authorization;
using Application.IdentityAccess;
using Application.Orders;
using Application.Tenancy;

namespace Application.CoreApi;

internal static class TenantOrderEndpoint
{
    internal const long MaximumCreateRequestBodyBytes = 64 * 1024;
    internal const long MaximumAbandonRequestBodyBytes = 1024;
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 50;

    internal static async Task<IResult> CreateAsync(
        Guid tenantId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        IAuthorizationService authorization,
        CreateOrderDraft createOrderDraft,
        CancellationToken cancellationToken)
    {
        if (httpContext.Request.ContentLength is > MaximumCreateRequestBodyBytes)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Order request is too large.",
                detail: "The order request exceeds the supported request size.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "request_too_large",
                });
        }

        var access = await TenantRequestAccess.ResolveAsync(
            tenantId,
            httpContext,
            principal,
            resolveAccount,
            resolveTenantContext,
            cancellationToken);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var authorizationFailure = await AuthorizeAsync(
            principal,
            access.TenantContext!,
            authorization,
            CreateOrderRequirement.Instance,
            "The account is not permitted to create orders in this tenant.",
            cancellationToken);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        if (!TryGetIdempotencyKey(httpContext.Request.Headers, out var idempotencyKey))
        {
            return InvalidRequest(
                "idempotency_key_invalid",
                "Idempotency-Key is required and must contain one header value.");
        }

        var payloadResult = await ReadCreatePayloadAsync(httpContext.Request, cancellationToken);
        if (payloadResult.Failure is not null)
        {
            return payloadResult.Failure;
        }

        CreateOrderDraftResult result;
        try
        {
            result = await createOrderDraft.ExecuteAsync(
                access.TenantContext!,
                payloadResult.Request!,
                idempotencyKey!,
                cancellationToken);
        }
        catch (OrderDraftValidationException exception)
        {
            return InvalidRequest(exception.Code, exception.Message);
        }

        if (result.Status == CreateOrderDraftStatus.IdempotencyKeyConflict)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency key conflict.",
                detail: "The Idempotency-Key has already been used for a different order request.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "idempotency_key_conflict",
                });
        }

        var order = result.Order
            ?? throw new InvalidOperationException("A successful order command did not return an order draft.");
        httpContext.Response.Headers.CacheControl = "no-store";
        var response = ToResponse(order);

        if (result.Status == CreateOrderDraftStatus.Replayed)
        {
            httpContext.Response.Headers.Append("Idempotency-Replayed", "true");
            return TypedResults.Ok(response);
        }

        if (result.Status != CreateOrderDraftStatus.Created)
        {
            throw new InvalidOperationException("The order command returned an unsupported status.");
        }

        return TypedResults.Created(
            $"/api/v1/tenants/{tenantId:D}/orders/{order.OrderId:D}",
            response);
    }

    internal static async Task<IResult> GetAsync(
        Guid tenantId,
        Guid orderId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        IAuthorizationService authorization,
        GetOrderDraft getOrderDraft,
        CancellationToken cancellationToken)
    {
        var access = await TenantRequestAccess.ResolveAsync(
            tenantId,
            httpContext,
            principal,
            resolveAccount,
            resolveTenantContext,
            cancellationToken);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var authorizationFailure = await AuthorizeAsync(
            principal,
            access.TenantContext!,
            authorization,
            ViewOrdersRequirement.Instance,
            "The account is not permitted to view orders in this tenant.",
            cancellationToken);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        OrderDraftSnapshot? order;
        try
        {
            order = await getOrderDraft.ExecuteAsync(
                access.TenantContext!,
                orderId,
                cancellationToken);
        }
        catch (OrderDraftValidationException exception)
        {
            return InvalidRequest(exception.Code, exception.Message);
        }

        if (order is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order not found.",
                detail: "The requested order was not found in this tenant.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "order_not_found",
                });
        }

        httpContext.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(ToResponse(order));
    }

    internal static async Task<IResult> AbandonAsync(
        Guid tenantId,
        Guid orderId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        IAuthorizationService authorization,
        AbandonOrderDraft abandonOrderDraft,
        CancellationToken cancellationToken)
    {
        var access = await TenantRequestAccess.ResolveAsync(
            tenantId,
            httpContext,
            principal,
            resolveAccount,
            resolveTenantContext,
            cancellationToken);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var authorizationFailure = await AuthorizeAsync(
            principal,
            access.TenantContext!,
            authorization,
            AbandonOrderRequirement.Instance,
            "The account is not permitted to abandon orders in this tenant.",
            cancellationToken);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        if (httpContext.Request.ContentLength is > MaximumAbandonRequestBodyBytes)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Order request is too large.",
                detail: "The order request exceeds the supported request size.",
                extensions: new Dictionary<string, object?> { ["code"] = "request_too_large" });
        }

        if (!TryGetIdempotencyKey(httpContext.Request.Headers, out var idempotencyKey))
        {
            return InvalidRequest(
                "idempotency_key_invalid",
                "Idempotency-Key is required and must contain one header value.");
        }

        if (!httpContext.Request.HasJsonContentType())
        {
            return InvalidRequest("request_invalid", "The request body must use application/json.");
        }

        long expectedRevision;
        try
        {
            var body = new byte[checked((int)MaximumAbandonRequestBodyBytes + 1)];
            var received = 0;
            while (received < body.Length)
            {
                var count = await httpContext.Request.Body.ReadAsync(
                    body.AsMemory(received),
                    cancellationToken);
                if (count == 0)
                {
                    break;
                }

                received += count;
            }

            if (received > MaximumAbandonRequestBodyBytes)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status413PayloadTooLarge,
                    title: "Order request is too large.",
                    detail: "The order request exceeds the supported request size.",
                    extensions: new Dictionary<string, object?> { ["code"] = "request_too_large" });
            }

            using var payload = JsonDocument.Parse(
                body.AsMemory(0, received),
                new JsonDocumentOptions { MaxDepth = 2 });
            if (payload.RootElement.ValueKind != JsonValueKind.Object)
            {
                return InvalidRequest("expected_revision_invalid", "Expected revision must be one positive JSON integer.");
            }

            using var properties = payload.RootElement.EnumerateObject();
            if (!properties.MoveNext() ||
                !properties.Current.NameEquals("expectedRevision") ||
                properties.Current.Value.ValueKind != JsonValueKind.Number ||
                !properties.Current.Value.TryGetInt64(out expectedRevision) ||
                expectedRevision < 1 ||
                properties.MoveNext())
            {
                return InvalidRequest("expected_revision_invalid", "Expected revision must be one positive JSON integer.");
            }
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Order request is too large.",
                detail: "The order request exceeds the supported request size.",
                extensions: new Dictionary<string, object?> { ["code"] = "request_too_large" });
        }
        catch (Exception exception) when (exception is BadHttpRequestException or JsonException)
        {
            return InvalidRequest("request_invalid", "The request body must be a valid JSON abandon request.");
        }

        AbandonOrderDraftResult result;
        try
        {
            result = await abandonOrderDraft.ExecuteAsync(
                access.TenantContext!,
                new AbandonOrderDraftRequest(orderId, expectedRevision),
                idempotencyKey!,
                cancellationToken);
        }
        catch (OrderDraftValidationException exception)
        {
            return InvalidRequest(exception.Code, exception.Message);
        }

        if (result.Status == AbandonOrderDraftStatus.NotFound)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order not found.",
                detail: "The requested order was not found in this tenant.",
                extensions: new Dictionary<string, object?> { ["code"] = "order_not_found" });
        }

        if (result.Status is AbandonOrderDraftStatus.RevisionConflict or
            AbandonOrderDraftStatus.AlreadyAbandoned or
            AbandonOrderDraftStatus.IdempotencyKeyConflict)
        {
            var (code, detail) = result.Status switch
            {
                AbandonOrderDraftStatus.RevisionConflict =>
                    ("revision_conflict", "The order revision does not match the expected revision."),
                AbandonOrderDraftStatus.AlreadyAbandoned =>
                    ("order_already_abandoned", "The order draft has already been abandoned."),
                _ =>
                    ("idempotency_key_conflict", "The Idempotency-Key has already been used for a different abandon request."),
            };
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Order conflict.",
                detail: detail,
                extensions: new Dictionary<string, object?> { ["code"] = code });
        }

        if (result.Status is not (AbandonOrderDraftStatus.Abandoned or AbandonOrderDraftStatus.Replayed))
        {
            throw new InvalidOperationException("The abandon command returned an unsupported status.");
        }

        var order = result.Order
            ?? throw new InvalidOperationException("A successful abandon command did not return an order draft.");
        if (order.State != OrderDraftState.Abandoned || order.AbandonedAt is null)
        {
            throw new InvalidOperationException("A successful abandon command returned an invalid abandonment receipt.");
        }
        httpContext.Response.Headers.CacheControl = "no-store";
        if (result.Status == AbandonOrderDraftStatus.Replayed)
        {
            httpContext.Response.Headers.Append("Idempotency-Replayed", "true");
        }

        return TypedResults.Ok(new AbandonOrderDraftResponse(
            order.OrderId,
            ToWireState(order.State),
            order.Revision,
            order.AbandonedAt.Value));
    }

    internal static async Task<IResult> ListAsync(
        Guid tenantId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        IAuthorizationService authorization,
        ListOrderDrafts listOrderDrafts,
        CancellationToken cancellationToken)
    {
        var access = await TenantRequestAccess.ResolveAsync(
            tenantId,
            httpContext,
            principal,
            resolveAccount,
            resolveTenantContext,
            cancellationToken);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var authorizationFailure = await AuthorizeAsync(
            principal,
            access.TenantContext!,
            authorization,
            ViewOrdersRequirement.Instance,
            "The account is not permitted to view orders in this tenant.",
            cancellationToken);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        if (!TryReadPageRequest(
                tenantId,
                httpContext.Request.Query,
                out var pageSize,
                out var after,
                out var failure))
        {
            return failure!;
        }

        OrderDraftPage page;
        try
        {
            page = await listOrderDrafts.ExecuteAsync(
                access.TenantContext!,
                new ListOrderDraftsRequest(pageSize, after),
                cancellationToken);
        }
        catch (OrderDraftValidationException exception)
        {
            return InvalidRequest(exception.Code, exception.Message);
        }

        httpContext.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new OrderDraftPageResponse(
            page.Items.Select(item => new OrderDraftListItemResponse(
                item.OrderId,
                item.Summary,
                item.CurrencyCode,
                item.Total,
                item.Revision,
                item.CreatedAt,
                ToWireState(item.State),
                item.AbandonedAt)).ToArray(),
            page.NextCursor is null ? null : OrderDraftPageCursorCodec.Encode(tenantId, page.NextCursor)));
    }

    private static async Task<IResult?> AuthorizeAsync(
        ClaimsPrincipal principal,
        TenantContext tenantContext,
        IAuthorizationService authorization,
        IAuthorizationRequirement requirement,
        string deniedDetail,
        CancellationToken cancellationToken)
    {
        AuthorizationResult decision;
        try
        {
            decision = await authorization.AuthorizeAsync(
                principal,
                new TenantOrderResource(tenantContext, cancellationToken),
                requirement);
        }
        catch (AuthorizationProviderUnavailableException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Authorization is temporarily unavailable.",
                detail: "The request could not be authorized safely.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "authorization_unavailable",
                });
        }

        return decision.Succeeded
            ? null
            : TenantRequestAccess.Problem("tenant_permission_denied", deniedDetail);
    }

    private static bool TryGetIdempotencyKey(
        IHeaderDictionary headers,
        out string? idempotencyKey)
    {
        if (!headers.TryGetValue("Idempotency-Key", out StringValues values) ||
            values.Count != 1)
        {
            idempotencyKey = null;
            return false;
        }

        idempotencyKey = values[0];
        return true;
    }

    private static bool TryReadPageRequest(
        Guid tenantId,
        IQueryCollection query,
        out int pageSize,
        out OrderDraftPageCursor? after,
        out IResult? failure)
    {
        pageSize = DefaultPageSize;
        after = null;
        failure = null;

        if (query.TryGetValue("limit", out var limitValues))
        {
            if (limitValues.Count != 1 ||
                !int.TryParse(
                    limitValues[0],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out pageSize) ||
                pageSize is < 1 or > MaximumPageSize)
            {
                failure = InvalidRequest(
                    "page_size_invalid",
                    $"The limit query parameter must occur once and be an integer from 1 to {MaximumPageSize}.");
                return false;
            }
        }

        if (query.TryGetValue("after", out var afterValues))
        {
            if (afterValues.Count != 1 ||
                !OrderDraftPageCursorCodec.TryDecode(afterValues[0], tenantId, out after))
            {
                failure = InvalidRequest(
                    "cursor_invalid",
                    "The after query parameter must be one supported opaque cursor value.");
                return false;
            }
        }

        return true;
    }

    private static async Task<CreatePayloadResult> ReadCreatePayloadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        CreateOrderDraftPayload? payload;
        try
        {
            payload = await request.ReadFromJsonAsync<CreateOrderDraftPayload>(cancellationToken);
        }
        catch (Exception exception) when (exception is BadHttpRequestException or NotSupportedException or System.Text.Json.JsonException)
        {
            return CreatePayloadResult.Invalid(InvalidRequest(
                "request_invalid",
                "The request body must be a valid JSON order draft."));
        }

        if (payload is null || payload.Lines is null || payload.Lines.Any(line => line is null))
        {
            return CreatePayloadResult.Invalid(InvalidRequest(
                "request_invalid",
                "The request body must contain an order draft and non-null lines."));
        }

        var lines = payload.Lines
            .Select(line => new OrderDraftLineInput(
                line!.Description ?? string.Empty,
                line.Quantity,
                line.UnitCode ?? string.Empty,
                line.UnitPrice))
            .ToArray();
        return CreatePayloadResult.Valid(new CreateOrderDraftRequest(
            payload.Summary ?? string.Empty,
            payload.CurrencyCode ?? string.Empty,
            lines));
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult InvalidRequest(
        string code,
        string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid order request.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });

    private static OrderDraftResponse ToResponse(OrderDraftSnapshot order) =>
        new(
            order.OrderId,
            order.Summary,
            order.CurrencyCode,
            order.Total,
            order.Revision,
            order.CreatedAt,
            order.Lines.Select(line => new OrderDraftLineResponse(
                line.Position,
                line.Description,
                line.Quantity,
                line.UnitCode,
                line.UnitPrice,
                line.LineTotal)).ToArray(),
            ToWireState(order.State),
            order.AbandonedAt);

    private static string ToWireState(OrderDraftState state) => state switch
    {
        OrderDraftState.Draft => "draft",
        OrderDraftState.Abandoned => "abandoned",
        _ => throw new InvalidOperationException("The order returned an unsupported state."),
    };

    private sealed record CreatePayloadResult(
        CreateOrderDraftRequest? Request,
        IResult? Failure)
    {
        internal static CreatePayloadResult Valid(CreateOrderDraftRequest request) => new(request, null);

        internal static CreatePayloadResult Invalid(IResult failure) => new(null, failure);
    }
}

internal sealed record CreateOrderDraftPayload(
    string? Summary,
    string? CurrencyCode,
    IReadOnlyList<CreateOrderDraftLinePayload?>? Lines);

internal sealed record AbandonOrderDraftResponse(
    Guid OrderId,
    string State,
    long Revision,
    DateTimeOffset AbandonedAt);

internal sealed record CreateOrderDraftLinePayload(
    string? Description,
    decimal Quantity,
    string? UnitCode,
    decimal UnitPrice);

internal sealed record OrderDraftResponse(
    Guid OrderId,
    string Summary,
    string CurrencyCode,
    decimal Total,
    long Revision,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderDraftLineResponse> Lines,
    string State,
    DateTimeOffset? AbandonedAt);

internal sealed record OrderDraftLineResponse(
    int Position,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice,
    decimal LineTotal);

internal sealed record OrderDraftPageResponse(
    IReadOnlyList<OrderDraftListItemResponse> Items,
    string? NextCursor);

internal sealed record OrderDraftListItemResponse(
    Guid OrderId,
    string Summary,
    string CurrencyCode,
    decimal Total,
    long Revision,
    DateTimeOffset CreatedAt,
    string State,
    DateTimeOffset? AbandonedAt);

internal static class OrderDraftPageCursorCodec
{
    private const string Version = "v1";
    internal const int MaximumEncodedLength = 128;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static string Encode(Guid tenantId, OrderDraftPageCursor cursor)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        }

        var createdAt = cursor.CreatedAt.ToUniversalTime();
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{Version}:{tenantId:N}:{createdAt.Ticks}:{cursor.OrderId:N}");
        return Convert.ToBase64String(StrictUtf8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    internal static bool TryDecode(
        string? encoded,
        Guid tenantId,
        out OrderDraftPageCursor? cursor)
    {
        cursor = null;
        if (tenantId == Guid.Empty ||
            string.IsNullOrEmpty(encoded) || encoded.Length > MaximumEncodedLength ||
            encoded.Any(character => !IsBase64UrlCharacter(character)))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            var base64 = encoded.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return false;
        }

        string payload;
        try
        {
            payload = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        var values = payload.Split(':');
        if (values.Length != 4 ||
            !string.Equals(values[0], Version, StringComparison.Ordinal) ||
            !Guid.TryParseExact(values[1], "N", out var cursorTenantId) ||
            cursorTenantId != tenantId ||
            !long.TryParse(values[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
            ticks < DateTimeOffset.MinValue.Ticks || ticks > DateTimeOffset.MaxValue.Ticks ||
            !Guid.TryParseExact(values[3], "N", out var orderId) ||
            orderId == Guid.Empty)
        {
            return false;
        }

        try
        {
            cursor = new OrderDraftPageCursor(new DateTimeOffset(ticks, TimeSpan.Zero), orderId);
            return string.Equals(Encode(tenantId, cursor), encoded, StringComparison.Ordinal);
        }
        catch (ArgumentOutOfRangeException)
        {
            cursor = null;
            return false;
        }
    }

    private static bool IsBase64UrlCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
}
