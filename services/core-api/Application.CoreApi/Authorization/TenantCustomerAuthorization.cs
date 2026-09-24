using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi.Authorization;

internal sealed record TenantCustomerResource(TenantContext TenantContext, CancellationToken CancellationToken);

internal sealed class CreateOrganizationRequirement : IAuthorizationRequirement
{
    internal static CreateOrganizationRequirement Instance { get; } = new();
}

internal sealed class ViewOrganizationsRequirement : IAuthorizationRequirement
{
    internal static ViewOrganizationsRequirement Instance { get; } = new();
}

internal sealed class CreateProgramRequirement : IAuthorizationRequirement
{
    internal static CreateProgramRequirement Instance { get; } = new();
}

internal sealed class ViewProgramsRequirement : IAuthorizationRequirement
{
    internal static ViewProgramsRequirement Instance { get; } = new();
}

internal sealed class CreateOrganizationAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : AuthorizationHandler<CreateOrganizationRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CreateOrganizationRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanCreateOrganizationAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}

internal sealed class ViewOrganizationsAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : AuthorizationHandler<ViewOrganizationsRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ViewOrganizationsRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanViewOrganizationsAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}

internal sealed class CreateProgramAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : AuthorizationHandler<CreateProgramRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CreateProgramRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanCreateProgramAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}

internal sealed class ViewProgramsAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : AuthorizationHandler<ViewProgramsRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ViewProgramsRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanViewProgramsAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}
