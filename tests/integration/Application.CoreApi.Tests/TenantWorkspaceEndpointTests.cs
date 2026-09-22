using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class TenantWorkspaceEndpointTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;
    private readonly HttpClient _client;

    public TenantWorkspaceEndpointTests(WhiteLabelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CurrentMemberWithPermissionCanViewTenantWorkspace()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("workspace-allowed-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Allowed Tenant");
        _factory.SetWorkspaceDecision(accountId, tenantId, allowed: true);

        using var request = AuthenticatedRequest(
            tenantId,
            _factory.CreateToken("workspace-allowed-subject"));
        using var response = await _client.SendAsync(request);
        var workspace = await response.Content.ReadFromJsonAsync<TenantWorkspaceContract>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(new TenantWorkspaceContract(tenantId, "Allowed Tenant"), workspace);
        Assert.Equal(1, _factory.GetWorkspaceCheckCount(accountId, tenantId));
    }

    [Fact]
    public async Task PermissionDenialFailsClosed()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("workspace-denied-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Denied Tenant");
        _factory.SetWorkspaceDecision(accountId, tenantId, allowed: false);

        using var request = AuthenticatedRequest(
            tenantId,
            _factory.CreateToken("workspace-denied-subject"));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenant_permission_denied", await ReadProblemCodeAsync(response));
        Assert.Equal(1, _factory.GetWorkspaceCheckCount(accountId, tenantId));
    }

    [Fact]
    public async Task MissingMembershipIsRejectedBeforeOpenFga()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("workspace-nonmember-subject", accountId);
        _factory.SetWorkspaceDecision(accountId, tenantId, allowed: true);

        using var request = AuthenticatedRequest(
            tenantId,
            _factory.CreateToken("workspace-nonmember-subject"));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenant_access_denied", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetWorkspaceCheckCount(accountId, tenantId));
    }

    [Fact]
    public async Task AnotherAccountsMembershipCannotAuthorizeTheCaller()
    {
        var callerAccountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("workspace-cross-account-subject", callerAccountId);
        _factory.AddTenantMembership(otherAccountId, tenantId, "Other Account Tenant");
        _factory.SetWorkspaceDecision(callerAccountId, tenantId, allowed: true);

        using var request = AuthenticatedRequest(
            tenantId,
            _factory.CreateToken("workspace-cross-account-subject"));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenant_access_denied", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetWorkspaceCheckCount(callerAccountId, tenantId));
    }

    [Fact]
    public async Task ProviderOutageReturnsSafeUnavailableProblem()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("workspace-provider-outage-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Outage Tenant");
        _factory.SetWorkspaceUnavailable(accountId, tenantId);

        using var request = AuthenticatedRequest(
            tenantId,
            _factory.CreateToken("workspace-provider-outage-subject"));
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("authorization_unavailable", await ReadProblemCodeAsync(response, body));
        Assert.DoesNotContain("Synthetic", body, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenFGA", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TenantRouteRequiresAuthentication()
    {
        using var response = await _client.GetAsync($"/api/v1/tenants/{Guid.NewGuid():D}/workspace");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("authentication_required", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task MalformedTenantRouteDoesNotEnterTheTenantPipeline()
    {
        using var response = await _client.GetAsync("/api/v1/tenants/not-a-guid/workspace");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpRequestMessage AuthenticatedRequest(Guid tenantId, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/tenants/{tenantId:D}/workspace");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<string?> ReadProblemCodeAsync(
        HttpResponseMessage response,
        string? body = null)
    {
        body ??= await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed record TenantWorkspaceContract(Guid TenantId, string DisplayName);
}
