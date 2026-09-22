using System.Security.Claims;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
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
        var candidate = httpContext.GetTenantInfo<TenantInfo>()?.Identifier;
        if (!Guid.TryParseExact(candidate, "D", out var candidateTenantId) ||
            candidateTenantId != tenantId)
        {
            return Denied("tenant_context_invalid", "The tenant context could not be established.");
        }

        var access = await AuthenticatedAccountAccess.ResolveAsync(
            principal,
            resolveAccount,
            cancellationToken);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var tenantContext = await resolveTenantContext.ExecuteAsync(
            access.Account!.AccountId,
            tenantId,
            cancellationToken);
        if (tenantContext is null)
        {
            return Denied("tenant_access_denied", "The requested tenant is not available to this account.");
        }

        AuthorizationResult decision;
        try
        {
            decision = await authorization.AuthorizeAsync(
                principal,
                new TenantWorkspaceResource(tenantContext, cancellationToken),
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
            return Denied("tenant_permission_denied", "The account is not permitted to enter this tenant.");
        }

        var currentMembership = (await memberships.ListActiveAsync(
                access.Account.AccountId,
                cancellationToken))
            .SingleOrDefault(value => value.TenantId == tenantId);
        if (currentMembership is null)
        {
            return Denied("tenant_access_denied", "The requested tenant is not available to this account.");
        }

        httpContext.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new TenantWorkspaceResponse(
            currentMembership.TenantId,
            currentMembership.DisplayName));
    }

    private static ProblemHttpResult Denied(string code, string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Tenant access denied.",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}

internal sealed record TenantWorkspaceResponse(Guid TenantId, string DisplayName);
