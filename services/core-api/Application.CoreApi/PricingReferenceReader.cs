using Application.Catalog;
using Application.Customers;
using Application.Pricing;
using Application.Tenancy;

namespace Application.CoreApi;

internal sealed class PricingReferenceReader(ResolveTenantContext tenants, GetCatalogItem items, GetCatalogUnit units,
    GetCustomerIndividual individuals, GetCustomerOrganization organizations, GetCustomerProgram programs, GetCatalogConversion conversions) : IPricingReferenceReader
{
    public async Task<string> RequireCompatibleUnitAsync(PricingActorContext actor, Guid itemId, Guid unitId, long? conversionRevision, CancellationToken ct)
    {
        if (itemId == Guid.Empty || unitId == Guid.Empty)
            throw new PricingValidationException("catalog_identity_invalid", "Stable item and unit identities are required.");
        var tenant = await RequireTenantAsync(actor, ct);
        var item = await items.ExecuteAsync(tenant, itemId, ct);
        var unit = await units.ExecuteAsync(tenant, unitId, ct);
        if (item is null || unit is null || item.TenantId != actor.TenantId || unit.TenantId != actor.TenantId)
            throw new PricingValidationException("catalog_fact_not_found", "Catalog item/unit not found in this tenant.");
        if (item.Status != CatalogEntityStatus.Active || unit.Status != CatalogEntityStatus.Active)
            throw new PricingValidationException("catalog_unit_incompatible", "Pricing requires an active compatible Catalog item/unit.");
        if (item.BaseUnitId == unitId)
        {
            if (conversionRevision.HasValue)
                throw new PricingValidationException("catalog_conversion_invalid", "Same-unit pricing uses intrinsic identity, not a supplied conversion revision.");
            return unit.Code;
        }
        var target = await units.ExecuteAsync(tenant, item.BaseUnitId, ct);
        if (!conversionRevision.HasValue || target is null || target.TenantId != actor.TenantId || target.Status != CatalogEntityStatus.Active)
            throw new PricingValidationException("catalog_conversion_required", "Non-base pricing units require an explicit compatible Catalog conversion revision.");
        var conversion = await conversions.ExecuteAsync(tenant, unitId, item.BaseUnitId, conversionRevision.Value, ct);
        if (conversion is null || conversion.TenantId != actor.TenantId || conversion.SourceUnitId != unitId ||
            conversion.TargetUnitId != item.BaseUnitId || conversion.Revision != conversionRevision.Value)
            throw new PricingValidationException("catalog_conversion_invalid", "Catalog conversion does not match the item/unit context.");
        return unit.Code;
    }

    public async Task RequireContextAsync(PricingActorContext actor, PriceSelectionContext context, CancellationToken ct)
    {
        var tenant = await RequireTenantAsync(actor, ct);
        if (context.CustomerId.HasValue)
        {
            var customer = await individuals.ExecuteAsync(tenant, context.CustomerId.Value, ct);
            if (customer is null || customer.TenantId != actor.TenantId || customer.Availability != CustomerIndividualAvailability.Active || customer.RedirectTargetIndividualId.HasValue)
                throw new PricingValidationException("pricing_customer_invalid", "Customer is not a current active tenant-owned individual.");
        }
        if (context.OrganizationId.HasValue)
        {
            var organization = await organizations.ExecuteAsync(tenant, context.OrganizationId.Value, ct);
            if (organization is null || organization.TenantId != actor.TenantId)
                throw new PricingValidationException("pricing_organization_invalid", "Organization is not owned by this tenant.");
        }
        if (context.ProgramId.HasValue)
        {
            var program = await programs.ExecuteAsync(tenant, context.ProgramId.Value, ct);
            if (program is null || program.TenantId != actor.TenantId ||
                (context.OrganizationId.HasValue && context.OrganizationId != program.OrganizationId))
                throw new PricingValidationException("pricing_program_invalid", "Program does not belong to the tenant/organization context.");
        }
        // Wholesale is an explicit commercial applicability mode. There is no
        // inferred GUID-based tier or undisclosed customer eligibility lookup.
    }

    private async Task<TenantContext> RequireTenantAsync(PricingActorContext actor, CancellationToken ct) =>
        await tenants.ExecuteAsync(actor.AccountId, actor.TenantId, ct)
        ?? throw new PricingValidationException("tenant_access_invalid", "Current tenant membership is required.");
}
