using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Application.CoreApi.Authorization;
using Application.IdentityAccess;
using Application.Tenancy;

namespace Application.CoreApi;

internal static class TenantWorkspaceEndpoint
{
    public static async Task<IResult> GetAsync(
        Guid tenantId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        ITenantMembershipDirectory memberships,
        IAuthorizationService authorization,
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

        AuthorizationResult decision;
        try
        {
            decision = await authorization.AuthorizeAsync(
                principal,
                new TenantWorkspaceResource(access.TenantContext!, cancellationToken),
                ViewTenantWorkspaceRequirement.Instance);
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

        if (!decision.Succeeded)
        {
            return TenantRequestAccess.Problem(
                "tenant_permission_denied",
                "The account is not permitted to enter this tenant.");
        }

        var currentMembership = (await memberships.ListActiveAsync(
                access.TenantContext!.AccountId,
                cancellationToken))
            .SingleOrDefault(value => value.TenantId == tenantId);
        if (currentMembership is null)
        {
            return TenantRequestAccess.Problem(
                "tenant_access_denied",
                "The requested tenant is not available to this account.");
        }

        httpContext.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new TenantWorkspaceResponse(
            currentMembership.TenantId,
            currentMembership.DisplayName));
    }
}

internal sealed record TenantWorkspaceResponse(Guid TenantId, string DisplayName);
