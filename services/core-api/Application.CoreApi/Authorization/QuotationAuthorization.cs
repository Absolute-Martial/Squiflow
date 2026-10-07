using Application.Quotations;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi.Authorization;

internal sealed class QuotationAuthority(OpenFgaTenantAuthorization quotations, ITenantOrderAuthorization orders,
    ITenantCatalogAuthorization catalog, ITenantPricingAuthorization pricing) : IQuotationAuthority
{
    public Task<bool> CheckAsync(TenantContext context, QuotationCapability capability, CancellationToken ct) => capability switch
    {
        QuotationCapability.Create or QuotationCapability.Edit or QuotationCapability.View or QuotationCapability.Issue =>
            quotations.CheckQuotationAsync(context.AccountId, context.TenantId, capability, ct),
        QuotationCapability.ManualPricing => orders.CanApplyManualPriceAsync(context.AccountId, context.TenantId, ct),
        QuotationCapability.CatalogView => catalog.CanViewAsync(context.AccountId, context.TenantId, ct),
        QuotationCapability.PricingView => pricing.CanViewAsync(context.AccountId, context.TenantId, ct),
        QuotationCapability.Override => pricing.CanOverrideAsync(context.AccountId, context.TenantId, ct),
        QuotationCapability.OverrideBeyondPolicy => pricing.CanOverrideBeyondPolicyAsync(context.AccountId, context.TenantId, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(capability)),
    };
}
internal sealed record QuotationRequirement(QuotationCapability Capability) : IAuthorizationRequirement;
internal sealed class QuotationAuthorizationHandler(IQuotationAuthority authority)
    : AuthorizationHandler<QuotationRequirement, TenantCustomerResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, QuotationRequirement requirement, TenantCustomerResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is true &&
            await authority.CheckAsync(resource.TenantContext, requirement.Capability, resource.CancellationToken)) context.Succeed(requirement);
    }
}
