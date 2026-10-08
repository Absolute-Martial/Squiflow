using System.Text.Json.Serialization;
using Application.Profiles;
using Application.Tenancy;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Application.CoreApi;

internal static class TenantProfilePolicyEndpoints
{
    internal static async Task<IResult> GetAsync(
        Guid tenantId,
        HttpContext http,
        IProfileStore profiles,
        CancellationToken cancellationToken)
    {
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, http);
        var state = await profiles.GetPolicyAsync(context, cancellationToken).ConfigureAwait(false)
            ?? new TenantPolicyState(tenantId, 0, false, null);
        if (state.TenantId != tenantId)
            throw new InvalidOperationException("Profile policy query returned another tenant's state.");
        http.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(state);
    }

    internal static async Task<IResult> EditAsync(
        Guid tenantId,
        HttpContext http,
        IProfileStore profiles,
        ITenantAuthorizationAdministrationStore authorizations,
        CancellationToken cancellationToken)
    {
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, http);
        if (!TryKey(http.Request, out var key)) return InvalidKey();
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<EditPolicyPayload>(
            http.Request, cancellationToken, strict: true).ConfigureAwait(false);
        if (payload.Failure is not null) return payload.Failure;

        try
        {
            var authRevision = await authorizations.GetAuthorizationRevisionAsync(tenantId, cancellationToken)
                .ConfigureAwait(false) ?? 1;
            var result = await profiles.EditPolicyAsync(
                context,
                new EditTenantPolicyRequest(payload.Value!.ExpectedRevision, payload.Value.RequireReferenceForProgramOrders),
                authRevision,
                key!,
                cancellationToken).ConfigureAwait(false);
            return PolicyCommandResult(result, http);
        }
        catch (ProfileValidationException error)
        {
            return Invalid(error.Code, error.Message);
        }
        catch (ArgumentException)
        {
            return Invalid("profile_policy_request_invalid", "The profile policy request is invalid.");
        }
    }

    internal static async Task<IResult> PublishAsync(
        Guid tenantId,
        HttpContext http,
        IProfileStore profiles,
        ITenantAuthorizationAdministrationStore authorizations,
        CancellationToken cancellationToken)
    {
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, http);
        if (!TryKey(http.Request, out var key)) return InvalidKey();
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<PublishPolicyPayload>(
            http.Request, cancellationToken, strict: true).ConfigureAwait(false);
        if (payload.Failure is not null) return payload.Failure;

        try
        {
            var authRevision = await authorizations.GetAuthorizationRevisionAsync(tenantId, cancellationToken)
                .ConfigureAwait(false) ?? 1;
            var result = await profiles.PublishPolicyAsync(
                context,
                new PublishTenantPolicyRequest(payload.Value!.ExpectedRevision),
                authRevision,
                key!,
                cancellationToken).ConfigureAwait(false);
            return PolicyCommandResult(result, http);
        }
        catch (ProfileValidationException error)
        {
            return Invalid(error.Code, error.Message);
        }
        catch (ArgumentException)
        {
            return Invalid("profile_policy_request_invalid", "The profile policy request is invalid.");
        }
    }

    private static IResult PolicyCommandResult(ProfileCommandResult result, HttpContext http)
    {
        switch (result.Status)
        {
            case ProfileCommandStatus.Edited:
                if (result.PolicyState is null) throw new InvalidOperationException("Profile policy edit returned no state.");
                break;
            case ProfileCommandStatus.PolicyPublished:
                if (result.PublishedPolicy is null) throw new InvalidOperationException("Profile policy publication returned no revision.");
                break;
            case ProfileCommandStatus.Replayed:
                http.Response.Headers.Append("Idempotency-Replayed", "true");
                break;
            case ProfileCommandStatus.NotFound:
                return TenantCustomerEndpoint.NotFound("profile_policy_not_found", "Profile policy not found.");
            case ProfileCommandStatus.RevisionConflict:
                return Conflict("profile_policy_revision_conflict");
            case ProfileCommandStatus.IdempotencyKeyConflict:
                return Conflict("idempotency_key_conflict");
            default:
                throw new InvalidOperationException("Profile policy operation returned an unsupported result.");
        }

        http.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new ProfilePolicyCommandResponse(
            result.Status.ToString(), result.PolicyState, result.PublishedPolicy));
    }

    private static bool TryKey(HttpRequest request, out string? key)
    {
        var values = request.Headers["Idempotency-Key"];
        key = values.Count == 1 ? values[0] : null;
        if (key is null) return false;
        try
        {
            key = TenantProfileRules.Key(key);
            return true;
        }
        catch (ProfileValidationException)
        {
            key = null;
            return false;
        }
    }

    private static ProblemHttpResult InvalidKey() =>
        TenantCustomerEndpoint.Invalid("idempotency_key_invalid", "One bounded Idempotency-Key header is required.");

    private static ProblemHttpResult Invalid(string code, string detail) => TenantCustomerEndpoint.Invalid(code, detail);

    private static ProblemHttpResult Conflict(string code) => TypedResults.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Profile policy command conflicts with current state.",
        extensions: new Dictionary<string, object?> { ["code"] = code });
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record EditPolicyPayload(
    [property: JsonRequired] long ExpectedRevision,
    [property: JsonRequired] bool RequireReferenceForProgramOrders);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PublishPolicyPayload([property: JsonRequired] long ExpectedRevision);

internal sealed record ProfilePolicyCommandResponse(
    string Status,
    TenantPolicyState? Policy,
    PublishedTenantPolicy? PublishedPolicy);
