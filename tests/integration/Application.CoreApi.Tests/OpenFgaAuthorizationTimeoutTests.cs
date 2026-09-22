using Application.CoreApi.Authorization;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFga.Sdk.Client;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OpenFgaAuthorizationTimeoutTests
{
    [Fact]
    public async Task ProviderCallCannotExceedConfiguredBudget()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new NeverRespondingHandler());
        using var client = new OpenFgaClient(configuration.ToClientConfiguration(), httpClient);
        var authorization = new OpenFgaTenantAuthorization(
            client,
            configuration,
            NullLogger<OpenFgaTenantAuthorization>.Instance);

        var started = Stopwatch.StartNew();
        await Assert.ThrowsAsync<AuthorizationProviderUnavailableException>(() =>
            authorization.CanViewAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

        Assert.InRange(started.Elapsed, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task CallerCancellationIsNotReclassifiedAsProviderOutage()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new NeverRespondingHandler());
        using var client = new OpenFgaClient(configuration.ToClientConfiguration(), httpClient);
        var authorization = new OpenFgaTenantAuthorization(
            client,
            configuration,
            NullLogger<OpenFgaTenantAuthorization>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            authorization.CanViewAsync(Guid.NewGuid(), Guid.NewGuid(), cancellation.Token));
    }

    [Fact]
    public async Task SdkRequestCarriesPinnedModelHigherConsistencyAndOpaqueContext()
    {
        var configuration = CreateConfiguration();
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new OpenFgaClient(configuration.ToClientConfiguration(), httpClient);
        var authorization = new OpenFgaTenantAuthorization(
            client,
            configuration,
            NullLogger<OpenFgaTenantAuthorization>.Instance);
        var accountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.True(await authorization.CanViewAsync(accountId, tenantId, CancellationToken.None));

        Assert.Equal(
            "/stores/01ARZ3NDEKTSV4RRFFQ69G5FAV/check",
            handler.RequestUri?.AbsolutePath);
        using var body = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        Assert.Equal(
            "01ARZ3NDEKTSV4RRFFQ69G5FAW",
            body.RootElement.GetProperty("authorization_model_id").GetString());
        Assert.Equal(
            "HIGHER_CONSISTENCY",
            body.RootElement.GetProperty("consistency").GetString());
        var tuple = body.RootElement
            .GetProperty("contextual_tuples")
            .GetProperty("tuple_keys")[0];
        Assert.Equal("user:11111111111111111111111111111111", tuple.GetProperty("user").GetString());
        Assert.Equal("member", tuple.GetProperty("relation").GetString());
        Assert.Equal("tenant:22222222222222222222222222222222", tuple.GetProperty("object").GetString());
    }

    private static OpenFgaAuthorizationConfiguration CreateConfiguration() =>
        OpenFgaAuthorizationConfiguration.From(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:OpenFga:ApiUrl"] = "http://localhost:8080",
                ["Authorization:OpenFga:StoreId"] = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                ["Authorization:OpenFga:AuthorizationModelId"] = "01ARZ3NDEKTSV4RRFFQ69G5FAW",
                ["Authorization:OpenFga:RequestTimeoutSeconds"] = "1",
                ["Authorization:OpenFga:MaximumRetries"] = "1",
                ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
                ["Authorization:OpenFga:CredentialMethod"] = "None",
            })
            .Build());

    private sealed class NeverRespondingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The synthetic handler should only complete by cancellation.");
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        internal Uri? RequestUri { get; private set; }

        internal string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"allowed\":true}", Encoding.UTF8, "application/json"),
            };
        }
    }
}
