using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.CoreApi;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class TenantMembershipPageEndpointTests
{
    [Fact]
    public async Task PagesCurrentAccountMembershipsWithoutChangingTheLegacyArrayContract()
    {
        await using var factory = new WhiteLabelApiFactory();
        var account = Guid.NewGuid();
        var subject = "membership-page-human";
        factory.Bind(subject, account);
        var ids = Enumerable.Range(1, 53).Select(i => Guid.Parse($"00000000-0000-0000-0000-{i:D12}")).ToArray();
        foreach (var id in ids) factory.AddTenantMembership(account, id, "Current tenant");
        factory.AddTenantMembership(Guid.NewGuid(), Guid.NewGuid(), "Another account's tenant");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(subject));
        using var firstResponse = await client.GetAsync("/api/v1/account/tenants/page");
        Assert.True(firstResponse.Headers.CacheControl?.NoStore);
        var first = await firstResponse.Content.ReadFromJsonAsync<TenantMembershipPageResponse>();
        Assert.Equal(ids[..50], first!.Items.Select(x => x.TenantId));
        Assert.Equal(ids[49], first.NextTenantId);
        factory.RemoveTenantMembership(account, ids[51]);
        var second = await client.GetFromJsonAsync<TenantMembershipPageResponse>($"/api/v1/account/tenants/page?afterTenantId={first.NextTenantId:D}");
        Assert.Equal(new[] { ids[50], ids[52] }, second!.Items.Select(x => x.TenantId));
        Assert.Null(second.NextTenantId);
        var legacy = await client.GetFromJsonAsync<TenantMembershipResponse[]>("/api/v1/account/tenants");
        Assert.Equal(52, legacy!.Length);
    }

    [Fact]
    public async Task RequiresAuthenticationAndRejectsEmptyContinuation()
    {
        await using var factory = new WhiteLabelApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/tenants/page")).StatusCode);
        factory.Bind("bounded-member", Guid.NewGuid());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken("bounded-member"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/account/tenants/page?afterTenantId={Guid.Empty:D}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/account/tenants/page?afterTenantId=invalid")).StatusCode);
    }
}
