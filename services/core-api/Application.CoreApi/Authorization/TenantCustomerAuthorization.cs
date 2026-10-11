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

internal sealed class CreateIndividualRequirement : IAuthorizationRequirement
{
    internal static CreateIndividualRequirement Instance { get; } = new();
}

internal sealed class ViewIndividualsRequirement : IAuthorizationRequirement
{
    internal static ViewIndividualsRequirement Instance { get; } = new();
}

internal sealed class ChangeIndividualAvailabilityRequirement : IAuthorizationRequirement
{
    internal static ChangeIndividualAvailabilityRequirement Instance { get; } = new();
}

internal sealed class EditIndividualContactRequirement : IAuthorizationRequirement
{
    internal static EditIndividualContactRequirement Instance { get; } = new();
}

internal sealed class ViewRepresentativesRequirement : IAuthorizationRequirement
{
    internal static ViewRepresentativesRequirement Instance { get; } = new();
}

internal sealed class ManageRepresentativesRequirement : IAuthorizationRequirement
{
    internal static ManageRepresentativesRequirement Instance { get; } = new();
}

internal sealed class ResolveCustomerDuplicatesRequirement : IAuthorizationRequirement
{
    internal static ResolveCustomerDuplicatesRequirement Instance { get; } = new();
}

internal sealed class ConsolidateCustomerDuplicatesRequirement : IAuthorizationRequirement
{
    internal static ConsolidateCustomerDuplicatesRequirement Instance { get; } = new();
}

internal sealed class ImportCustomersRequirement : IAuthorizationRequirement
{
    internal static ImportCustomersRequirement Instance { get; } = new();
}

internal sealed class ViewCatalogRequirement : IAuthorizationRequirement
{
    internal static ViewCatalogRequirement Instance { get; } = new();
}

internal sealed class ManageCatalogRequirement : IAuthorizationRequirement
{
    internal static ManageCatalogRequirement Instance { get; } = new();
}

internal sealed class ViewPricingRequirement : IAuthorizationRequirement
{
    internal static ViewPricingRequirement Instance { get; } = new();
}

internal sealed class EditPricingDraftRequirement : IAuthorizationRequirement
{
    internal static EditPricingDraftRequirement Instance { get; } = new();
}

internal sealed class PublishPricingRequirement : IAuthorizationRequirement
{
    internal static PublishPricingRequirement Instance { get; } = new();
}

internal sealed class RetirePricingRequirement : IAuthorizationRequirement
{
    internal static RetirePricingRequirement Instance { get; } = new();
}

internal sealed class OverridePricingRequirement : IAuthorizationRequirement
{
    internal static OverridePricingRequirement Instance { get; } = new();
}

internal sealed class OverrideBeyondPolicyPricingRequirement : IAuthorizationRequirement
{
    internal static OverrideBeyondPolicyPricingRequirement Instance { get; } = new();
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

internal sealed class CreateIndividualAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : AuthorizationHandler<CreateIndividualRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CreateIndividualRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanCreateIndividualAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}

internal sealed class ViewIndividualsAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : AuthorizationHandler<ViewIndividualsRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ViewIndividualsRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanViewIndividualsAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}

internal sealed class ChangeIndividualAvailabilityAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : AuthorizationHandler<ChangeIndividualAvailabilityRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ChangeIndividualAvailabilityRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanChangeIndividualAvailabilityAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}

internal sealed class EditIndividualContactAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : AuthorizationHandler<EditIndividualContactRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EditIndividualContactRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanEditIndividualContactAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}

internal sealed class ViewRepresentativesAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : AuthorizationHandler<ViewRepresentativesRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ViewRepresentativesRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanViewRepresentativesAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}

internal sealed class ManageRepresentativesAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : AuthorizationHandler<ManageRepresentativesRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ManageRepresentativesRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authorization.CanManageRepresentativesAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}

internal abstract class CustomerAuthorizationHandler<TRequirement>
    : AuthorizationHandler<TRequirement, TenantCustomerResource>
    where TRequirement : IAuthorizationRequirement
{
    protected abstract Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TRequirement requirement,
        TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await IsAllowedAsync(resource.TenantContext.AccountId, resource.TenantContext.TenantId, resource.CancellationToken))
            context.Succeed(requirement);
    }
}

internal sealed class ResolveCustomerDuplicatesAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : CustomerAuthorizationHandler<ResolveCustomerDuplicatesRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanResolveCustomerDuplicatesAsync(accountId, tenantId, cancellationToken);
}

internal sealed class ConsolidateCustomerDuplicatesAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : CustomerAuthorizationHandler<ConsolidateCustomerDuplicatesRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanConsolidateCustomerDuplicatesAsync(accountId, tenantId, cancellationToken);
}

internal sealed class ImportCustomersAuthorizationHandler(ITenantCustomerAuthorization authorization)
    : CustomerAuthorizationHandler<ImportCustomersRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanImportCustomersAsync(accountId, tenantId, cancellationToken);
}

internal abstract class CapabilityAuthorizationHandler<TRequirement>
    : AuthorizationHandler<TRequirement>
    where TRequirement : IAuthorizationRequirement
{
    protected abstract Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TRequirement requirement)
    {
        var resource = context.Resource switch
        {
            TenantCustomerResource customer => (customer.TenantContext, customer.CancellationToken),
            TenantOrderResource order => (order.TenantContext, order.CancellationToken),
            _ => ((TenantContext?)null, default(CancellationToken)),
        };
        if (context.User.Identity?.IsAuthenticated is true &&
            resource.Item1 is { } tenant &&
            await IsAllowedAsync(tenant.AccountId, tenant.TenantId, resource.Item2))
            context.Succeed(requirement);
    }
}

internal sealed class ViewCatalogAuthorizationHandler(ITenantCatalogAuthorization authorization)
    : CapabilityAuthorizationHandler<ViewCatalogRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanViewAsync(accountId, tenantId, cancellationToken);
}

internal sealed class ManageCatalogAuthorizationHandler(ITenantCatalogAuthorization authorization)
    : CapabilityAuthorizationHandler<ManageCatalogRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanManageAsync(accountId, tenantId, cancellationToken);
}

internal sealed class ViewPricingAuthorizationHandler(ITenantPricingAuthorization authorization)
    : CapabilityAuthorizationHandler<ViewPricingRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanViewAsync(accountId, tenantId, cancellationToken);
}

internal sealed class EditPricingDraftAuthorizationHandler(ITenantPricingAuthorization authorization)
    : CapabilityAuthorizationHandler<EditPricingDraftRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanEditDraftAsync(accountId, tenantId, cancellationToken);
}

internal sealed class PublishPricingAuthorizationHandler(ITenantPricingAuthorization authorization)
    : CapabilityAuthorizationHandler<PublishPricingRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanPublishAsync(accountId, tenantId, cancellationToken);
}

internal sealed class RetirePricingAuthorizationHandler(ITenantPricingAuthorization authorization)
    : CapabilityAuthorizationHandler<RetirePricingRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanRetireAsync(accountId, tenantId, cancellationToken);
}

internal sealed class OverridePricingAuthorizationHandler(ITenantPricingAuthorization authorization)
    : CapabilityAuthorizationHandler<OverridePricingRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanOverrideAsync(accountId, tenantId, cancellationToken);
}

internal sealed class OverrideBeyondPolicyPricingAuthorizationHandler(ITenantPricingAuthorization authorization)
    : CapabilityAuthorizationHandler<OverrideBeyondPolicyPricingRequirement>
{
    protected override Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        authorization.CanOverrideBeyondPolicyAsync(accountId, tenantId, cancellationToken);
}
