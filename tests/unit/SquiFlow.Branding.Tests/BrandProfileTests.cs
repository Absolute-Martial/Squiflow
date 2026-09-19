using SquiFlow.Branding;
using Xunit;

namespace SquiFlow.Branding.Tests;

public sealed class BrandProfileTests
{
    [Fact]
    public void CapabilityRemainsHostAndProviderNeutral()
    {
        var references = typeof(BrandProfile).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("FSH", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateAcceptsRelativeAssetsAndHttpsLinks()
    {
        var profile = CreateProfile(supportUrl: "https://support.example.test/help");

        Assert.Equal("Example Desk", profile.DisplayName);
        Assert.Equal("/brand/logo.svg", profile.LogoUrl);
        Assert.Equal("https://support.example.test/help", profile.SupportUrl);
        Assert.Equal(24, profile.Revision.Length);
    }

    [Theory]
    [InlineData("http://example.test/logo.svg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//example.test/logo.svg")]
    [InlineData("https://user:secret@example.test/logo.svg")]
    public void CreateRejectsUnsafeBrandingUrls(string unsafeUrl)
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateProfile(logoUrl: unsafeUrl));

        Assert.Equal("logoUrl", exception.ParamName);
    }

    [Theory]
    [InlineData("contains spaces")]
    [InlineData("theme/escape")]
    [InlineData("theme.css")]
    public void CreateRejectsThemeKeysOutsideTheAllowList(string themeKey)
    {
        Assert.Throws<ArgumentException>(() => CreateProfile(themeKey: themeKey));
    }

    [Fact]
    public void RevisionChangesWhenPublicBrandingChanges()
    {
        var first = CreateProfile(displayName: "Example Desk");
        var second = CreateProfile(displayName: "Example Studio");

        Assert.NotEqual(first.Revision, second.Revision);
    }

    private static BrandProfile CreateProfile(
        string displayName = "Example Desk",
        string themeKey = "example-light",
        string logoUrl = "/brand/logo.svg",
        string supportUrl = "/support") =>
        BrandProfile.Create(
            displayName,
            "Example",
            "Example Company Ltd.",
            themeKey,
            logoUrl,
            "/brand/favicon.svg",
            supportUrl,
            "/privacy",
            "/terms");
}
