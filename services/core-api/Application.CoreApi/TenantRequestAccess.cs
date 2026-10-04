using System.Security.Claims;
using Application.CoreApi.Composition;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Application.IdentityAccess;
using Application.Tenancy;

namespace Application.CoreApi;

internal static class TenantRequestAccess
{
    private static readonly object ResolvedTenantContextKey = new();

    internal static async Task<TenantRequestAccessResult> ResolveAsync(
        Guid tenantId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.Items.TryGetValue(ResolvedTenantContextKey, out var cachedValue) &&
            cachedValue is TenantContext cachedContext)
        {
            if (cachedContext.TenantId != tenantId)
            {
                throw new InvalidOperationException("A request cannot resolve multiple tenant contexts.");
            }

            return TenantRequestAccessResult.Allowed(cachedContext);
        }

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
        if (tenantContext is null)
            return TenantRequestAccessResult.Denied(Problem(
                "tenant_access_denied", "The requested tenant is not available to this account."));

        cancellationToken.ThrowIfCancellationRequested();
        if (!httpContext.RequestServices.GetRequiredService<TenantRequestAdmission>().TryEnter(tenantContext.TenantId))
            return TenantRequestAccessResult.Denied(TypedResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "The tenant is at its concurrent request limit.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "tenant_capacity_exceeded",
                    ["traceId"] = httpContext.TraceIdentifier,
                }));
        httpContext.Items[ResolvedTenantContextKey] = tenantContext;
        return TenantRequestAccessResult.Allowed(tenantContext);
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
