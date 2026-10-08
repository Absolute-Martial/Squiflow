using Application.Profiles;
using Xunit;

namespace Application.Profiles.Tests;

public sealed class CommercialFeatureCatalogTests
{
    [Fact]
    public void CatalogHasStableFingerprintAndFixedFeatureDefinitions()
    {
        var catalog = CommercialFeatureCatalog.Catalog;

        Assert.Equal(
            "c6dde0b42260cccd4264553d69cfd5bfdf8104362e0dd6019e54ba1e1106c9db",
            catalog.Fingerprint);
        Assert.Equal(
            [
                CommercialFeatureCatalog.CustomerOrganizations,
                CommercialFeatureCatalog.CustomerPrograms,
                CommercialFeatureCatalog.OrderDrafts,
                CommercialFeatureCatalog.OrderProgramAttribution,
            ],
            catalog.Definitions.Select(definition => definition.Id));
        Assert.All(catalog.Definitions, definition =>
        {
            Assert.True(definition.IsAlwaysEnabled);
            Assert.False(definition.IsTenantSelectable);
        });
        Assert.Equal(
            [CommercialFeatureCatalog.CustomerOrganizations],
            catalog.Definitions.Single(definition => definition.Id == CommercialFeatureCatalog.CustomerPrograms)
                .Dependencies);
        Assert.Empty(
            catalog.Definitions.Single(definition => definition.Id == CommercialFeatureCatalog.OrderDrafts)
                .Dependencies);
        Assert.Equal(
            [CommercialFeatureCatalog.CustomerPrograms, CommercialFeatureCatalog.OrderDrafts],
            catalog.Definitions.Single(definition => definition.Id == CommercialFeatureCatalog.OrderProgramAttribution)
                .Dependencies.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CatalogFingerprintDoesNotDependOnDefinitionOrder()
    {
        var reversed = FeatureCatalog.Create(CommercialFeatureCatalog.Catalog.Definitions.Reverse());

        Assert.Equal(CommercialFeatureCatalog.Catalog.Fingerprint, reversed.Fingerprint);
    }

    [Fact]
    public void EmptySelectionIncludesTheFixedCatalogAndTenantCannotSelectFeatures()
    {
        var catalog = CommercialFeatureCatalog.Catalog;

        Assert.Equal(
            catalog.Definitions.Select(definition => definition.Id),
            catalog.Compile([]).EffectiveFeatureIds);
        Assert.All(catalog.Definitions, definition =>
            Assert.Throws<ArgumentException>(() => catalog.Compile([definition.Id])));
    }
}
