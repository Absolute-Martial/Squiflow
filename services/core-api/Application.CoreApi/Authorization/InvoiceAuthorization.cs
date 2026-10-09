using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi.Authorization;

internal enum TenantInvoicePermission { Issue, View }

internal interface ITenantInvoiceAuthorization
{
    Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, TenantInvoicePermission permission,
        CancellationToken cancellationToken);
}

internal sealed record TenantInvoiceResource(TenantContext TenantContext, CancellationToken CancellationToken);
internal sealed record TenantInvoiceRequirement(TenantInvoicePermission Permission) : IAuthorizationRequirement;

internal sealed class TenantInvoiceAuthorizationHandler(ITenantInvoiceAuthorization authorization)
    : AuthorizationHandler<TenantInvoiceRequirement, TenantInvoiceResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        TenantInvoiceRequirement requirement, TenantInvoiceResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true && await authorization.IsAllowedAsync(
            resource.TenantContext.AccountId, resource.TenantContext.TenantId, requirement.Permission,
            resource.CancellationToken).ConfigureAwait(false))
            context.Succeed(requirement);
    }
}
