using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi.Authorization;

internal sealed record TenantProfilePolicyRequirement(TenantProfilePolicyPermission Permission)
    : IAuthorizationRequirement;

internal sealed class TenantProfilePolicyAuthorizationHandler(ITenantProfilePolicyAuthorization authorization)
    : AuthorizationHandler<TenantProfilePolicyRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantProfilePolicyRequirement requirement,
        TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.IsAllowedAsync(
                resource.TenantContext.AccountId,
                resource.TenantContext.TenantId,
                requirement.Permission,
                resource.CancellationToken).ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}
