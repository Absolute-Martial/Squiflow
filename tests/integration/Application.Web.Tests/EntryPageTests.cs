using System.Net;
using System.Net.Http.Json;
using Application.Branding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.Web.Tests;

public sealed class EntryPageTests
{
    [Fact]
    public async Task RendersOnlyConfiguredBrandingWithSafeRelativeLinksAndSecurityHeaders()
    {
        await using var factory = new EntryFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fieldwork Services", html, StringComparison.Ordinal);
        Assert.Contains("https://api.example.com/support", html, StringComparison.Ordinal);
        Assert.Contains("Service available", html, StringComparison.Ordinal);
        Assert.Contains("does not sign you in", html, StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        var policies = string.Join("; ", response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self'", policies, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policies, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", policies, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EncodesUntrustedNamesAsTextInsteadOfMarkup()
    {
        await using var factory = new EntryFactory("<script>alert(1)</script>");
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(503, "Service is getting ready")]
    [InlineData(500, "Unable to connect")]
    public async Task ReadinessFailureNeverRendersAvailableOrFalseAccess(int status, string heading)
    {
        await using var factory = new EntryFactory(readiness: status);
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        Assert.Contains(heading, html, StringComparison.Ordinal);
        Assert.DoesNotContain("Service available", html, StringComparison.Ordinal);
        Assert.DoesNotContain("private provider diagnostic", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachPageLoadChecksTheCurrentServiceAfterAnOutage()
    {
        await using var factory = new EntryFactory();
        using var client = factory.CreateClient();
        Assert.Contains("Service available", await client.GetStringAsync("/"), StringComparison.Ordinal);
        factory.Readiness = 503;
        Assert.Contains("Service is getting ready", await client.GetStringAsync("/"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnexpectedRenderingFailureKeepsTheRecoveryPageSafeAndStyled()
    {
        await using var factory = new EntryFactory(failRendering: true);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("This page is unavailable.", html, StringComparison.Ordinal);
        Assert.Contains("/tokens.css", html, StringComparison.Ordinal);
        Assert.Contains("Return to application entry", html, StringComparison.Ordinal);
        Assert.DoesNotContain("private rendering diagnostic", html, StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private sealed class EntryFactory(string name = "Fieldwork", int readiness = 200, bool failRendering = false) : WebApplicationFactory<Program>
    {
        public int Readiness { get; set; } = readiness;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ApplicationEntry:CoreApiBaseUrl", "https://api.example.com");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<HttpClient>();
                services.AddSingleton(new HttpClient(new EntryHandler(this, name)));
                if (failRendering)
                {
                    services.RemoveAll<Application.ApplicationEntry.ApplicationEntryClient>();
                    services.AddSingleton<Application.ApplicationEntry.ApplicationEntryClient>(_ => throw new InvalidOperationException("private rendering diagnostic"));
                }
            });
        }
    }

    private sealed class EntryHandler(EntryFactory factory, string name) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = request.RequestUri!.AbsolutePath == "/api/v1/application/bootstrap"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(BrandProfile.Create(name, name, "Fieldwork Services", "default",
                        "/logo.svg", "/favicon.svg", "/support", "/privacy", "/terms"))
                }
                : new HttpResponseMessage((HttpStatusCode)factory.Readiness) { Content = new StringContent("private provider diagnostic") };
            return Task.FromResult(response);
        }
    }
}
