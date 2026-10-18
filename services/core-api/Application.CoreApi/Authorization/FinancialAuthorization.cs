using Application.Invoices;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi.Authorization;

internal interface ITenantFinancialAuthorization
{
    Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, FinancialCapability permission, CancellationToken cancellationToken);
}

internal sealed record TenantFinancialResource(TenantContext TenantContext, CancellationToken CancellationToken);
internal sealed record TenantFinancialRequirement(FinancialCapability Permission) : IAuthorizationRequirement;

internal sealed class TenantFinancialAuthorizationHandler(ITenantFinancialAuthorization authorization)
    : AuthorizationHandler<TenantFinancialRequirement, TenantFinancialResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        TenantFinancialRequirement requirement, TenantFinancialResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true && await authorization.IsAllowedAsync(
            resource.TenantContext.AccountId, resource.TenantContext.TenantId, requirement.Permission,
            resource.CancellationToken).ConfigureAwait(false)) context.Succeed(requirement);
    }
}
