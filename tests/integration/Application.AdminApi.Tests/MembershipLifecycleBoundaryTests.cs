using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
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

    [Fact]
    public async Task InvalidRevisionShapesHaveNoDurableEffectsAndDoNotConsumeIdempotencyKeys()
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
        using var invitation = await client.SendAsync(Request(
            $"/api/v1/platform/tenants/{tenantId}/memberships",
            Guid.NewGuid().ToString("N"),
            $$"""{"accountId":"{{accountId}}"}"""));
        Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);
        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();

        foreach (var (path, code, sql, availability) in new[]
        {
            ($"/api/v1/platform/tenants/{tenantId}/memberships/{accountId}/activate",
                "invalid_membership_request",
                """
                SELECT availability, revision,
                    (SELECT count(*) FROM tenancy.membership_lifecycle_receipts
                     WHERE changed_by_principal_id = @principal AND idempotency_key = @key)
                FROM tenancy.memberships WHERE tenant_id = @tenant AND account_id = @account
                """, (short)3),
            ($"/api/v1/platform/tenants/{tenantId}/lifecycle/suspend",
                "invalid_tenant_lifecycle_request",
                """
                SELECT availability, revision,
                    (SELECT count(*) FROM tenancy.tenant_lifecycle_receipts
                     WHERE changed_by_principal_id = @principal AND idempotency_key = @key)
                FROM tenancy.tenants WHERE id = @tenant
                """, (short)1),
        })
        {
            var key = Guid.NewGuid().ToString("N");
            foreach (var payload in new[]
            {
                """{"expectedRevision":"1"}""", """{"expectedRevision":null}""",
                """{"expectedRevision":true}""", """{"expectedRevision":[]}""",
                """{"expectedRevision":{}}""", """{"expectedRevision":0}""",
                """{"expectedRevision":-1}""", """{"expectedRevision":2147483648}""",
                """{"expectedRevision":1.5}""", """{"expectedRevision":1,"expectedRevision":1}""",
                """{"expectedRevision":1,"extra":true}""", "{}",
            })
            {
                using var response = await client.SendAsync(Request(path, key, payload));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
                using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("principal", environment.PrincipalId);
                command.Parameters.AddWithValue("key", key);
                command.Parameters.AddWithValue("tenant", tenantId);
                command.Parameters.AddWithValue("account", accountId);
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(availability, reader.GetInt16(0));
                Assert.Equal(1, reader.GetInt32(1));
                Assert.Equal(0L, reader.GetInt64(2));
            }

            using var corrected = await client.SendAsync(Request(path, key, """{"expectedRevision":1}"""));
            Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
            using var correctedBody = JsonDocument.Parse(await corrected.Content.ReadAsStringAsync());
            Assert.Equal(2, correctedBody.RootElement.GetProperty("revision").GetInt32());
            using var replayed = await client.SendAsync(Request(path, key, """{"expectedRevision":1}"""));
            Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
            using var replayedBody = JsonDocument.Parse(await replayed.Content.ReadAsStringAsync());
            Assert.Equal(2, replayedBody.RootElement.GetProperty("revision").GetInt32());
        }
    }

    [Fact]
    public async Task RegisteredDeviceAuthorityPrecedesLifecycleBodyParsing()
    {
        using var factory = environment.CreateFactory(certificate: null);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", environment.CreateToken());
        var tenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        foreach (var path in new[]
        {
            $"/api/v1/platform/tenants/{tenantId}/memberships/{accountId}/activate",
            $"/api/v1/platform/tenants/{tenantId}/lifecycle/suspend",
        })
        {
            foreach (var payload in new[] { "{", """{"expectedRevision":"1"}""" })
            {
                using var response = await client.SendAsync(Request(path, Guid.NewGuid().ToString("N"), payload));
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal("admin_device_required", problem.RootElement.GetProperty("code").GetString());
                Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            }
        }
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
