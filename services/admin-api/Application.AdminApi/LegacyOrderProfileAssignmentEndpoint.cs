using System.Text.Json;
using Application.Orders.Postgres;

namespace Application.AdminApi;

internal static class LegacyOrderProfileAssignmentEndpoint
{
    internal static async Task<IResult> PostAsync(
        Guid tenantId,
        Guid orderId,
        HttpContext http,
        PostgresOrderDraftStore orders)
    {
        var access = AdminApiPlatformAuthorization.GetRequiredAccess(http);
        if (!TryKey(http, out var key))
            return BoundedAdminJson.Problem(400, "idempotency_key_invalid", "One bounded Idempotency-Key header is required.");

        var payload = await BoundedAdminJson.ReadObjectAsync(http.Request, http.RequestAborted).ConfigureAwait(false);
        using var document = payload.Document;
        if (payload.Failure is not null) return payload.Failure;
        if (!TryExpectedRevision(document!.RootElement, out var expectedRevision))
            return BoundedAdminJson.Problem(400, "legacy_order_profile_request_invalid", "The legacy Order profile request is invalid.");

        LegacyOrderPolicyAssignmentResult result;
        try
        {
            result = await orders.AssignLegacyPolicyAsync(tenantId, orderId, expectedRevision,
                access.PrincipalId, access.DeviceId, key!, http.RequestAborted).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return BoundedAdminJson.Problem(400, "legacy_order_profile_request_invalid", "The legacy Order profile request is invalid.");
        }

        switch (result.Status)
        {
            case LegacyOrderPolicyAssignmentStatus.Assigned:
            case LegacyOrderPolicyAssignmentStatus.Replayed:
                if (result.Status == LegacyOrderPolicyAssignmentStatus.Replayed)
                    http.Response.Headers.Append("Idempotency-Replayed", "true");
                return Results.Json(
                    new LegacyOrderProfileAssignmentResponse(result.Status.ToString(), result.OrderId, result.ObservedRevision),
                    statusCode: result.Status == LegacyOrderPolicyAssignmentStatus.Assigned
                        ? StatusCodes.Status201Created
                        : StatusCodes.Status200OK);
            case LegacyOrderPolicyAssignmentStatus.NotFound:
                return BoundedAdminJson.Problem(404, "order_not_found", "The draft Order was not found.");
            case LegacyOrderPolicyAssignmentStatus.RevisionConflict:
                return BoundedAdminJson.Problem(409, "order_revision_conflict", "The draft Order changed before profile assignment.");
            case LegacyOrderPolicyAssignmentStatus.NotDraft:
                return BoundedAdminJson.Problem(409, "order_not_draft", "Only a draft Order can receive a legacy profile assignment.");
            case LegacyOrderPolicyAssignmentStatus.AlreadyBound:
                return BoundedAdminJson.Problem(409, "order_profile_already_bound", "The draft Order already has a profile assignment.");
            case LegacyOrderPolicyAssignmentStatus.IdempotencyKeyConflict:
                return BoundedAdminJson.Problem(409, "idempotency_key_conflict", "The Idempotency-Key conflicts with another command.");
            case LegacyOrderPolicyAssignmentStatus.ProfileUnavailable:
                return BoundedAdminJson.Problem(409, "legacy_order_profile_unavailable", "A compatible legacy profile is unavailable.");
            default:
                throw new InvalidOperationException("Legacy Order profile assignment returned an unsupported result.");
        }
    }

    private static bool TryExpectedRevision(JsonElement root, out long expectedRevision)
    {
        expectedRevision = default;
        var seen = false;
        foreach (var property in root.EnumerateObject())
        {
            if (seen || !property.NameEquals("expectedRevision") || property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetInt64(out expectedRevision) || expectedRevision < 1 || expectedRevision == long.MaxValue)
                return false;
            seen = true;
        }
        return seen;
    }

    private static bool TryKey(HttpContext http, out string? key)
    {
        var values = http.Request.Headers["Idempotency-Key"];
        key = values.Count == 1 ? values[0]?.Trim() : null;
        return key is { Length: > 0 and <= 128 } && !key.Any(char.IsControl);
    }
}

internal sealed record LegacyOrderProfileAssignmentResponse(string Status, Guid OrderId, long? ObservedRevision);
