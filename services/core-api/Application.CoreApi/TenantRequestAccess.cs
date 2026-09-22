using System.Security.Claims;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Application.IdentityAccess;
using Application.Tenancy;

namespace Application.CoreApi;

internal static class TenantRequestAccess
{
    internal static async Task<TenantRequestAccessResult> ResolveAsync(
        Guid tenantId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        CancellationToken cancellationToken)
    {
        var candidate = httpContext.GetTenantInfo<TenantInfo>()?.Identifier;
        if (!Guid.TryParseExact(candidate, "D", out var candidateTenantId) ||
            candidateTenantId != tenantId)
        {
            return TenantRequestAccessResult.Denied(Problem(
                "tenant_context_invalid",
                "The tenant context could not be established."));
        }

        var account = await AuthenticatedAccountAccess.ResolveAsync(
            principal,
            resolveAccount,
            cancellationToken);
        if (account.Failure is not null)
        {
            return TenantRequestAccessResult.Denied(account.Failure);
        }

        var tenantContext = await resolveTenantContext.ExecuteAsync(
            account.Account!.AccountId,
            tenantId,
            cancellationToken);
        return tenantContext is null
            ? TenantRequestAccessResult.Denied(Problem(
                "tenant_access_denied",
                "The requested tenant is not available to this account."))
            : TenantRequestAccessResult.Allowed(tenantContext);
    }

    internal static ProblemHttpResult Problem(string code, string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Tenant access denied.",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}

internal sealed record TenantRequestAccessResult(
    TenantContext? TenantContext,
    ProblemHttpResult? Failure)
{
    internal static TenantRequestAccessResult Allowed(TenantContext context) => new(context, null);

    internal static TenantRequestAccessResult Denied(ProblemHttpResult failure) => new(null, failure);
}
