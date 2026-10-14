using System.Net;
using System.Text.Json;
using Application.ApplicationEntry;
using Application.Branding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Application.ApplicationEntry.Tests;

public sealed class ApplicationEntryClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly BrandProfile Brand = BrandProfile.Create("Fieldwork", "Fieldwork", "Fieldwork Services",
        "default", "/logo.svg", "/favicon.svg", "/support", "https://example.com/privacy", "/terms");

    [Theory]
    [InlineData("https://app.example.com", true)]
    [InlineData("https://app.example.com/", true)]
    [InlineData("http://localhost:5000", true)]
    [InlineData("http://127.0.0.1:5000", true)]
    [InlineData("http://[::1]:5000", true)]
    [InlineData("http://app.example.com", false)]
    [InlineData("https://user:password@app.example.com", false)]
    [InlineData("https://app.example.com/api", false)]
    [InlineData("https://app.example.com/?token=value", false)]
    [InlineData("https://app.example.com/#fragment", false)]
    [InlineData("file:///tmp/app", false)]
    [InlineData("https://app.example.com\\redirect", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ServiceAddressesRequireASafeOrigin(string? input, bool valid)
    {
        Assert.Equal(valid, ServiceAddress.TryParse(input, out var address));
        Assert.Equal(valid, address is not null);
    }

    [Theory]
    [InlineData(200, ConnectionStatus.Ready)]
    [InlineData(503, ConnectionStatus.NotReady)]
    [InlineData(401, ConnectionStatus.Unavailable)]
    public async Task ChecksRealHttpBrandingAndReadinessWithoutTreatingHealthAsAccess(int readiness, ConnectionStatus expected)
    {
        await using var server = await StartServerAsync(context => context.Response.WriteAsJsonAsync(Brand), readiness);
        using var http = ApplicationEntryClient.CreateHttpClient();
        var address = new Uri(server.Urls.Single());
        var result = await new ApplicationEntryClient(http).CheckAsync(address);

        Assert.Equal(expected, result.Status);
        if (expected == ConnectionStatus.Unavailable)
        {
            Assert.Null(result.Application);
        }
        else
        {
            Assert.Equal(Brand, result.Application!.Brand);
            Assert.Equal(new Uri(address, "/support"), result.Application.SupportUrl);
        }
        Assert.Contains(expected == ConnectionStatus.Ready ? "does not sign you in" : "service", result.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("application/json", ConnectionStatus.Ready)]
    [InlineData("APPLICATION/JSON", ConnectionStatus.Ready)]
    [InlineData("text/html", ConnectionStatus.InvalidResponse)]
    public async Task JsonMediaTypesAreCaseInsensitiveAndOtherTypesAreRejected(string mediaType, ConnectionStatus expected)
    {
        await using var server = await StartServerAsync(async context =>
        {
            context.Response.ContentType = mediaType;
            await context.Response.WriteAsync(JsonSerializer.Serialize(Brand, JsonOptions));
        });
        using var http = ApplicationEntryClient.CreateHttpClient();
        var result = await new ApplicationEntryClient(http).CheckAsync(new Uri(server.Urls.Single()));
        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"displayName\":123}")]
    [InlineData("not-json")]
    public async Task MalformedPublicDetailsAreSafeExplicitFailures(string payload)
    {
        await using var server = await StartServerAsync(async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(payload);
        });
        using var http = ApplicationEntryClient.CreateHttpClient();
        var result = await new ApplicationEntryClient(http).CheckAsync(new Uri(server.Urls.Single()));
        Assert.Equal(ConnectionStatus.InvalidResponse, result.Status);
        Assert.Null(result.Application);
        Assert.DoesNotContain(payload, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChunkedOversizedResponsesAreBoundedEvenWithoutContentLength()
    {
        await using var server = await StartServerAsync(async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.StartAsync();
            await context.Response.WriteAsync(new string('x', ApplicationEntryClient.MaximumBootstrapBytes + 1));
        });
        using var http = ApplicationEntryClient.CreateHttpClient();
        var result = await new ApplicationEntryClient(http).CheckAsync(new Uri(server.Urls.Single()));
        Assert.Equal(ConnectionStatus.InvalidResponse, result.Status);
        Assert.Null(result.Application);
    }

    [Theory]
    [InlineData("revision", "invalid-revision")]
    [InlineData("supportUrl", "javascript:alert(1)")]
    [InlineData("supportUrl", "/\\external.example")]
    public async Task UnsafeLinksAndMismatchedRevisionsNeverBecomeBranding(string field, string value)
    {
        var document = JsonSerializer.SerializeToNode(Brand, JsonOptions)!;
        document[field] = value;
        await using var server = await StartServerAsync(async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(document.ToJsonString());
        });
        using var http = ApplicationEntryClient.CreateHttpClient();
        var result = await new ApplicationEntryClient(http).CheckAsync(new Uri(server.Urls.Single()));
        Assert.Equal(ConnectionStatus.InvalidResponse, result.Status);
        Assert.Null(result.Application);
    }

    [Fact]
    public async Task RedirectsAreNotFollowedAndProviderBodiesAreNotDisclosed()
    {
        var redirected = false;
        await using var destination = await StartServerAsync(context =>
        {
            redirected = true;
            return context.Response.WriteAsJsonAsync(Brand);
        });
        await using var source = await StartServerAsync(context =>
        {
            context.Response.Redirect(new Uri(new Uri(destination.Urls.Single()), "/api/v1/application/bootstrap").AbsoluteUri);
            return context.Response.WriteAsync("private provider diagnostic");
        });
        using var http = ApplicationEntryClient.CreateHttpClient();
        var result = await new ApplicationEntryClient(http).CheckAsync(new Uri(source.Urls.Single()));
        Assert.Equal(ConnectionStatus.Unavailable, result.Status);
        Assert.False(redirected);
        Assert.DoesNotContain("private provider diagnostic", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationRemainsCancellationRatherThanDeadlineOrSuccess()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await StartServerAsync(async context =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
        });
        using var http = ApplicationEntryClient.CreateHttpClient();
        using var cancellation = new CancellationTokenSource();
        var pending = new ApplicationEntryClient(http).CheckAsync(new Uri(server.Urls.Single()), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task AServiceThatNeverRespondsHitsTheWholeCheckDeadline()
    {
        await using var server = await StartServerAsync(context => Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted));
        using var http = ApplicationEntryClient.CreateHttpClient();
        var result = await new ApplicationEntryClient(http).CheckAsync(new Uri(server.Urls.Single())).WaitAsync(TimeSpan.FromSeconds(12));
        Assert.Equal(ConnectionStatus.TimedOut, result.Status);
        Assert.Null(result.Application);
    }

    private static async Task<WebApplication> StartServerAsync(Func<HttpContext, Task> bootstrap, int readiness = 200)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var server = builder.Build();
        server.MapGet("/api/v1/application/bootstrap", context => bootstrap(context));
        server.MapGet("/health/ready", () => Results.StatusCode(readiness));
        await server.StartAsync();
        return server;
    }
}
