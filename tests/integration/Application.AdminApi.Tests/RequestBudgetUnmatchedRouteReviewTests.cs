using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class RequestBudgetUnmatchedRouteReviewTests(AdminApiTestEnvironment environment)
{
    [Theory]
    [InlineData("GET", "/api/v1/platform/not-an-endpoint", false, HttpStatusCode.NotFound)]
    [InlineData("POST", "/api/v1/platform/tenants/not-a-guid/memberships", true, HttpStatusCode.NotFound)]
    [InlineData("PUT", "/api/v1/platform/access", true, HttpStatusCode.MethodNotAllowed)]
    public async Task UnmatchedOrRejectedRoutesDoNotEnterPlatformAuthority(
        string method,
        string path,
        bool authenticated,
        HttpStatusCode expected)
    {
        await environment.ClearAccessAuditAsync();
        using var factory = environment.CreateFactory(certificate: null);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("X-Platform-Admin", "true");
        request.Headers.Add("X-Admin-Device", "registered");
        request.Headers.Add("traceparent", "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");
        if (authenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", environment.CreateToken());
        }

        using var response = await client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(5));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(body);
        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        await using var auditCount = new NpgsqlCommand(
            "SELECT count(*) FROM platform_administration.access_audit_events", connection);
        Assert.Equal(0L, await auditCount.ExecuteScalarAsync());
    }
}
