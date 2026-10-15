using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Application.Branding;
using Application.Entry.TestSupport;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.Web.Tests;

public sealed class LoginProtocolTests
{
    [Fact]
    public async Task RealCodePkceCookieFlowKeepsTokensServerSideAndLogoutRemovesSession()
    {
        await using var factory = new LoginFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/auth/login", new FormUrlEncodedContent([]))).StatusCode);
        var callback = await CompleteLoginAsync(factory, client);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/workspace", callback.Headers.Location!.OriginalString);
        Assert.True(factory.Protocol.PkceVerified);
        Assert.True(factory.Protocol.ClientSecretSeen);
        var cookie = string.Join(";", callback.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-Application.Session=", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EntryProtocolFixture.AccessToken, cookie, StringComparison.Ordinal);
        var page = await client.GetStringAsync("/workspace");
        Assert.Contains("North workspace", page, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;South workspace&lt;/script&gt;", page, StringComparison.Ordinal);
        Assert.DoesNotContain(EntryProtocolFixture.AccessToken, page, StringComparison.Ordinal);
        Assert.DoesNotContain("test-confidential-secret", page, StringComparison.Ordinal);
        var token = AntiForgery(page);
        using var logout = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/workspace")).StatusCode);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task RejectsInvalidNonceAudienceAndSignature(bool nonce, bool audience, bool signature)
    {
        await using var factory = new LoginFactory();
        factory.Protocol.InvalidNonce = nonce; factory.Protocol.InvalidAudience = audience; factory.Protocol.InvalidSignature = signature;
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        using var response = await CompleteLoginAsync(factory, client);
        Assert.Equal("/login-result?status=failed", response.Headers.Location!.OriginalString);
        Assert.DoesNotContain("__Host-Application.Session=", string.Join(';', response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : []), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/workspace")).StatusCode);
    }

    [Fact]
    public async Task RejectsCallbackWithoutTransactionAndLoginFromWrongCanonicalOrigin()
    {
        await using var factory = new LoginFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        var badCallback = await client.GetAsync("/signin-oidc?code=synthetic-code&state=untrusted");
        Assert.Equal("/login-result?status=failed", badCallback.Headers.Location!.OriginalString);
        using var wrongHost = new HttpRequestMessage(HttpMethod.Post, "https://other.example.test/auth/login") { Content = new FormUrlEncodedContent([]) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(wrongHost)).StatusCode);
    }

    [Fact]
    public async Task EmptyMembershipAndDisabledAccountNeverRenderAnEnteredWorkspace()
    {
        await using var factory = new LoginFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        using var loggedIn = await CompleteLoginAsync(factory, client);
        factory.Protocol.NoMemberships = true;
        Assert.Contains("No workspaces are available", await client.GetStringAsync("/workspace"), StringComparison.Ordinal);
        factory.Protocol.DisabledAccount = true;
        var disabled = await client.GetStringAsync("/workspace");
        Assert.Contains("cannot access an active application account", disabled, StringComparison.Ordinal);
        Assert.DoesNotContain("Workspace access confirmed", disabled, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> CompleteLoginAsync(LoginFactory factory, HttpClient client)
    {
        var page = await client.GetStringAsync("/");
        using var begin = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = AntiForgery(page) }));
        Assert.Equal(HttpStatusCode.Redirect, begin.StatusCode);
        var authorization = begin.Headers.Location!;
        factory.Protocol.ObserveAuthorization(authorization);
        var query = QueryHelpers.ParseQuery(authorization.Query);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.DoesNotContain("offline_access", query["scope"].ToString(), StringComparison.Ordinal);
        return await client.GetAsync("/signin-oidc?code=synthetic-code&state=" + Uri.EscapeDataString(query["state"].ToString()));
    }

    private static string AntiForgery(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    private sealed class LoginFactory : WebApplicationFactory<Program>
    {
        internal EntryProtocolFixture Protocol { get; } = new();
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
            builder.ConfigureServices(services =>
            {
                services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options => options.Backchannel = new HttpClient(Protocol, false));
                services.RemoveAll<HttpClient>();
                services.AddSingleton(new HttpClient(new PublicAndProtectedHandler(Protocol)));
            });
        }
    }

    private sealed class PublicAndProtectedHandler(EntryProtocolFixture protocol) : DelegatingHandler(protocol)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            request.RequestUri!.AbsolutePath == "/api/v1/application/bootstrap"
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(BrandProfile.Create("Fieldwork", "Fieldwork", "Fieldwork Services", "default", "/logo.svg", "/favicon.svg", "/support", "/privacy", "/terms")) })
                : base.SendAsync(request, cancellationToken);
    }
}
