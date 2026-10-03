using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.PlatformAdministration;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class AdminApiBoundaryTests(AdminApiTestEnvironment environment)
{
    [Fact]
    public async Task PlatformAccessRequiresAuthentication()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        using var response = await client.GetAsync("/api/v1/platform/access");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("authentication_required", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PlatformAccessRequiresRegisteredAdminDeviceAndAuditsDenial()
    {
        await environment.ClearAccessAuditAsync();
        using var factory = environment.CreateFactory(certificate: null);
        using var client = CreateClient(factory);
        using var request = AuthenticatedRequest(environment.CreateToken());
        using var response = await client.SendAsync(request);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("admin_device_required", problem.RootElement.GetProperty("code").GetString());
        var audit = await environment.LatestAuditAsync();
        Assert.Equal((short)PlatformAdminAccessAuditOutcome.Denied, audit.Outcome);
        Assert.Equal("admin_device_required", audit.Reason);
        Assert.Null(audit.PrincipalId);
        Assert.Null(audit.DeviceId);
    }

    [Fact]
    public async Task DifferentExternalIdentityCannotUseRegisteredDevice()
    {
        await environment.ClearAccessAuditAsync();
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        using var request = AuthenticatedRequest(environment.CreateToken("different-admin-subject"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var audit = await environment.LatestAuditAsync();
        Assert.Equal((short)PlatformAdminAccessAuditOutcome.Denied, audit.Outcome);
        Assert.Equal("admin_device_not_active", audit.Reason);
        Assert.Null(audit.PrincipalId);
        Assert.Null(audit.DeviceId);
    }

    [Fact]
    public async Task ExactIdentityRegisteredDeviceAndPinnedOpenFgaPermissionAllowAccessAndAudit()
    {
        await environment.ClearAccessAuditAsync();
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        using var request = AuthenticatedRequest(environment.CreateToken());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<PlatformAdminAccessResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(environment.PrincipalId, body?.PrincipalId);
        Assert.Equal(environment.DeviceId, body?.DeviceId);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var audit = await environment.LatestAuditAsync();
        Assert.Equal((short)PlatformAdminAccessAuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("authorized", audit.Reason);
        Assert.Equal(environment.PrincipalId, audit.PrincipalId);
        Assert.Equal(environment.DeviceId, audit.DeviceId);
    }

    [Fact]
    public async Task HealthBoundariesUseRealDatabaseAndOpenFga()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("no-store", live.Headers.CacheControl?.ToString());
        Assert.Equal("no-store", ready.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task AuthorizationProviderFailureFailsClosedWithSafeResponseAndAudit()
    {
        await environment.ClearAccessAuditAsync();
        using var factory = environment.CreateFactory(
            environment.DeviceCertificate,
            "http://127.0.0.1:1");
        using var client = CreateClient(factory);
        using var request = AuthenticatedRequest(environment.CreateToken());
        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(responseBody);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("authorization_unavailable", problem.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(environment.ModelId, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", responseBody, StringComparison.Ordinal);
        var audit = await environment.LatestAuditAsync();
        Assert.Equal((short)PlatformAdminAccessAuditOutcome.Denied, audit.Outcome);
        Assert.Equal("authorization_unavailable", audit.Reason);
        Assert.Equal(environment.PrincipalId, audit.PrincipalId);
        Assert.Equal(environment.DeviceId, audit.DeviceId);
    }

    [Fact]
    public async Task RuntimeDatabaseRoleCannotRewriteAuthorityOrReadAuditEvidence()
    {
        await using var connection = new NpgsqlConnection(environment.RuntimeConnectionString);
        await connection.OpenAsync();

        await using var update = new NpgsqlCommand(
            "UPDATE platform_administration.principals SET availability = 2",
            connection);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);

        await using var bootstrap = new NpgsqlCommand(
            "SELECT count(*) FROM platform_administration.bootstrap_state",
            connection);
        denied = await Assert.ThrowsAsync<PostgresException>(() => bootstrap.ExecuteScalarAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);

        await using var audit = new NpgsqlCommand(
            "SELECT count(*) FROM platform_administration.access_audit_events",
            connection);
        denied = await Assert.ThrowsAsync<PostgresException>(() => audit.ExecuteScalarAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    [Fact]
    public async Task TenantCreationCommitsOneRetainedEffectAndRechecksAuthorityOnReplay()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        var key = Guid.NewGuid().ToString("N");
        using var create = await client.SendAsync(ProvisionRequest(environment.CreateToken(), key,
            "{\"displayName\":\"Program organization\"}"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var first = await create.Content.ReadFromJsonAsync<TenantProvisioningResponse>();
        Assert.NotNull(first);
        Assert.Equal("active", first.Availability);
        using var replay = await client.SendAsync(ProvisionRequest(environment.CreateToken(), key,
            "{\"displayName\":\"Program organization\"}"));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(first, await replay.Content.ReadFromJsonAsync<TenantProvisioningResponse>());
        Assert.Equal("no-store", replay.Headers.CacheControl?.ToString());
        using var conflict = await client.SendAsync(ProvisionRequest(environment.CreateToken(), key,
            "{\"displayName\":\"Changed intent\"}"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var denied = await client.SendAsync(ProvisionRequest(
            environment.CreateToken("not-the-admin"), key,
            "{\"displayName\":\"Program organization\"}"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand(
            "SELECT count(*) FROM tenancy.tenant_provisioning_receipts WHERE idempotency_key = @key",
            connection);
        count.Parameters.AddWithValue("key", key);
        Assert.Equal(1L, await count.ExecuteScalarAsync());
    }

    [Theory]
    [InlineData("{\"displayName\":\"Tenant\",\"extra\":1}")]
    [InlineData("{\"displayName\":\"Tenant\",\"DisplayName\":\"Other\"}")]
    [InlineData("{\"displayName\":null}")]
    [InlineData("{\"displayName\":42}")]
    [InlineData("{\"displayName\":\"\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{")]
    public async Task InvalidProvisioningBodyCannotCreateDurableEffects(string body)
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        var key = Guid.NewGuid().ToString("N");
        using var response = await client.SendAsync(ProvisionRequest(environment.CreateToken(), key, body));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand(
            "SELECT count(*) FROM tenancy.tenant_provisioning_receipts WHERE idempotency_key = @key", connection);
        count.Parameters.AddWithValue("key", key);
        Assert.Equal(0L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task RegisteredDeviceAuthorityPrecedesMalformedOrOversizedProvisioningBody()
    {
        using var factory = environment.CreateFactory(certificate: null);
        using var client = CreateClient(factory);
        using var malformed = await client.SendAsync(ProvisionRequest(environment.CreateToken(), "denied-malformed", "{"));
        Assert.Equal(HttpStatusCode.Forbidden, malformed.StatusCode);
        using var oversized = await client.SendAsync(ProvisionRequest(environment.CreateToken(), "denied-oversized",
            new string('x', 5000)));
        Assert.Equal(HttpStatusCode.Forbidden, oversized.StatusCode);
    }

    [Fact]
    public async Task AuthorizedProvisioningEnforcesBodyBoundAndMediaType()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        using var oversized = await client.SendAsync(ProvisionRequest(environment.CreateToken(), "oversized",
            new string('x', 5000)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        using var request = ProvisionRequest(environment.CreateToken(), "wrong-media", "displayName=Tenant");
        request.Content!.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var wrongMedia = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongMedia.StatusCode);
    }

    [Fact]
    public async Task RuntimeCannotChangeOrDeleteProvisioningFacts()
    {
        await using var connection = new NpgsqlConnection(environment.RuntimeConnectionString);
        await connection.OpenAsync();
        foreach (var sql in new[]
        {
            "UPDATE tenancy.tenants SET display_name = 'tampered'",
            "DELETE FROM tenancy.tenants",
            "UPDATE tenancy.tenant_provisioning_receipts SET display_name = 'tampered'",
            "DELETE FROM tenancy.tenant_provisioning_receipts"
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
    }

    private static HttpRequestMessage ProvisionRequest(string token, string key, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/platform/tenants")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static HttpClient CreateClient(AdminApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    private static HttpRequestMessage AuthenticatedRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/platform/access");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
