using System.Security.Claims;
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
                line.LineTotal)).ToArray());

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
    IReadOnlyList<OrderDraftLineResponse> Lines);

internal sealed record OrderDraftLineResponse(
    int Position,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice,
    decimal LineTotal);
