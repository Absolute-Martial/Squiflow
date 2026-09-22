using Microsoft.AspNetCore.Authorization;
using Application.Tenancy;

namespace Application.CoreApi.Authorization;

internal sealed record TenantOrderResource(
    TenantContext TenantContext,
    CancellationToken CancellationToken);

internal sealed class CreateOrderRequirement : IAuthorizationRequirement
{
    internal static CreateOrderRequirement Instance { get; } = new();

    private CreateOrderRequirement()
    {
    }
}

internal sealed class ViewOrdersRequirement : IAuthorizationRequirement
{
    internal static ViewOrdersRequirement Instance { get; } = new();

    private ViewOrdersRequirement()
    {
    }
}

internal sealed class CreateOrderAuthorizationHandler(ITenantOrderAuthorization authorization)
    : AuthorizationHandler<CreateOrderRequirement, TenantOrderResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CreateOrderRequirement requirement,
        TenantOrderResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanCreateAsync(
                resource.TenantContext.AccountId,
                resource.TenantContext.TenantId,
                resource.CancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}

internal sealed class ViewOrdersAuthorizationHandler(ITenantOrderAuthorization authorization)
    : AuthorizationHandler<ViewOrdersRequirement, TenantOrderResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ViewOrdersRequirement requirement,
        TenantOrderResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanViewAsync(
                resource.TenantContext.AccountId,
                resource.TenantContext.TenantId,
                resource.CancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
