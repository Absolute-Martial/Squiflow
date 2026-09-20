using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.CoreApi;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class TenantMembershipEndpointTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;
    private readonly HttpClient _client;

    public TenantMembershipEndpointTests(WhiteLabelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListsOnlyMembershipsResolvedForTheAuthenticatedAccount()
    {
        var accountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("tenant-list-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Current Tenant");
        _factory.AddTenantMembership(otherAccountId, Guid.NewGuid(), "Other Account Tenant");

        using var request = AuthenticatedRequest(_factory.CreateToken("tenant-list-subject"));
        using var response = await _client.SendAsync(request);
        var memberships = await response.Content.ReadFromJsonAsync<TenantMembershipResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var membership = Assert.Single(Assert.IsType<TenantMembershipResponse[]>(memberships));
        Assert.Equal(new TenantMembershipResponse(tenantId, "Current Tenant"), membership);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task ValidButUnboundIdentityCannotDiscoverTenantNames()
    {
        using var request = AuthenticatedRequest(_factory.CreateToken("tenant-unbound-subject"));
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Tenant", body, StringComparison.Ordinal);
    }

    private static HttpRequestMessage AuthenticatedRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account/tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
