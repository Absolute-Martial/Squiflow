using System.Security.Claims;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.IdentityAccess;
using Application.Orders;
using Application.Orders.Pricing;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi;

internal static class TenantOrderPricePreviewEndpoint
{
    internal const int MaximumRequestBodyBytes = 64 * 1024;

    internal static async Task<IResult> PreviewAsync(
        Guid tenantId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var access = await TenantRequestAccess.ResolveAsync(
            tenantId, httpContext, principal, resolveAccount, resolveTenantContext, cancellationToken);
        if (access.Failure is not null)
            return access.Failure;

        AuthorizationResult decision;
        try
        {
            decision = await authorization.AuthorizeAsync(principal,
                new TenantOrderResource(access.TenantContext!, cancellationToken), CreateOrderRequirement.Instance);
        }
        catch (AuthorizationProviderUnavailableException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Authorization is temporarily unavailable.",
                detail: "The request could not be authorized safely.",
                extensions: new Dictionary<string, object?> { ["code"] = "authorization_unavailable" });
        }

        if (!decision.Succeeded)
            return TenantRequestAccess.Problem("tenant_permission_denied",
                "The account is not permitted to preview order prices in this tenant.");

        var pricingFailure = await TenantOrderEndpoint.AuthorizeAsync(
            principal, access.TenantContext!, authorization, ApplyManualPriceRequirement.Instance,
            "The account is not permitted to enter manual order prices in this tenant.", cancellationToken);
        if (pricingFailure is not null)
            return pricingFailure;

        if (httpContext.Request.ContentLength is > MaximumRequestBodyBytes)
            return TooLarge();
        if (!httpContext.Request.HasJsonContentType())
            return Invalid("request_invalid", "The request body must be a valid UTF-8 JSON price preview.");

        OrderDraftPricePreviewPayload? payload;
        try
        {
            // Bound the actual stream too: chunked requests need not declare Content-Length.
            var body = new byte[MaximumRequestBodyBytes + 1];
            var length = await httpContext.Request.Body.ReadAtLeastAsync(
                body, body.Length, throwOnEndOfStream: false, cancellationToken);
            if (length > MaximumRequestBodyBytes)
                return TooLarge();
            payload = JsonSerializer.Deserialize<OrderDraftPricePreviewPayload>(body.AsSpan(0, length), JsonSerializerOptions.Web);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge();
        }
        catch (Exception exception) when (exception is BadHttpRequestException or JsonException)
        {
            return Invalid("request_invalid", "The request body must be a valid UTF-8 JSON price preview.");
        }

        if (payload?.Lines is null || payload.Lines.Any(line => line is null))
            return Invalid("request_invalid", "The request body must contain a price preview and non-null lines.");

        OrderDraftPricePreview preview;
        try
        {
            preview = PreviewOrderDraftPrice.Execute(new OrderDraftPricePreviewRequest(
                payload.CurrencyCode ?? string.Empty,
                payload.Lines.Select(line => new OrderDraftLineInput(
                    line!.Description ?? string.Empty, line.Quantity,
                    line.UnitCode ?? string.Empty, line.UnitPrice)).ToArray()));
        }
        catch (OrderDraftValidationException exception)
        {
            return Invalid(exception.Code, exception.Message);
        }

        return TypedResults.Ok(new OrderDraftPricePreviewResponse(preview.CurrencyCode,
            preview.Lines.Select(line => new OrderDraftLineResponse(
                line.Position, line.Description, line.Quantity, line.UnitCode, line.UnitPrice, line.LineTotal)).ToArray(),
            preview.Total));
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult TooLarge() => TypedResults.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "Price preview request is too large.",
        detail: "The price preview request exceeds the supported request size.",
        extensions: new Dictionary<string, object?> { ["code"] = "request_too_large" });

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Invalid(string code, string detail) => TypedResults.Problem(
        statusCode: StatusCodes.Status400BadRequest, title: "Invalid price preview request.", detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}

internal sealed record OrderDraftPricePreviewPayload(
    string? CurrencyCode,
    IReadOnlyList<CreateOrderDraftLinePayload?>? Lines);

internal sealed record OrderDraftPricePreviewResponse(
    string CurrencyCode,
    IReadOnlyList<OrderDraftLineResponse> Lines,
    decimal Total);
