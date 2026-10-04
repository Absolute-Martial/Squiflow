using System.Text.Json;
using Application.Tenancy;

namespace Application.AdminApi;

internal static class TenantProvisioningEndpoint
{
    private const string IdempotencyHeader = "Idempotency-Key";
    private const int MaximumRequestBodyBytes = 4096;

    internal static async Task<IResult> PostAsync(
        HttpContext context,
        ProvisionTenant provisionTenant)
    {
        var access = AdminApiPlatformAuthorization.GetRequiredAccess(context);

        var idempotencyValues = context.Request.Headers[IdempotencyHeader];
        if (idempotencyValues.Count != 1)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "idempotency_key_required",
                "Exactly one Idempotency-Key header is required.");
        }

        var payload = await ReadDisplayNameAsync(context.Request, context.RequestAborted)
            .ConfigureAwait(false);
        if (payload.Failure is not null)
        {
            return payload.Failure;
        }

        TenantProvisioningIntent intent;
        try
        {
            intent = TenantProvisioningIntent.Create(
                payload.DisplayName!,
                idempotencyValues[0] ?? string.Empty);
        }
        catch (ArgumentException)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "invalid_tenant_provisioning_request",
                "The tenant provisioning request is invalid.");
        }

        var actor = TenantProvisioningActor.Create(
            access.PrincipalId,
            access.DeviceId);
        var result = await provisionTenant
            .ExecuteAsync(actor, intent, context.RequestAborted)
            .ConfigureAwait(false);

        return result.Status switch
        {
            ProvisionTenantStatus.Created when result.Tenant is not null =>
                Results.Json(
                    Response(result.Tenant),
                    statusCode: StatusCodes.Status201Created),
            ProvisionTenantStatus.Replayed when result.Tenant is not null =>
                TypedResults.Ok(Response(result.Tenant)),
            ProvisionTenantStatus.IdempotencyKeyConflict =>
                Problem(
                    StatusCodes.Status409Conflict,
                    "idempotency_key_conflict",
                    "The Idempotency-Key was already used for a different tenant provisioning request."),
            _ => throw new InvalidOperationException(
                "Tenant provisioning returned an invalid result."),
        };
    }

    private static TenantProvisioningResponse Response(TenantProvisioningSnapshot tenant) =>
        new(
            tenant.TenantId,
            tenant.DisplayName,
            "active",
            tenant.ActivatedAt);

    private static IResult Problem(int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private static async Task<(string? DisplayName, IResult? Failure)> ReadDisplayNameAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is long contentLength && contentLength > MaximumRequestBodyBytes)
        {
            return (null, Problem(StatusCodes.Status413PayloadTooLarge, "request_too_large", "The request body is too large."));
        }

        if (!request.HasJsonContentType())
        {
            return (null, Problem(StatusCodes.Status415UnsupportedMediaType, "unsupported_media_type", "The request body must use application/json."));
        }

        try
        {
            var body = new byte[MaximumRequestBodyBytes + 1];
            var received = 0;
            while (received < body.Length)
            {
                var count = await request.Body.ReadAsync(body.AsMemory(received), cancellationToken)
                    .ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                received += count;
            }

            if (received > MaximumRequestBodyBytes)
            {
                return (null, Problem(StatusCodes.Status413PayloadTooLarge, "request_too_large", "The request body is too large."));
            }

            using var document = JsonDocument.Parse(
                body.AsMemory(0, received),
                new JsonDocumentOptions { MaxDepth = 2 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return InvalidPayload();
            }

            using var properties = document.RootElement.EnumerateObject();
            if (!properties.MoveNext() ||
                !properties.Current.NameEquals("displayName") ||
                properties.Current.Value.ValueKind != JsonValueKind.String ||
                properties.Current.Value.GetString() is not { } displayName ||
                properties.MoveNext())
            {
                return InvalidPayload();
            }

            return (displayName, null);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, Problem(StatusCodes.Status413PayloadTooLarge, "request_too_large", "The request body is too large."));
        }
        catch (Exception exception) when (exception is BadHttpRequestException or JsonException)
        {
            return InvalidPayload();
        }
    }

    private static (string? DisplayName, IResult? Failure) InvalidPayload() =>
        (null, Problem(
            StatusCodes.Status400BadRequest,
            "invalid_tenant_provisioning_request",
            "The tenant provisioning request is invalid."));
}

internal sealed record TenantProvisioningResponse(
    Guid TenantId,
    string DisplayName,
    string Availability,
    DateTimeOffset ActivatedAt);
