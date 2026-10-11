using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Profiles;
using Application.Profiles.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using Npgsql;
using System.Text;
using Application.PlatformAdministration;
using Xunit;

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class TenantProfileAuthorityBoundaryTests(AdminApiTestEnvironment environment)
{
    [Fact]
    public async Task PlatformAdministratorDoesNotInheritProfilePublishOrActivateAuthority()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var tenantId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await environment.ClearAccessAuditAsync();
        using var publish = Request(
            HttpMethod.Post,
            $"/api/v1/platform/tenants/{tenantId:D}/profiles",
            environment.CreateToken(),
            "profile-publish",
            "{\"expectedAuthorityRevision\":0,\"publishedPolicyRevisionId\":\"" + Guid.NewGuid().ToString("D") + "\"}");
        using var publishResponse = await client.SendAsync(publish);
        Assert.Equal(HttpStatusCode.Forbidden, publishResponse.StatusCode);
        var publishAudit = await environment.LatestAuditAsync();
        Assert.Equal($"tenant_profile_publish:{tenantId:N}", publishAudit.Operation);
        Assert.Equal((short)PlatformAdminAccessAuditOutcome.Denied, publishAudit.Outcome);

        using var activate = Request(
            HttpMethod.Post,
            $"/api/v1/platform/tenants/{tenantId:D}/profiles/{profileId:D}/activate",
            environment.CreateToken(),
            "profile-activate",
            "{\"expectedAuthorityRevision\":0}");
        using var activateResponse = await client.SendAsync(activate);
        Assert.Equal(HttpStatusCode.Forbidden, activateResponse.StatusCode);

        var latest = await environment.LatestAuditAsync();
        Assert.Equal($"tenant_profile_activate:{tenantId:N}", latest.Operation);
        Assert.Equal((short)PlatformAdminAccessAuditOutcome.Denied, latest.Outcome);
        Assert.Equal("authorization_denied", latest.Reason);

        using var legacyAssignment = Request(
            HttpMethod.Post,
            $"/api/v1/platform/tenants/{tenantId:D}/profiles/legacy-orders/{orderId:D}/assign",
            environment.CreateToken(),
            "legacy-profile-assignment",
            "{\"expectedRevision\":1}");
        using var legacyResponse = await client.SendAsync(legacyAssignment);
        Assert.Equal(HttpStatusCode.Forbidden, legacyResponse.StatusCode);
        var assignmentAudit = await environment.LatestAuditAsync();
        Assert.Equal($"legacy_order_profile_assignment:{tenantId:N}:{orderId:N}", assignmentAudit.Operation);
        Assert.Equal((short)PlatformAdminAccessAuditOutcome.Denied, assignmentAudit.Outcome);
    }

    [Fact]
    public async Task CurrentAdminEntryAndSeparateProfileRightsGuardPublicationActivationAndReplay()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var token = environment.CreateToken();
        var suffix = Guid.NewGuid().ToString("N");
        Guid accountId;
        using (var request = Request(HttpMethod.Post, "/api/v1/platform/accounts", token, "profile-account-" + suffix,
                   JsonSerializer.Serialize(new { subject = "profile-human-" + suffix })))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            accountId = body.RootElement.GetProperty("accountId").GetGuid();
        }
        Guid tenantId;
        using (var request = Request(HttpMethod.Post, "/api/v1/platform/tenants", token, "profile-tenant-" + suffix,
                   JsonSerializer.Serialize(new { displayName = "Profile tenant " + suffix })))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            tenantId = body.RootElement.GetProperty("tenantId").GetGuid();
        }
        using (var request = Request(HttpMethod.Post, $"/api/v1/platform/tenants/{tenantId:D}/memberships/initial-owner", token,
                   "profile-owner-" + suffix, JsonSerializer.Serialize(new { accountId })))
        using (var response = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var ownerSource = NpgsqlDataSource.Create(environment.OwnerConnectionString);
        await using var tenancy = TenancyPostgresMigrations.CreateContext(environment.OwnerConnectionString);
        var context = (await new ResolveTenantContext(new PostgresTenantMembershipDirectory(tenancy)).ExecuteAsync(accountId, tenantId, default))!;
        var profiles = new PostgresProfileStore(ownerSource, TimeProvider.System);
        var edit = await profiles.EditPolicyAsync(context, new(0, true), 1, "host-policy-" + suffix, default);
        var policy = await profiles.PublishPolicyAsync(context, new(edit.PolicyState!.Revision), 1, "host-policy-publish-" + suffix, default);
        var publishPath = $"/api/v1/platform/tenants/{tenantId:D}/profiles";
        var publishKey = "host-profile-" + suffix;
        var publishBody = JsonSerializer.Serialize(new { expectedAuthorityRevision = 0, publishedPolicyRevisionId = policy.PublishedPolicy!.PolicyRevisionId });
        var publishGranted = false;
        var activateGranted = false;
        var entryRemoved = false;
        try
        {
            await SetPlatformRelationAsync("can_publish_tenant_profile", true); publishGranted = true;
            ProfileCommandResult publication;
            using (var request = Request(HttpMethod.Post, publishPath, token, publishKey, publishBody))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                Assert.True(response.Headers.CacheControl?.NoStore);
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var profile = body.RootElement.GetProperty("profile").Deserialize<TenantProfileSnapshot>(JsonSerializerOptions.Web)!;
                var authority = body.RootElement.GetProperty("authority").Deserialize<TenantProfileAuthority>(JsonSerializerOptions.Web)!;
                publication = new(ProfileCommandStatus.ProfilePublished, Profile: profile, Authority: authority);
            }
            Assert.Equal(environment.PrincipalId, publication.Profile!.PublishedByPrincipalId);
            Assert.Equal(environment.DeviceId, publication.Profile.PublishedByDeviceId);
            Assert.Equal(1, publication.Profile.ObservedAuthorizationRevision);
            await using (var advance = ownerSource.CreateCommand("INSERT INTO tenancy.tenant_authorization_state(tenant_id,revision,updated_at) VALUES(@tenant,2,now())"))
            {
                advance.Parameters.AddWithValue("tenant", tenantId); await advance.ExecuteNonQueryAsync();
            }
            using (var request = Request(HttpMethod.Post, publishPath, token, publishKey, publishBody))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("true", Assert.Single(response.Headers.GetValues("Idempotency-Replayed")));
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(1, body.RootElement.GetProperty("profile").GetProperty("observedAuthorizationRevision").GetInt64());
            }
            var activatePath = $"{publishPath}/{publication.Profile.ProfileId:D}/activate";
            var activateBody = JsonSerializer.Serialize(new { expectedAuthorityRevision = publication.Authority!.Revision });
            using (var request = Request(HttpMethod.Post, activatePath, token, "activate-" + suffix, activateBody))
            using (var response = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await SetPlatformRelationAsync("can_activate_tenant_profile", true); activateGranted = true;
            using (var request = Request(HttpMethod.Post, activatePath, token, "activate-" + suffix, activateBody))
            using (var response = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var afterActivation = await profiles.GetAuthorityAsync(tenantId, default);
            Assert.Equal(publication.Profile.ProfileId, afterActivation!.ActiveProfileId);
            await SetPlatformRelationAsync("can_publish_tenant_profile", false); publishGranted = false;
            using (var request = Request(HttpMethod.Post, publishPath, token, publishKey, publishBody))
            using (var response = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await SetPlatformRelationAsync("can_publish_tenant_profile", true); publishGranted = true;
            await SetPlatformRelationAsync("administrator", false); entryRemoved = true;
            using (var request = Request(HttpMethod.Post, publishPath, token, publishKey, publishBody))
            using (var response = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            using (var request = Request(HttpMethod.Post, activatePath, token, "activate-" + suffix, activateBody))
            using (var response = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(afterActivation, await profiles.GetAuthorityAsync(tenantId, default));
        }
        finally
        {
            if (entryRemoved) await SetPlatformRelationAsync("administrator", true);
            if (publishGranted) await SetPlatformRelationAsync("can_publish_tenant_profile", false);
            if (activateGranted) await SetPlatformRelationAsync("can_activate_tenant_profile", false);
        }
    }

    private async Task SetPlatformRelationAsync(string relation, bool enabled)
    {
        using var client = new HttpClient { BaseAddress = new Uri(environment.OpenFgaApiUrl) };
        var tuples = new { tuple_keys = new[] { new { user = $"user:{environment.PrincipalId:N}", relation, @object = "platform:root" } } };
        var body = new Dictionary<string, object> { ["authorization_model_id"] = environment.ModelId, [enabled ? "writes" : "deletes"] = tuples };
        using var response = await client.PostAsJsonAsync($"/stores/{environment.StoreId}/write", body);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string token, string key, string json)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }
}
