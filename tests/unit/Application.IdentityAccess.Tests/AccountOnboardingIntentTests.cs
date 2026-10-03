using Xunit;

namespace Application.IdentityAccess.Tests;

public sealed class AccountOnboardingIntentTests
{
    private static readonly ExternalIdentity Identity =
        ExternalIdentity.Create("https://identity.example.test", "stable-subject");

    [Fact]
    public void ActorRequiresExactPrincipalAndDeviceIdentities()
    {
        Assert.Throws<ArgumentException>(() => IdentityAdministrationActor.Create(Guid.Empty, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => IdentityAdministrationActor.Create(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void FingerprintIncludesExactIdentityAndLinkTarget()
    {
        var first = AccountOnboardingIntent.Create(Identity, " operation ");
        var same = AccountOnboardingIntent.Create(Identity, "operation");
        var different = AccountOnboardingIntent.Create(
            ExternalIdentity.Create(Identity.Issuer, "other-subject"), "operation");
        var accountId = Guid.NewGuid();
        var link = ExternalIdentityLinkIntent.Create(accountId, Identity, "operation");
        var otherTarget = ExternalIdentityLinkIntent.Create(Guid.NewGuid(), Identity, "operation");

        Assert.Equal("operation", first.IdempotencyKey);
        Assert.Equal(first.Fingerprint, same.Fingerprint);
        Assert.NotEqual(first.Fingerprint, different.Fingerprint);
        Assert.NotEqual(link.Fingerprint, otherTarget.Fingerprint);
        Assert.NotEqual(first.Fingerprint, link.Fingerprint);
    }

    [Fact]
    public void IntentRejectsOversizedAndMalformedIdempotencyKeys()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AccountOnboardingIntent.Create(Identity, new string('a', AccountOnboardingIntent.IdempotencyKeyLimit + 1)));
        Assert.Throws<ArgumentException>(() => AccountOnboardingIntent.Create(Identity, "line\nfeed"));
        Assert.Throws<ArgumentException>(() => AccountOnboardingIntent.Create(Identity, "bad\uD800"));
        Assert.Throws<ArgumentException>(() => AccountOnboardingIntent.Create(Identity, "   "));
        Assert.Throws<ArgumentException>(() => ExternalIdentityLinkIntent.Create(Guid.Empty, Identity, "key"));
    }
}
