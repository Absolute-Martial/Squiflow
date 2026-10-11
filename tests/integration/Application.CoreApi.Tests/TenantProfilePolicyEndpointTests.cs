using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Application.CoreApi.Authorization;
using Application.Profiles;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class TenantProfilePolicyEndpointTests
{
    [Fact]
    public async Task PolicyReadsAndCommandsRequireTheirOwnCurrentPermissionBeforeStoreAccess()
    {
        using var factory = new WhiteLabelApiFactory();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        factory.Bind(accountId.ToString("D"), accountId);
        factory.AddTenantMembership(accountId, tenantId, "Profile policy tenant");
        factory.SetProfilePolicyDecision(accountId, tenantId, TenantProfilePolicyPermission.View, allowed: true);

        using var client = factory.CreateClient();
        var path = $"/api/v1/tenants/{tenantId:D}/profile-policy";
        using var read = new HttpRequestMessage(HttpMethod.Get, path);
        read.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(accountId.ToString("D")));
        using var readResponse = await client.SendAsync(read);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.True(readResponse.Headers.CacheControl?.NoStore);
        Assert.Equal(1, factory.GetProfilePolicyStoreCallCount);
        var policy = await readResponse.Content.ReadFromJsonAsync<TenantPolicyState>();
        Assert.Equal(tenantId, policy?.TenantId);
        Assert.Equal(0, policy?.Revision);
        Assert.False(policy?.RequireReferenceForProgramOrders);

        using var edit = Request(HttpMethod.Put, path, accountId, factory,
            "policy-edit", "{\"expectedRevision\":0,\"requireReferenceForProgramOrders\":true}");
        using var editResponse = await client.SendAsync(edit);
        Assert.Equal(HttpStatusCode.Forbidden, editResponse.StatusCode);
        Assert.Equal(1, factory.GetProfilePolicyStoreCallCount);

        using var publish = Request(HttpMethod.Post, path + "/publish", accountId, factory,
            "policy-publish", "{\"expectedRevision\":1}");
        using var publishResponse = await client.SendAsync(publish);
        Assert.Equal(HttpStatusCode.Forbidden, publishResponse.StatusCode);
        Assert.Equal(1, factory.GetProfilePolicyStoreCallCount);
    }

    private static HttpRequestMessage Request(
        HttpMethod method,
        string path,
        Guid accountId,
        WhiteLabelApiFactory factory,
        string idempotencyKey,
        string json)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(accountId.ToString("D")));
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }
}
