using Xunit;

namespace Application.Profiles.Tests;

public sealed class TenantProfileRulesTests
{
    [Theory]
    [InlineData("")]
    [InlineData("key with space")]
    [InlineData("key\n")]
    [InlineData("\u00e9")]
    public void SemanticKeysRejectEmptyControlAndUnsupportedCharacters(string key) =>
        Assert.Throws<ProfileValidationException>(() => TenantProfileRules.Key(key));

    [Fact]
    public void SemanticKeyAndExpectedRevisionBoundsAreExplicit()
    {
        Assert.Equal(new string('x', 128), TenantProfileRules.Key(" " + new string('x', 128) + " "));
        Assert.Throws<ProfileValidationException>(() => TenantProfileRules.Key(new string('x', 129)));
        TenantProfileRules.RequireExpectedRevision(0);
        Assert.Throws<ProfileValidationException>(() => TenantProfileRules.RequireExpectedRevision(-1));
        Assert.Throws<ProfileValidationException>(() => TenantProfileRules.RequireExpectedRevision(long.MaxValue));
        Assert.Throws<ProfileValidationException>(() => TenantProfileRules.RequireAuthorizationRevision(0));
    }

    [Fact]
    public void StoredSnapshotMustMatchExactVersionCatalogSelectionTenantAndTypedPolicy()
    {
        var tenant = Guid.NewGuid(); var selection = CommercialFeatureCatalog.Catalog.Compile([]);
        var at = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var policy = new PublishedTenantPolicy(Guid.NewGuid(), tenant, 2, false, Guid.NewGuid(), at, 1);
        var profile = new TenantProfileSnapshot(TenantProfileRules.SupportedVersion, Guid.NewGuid(), tenant,
            selection.CatalogFingerprint, selection.SelectionFingerprint, selection.EffectiveFeatureIds, policy, Guid.NewGuid(), Guid.NewGuid(), at, 1, true);
        TenantProfileRules.ValidateProfile(profile, tenant);
        Assert.Throws<InvalidOperationException>(() => TenantProfileRules.ValidateProfile(profile with { Version = "tenant-profile/v2" }, tenant));
        Assert.Throws<InvalidOperationException>(() => TenantProfileRules.ValidateProfile(profile with { FeatureIds = [] }, tenant));
        Assert.Throws<InvalidOperationException>(() => TenantProfileRules.ValidateProfile(profile with { SelectionFingerprint = "unknown" }, tenant));
        Assert.Throws<InvalidOperationException>(() => TenantProfileRules.ValidateProfile(profile, Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => TenantProfileRules.ValidateProfile(profile with { Policy = policy with { RequireReferenceForProgramOrders = true } }, tenant));
        Assert.Throws<InvalidOperationException>(() => TenantProfileRules.ValidateProfile(profile with { PublishedByDeviceId = Guid.Empty }, tenant));
    }
}
