using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi.Authorization;

internal sealed class ManageTenantRolesRequirement : IAuthorizationRequirement
{
    internal static ManageTenantRolesRequirement Instance { get; } = new();

    private ManageTenantRolesRequirement()
    {
    }
}

internal sealed record TenantAuthorizationAdministrationResource(
    TenantContext TenantContext,
    CancellationToken CancellationToken);

internal sealed class ManageTenantRolesAuthorizationHandler(
    ITenantAuthorizationAdministrationStore store)
    : AuthorizationHandler<ManageTenantRolesRequirement, TenantAuthorizationAdministrationResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ManageTenantRolesRequirement requirement,
        TenantAuthorizationAdministrationResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is not true)
        {
            return;
        }

        if (await store.IsInitialOwnerAsync(
                resource.TenantContext.TenantId,
                resource.TenantContext.AccountId,
                resource.CancellationToken).ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}
