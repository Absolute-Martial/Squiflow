using System.Net;
using Application.Entry.TestSupport;
using Application.Web.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessPageTests
{
    [Fact]
    public async Task IdentityVerificationRequiresCurrentSessionCsrfAndConfiguredAssuranceAndUsesFixedLocalReturn()
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        using var anonymous = await client.PostAsync("/auth/verify-identity", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        await LoginAsync(factory, client);
        var route = $"/workspace/{factory.Protocol.FirstTenant:D}/billing";
        var page = await client.GetStringAsync(route);
        Assert.Contains("Verify identity for corrections", page, StringComparison.Ordinal);
        var previousSession = factory.LastSession;
        Assert.NotNull(previousSession);
        var tickets = factory.Services.GetRequiredService<WebSessionTickets>();
        Assert.NotNull(tickets.AccessToken(previousSession));
        using var forged = await client.PostAsync("/auth/verify-identity", new FormUrlEncodedContent(new Dictionary<string, string> { ["tenantId"] = factory.Protocol.FirstTenant.ToString("D") }));
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        var form = new Dictionary<string, string> { ["tenantId"] = factory.Protocol.FirstTenant.ToString("D"), ["__RequestVerificationToken"] = AntiForgery(page) };
        form["returnUrl"] = "https://untrusted.example.test/steal";
        using var arbitraryReturn = await client.PostAsync("/auth/verify-identity", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.BadRequest, arbitraryReturn.StatusCode);
        form.Remove("returnUrl");
        using var challenge = await client.PostAsync("/auth/verify-identity", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        Assert.Equal("login", query["prompt"].ToString()); Assert.Equal("0", query["max_age"].ToString());
        Assert.Equal("urn:application:assurance:test", query["acr_values"].ToString());
        Assert.Equal(EntryProtocolFixture.Issuer, challenge.Headers.Location.GetLeftPart(UriPartial.Authority));
        Assert.Null(tickets.AccessToken(previousSession));
        // The old server session is invalidated before the external challenge is completed.
        using var oldSession = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Redirect, oldSession.StatusCode);
        factory.Protocol.ObserveAuthorization(challenge.Headers.Location);
        using var callback = await client.GetAsync("/signin-oidc?code=synthetic-code&state=" + Uri.EscapeDataString(query["state"].ToString()));
        Assert.Equal(route, callback.Headers.Location!.OriginalString);
        using var signedIn = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }

    [Fact]
    public async Task MissingStrongAuthenticationConfigurationDoesNotStartAWeakerChallenge()
    {
        await using var factory = new BusinessFactory { StrongAuthenticationAcr = null };
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        await LoginAsync(factory, client);
        var page = await client.GetStringAsync($"/workspace/{factory.Protocol.FirstTenant:D}/billing");
        Assert.DoesNotContain("Verify identity for corrections</button>", page, StringComparison.Ordinal);
        Assert.Contains("Fresh identity verification is unavailable", page, StringComparison.Ordinal);
        using var response = await client.PostAsync("/auth/verify-identity", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["tenantId"] = factory.Protocol.FirstTenant.ToString("D"),
            ["__RequestVerificationToken"] = AntiForgery(page),
        }));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }
}
