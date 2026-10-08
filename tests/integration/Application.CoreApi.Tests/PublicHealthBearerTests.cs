using System.Net;
using System.Net.Http.Headers;
using Application.CoreApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class PublicHealthBearerTests
{

    // Test-orchestration watchdog only: it bounds host startup, dependency entry, pipeline
    // completion, response delivery and cleanup draining. The application deadlines under test
    // stay configured on the host; this budget only prevents a loaded parallel run from failing
    // the assertion by starving the harness.
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(30);
    [Theory]
    [InlineData("/health/live", false)]
    [InlineData("/health/live", true)]
    [InlineData("/health/ready", false)]
    [InlineData("/health/ready", true)]
    [InlineData("/api/v1/application/bootstrap", false)]
    [InlineData("/api/v1/application/bootstrap", true)]
    [InlineData("/openapi/v1.json", false)]
    [InlineData("/openapi/v1.json", true)]
    public async Task PublicHealthDoesNotInvokeDiscovery(string path, bool malformedBearer)
    {
        var discovery = new BlockingDiscovery();
        using var baseline = new WhiteLabelApiFactory();
        using var factory = baseline.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                    options => options.ConfigurationManager = discovery)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            malformedBearer ? "malformed-public-bearer" : baseline.CreateToken());
        using var caller = new CancellationTokenSource(Watchdog);
        var pending = client.SendAsync(request, caller.Token);
        try
        {
            var completed = await Task.WhenAny(pending, discovery.Entered.Task)
                .WaitAsync(Watchdog);
            Assert.False(discovery.Entered.Task.IsCompleted, "Public health invoked identity-provider discovery.");
            Assert.Same(pending, completed);
            using var response = await pending;
            Assert.Equal(path == "/health/ready" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK,
                response.StatusCode);
            if (path == "/api/v1/application/bootstrap")
            {
                Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString());
                Assert.NotNull(response.Headers.ETag);
                Assert.Contains("Example Operations", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            }
            else if (path == "/health/ready")
            {
                Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
                Assert.Empty(await response.Content.ReadAsStringAsync());
            }
            else if (path == "/openapi/v1.json")
            {
                Assert.Contains("openapi", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            }
        }
        finally
        {
            caller.Cancel();
            if (discovery.Entered.Task.IsCompleted)
            {
                await discovery.Exited.Task.WaitAsync(Watchdog);
            }
            try
            {
                using var response = await pending.WaitAsync(Watchdog);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Fact]
    public async Task ProtectedRouteStillInvokesDiscoveryAndHonorsCallerCancellation()
    {
        var discovery = new BlockingDiscovery();
        using var baseline = new WhiteLabelApiFactory();
        using var factory = baseline.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                    options => options.ConfigurationManager = discovery)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseline.CreateToken());
        using var caller = new CancellationTokenSource(Watchdog);
        var pending = client.SendAsync(request, caller.Token);
        try
        {
            await discovery.Entered.Task.WaitAsync(Watchdog);
        }
        finally
        {
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Watchdog));
            await discovery.Exited.Task.WaitAsync(Watchdog);
        }
    }

    [Theory]
    [InlineData("valid", HttpStatusCode.OK)]
    [InlineData("invalid", HttpStatusCode.Unauthorized)]
    [InlineData("missing", HttpStatusCode.Unauthorized)]
    public async Task ProtectedIdentityContractIsPreserved(string identity, HttpStatusCode expected)
    {
        using var factory = new WhiteLabelApiFactory();
        factory.Bind("subject-42", Guid.NewGuid());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account");
        if (identity != "missing")
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
                identity == "valid" ? factory.CreateToken() : "malformed-protected-bearer");
        }
        using var response = await client.SendAsync(request).WaitAsync(Watchdog);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task UnknownRouteWithBearerRemainsNotFound()
    {
        using var factory = new WhiteLabelApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/unknown-public-auth-review");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        using var response = await client.SendAsync(request).WaitAsync(Watchdog);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public async Task PublicAuthenticationSkipRequiresUnprotectedClassification(
        bool classifiedPublic, bool requiresAuthorization, bool expectedSkip)
    {
        using var factory = new WhiteLabelApiFactory();
        using var client = factory.CreateClient();
        var options = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var metadata = new List<object>();
        if (classifiedPublic)
        {
            metadata.Add(new EndpointAccessMetadata(EndpointAccess.PublicLiveness));
        }
        if (requiresAuthorization)
        {
            metadata.Add(new AuthorizeAttribute());
        }
        var http = new DefaultHttpContext();
        http.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(metadata), "public-auth-metadata-review"));
        var context = new MessageReceivedContext(http,
            new Microsoft.AspNetCore.Authentication.AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)), options);
        await options.Events.OnMessageReceived(context);
        if (expectedSkip)
        {
            Assert.NotNull(context.Result);
            Assert.True(context.Result.None);
        }
        else
        {
            Assert.Null(context.Result);
        }
    }

    private sealed class BlockingDiscovery : IConfigurationManager<OpenIdConnectConfiguration>
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
        {
            Entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancel).ConfigureAwait(false);
                throw new InvalidOperationException("Discovery must end through cancellation.");
            }
            finally
            {
                Exited.TrySetResult();
            }
        }

        public void RequestRefresh() { }
    }
}
