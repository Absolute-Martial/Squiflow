using Application.CoreApi.Authorization;
using Microsoft.Extensions.Configuration;
using OpenFga.Sdk.Configuration;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OpenFgaAuthorizationConfigurationTests
{
    [Fact]
    public void ExactStoreAndModelAreRequired()
    {
        var values = ValidValues();
        values["Authorization:OpenFga:AuthorizationModelId"] = "";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            OpenFgaAuthorizationConfiguration.From(Build(values)));

        Assert.Contains("AuthorizationModelId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemotePlaintextProviderIsRejected()
    {
        var values = ValidValues();
        values["Authorization:OpenFga:ApiUrl"] = "http://openfga.example.test";

        Assert.Throws<InvalidOperationException>(() =>
            OpenFgaAuthorizationConfiguration.From(Build(values)));
    }

    [Fact]
    public void IgnoredCredentialMaterialIsRejected()
    {
        var values = ValidValues();
        values["Authorization:OpenFga:ApiToken"] = "must-not-be-ignored";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            OpenFgaAuthorizationConfiguration.From(Build(values)));

        Assert.DoesNotContain("must-not-be-ignored", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiTokenCredentialIsMappedWithoutChangingPinnedIdentifiers()
    {
        var values = ValidValues();
        values["Authorization:OpenFga:CredentialMethod"] = "ApiToken";
        values["Authorization:OpenFga:ApiToken"] = "synthetic-token";

        var configuration = OpenFgaAuthorizationConfiguration.From(Build(values));
        var client = configuration.ToClientConfiguration();

        Assert.Equal("01ARZ3NDEKTSV4RRFFQ69G5FAV", client.StoreId);
        Assert.Equal("01ARZ3NDEKTSV4RRFFQ69G5FAW", client.AuthorizationModelId);
        Assert.Equal(1, client.MaxRetry);
        Assert.Equal(100, client.MinWaitInMs);
        var credentials = Assert.IsType<Credentials>(client.Credentials);
        Assert.Equal(CredentialsMethod.ApiToken, credentials.Method);
        Assert.Equal("synthetic-token", credentials.Config?.ApiToken);
    }

    private static Dictionary<string, string?> ValidValues() => new()
    {
        ["Authorization:OpenFga:ApiUrl"] = "http://localhost:8080",
        ["Authorization:OpenFga:StoreId"] = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        ["Authorization:OpenFga:AuthorizationModelId"] = "01ARZ3NDEKTSV4RRFFQ69G5FAW",
        ["Authorization:OpenFga:RequestTimeoutSeconds"] = "3",
        ["Authorization:OpenFga:MaximumRetries"] = "1",
        ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
        ["Authorization:OpenFga:CredentialMethod"] = "None",
    };

    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
