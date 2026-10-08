namespace Application.Profiles;

public static class CommercialFeatureCatalog
{
    public const string CustomerOrganizations = "customers.organizations";
    public const string CustomerPrograms = "customers.programs";
    public const string OrderDrafts = "orders.drafts";
    public const string OrderProgramAttribution = "orders.program-attribution";

    public static FeatureCatalog Catalog { get; } = FeatureCatalog.Create(
    [
        FeatureDefinition.Create(CustomerOrganizations, isAlwaysEnabled: true, isTenantSelectable: false),
        FeatureDefinition.Create(
            CustomerPrograms,
            [CustomerOrganizations],
            isAlwaysEnabled: true,
            isTenantSelectable: false),
        FeatureDefinition.Create(OrderDrafts, isAlwaysEnabled: true, isTenantSelectable: false),
        FeatureDefinition.Create(
            OrderProgramAttribution,
            [OrderDrafts, CustomerPrograms],
            isAlwaysEnabled: true,
            isTenantSelectable: false),
    ]);
}
