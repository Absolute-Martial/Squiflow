using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi.Authorization;

internal enum TenantPurchasingPermission { View, ManageSupplier, RecordRequest, RecordReceipt, RecordPayment, Correct }

internal interface ITenantPurchasingAuthorization
{
    Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, TenantPurchasingPermission permission,
        CancellationToken cancellationToken);
}

internal sealed record TenantPurchasingResource(TenantContext TenantContext, CancellationToken CancellationToken);
internal sealed record TenantPurchasingRequirement(TenantPurchasingPermission Permission) : IAuthorizationRequirement;

internal sealed class TenantPurchasingAuthorizationHandler(ITenantPurchasingAuthorization authorization)
    : AuthorizationHandler<TenantPurchasingRequirement, TenantPurchasingResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        TenantPurchasingRequirement requirement, TenantPurchasingResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true && await authorization.IsAllowedAsync(
            resource.TenantContext.AccountId, resource.TenantContext.TenantId,
            requirement.Permission, resource.CancellationToken).ConfigureAwait(false))
            context.Succeed(requirement);
    }
}
