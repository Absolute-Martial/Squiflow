using Xunit;

namespace Application.IdentityAccess.Tests;

public sealed class ExternalIdentityTests
{
    [Fact]
    public void CreatePreservesTheExactIssuerAndSubjectIdentity()
    {
        var identity = ExternalIdentity.Create(
            "https://identity.example.test/oidc/v1",
            "account-subject-42");

        Assert.Equal("https://identity.example.test/oidc/v1", identity.Issuer);
        Assert.Equal("account-subject-42", identity.Subject);
    }

    [Fact]
    public void CreateDoesNotNormalizeTheSubjectIdentity()
    {
        var identity = ExternalIdentity.Create(
            "https://identity.example.test",
            " subject-with-significant-spaces ");

        Assert.Equal(" subject-with-significant-spaces ", identity.Subject);
    }

    [Theory]
    [InlineData("http://identity.example.test")]
    [InlineData("https://user:secret@identity.example.test")]
    [InlineData("https://identity.example.test?issuer=other")]
    [InlineData(" https://identity.example.test")]
    [InlineData("not-an-issuer")]
    public void CreateRejectsInvalidIssuerShapes(string issuer)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ExternalIdentity.Create(issuer, "subject"));

        Assert.Equal("issuer", exception.ParamName);
    }

    [Fact]
    public void CapabilityRemainsHostProviderAndAuthorizationNeutral()
    {
        var references = typeof(ExternalIdentity).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("OpenFga", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.StartsWith("FSH", StringComparison.Ordinal));
    }
}
