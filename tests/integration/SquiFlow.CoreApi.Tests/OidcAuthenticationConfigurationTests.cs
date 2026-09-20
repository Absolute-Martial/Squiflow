using Microsoft.Extensions.Configuration;
using SquiFlow.CoreApi;
using Xunit;

namespace SquiFlow.CoreApi.Tests;

public sealed class OidcAuthenticationConfigurationTests
{
    [Theory]
    [InlineData("http://identity.example.test", "application-core-api")]
    [InlineData("https://identity.example.test?other=true", "application-core-api")]
    [InlineData(" https://identity.example.test", "application-core-api")]
    [InlineData("https://identity.example.test", " application-core-api")]
    public void InvalidTrustConfigurationFailsAtStartup(string authority, string audience)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = authority,
                ["Authentication:Audience"] = audience,
                ["Authentication:BackchannelTimeoutSeconds"] = "10",
                ["Authentication:ClockSkewSeconds"] = "60",
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
                ["Authentication:Audience"] = "application-core-api",
                ["Authentication:BackchannelTimeoutSeconds"] = "12",
                ["Authentication:ClockSkewSeconds"] = "45",
            })
            .Build();

        var validated = OidcAuthenticationConfiguration.From(configuration);

        Assert.Equal("https://identity.example.test/oidc/v1", validated.Authority);
        Assert.Equal("application-core-api", validated.Audience);
        Assert.Equal(TimeSpan.FromSeconds(12), validated.BackchannelTimeout);
        Assert.Equal(TimeSpan.FromSeconds(45), validated.ClockSkew);
    }

    [Theory]
    [InlineData("Authentication:BackchannelTimeoutSeconds", "0")]
    [InlineData("Authentication:BackchannelTimeoutSeconds", "121")]
    [InlineData("Authentication:BackchannelTimeoutSeconds", null)]
    [InlineData("Authentication:ClockSkewSeconds", "-1")]
    [InlineData("Authentication:ClockSkewSeconds", "301")]
    [InlineData("Authentication:ClockSkewSeconds", null)]
    [InlineData("Authentication:ClockSkewSeconds", "not-an-integer")]
    public void InvalidTimingConfigurationFailsAtStartup(string key, string? value)
    {
        var values = new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = "https://identity.example.test",
            ["Authentication:Audience"] = "application-core-api",
            ["Authentication:BackchannelTimeoutSeconds"] = "10",
            ["Authentication:ClockSkewSeconds"] = "60",
        };
        values[key] = value;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            OidcAuthenticationConfiguration.From(configuration));
    }
}
