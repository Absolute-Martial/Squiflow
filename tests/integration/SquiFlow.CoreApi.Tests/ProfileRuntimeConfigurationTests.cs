using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SquiFlow.CoreApi.Composition;
using Xunit;

namespace SquiFlow.CoreApi.Tests;

public sealed class ProfileRuntimeConfigurationTests
{
    [Fact]
    public void MissingResourceLimitsFailOptionsValidation()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfileRuntimeComposition(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ProfileRuntimeOptions>>().Value);
    }
}
