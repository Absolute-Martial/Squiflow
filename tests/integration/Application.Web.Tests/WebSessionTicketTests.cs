using System.Security.Claims;
using System.Security.Cryptography;
using Application.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Application.Web.Tests;

public sealed class WebSessionTicketTests
{
    [Fact]
    public async Task LogoutExpiryAndRestartLoseTheServerCredential()
    {
        var time = new ManualTime(); var store = new WebSessionTickets(time);
        var key = await store.StoreAsync(Ticket(time.GetUtcNow().AddMinutes(1)));
        Assert.Equal("synthetic-private-token", store.AccessToken(key));
        Assert.Null(new WebSessionTickets(time).AccessToken(key));
        time.Now += TimeSpan.FromMinutes(2);
        Assert.Null(store.AccessToken(key));
        var second = await store.StoreAsync(Ticket(time.GetUtcNow().AddMinutes(1)));
        await store.RemoveAsync(second); Assert.Null(store.AccessToken(second));
    }

    [Fact]
    public async Task CapacityIsBoundedAndRenewalCannotExtendAbsoluteExpiry()
    {
        var time = new ManualTime(); var store = new WebSessionTickets(time);
        var ticket = Ticket(time.GetUtcNow().AddMinutes(1)); var key = await store.StoreAsync(ticket);
        ticket.Properties.ExpiresUtc = time.GetUtcNow().AddHours(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.RenewAsync(key, ticket));
        for (var i = 1; i < WebSessionTickets.MaximumSessions; i++) await store.StoreAsync(Ticket(time.GetUtcNow().AddMinutes(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.StoreAsync(Ticket(time.GetUtcNow().AddMinutes(1))));
        time.Now += TimeSpan.FromMinutes(2);
        Assert.NotNull(await store.RetrieveAsync(await store.StoreAsync(Ticket(time.GetUtcNow().AddMinutes(1)))));
    }

    private static AuthenticationTicket Ticket(DateTimeOffset expires)
    {
        var key = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var properties = new AuthenticationProperties { ExpiresUtc = expires };
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "synthetic-private-token" }]);
        return new(new ClaimsPrincipal(new ClaimsIdentity([new Claim(WebSessionTickets.SessionClaim, key)], "session")), properties, "Cookies");
    }
    private sealed class ManualTime : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
