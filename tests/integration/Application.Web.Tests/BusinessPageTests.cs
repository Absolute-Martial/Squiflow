using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Application.Branding;
using Application.Entry.TestSupport;
using Application.Web.Business;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessPageTests
{
    [Theory]
    [InlineData("customers")]
    [InlineData("customers/individuals")]
    [InlineData("catalog")]
    [InlineData("pricing")]
    [InlineData("orders")]
    [InlineData("billing")]
    [InlineData("billing/payments/20000000-0000-0000-0000-000000000001")]
    [InlineData("billing/invoices/20000000-0000-0000-0000-000000000001")]
    public async Task BusinessRoutesRequireTheExistingSessionAndDoNotAcceptHttpWrites(string resource)
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        var route = $"/workspace/{factory.Protocol.FirstTenant:D}/{resource}";
        using var anonymous = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        using var forged = await client.PostAsync(route, JsonContent.Create(new { displayName = "Forged" }));
        Assert.False(forged.IsSuccessStatusCode);
        Assert.Empty(factory.BusinessRequests);
    }

    [Fact]
    public async Task ActualPipelineChecksTenantBeforeLoadingBusinessRecordsAndEncodesNames()
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        await LoginAsync(factory, client);
        var route = $"/workspace/{factory.Protocol.FirstTenant:D}/customers";
        using var response = await client.GetAsync(route);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("&lt;script&gt;Customer name&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>Customer name</script>", html, StringComparison.Ordinal);
        Assert.Contains("Create an organization", html, StringComparison.Ordinal);
        Assert.Contains($"/workspace/{factory.Protocol.FirstTenant:D}/catalog", html, StringComparison.Ordinal);
        Assert.DoesNotContain(EntryProtocolFixture.AccessToken, html, StringComparison.Ordinal);
        Assert.DoesNotContain("test-confidential-secret", html, StringComparison.Ordinal);
        Assert.Single(factory.BusinessRequests);
        factory.Protocol.DenyWorkspace = true;
        var denied = await client.GetStringAsync($"/workspace/{factory.Protocol.SecondTenant:D}/customers");
        Assert.Contains("Workspace unavailable", denied, StringComparison.Ordinal);
        Assert.DoesNotContain("Customer name", denied, StringComparison.Ordinal);
        Assert.DoesNotContain("Create an organization", denied, StringComparison.Ordinal);
        Assert.Single(factory.BusinessRequests);
    }

    [Fact]
    public async Task PermissionDenialDoesNotGrantOrHideIndependentCreateAndWrongTenantDataIsRejected()
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        await LoginAsync(factory, client);
        factory.DenyReads = true;
        var html = await client.GetStringAsync($"/workspace/{factory.Protocol.FirstTenant:D}/customers");
        Assert.Contains("Each operation needs its own permission", html, StringComparison.Ordinal);
        Assert.Contains("Create an organization", html, StringComparison.Ordinal);
        Assert.DoesNotContain("private permission diagnostic", html, StringComparison.Ordinal);
        factory.DenyReads = false; factory.WrongTenant = true;
        html = await client.GetStringAsync($"/workspace/{factory.Protocol.FirstTenant:D}/customers");
        Assert.Contains("unsupported response", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Customer name", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogoutAndCanonicalOriginChecksApplyToEveryBusinessRoute()
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        await LoginAsync(factory, client);
        var route = $"/workspace/{factory.Protocol.FirstTenant:D}/customers";
        var page = await client.GetStringAsync(route);
        using var forgedLogout = await client.PostAsync("/auth/logout", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, forgedLogout.StatusCode);
        using var wrongOrigin = new HttpRequestMessage(HttpMethod.Get, "https://other.example.test" + route);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(wrongOrigin)).StatusCode);
        using var logout = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = AntiForgery(page) }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task InteractiveBusinessTransportRejectsForeignOrigins()
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        await LoginAsync(factory, client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/_blazor/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", "https://untrusted.example.test");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.BusinessRequests);
        using var sameOrigin = new HttpRequestMessage(HttpMethod.Post, "/_blazor/negotiate?negotiateVersion=1");
        sameOrigin.Headers.Add("Origin", "https://web.example.test");
        using var allowed = await client.SendAsync(sameOrigin);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    private static async Task LoginAsync(BusinessFactory factory, HttpClient client)
    {
        var page = await client.GetStringAsync("/");
        using var begin = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = AntiForgery(page) }));
        Assert.Equal(HttpStatusCode.Redirect, begin.StatusCode);
        factory.Protocol.ObserveAuthorization(begin.Headers.Location!);
        var query = QueryHelpers.ParseQuery(begin.Headers.Location!.Query);
        using var callback = await client.GetAsync("/signin-oidc?code=synthetic-code&state=" + Uri.EscapeDataString(query["state"].ToString()));
        Assert.Equal("/workspace", callback.Headers.Location!.OriginalString);
    }
    private static string AntiForgery(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    private sealed class BusinessFactory : WebApplicationFactory<Program>
    {
        internal EntryProtocolFixture Protocol { get; } = new();
        internal List<string> BusinessRequests { get; } = [];
        internal string? LastSession { get; private set; }
        internal bool DenyReads { get; set; }
        internal bool WrongTenant { get; set; }
        internal string? StrongAuthenticationAcr { get; set; } = "urn:application:assurance:test";
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ApplicationEntry:CoreApiBaseUrl", "https://api.example.test");
            builder.UseSetting("Authentication:Enabled", "true");
            builder.UseSetting("Authentication:Authority", EntryProtocolFixture.Issuer);
            builder.UseSetting("Authentication:PublicOrigin", "https://web.example.test");
            builder.UseSetting("Authentication:ClientId", Protocol.ClientId);
            builder.UseSetting("Authentication:ClientSecret", "test-confidential-secret");
            builder.UseSetting("Authentication:ApiScope", EntryProtocolFixture.ApiScope);
            if (StrongAuthenticationAcr is not null) builder.UseSetting("Authentication:StrongAuthenticationAcr", StrongAuthenticationAcr);
            builder.ConfigureServices(services =>
            {
                services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options => options.Backchannel = new HttpClient(Protocol, false));
                services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
                    Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, options =>
                    {
                        var previous = options.Events.OnValidatePrincipal;
                        options.Events.OnValidatePrincipal = async context =>
                        {
                            await previous(context);
                            LastSession = context.Principal?.FindFirst(Application.Web.Authentication.WebSessionTickets.SessionClaim)?.Value;
                        };
                    });
                services.RemoveAll<HttpClient>();
                services.AddSingleton(new HttpClient(new BusinessHandler(this, Protocol)));
            });
        }
    }
    private sealed class BusinessHandler(BusinessFactory factory, EntryProtocolFixture protocol) : DelegatingHandler(protocol)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/v1/application/bootstrap") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(BrandProfile.Create("Fieldwork", "Fieldwork", "Fieldwork Services", "default", "/logo.svg", "/favicon.svg", "/support", "/privacy", "/terms")) });
            if (path.Contains("/customers/", StringComparison.Ordinal))
            {
                factory.BusinessRequests.Add(path);
                Assert.Equal(EntryProtocolFixture.AccessToken, request.Headers.Authorization?.Parameter);
                if (factory.DenyReads) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("private permission diagnostic") });
                var record = new OrganizationRecord(Guid.Parse("20000000-0000-0000-0000-000000000002"),
                    factory.WrongTenant ? factory.Protocol.SecondTenant : factory.Protocol.FirstTenant,
                    "<script>Customer name</script>", DateTimeOffset.UtcNow);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new OrganizationPage([record], null)) });
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
