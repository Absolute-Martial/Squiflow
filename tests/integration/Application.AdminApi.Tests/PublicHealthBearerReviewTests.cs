using System.Net;
using System.Net.Http.Headers;
using Application.AdminApi;
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

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class PublicHealthBearerReviewTests(AdminApiTestEnvironment environment)
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
    public async Task PublicHealthDoesNotInvokeDiscovery(string path, bool malformedBearer)
    {
        var discovery = new BlockingDiscovery();
        using var baseline = environment.CreateFactory(certificate: null);
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
            malformedBearer ? "malformed-public-bearer" : environment.CreateToken());
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pending = client.SendAsync(request, caller.Token);
        try
        {
            var completed = await Task.WhenAny(pending, discovery.Entered.Task)
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(discovery.Entered.Task.IsCompleted, "Public health invoked identity-provider discovery.");
            Assert.Same(pending, completed);
            using var response = await pending;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
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
        using var baseline = environment.CreateFactory(certificate: environment.DeviceCertificate);
        using var factory = baseline.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                    options => options.ConfigurationManager = discovery)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/platform/access");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", environment.CreateToken());
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/platform/access");
        if (identity != "missing")
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
                identity == "valid" ? environment.CreateToken() : "malformed-protected-bearer");
        }
        using var response = await client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task UnknownRouteWithBearerRemainsNotFound()
    {
        using var factory = environment.CreateFactory(certificate: null);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/unknown-public-auth-review");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", environment.CreateToken());
        using var response = await client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PublicLivenessDoesNotInvokeBearerValidation()
    {
        var validationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var baseline = environment.CreateFactory(certificate: null);
        using var factory = baseline.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var validate = options.Events.OnTokenValidated;
                    options.Events.OnTokenValidated = async context =>
                    {
                        await validate(context).ConfigureAwait(false);
                        validationEntered.TrySetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, context.HttpContext.RequestAborted)
                            .ConfigureAwait(false);
                    };
                })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", environment.CreateToken());
        using var callerBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pending = client.SendAsync(request, callerBudget.Token);
        try
        {
            var completed = await Task.WhenAny(pending, validationEntered.Task)
                .WaitAsync(Watchdog);
            Assert.False(validationEntered.Task.IsCompleted,
                "Public liveness entered JWT validation and is waiting on its dependency outside the protected deadline.");
            Assert.Same(pending, completed);
            using var response = await pending;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
        finally
        {
            callerBudget.Cancel();
            try
            {
                using var response = await pending.WaitAsync(Watchdog);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public async Task PublicAuthenticationSkipRequiresUnprotectedClassification(
        bool classifiedPublic, bool requiresAuthorization, bool expectedSkip)
    {
        using var factory = environment.CreateFactory(certificate: null);
        using var client = factory.CreateClient();
        var options = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var metadata = new List<object>();
        if (classifiedPublic)
        {
            metadata.Add(new AdminEndpointAccessMetadata(AdminEndpointAccess.PublicHealth));
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
