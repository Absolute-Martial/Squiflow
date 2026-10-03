using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class MembershipLifecycleBoundaryTests(AdminApiTestEnvironment environment)
{
    [Fact]
    public async Task InitialOwnerAndTenantLifecycleAreDurableReplayableAndAccessEffective()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", environment.CreateToken());

        var accountId = await CreateAccountAsync(client);
        var tenantId = await CreateTenantAsync(client);
        var ownerKey = Guid.NewGuid().ToString("N");
        using var owner = await client.SendAsync(Request(
            $"/api/v1/platform/tenants/{tenantId}/memberships/initial-owner",
            ownerKey,
            $$"""{"accountId":"{{accountId}}"}"""));
        Assert.Equal(HttpStatusCode.Created, owner.StatusCode);
        using var ownerBody = JsonDocument.Parse(await owner.Content.ReadAsStringAsync());
        Assert.True(ownerBody.RootElement.GetProperty("isInitialOwner").GetBoolean());
        Assert.Equal("active", ownerBody.RootElement.GetProperty("availability").GetString());

        using var ownerReplay = await client.SendAsync(Request(
            $"/api/v1/platform/tenants/{tenantId}/memberships/initial-owner",
            ownerKey,
            $$"""{"accountId":"{{accountId}}"}"""));
        Assert.Equal(HttpStatusCode.OK, ownerReplay.StatusCode);

        var suspendKey = Guid.NewGuid().ToString("N");
        using var suspended = await client.SendAsync(Request(
            $"/api/v1/platform/tenants/{tenantId}/lifecycle/suspend",
            suspendKey,
            """{"expectedRevision":1}"""));
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        using var suspendedBody = JsonDocument.Parse(await suspended.Content.ReadAsStringAsync());
        Assert.Equal("suspended", suspendedBody.RootElement.GetProperty("availability").GetString());

        using var reactivated = await client.SendAsync(Request(
            $"/api/v1/platform/tenants/{tenantId}/lifecycle/reactivate",
            Guid.NewGuid().ToString("N"),
            """{"expectedRevision":2}"""));
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.Equal("no-store", reactivated.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task InitialOwnerCannotBeRemovedAndInvalidBodiesCreateNoLifecycleReceipt()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", environment.CreateToken());
        var accountId = await CreateAccountAsync(client);
        var tenantId = await CreateTenantAsync(client);
        using var owner = await client.SendAsync(Request(
            $"/api/v1/platform/tenants/{tenantId}/memberships/initial-owner",
            Guid.NewGuid().ToString("N"),
            $$"""{"accountId":"{{accountId}}"}"""));
        Assert.Equal(HttpStatusCode.Created, owner.StatusCode);

        using var removal = await client.SendAsync(Request(
            $"/api/v1/platform/tenants/{tenantId}/memberships/{accountId}/remove",
            Guid.NewGuid().ToString("N"),
            """{"expectedRevision":1}"""));
        Assert.Equal(HttpStatusCode.Conflict, removal.StatusCode);
        using var body = JsonDocument.Parse(await removal.Content.ReadAsStringAsync());
        Assert.Equal("initial_owner_protected", body.RootElement.GetProperty("code").GetString());

        using var invalid = await client.SendAsync(Request(
            $"/api/v1/platform/tenants/{tenantId}/lifecycle/suspend",
            Guid.NewGuid().ToString("N"),
            """{"expectedRevision":1,"extra":true}"""));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client)
    {
        using var response = await client.SendAsync(Request(
            "/api/v1/platform/accounts",
            Guid.NewGuid().ToString("N"),
            $$"""{"subject":"member-{{Guid.NewGuid():N}}"}"""));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accountId").GetGuid();
    }

    private static async Task<Guid> CreateTenantAsync(HttpClient client)
    {
        using var response = await client.SendAsync(Request(
            "/api/v1/platform/tenants",
            Guid.NewGuid().ToString("N"),
            $$"""{"displayName":"Tenant {{Guid.NewGuid():N}}"}"""));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("tenantId").GetGuid();
    }

    private static HttpRequestMessage Request(string path, string key, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }
}
