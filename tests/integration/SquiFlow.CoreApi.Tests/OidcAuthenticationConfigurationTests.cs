using Microsoft.Extensions.Configuration;
using SquiFlow.CoreApi;
using Xunit;

namespace SquiFlow.CoreApi.Tests;

public sealed class OidcAuthenticationConfigurationTests
{
    [Theory]
    [InlineData("http://identity.example.test", "squiflow-core-api")]
    [InlineData("https://identity.example.test?other=true", "squiflow-core-api")]
    [InlineData(" https://identity.example.test", "squiflow-core-api")]
    [InlineData("https://identity.example.test", " squiflow-core-api")]
    public void InvalidTrustConfigurationFailsAtStartup(string authority, string audience)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = authority,
                ["Authentication:Audience"] = audience,
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            OidcAuthenticationConfiguration.From(configuration));
    }

    [Fact]
    public void ExactHttpsTrustConfigurationIsPreserved()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = "https://identity.example.test/oidc/v1",
                ["Authentication:Audience"] = "squiflow-core-api",
            })
            .Build();

        var validated = OidcAuthenticationConfiguration.From(configuration);

        Assert.Equal("https://identity.example.test/oidc/v1", validated.Authority);
        Assert.Equal("squiflow-core-api", validated.Audience);
    }
}
