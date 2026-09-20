using Microsoft.Extensions.Configuration;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class RequestHostConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData(" example.test")]
    [InlineData("example.test;EXAMPLE.TEST")]
    [InlineData("example.test;")]
    [InlineData("https://example.test")]
    [InlineData("example.test:443")]
    [InlineData("api.*.example.test")]
    [InlineData("*.192.0.2.10")]
    public void UnsafeOrMalformedAllowedHostsFailStartup(string? allowedHosts)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedHosts"] = allowedHosts,
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            RequestHostConfiguration.Validate(configuration));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("api.example.test;*.tenant.example.test")]
    [InlineData("192.0.2.10")]
    [InlineData("[2001:db8::1]")]
    public void ExactAndBoundedWildcardHostsAreAccepted(string allowedHosts)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedHosts"] = allowedHosts,
            })
            .Build();

        RequestHostConfiguration.Validate(configuration);
    }
}
