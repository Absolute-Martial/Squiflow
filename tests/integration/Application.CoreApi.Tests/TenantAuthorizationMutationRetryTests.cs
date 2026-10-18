using System.Net;
using System.Text;
using Application.CoreApi.Authorization;
using Application.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenFga.Sdk.Client;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class TenantAuthorizationMutationRetryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedSdkClientNeverRetriesAMutationEvenWhenSharedChecksAllowThreeRetries(bool networkFailure)
    {
        using var handler = new TransientWriteHandler(networkFailure);
        using var transport = new HttpClient(handler);
        var configuration = Configuration();
        Assert.Equal(3, configuration.MaximumRetries);
        using var client = new OpenFgaTenantAuthorizationAdministrationClient(configuration, transport);
        var provider = new OpenFgaTenantAuthorizationAdministrationProvider(client, configuration);
        var result = await provider.EnsureAsync(Proposal(), [], default);
        Assert.Equal(TenantAuthorizationProviderOutcome.Uncertain, result.Outcome);
        Assert.False(result.MutationCompleted);
        Assert.False(result.NoEffectProven);
        Assert.Equal(1, handler.Writes);
        Assert.Equal(0, handler.Checks);
    }

    [Fact]
    public void OrdinarySdkClientIsRejectedBeforeAnyProviderCallAndDiUsesTheOwnedClient()
    {
        var configuration = Configuration();
        using var ordinary = new OpenFgaClient(configuration.ToClientConfiguration());
        Assert.Throws<InvalidOperationException>(() => new OpenFgaTenantAuthorizationAdministrationProvider(ordinary, configuration));
        var services = new ServiceCollection();
        services.AddCoreApiAuthorization(configuration);
        using var container = services.BuildServiceProvider();
        Assert.IsType<OpenFgaTenantAuthorizationAdministrationClient>(container.GetRequiredService<OpenFgaTenantAuthorizationAdministrationClient>());
        Assert.IsType<OpenFgaTenantAuthorizationAdministrationProvider>(container.GetRequiredService<ITenantAuthorizationAdministrationProvider>());
        Assert.Equal(3, configuration.ToClientConfiguration().MaxRetry);
    }

    private static OpenFgaAuthorizationConfiguration Configuration() => OpenFgaAuthorizationConfiguration.From(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authorization:OpenFga:ApiUrl"] = "http://127.0.0.1:8080",
            ["Authorization:OpenFga:StoreId"] = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            ["Authorization:OpenFga:AuthorizationModelId"] = "01ARZ3NDEKTSV4RRFFQ69G5FAW",
            ["Authorization:OpenFga:RequestTimeoutSeconds"] = "5",
            ["Authorization:OpenFga:MaximumRetries"] = "3",
            ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
            ["Authorization:OpenFga:CredentialMethod"] = "None",
        }).Build());

    private static TenantAuthorizationProposal Proposal() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "no-sdk-write-retry", new string('A', 64), TenantAuthorizationProposalKind.GrantPermission,
        TenantAuthorizationProposalStatus.Pending, 1, null, Guid.NewGuid(), "invoices.issue", null, null, null,
        [], [], 0, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class TransientWriteHandler(bool networkFailure) : HttpMessageHandler
    {
        internal int Writes { get; private set; }
        internal int Checks { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/write", StringComparison.Ordinal))
            {
                Writes++;
                if (Writes == 1)
                {
                    if (networkFailure) throw new HttpRequestException("Synthetic transport loss after an unknown remote effect");
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    { Content = new StringContent("{\"code\":\"internal_error\",\"message\":\"synthetic failure\"}", Encoding.UTF8, "application/json") });
                }
                // A retry would falsely look successful; the test must never reach this response.
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
            }
            Checks++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"allowed\":true}", Encoding.UTF8, "application/json") });
        }
    }
}
