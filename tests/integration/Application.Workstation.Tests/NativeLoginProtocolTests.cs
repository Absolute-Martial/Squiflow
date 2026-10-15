using System.Net;
using System.Net.Sockets;
using Application.Entry.TestSupport;
using Application.Workstation.Authentication;
using Duende.IdentityModel.OidcClient.Browser;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Application.Workstation.Tests;

public sealed class NativeLoginProtocolTests
{
    [Theory]
    [InlineData(false, false, false, false, true)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    public async Task UsesRealLoopbackPkceAndValidatesSignedIdentity(bool nonce, bool audience, bool signature, bool wrongState, bool succeeds)
    {
        using var protocol = new EntryProtocolFixture { InvalidNonce = nonce, InvalidAudience = audience, InvalidSignature = signature };
        using var discoveryHttp = new HttpClient(protocol, false);
        var discovery = await discoveryHttp.GetDiscoveryDocumentAsync(EntryProtocolFixture.Issuer);
        Assert.False(discovery.IsError, discovery.Error);
        var opened = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var login = new NativeSignIn(new(new(EntryProtocolFixture.Issuer), new("https://api.example.test"), protocol.ClientId, EntryProtocolFixture.ApiScope),
            uri => { protocol.ObserveAuthorization(uri); opened.SetResult(uri); return Task.CompletedTask; }, () => new HttpClient(protocol, false));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = login.SignInAsync(cancellation.Token);
        var authorization = await opened.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var query = QueryHelpers.ParseQuery(authorization.Query);
        Assert.Equal("S256", query["code_challenge_method"]);
        var callback = new Uri(query["redirect_uri"].ToString());
        using var browser = new HttpClient();
        using var response = await browser.GetAsync(callback.AbsoluteUri + "?code=synthetic-code&state=" + Uri.EscapeDataString(wrongState ? "untrusted-state" : query["state"].ToString()), cancellation.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(succeeds, result is not null);
        Assert.Equal(!wrongState, protocol.PkceVerified);
        Assert.False(protocol.ClientSecretSeen);
        if (result is not null) Assert.Equal(EntryProtocolFixture.AccessToken, result.AccessToken);
        using var probe = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(() => probe.ConnectAsync(IPAddress.Loopback, callback.Port));
    }

    [Fact]
    public async Task CancelingNativeLoginClosesTheRealTransactionListener()
    {
        using var protocol = new EntryProtocolFixture();
        var opened = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var login = new NativeSignIn(new(new(EntryProtocolFixture.Issuer), new("https://api.example.test"), protocol.ClientId, EntryProtocolFixture.ApiScope),
            uri => { opened.SetResult(uri); return Task.CompletedTask; }, () => new HttpClient(protocol, false));
        using var cancellation = new CancellationTokenSource();
        var pending = login.SignInAsync(cancellation.Token);
        var authorization = await opened.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var callback = new Uri(QueryHelpers.ParseQuery(authorization.Query)["redirect_uri"].ToString());
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        using var probe = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(() => probe.ConnectAsync(IPAddress.Loopback, callback.Port));
    }

    [Fact]
    public async Task CallbackRejectsWrongHostAndPathThenCancellationClosesTheListener()
    {
        using var browser = new LoopbackLoginBrowser(_ => Task.CompletedTask);
        using var cancellation = new CancellationTokenSource();
        var pending = browser.InvokeAsync(new BrowserOptions("https://identity.example.test/authorize", browser.CallbackUri.AbsoluteUri), cancellation.Token);
        using var http = new HttpClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(new Uri(browser.CallbackUri, "/unexpected?code=test"))).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, browser.CallbackUri.AbsoluteUri + "?code=test&state=test");
        request.Headers.Host = "wrong.example.test";
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(request)).StatusCode);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
