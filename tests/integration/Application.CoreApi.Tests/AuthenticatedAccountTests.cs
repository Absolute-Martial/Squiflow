using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Application.CoreApi;
using Application.IdentityAccess;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class AuthenticatedAccountTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;
    private readonly HttpClient _client;

    public AuthenticatedAccountTests(WhiteLabelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AccountRequiresAuthentication()
    {
        using var response = await _client.GetAsync("/api/v1/account");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("authentication_required", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnexpectedProtectedEndpointFailureKeepsNoStorePolicy()
    {
        var subject = $"binding-failure-{Guid.NewGuid():N}";
        _factory.FailAccountBindingFor(subject);

        using var request = AuthenticatedRequest(_factory.CreateToken(subject));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("Synthetic account binding failure", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ValidIdentityResolvesOnlyTheStableActiveAccount()
    {
        var accountId = Guid.NewGuid();
        _factory.Bind("active-subject", accountId);

        using var request = AuthenticatedRequest(_factory.CreateToken("active-subject"));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<AuthenticatedAccountResponse>();
        Assert.Equal(accountId, account?.AccountId);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task InvalidTokenContractsAreRejectedBeforeAccountLookup()
    {
        using var otherRsa = RSA.Create(2048);
        var otherKey = new RsaSecurityKey(otherRsa) { KeyId = "untrusted-key" };
        var tokens = new[]
        {
            _factory.CreateToken(issuer: "https://wrong-issuer.example.test"),
            _factory.CreateToken(audience: "wrong-audience"),
            _factory.CreateToken(expires: DateTime.UtcNow.AddMinutes(-2)),
            _factory.CreateToken(signingKey: otherKey),
            _factory.CreateToken(subject: null),
            _factory.CreateToken(subject: new string('s', 256)),
        };

        foreach (var token in tokens)
        {
            using var request = AuthenticatedRequest(token);
            using var response = await _client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain(token, body, StringComparison.Ordinal);
            using var problem = JsonDocument.Parse(body);
            Assert.Equal("authentication_required", problem.RootElement.GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData("unbound-subject", null, "account_not_bound")]
    [InlineData("disabled-subject", AccountAvailability.Disabled, "account_disabled")]
    public async Task AccountStateFailsClosedWithStableProblemCode(
        string subject,
        AccountAvailability? availability,
        string expectedCode)
    {
        if (availability is not null)
        {
            _factory.Bind(subject, Guid.NewGuid(), availability.Value);
        }

        using var request = AuthenticatedRequest(_factory.CreateToken(subject));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void EveryReachableEndpointDeclaresItsAccessClassification()
    {
        var endpoints = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
            Assert.NotNull(endpoint.Metadata.GetMetadata<EndpointAccessMetadata>()));
    }

    private static HttpRequestMessage AuthenticatedRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
