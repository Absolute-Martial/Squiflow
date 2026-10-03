using Application.Tenancy;
using Xunit;

namespace Application.Tenancy.Tests;

public sealed class TenantLifecycleTests
{
    [Fact]
    public void IntentFingerprintIsStableAndCoversOperationTenantAndRevision()
    {
        var tenantId = Guid.NewGuid();
        var baseline = TenantLifecycleIntent.Create(
            TenantLifecycleOperation.Suspend, tenantId, 1, "first");
        var replay = TenantLifecycleIntent.Create(
            TenantLifecycleOperation.Suspend, tenantId, 1, "second");

        Assert.Equal(baseline.Fingerprint, replay.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, TenantLifecycleIntent.Create(
            TenantLifecycleOperation.Reactivate, tenantId, 1, "third").Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, TenantLifecycleIntent.Create(
            TenantLifecycleOperation.Suspend, Guid.NewGuid(), 1, "third").Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, TenantLifecycleIntent.Create(
            TenantLifecycleOperation.Suspend, tenantId, 2, "third").Fingerprint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad\nkey")]
    public void IntentRejectsInvalidKeys(string key)
    {
        Assert.ThrowsAny<ArgumentException>(() => TenantLifecycleIntent.Create(
            TenantLifecycleOperation.Suspend, Guid.NewGuid(), 1, key));
    }

    [Fact]
    public void IntentRejectsInvalidAuthorityInputs()
    {
        Assert.Throws<ArgumentException>(() => TenantLifecycleIntent.Create(
            TenantLifecycleOperation.Suspend, Guid.Empty, 1, "key"));
        Assert.Throws<ArgumentOutOfRangeException>(() => TenantLifecycleIntent.Create(
            TenantLifecycleOperation.Suspend, Guid.NewGuid(), 0, "key"));
        Assert.Throws<ArgumentOutOfRangeException>(() => TenantLifecycleIntent.Create(
            (TenantLifecycleOperation)99, Guid.NewGuid(), 1, "key"));
    }
}
