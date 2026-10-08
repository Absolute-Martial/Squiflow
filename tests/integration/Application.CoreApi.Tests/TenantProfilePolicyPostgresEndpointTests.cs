using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.IdentityAccess.Postgres;
using Application.Profiles;
using Application.Profiles.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.CoreApi.Tests;

[Collection(OrderProgramReferenceEndpointGroup.Name)]
public sealed class TenantProfilePolicyPostgresEndpointTests
{
    [Fact]
    public async Task ActualPolicyCommandsRetainPublishedFactsAndRecheckPermissionAndTenantOnReplay()
    {
        await using var database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithDatabase("policy_http_tests").WithUsername("postgres").WithPassword("local-policy-http-only").Build();
        await database.StartAsync();
        await using (var identity = IdentityAccessPostgresMigrations.CreateContext(database.GetConnectionString())) await identity.Database.MigrateAsync();
        await using (var tenancy = TenancyPostgresMigrations.CreateContext(database.GetConnectionString())) await tenancy.Database.MigrateAsync();
        await using (var profiles = ProfilesPostgresRegistration.CreateContext(database.GetConnectionString())) await profiles.Database.MigrateAsync();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await using var owner = new NpgsqlConnection(database.GetConnectionString()); await owner.OpenAsync();
        await using (var setup = owner.CreateCommand())
        {
            setup.CommandText = """
                INSERT INTO identity_access.accounts(id,availability,created_at) VALUES(@account,1,now());
                INSERT INTO tenancy.tenants(id,display_name,availability,created_at) VALUES(@tenant,'Policy HTTP tenant',1,now());
                INSERT INTO tenancy.memberships(tenant_id,account_id,availability,created_at,revision,is_initial_owner,activated_at) VALUES(@tenant,@account,1,now(),1,false,now());
                CREATE ROLE policy_http_runtime LOGIN PASSWORD 'local-runtime-only';
                GRANT USAGE ON SCHEMA profiles,tenancy TO policy_http_runtime;
                GRANT SELECT ON ALL TABLES IN SCHEMA profiles TO policy_http_runtime;
                GRANT SELECT ON tenancy.tenants,tenancy.memberships,tenancy.tenant_authorization_state TO policy_http_runtime;
                GRANT INSERT ON profiles.policy_heads,profiles.policy_revisions,profiles.command_receipts TO policy_http_runtime;
                GRANT UPDATE(revision,require_reference,published_policy_id) ON profiles.policy_heads TO policy_http_runtime;
                """;
            setup.Parameters.AddWithValue("account", accountId); setup.Parameters.AddWithValue("tenant", tenantId);
            await setup.ExecuteNonQueryAsync();
        }
        var runtime = new NpgsqlConnectionStringBuilder(database.GetConnectionString()) { Username = "policy_http_runtime", Password = "local-runtime-only" };
        using var identityFactory = new WhiteLabelApiFactory();
        identityFactory.Bind(accountId.ToString("D"), accountId);
        identityFactory.SetProfilePolicyDecision(accountId, tenantId, TenantProfilePolicyPermission.Edit, true);
        identityFactory.SetProfilePolicyDecision(accountId, tenantId, TenantProfilePolicyPermission.Publish, true);
        identityFactory.SetProfilePolicyDecision(accountId, tenantId, TenantProfilePolicyPermission.View, true);
        using var factory = identityFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(_ => NpgsqlDataSource.Create(runtime.ConnectionString));
            services.RemoveAll<IProfileStore>(); services.AddScoped<IProfileStore, PostgresProfileStore>();
            services.RemoveAll<ITenantMembershipDirectory>(); services.AddScoped<ITenantMembershipDirectory, PostgresTenantMembershipDirectory>();
        }));
        using var client = factory.CreateClient();
        var token = identityFactory.CreateToken(accountId.ToString("D"));
        var path = $"/api/v1/tenants/{tenantId:D}/profile-policy";
        using (var request = Request(HttpMethod.Put, path, token, "edit", "{\"expectedRevision\":0,\"requireReferenceForProgramOrders\":true}"))
        using (var response = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Guid publishedId;
        using (var request = Request(HttpMethod.Post, path + "/publish", token, "publish", "{\"expectedRevision\":1}"))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            publishedId = body.RootElement.GetProperty("publishedPolicy").GetProperty("policyRevisionId").GetGuid();
            Assert.True(body.RootElement.GetProperty("publishedPolicy").GetProperty("requireReferenceForProgramOrders").GetBoolean());
        }
        using (var request = Request(HttpMethod.Put, path, token, "next-edit", "{\"expectedRevision\":2,\"requireReferenceForProgramOrders\":false}"))
        using (var response = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var request = Request(HttpMethod.Post, path + "/publish", token, "publish", "{\"expectedRevision\":1}"))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("true", Assert.Single(response.Headers.GetValues("Idempotency-Replayed")));
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(publishedId, body.RootElement.GetProperty("publishedPolicy").GetProperty("policyRevisionId").GetGuid());
            Assert.True(body.RootElement.GetProperty("publishedPolicy").GetProperty("requireReferenceForProgramOrders").GetBoolean());
        }
        identityFactory.SetProfilePolicyDecision(accountId, tenantId, TenantProfilePolicyPermission.Publish, false);
        using (var request = Request(HttpMethod.Post, path + "/publish", token, "publish", "{\"expectedRevision\":1}"))
        using (var response = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using (var suspend = owner.CreateCommand())
        {
            suspend.CommandText = "UPDATE tenancy.tenants SET availability=2,revision=revision+1,suspended_at=now() WHERE id=@tenant";
            suspend.Parameters.AddWithValue("tenant", tenantId); await suspend.ExecuteNonQueryAsync();
        }
        using (var request = Request(HttpMethod.Put, path, token, "edit", "{\"expectedRevision\":0,\"requireReferenceForProgramOrders\":true}"))
        using (var response = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var inspect = owner.CreateCommand();
        inspect.CommandText = "SELECT revision,(SELECT count(*) FROM profiles.command_receipts WHERE tenant_id=@tenant) FROM profiles.policy_heads WHERE tenant_id=@tenant";
        inspect.Parameters.AddWithValue("tenant", tenantId); await using var reader = await inspect.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync()); Assert.Equal(3, reader.GetInt64(0)); Assert.Equal(3, reader.GetInt64(1));
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string token, string key, string json)
    {
        var request = new HttpRequestMessage(method, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); request.Headers.Add("Idempotency-Key", key);
        return request;
    }
}
