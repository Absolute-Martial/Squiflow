using System.Text.Json;
using Application.PlatformAdministration;
using Application.Profiles;
using Application.Tenancy;

namespace Application.AdminApi;

internal static class TenantProfileEndpoints
{
    internal static async Task<IResult> GetAuthorityAsync(
        Guid tenantId,
        IProfileStore profiles,
        CancellationToken cancellationToken)
    {
        var authority = await profiles.GetAuthorityAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return authority is null
            ? BoundedAdminJson.Problem(404, "tenant_profile_authority_not_found", "Tenant profile authority was not found.")
            : authority.TenantId != tenantId
                ? throw new InvalidOperationException("Profile authority query returned another tenant's state.")
                : Results.Ok(authority);
    }

    internal static async Task<IResult> GetProfileAsync(
        Guid tenantId,
        Guid profileId,
        IProfileStore profiles,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.GetProfileAsync(tenantId, profileId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
            return BoundedAdminJson.Problem(404, "tenant_profile_not_found", "Tenant profile was not found.");
        if (profile.TenantId != tenantId || profile.ProfileId != profileId)
            throw new InvalidOperationException("Profile query returned inconsistent identity facts.");
        return Results.Ok(profile);
    }

    internal static async Task<IResult> PublishAsync(
        Guid tenantId,
        HttpContext http,
        IProfileStore profiles,
        ITenantAuthorizationAdministrationStore authorizations)
    {
        var access = AdminApiPlatformAuthorization.GetRequiredAccess(http);
        if (!TryKey(http, out var key, out var keyFailure)) return keyFailure!;
        var payload = await BoundedAdminJson.ReadObjectAsync(http.Request, http.RequestAborted).ConfigureAwait(false);
        using var document = payload.Document;
        if (payload.Failure is not null) return payload.Failure;
        if (!TryPublishPayload(document!.RootElement, out var expectedAuthorityRevision,
                out var publishedPolicyRevisionId, out var isLegacyBaseline))
            return InvalidRequest();

        try
        {
            var observedAuthorizationRevision = await authorizations
                .GetAuthorizationRevisionAsync(tenantId, http.RequestAborted)
                .ConfigureAwait(false) ?? 1;
            var result = await profiles.PublishProfileAsync(
                new PlatformAdminAccess(access.PrincipalId, access.DeviceId),
                new PublishTenantProfileRequest(
                    tenantId,
                    expectedAuthorityRevision,
                    publishedPolicyRevisionId,
                    observedAuthorizationRevision,
                    isLegacyBaseline),
                key!,
                http.RequestAborted).ConfigureAwait(false);
            return CommandResult(result, http);
        }
        catch (ProfileValidationException error)
        {
            return BoundedAdminJson.Problem(400, error.Code, "The tenant profile request is invalid.");
        }
        catch (ArgumentException)
        {
            return InvalidRequest();
        }
    }

    internal static async Task<IResult> ActivateAsync(
        Guid tenantId,
        Guid profileId,
        HttpContext http,
        IProfileStore profiles)
    {
        var access = AdminApiPlatformAuthorization.GetRequiredAccess(http);
        if (!TryKey(http, out var key, out var keyFailure)) return keyFailure!;
        var payload = await BoundedAdminJson.ReadObjectAsync(http.Request, http.RequestAborted).ConfigureAwait(false);
        using var document = payload.Document;
        if (payload.Failure is not null) return payload.Failure;
        if (!TryActivatePayload(document!.RootElement, out var expectedAuthorityRevision, out var asLegacyBaseline))
            return InvalidRequest();

        try
        {
            var result = await profiles.ActivateProfileAsync(
                new PlatformAdminAccess(access.PrincipalId, access.DeviceId),
                new ActivateTenantProfileRequest(tenantId, expectedAuthorityRevision, profileId, asLegacyBaseline),
                key!,
                http.RequestAborted).ConfigureAwait(false);
            return CommandResult(result, http);
        }
        catch (ProfileValidationException error)
        {
            return BoundedAdminJson.Problem(400, error.Code, "The tenant profile request is invalid.");
        }
        catch (ArgumentException)
        {
            return InvalidRequest();
        }
    }

    internal static void MapTenantProfileEndpoints(this WebApplication app)
    {
        MapGet(app.MapGet("/api/v1/platform/tenants/{tenantId:guid}/profile-authority", GetAuthorityAsync),
            PlatformAdminPermission.ReadTenants, AdminEndpointAuditOperation.TenantProfileAuthorityRead, "ReadTenantProfileAuthority");
        MapGet(app.MapGet("/api/v1/platform/tenants/{tenantId:guid}/profiles/{profileId:guid}", GetProfileAsync),
            PlatformAdminPermission.ReadTenants, AdminEndpointAuditOperation.TenantProfileRead, "ReadTenantProfile");
        MapPost(app.MapPost("/api/v1/platform/tenants/{tenantId:guid}/profiles", PublishAsync),
            PlatformAdminPermission.PublishTenantProfile, AdminEndpointAuditOperation.TenantProfilePublish, "PublishTenantProfile");
        MapPost(app.MapPost("/api/v1/platform/tenants/{tenantId:guid}/profiles/{profileId:guid}/activate", ActivateAsync),
            PlatformAdminPermission.ActivateTenantProfile, AdminEndpointAuditOperation.TenantProfileActivate, "ActivateTenantProfile");
    }

    private static IResult CommandResult(ProfileCommandResult result, HttpContext http)
    {
        switch (result.Status)
        {
            case ProfileCommandStatus.ProfilePublished:
            case ProfileCommandStatus.Activated:
            case ProfileCommandStatus.LegacyBaselineSelected:
            case ProfileCommandStatus.Replayed:
                if (result.Status == ProfileCommandStatus.Replayed)
                    http.Response.Headers.Append("Idempotency-Replayed", "true");
                http.Response.Headers.CacheControl = "no-store";
                return Results.Json(new TenantProfileCommandResponse(
                    result.Status.ToString(), result.Profile, result.Authority),
                    statusCode: result.Status is ProfileCommandStatus.ProfilePublished or
                        ProfileCommandStatus.LegacyBaselineSelected
                        ? StatusCodes.Status201Created
                        : StatusCodes.Status200OK);
            case ProfileCommandStatus.NotFound:
                return BoundedAdminJson.Problem(404, "tenant_profile_target_not_found", "The tenant profile target was not found.");
            case ProfileCommandStatus.RevisionConflict:
                return Conflict("tenant_profile_revision_conflict");
            case ProfileCommandStatus.IdempotencyKeyConflict:
                return Conflict("idempotency_key_conflict");
            case ProfileCommandStatus.InvalidBaseline:
                return Conflict("tenant_profile_baseline_invalid");
            case ProfileCommandStatus.BaselineAlreadySelected:
                return Conflict("tenant_profile_baseline_already_selected");
            default:
                throw new InvalidOperationException("Profile command returned an unsupported result.");
        }
    }

    private static bool TryPublishPayload(
        JsonElement root,
        out long expectedAuthorityRevision,
        out Guid publishedPolicyRevisionId,
        out bool isLegacyBaseline)
    {
        expectedAuthorityRevision = default;
        publishedPolicyRevisionId = default;
        isLegacyBaseline = false;
        if (!TryFields(root, ["expectedAuthorityRevision", "publishedPolicyRevisionId"], ["isLegacyBaseline"], out var fields) ||
            !TryLong(fields["expectedAuthorityRevision"], out expectedAuthorityRevision) || expectedAuthorityRevision is < 0 or long.MaxValue ||
            !TryGuid(fields["publishedPolicyRevisionId"], out publishedPolicyRevisionId))
            return false;
        return !fields.TryGetValue("isLegacyBaseline", out var legacy) || TryBoolean(legacy, out isLegacyBaseline);
    }

    private static bool TryActivatePayload(JsonElement root, out long expectedAuthorityRevision, out bool asLegacyBaseline)
    {
        expectedAuthorityRevision = default;
        asLegacyBaseline = false;
        if (!TryFields(root, ["expectedAuthorityRevision"], ["asLegacyBaseline"], out var fields) ||
            !TryLong(fields["expectedAuthorityRevision"], out expectedAuthorityRevision) || expectedAuthorityRevision is < 0 or long.MaxValue)
            return false;
        return !fields.TryGetValue("asLegacyBaseline", out var legacy) || TryBoolean(legacy, out asLegacyBaseline);
    }

    private static bool TryFields(
        JsonElement root,
        string[] required,
        string[] optional,
        out Dictionary<string, JsonElement> fields)
    {
        fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!required.Contains(property.Name, StringComparer.Ordinal) &&
                !optional.Contains(property.Name, StringComparer.Ordinal) ||
                !fields.TryAdd(property.Name, property.Value))
                return false;
        }
        return required.All(fields.ContainsKey);
    }

    private static bool TryLong(JsonElement value, out long result) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out result) || AssignDefault(out result);

    private static bool AssignDefault(out long result)
    {
        result = default;
        return false;
    }

    private static bool TryGuid(JsonElement value, out Guid result)
    {
        result = default;
        return value.ValueKind == JsonValueKind.String && value.TryGetGuid(out result) && result != Guid.Empty;
    }

    private static bool TryBoolean(JsonElement value, out bool result)
    {
        result = false;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        result = value.GetBoolean();
        return true;
    }

    private static bool TryKey(HttpContext http, out string? key, out IResult? failure)
    {
        var values = http.Request.Headers["Idempotency-Key"];
        key = values.Count == 1 ? values[0] : null;
        try
        {
            if (key is not null) key = TenantProfileRules.Key(key);
        }
        catch (ProfileValidationException)
        {
            key = null;
        }
        failure = key is null
            ? BoundedAdminJson.Problem(400, "idempotency_key_invalid", "One bounded Idempotency-Key header is required.")
            : null;
        return failure is null;
    }

    private static IResult InvalidRequest() =>
        BoundedAdminJson.Problem(400, "tenant_profile_request_invalid", "The tenant profile request is invalid.");

    private static IResult Conflict(string code) =>
        BoundedAdminJson.Problem(409, code, "The tenant profile command conflicts with current state.");

    private static void MapGet(RouteHandlerBuilder endpoint, PlatformAdminPermission permission,
        AdminEndpointAuditOperation auditOperation, string name) => endpoint.WithName(name)
        .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
        .WithMetadata(new AdminEndpointPermissionMetadata(permission))
        .WithMetadata(new AdminEndpointAuditMetadata(auditOperation))
        .RequireAuthorization()
        .RequireRateLimiting(Composition.AdminApiAdmission.PolicyName);

    private static void MapPost(RouteHandlerBuilder endpoint, PlatformAdminPermission permission,
        AdminEndpointAuditOperation auditOperation, string name) => endpoint.WithName(name)
        .WithMetadata(new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration))
        .WithMetadata(new AdminEndpointPermissionMetadata(permission))
        .WithMetadata(new AdminEndpointAuditMetadata(auditOperation))
        .RequireAuthorization()
        .RequireRateLimiting(Composition.AdminApiAdmission.PolicyName);
}

internal sealed record TenantProfileCommandResponse(string Status, TenantProfileSnapshot? Profile, TenantProfileAuthority? Authority);
