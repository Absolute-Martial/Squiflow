using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Application.PlatformAdministration;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;
using OpenFga.Sdk.Model;
using Xunit;

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class PlatformRegistryBoundaryTests(AdminApiTestEnvironment environment)
{
    [Fact]
    public async Task RegistryReadsAreBoundedKeysetOrderedAndAudited()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var tenantA = Guid.Parse("11111111-1111-7111-8111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-7222-8222-222222222222");
        var accountA = Guid.Parse("33333333-3333-7333-8333-333333333333");
        var accountB = Guid.Parse("44444444-4444-7444-8444-444444444444");
        await SeedAsync(tenantA, tenantB, accountA, accountB, suffix);
        await environment.ClearAccessAuditAsync();

        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);

        using var tenantPage1 = await SendAsync(client, "/api/v1/platform/tenants?limit=1");
        Assert.Equal(HttpStatusCode.OK, tenantPage1.StatusCode);
        using var page1 = JsonDocument.Parse(await tenantPage1.Content.ReadAsStringAsync());
        Assert.Single(page1.RootElement.GetProperty("items").EnumerateArray());
        var tenantCursor = page1.RootElement.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(tenantCursor));

        using var tenantPage2 = await SendAsync(client, $"/api/v1/platform/tenants?limit=100&cursor={Uri.EscapeDataString(tenantCursor!)}");
        Assert.Equal(HttpStatusCode.OK, tenantPage2.StatusCode);

        using var accountPage = await SendAsync(client, "/api/v1/platform/accounts?limit=1");
        Assert.Equal(HttpStatusCode.OK, accountPage.StatusCode);
        using var accountBody = JsonDocument.Parse(await accountPage.Content.ReadAsStringAsync());
        var accountCursor = accountBody.RootElement.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accountCursor));

        using var crossResource = await SendAsync(client, $"/api/v1/platform/tenants?cursor={Uri.EscapeDataString(accountCursor!)}");
        Assert.Equal(HttpStatusCode.BadRequest, crossResource.StatusCode);
        using var crossProblem = JsonDocument.Parse(await crossResource.Content.ReadAsStringAsync());
        Assert.Equal("invalid_registry_cursor", crossProblem.RootElement.GetProperty("code").GetString());

        using var oversized = await SendAsync(client, "/api/v1/platform/accounts?limit=101");
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);

        using var membership = await SendAsync(client, $"/api/v1/platform/tenants/{tenantA:D}/memberships/{accountA:D}");
        Assert.Equal(HttpStatusCode.OK, membership.StatusCode);
        using var membershipBody = JsonDocument.Parse(await membership.Content.ReadAsStringAsync());
        Assert.Equal(7, membershipBody.RootElement.GetProperty("revision").GetInt32());
        Assert.True(membershipBody.RootElement.GetProperty("isInitialOwner").GetBoolean());
        Assert.Equal("active", membershipBody.RootElement.GetProperty("availability").GetString());

        using var tenant = await SendAsync(client, $"/api/v1/platform/tenants/{tenantA:D}");
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        using var tenantBody = JsonDocument.Parse(await tenant.Content.ReadAsStringAsync());
        Assert.Equal(3, tenantBody.RootElement.GetProperty("revision").GetInt32());

        var audits = await AccessAuditsAsync();
        Assert.Equal(7, audits.Length);
        Assert.All(audits, audit => Assert.Equal((short)PlatformAdminAccessAuditOutcome.Succeeded, audit.Outcome));
        Assert.Equal(3, audits.Count(audit => audit.Operation == "registry_tenants_browse"));
        Assert.Equal(2, audits.Count(audit => audit.Operation == "registry_accounts_browse"));
        Assert.Contains(audits, audit => audit.Operation == $"registry_membership_detail:{tenantA:N}:{accountA:N}");
        Assert.Contains(audits, audit => audit.Operation == $"registry_tenant_detail:{tenantA:N}");
    }


    [Fact]
    public async Task AbsentRegistryRelationsDenyDiscoveryAndAreAudited()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Application Admin API Tenant Registry Reader", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
        var principalId = Guid.CreateVersion7();
        var deviceId = Guid.CreateVersion7();
        const string subject = "tenant-registry-reader";
        await AddPlatformPrincipalAsync(principalId, deviceId, subject, certificate);
        await GrantAsync(principalId, "can_read_tenants");

        await environment.ClearAccessAuditAsync();

        using var factory = environment.CreateFactory(certificate);
        using var client = CreateClient(factory);
        using var tenants = await SendAsync(client, "/api/v1/platform/tenants", subject);
        Assert.Equal(HttpStatusCode.OK, tenants.StatusCode);
        using var accounts = await SendAsync(client, "/api/v1/platform/accounts", subject);
        Assert.Equal(HttpStatusCode.Forbidden, accounts.StatusCode);
        using var memberships = await SendAsync(client, $"/api/v1/platform/tenants/{Guid.NewGuid():D}/memberships", subject);
        Assert.Equal(HttpStatusCode.Forbidden, memberships.StatusCode);

        var audits = await AccessAuditsAsync();
        Assert.Contains(audits, audit => audit.Operation == "registry_tenants_browse" &&
            audit.Outcome == (short)PlatformAdminAccessAuditOutcome.Succeeded);
        Assert.Contains(audits, audit => audit.Operation == "registry_accounts_browse" &&
            audit.Outcome == (short)PlatformAdminAccessAuditOutcome.Denied &&
            audit.Reason == "authorization_denied");
        Assert.Contains(audits, audit => audit.Operation.StartsWith("registry_memberships_browse:", StringComparison.Ordinal) &&
            audit.Outcome == (short)PlatformAdminAccessAuditOutcome.Denied &&
            audit.Reason == "authorization_denied");
    }

    [Fact]
    public async Task AuthorizationPrecedesRegistryExistenceAndCursorDisclosure()
    {
        using var factory = environment.CreateFactory(certificate: null);
        using var client = CreateClient(factory);
        using var missing = await SendAsync(client, $"/api/v1/platform/accounts/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        using var invalidCursor = await SendAsync(client, "/api/v1/platform/tenants?cursor=not-a-cursor");
        Assert.Equal(HttpStatusCode.Forbidden, invalidCursor.StatusCode);
    }

    [Fact]
    public async Task AuthorizedUnknownRegistryIdsReturnNotFound()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        var tenantId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();

        using var tenant = await SendAsync(client, $"/api/v1/platform/tenants/{tenantId:D}");
        Assert.Equal(HttpStatusCode.NotFound, tenant.StatusCode);
        using var account = await SendAsync(client, $"/api/v1/platform/accounts/{accountId:D}");
        Assert.Equal(HttpStatusCode.NotFound, account.StatusCode);
        using var membership = await SendAsync(client, $"/api/v1/platform/tenants/{tenantId:D}/memberships/{accountId:D}");
        Assert.Equal(HttpStatusCode.NotFound, membership.StatusCode);
    }

    [Fact]
    public async Task MalformedRegistryCursorReturnsBadRequestAfterAuthorization()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);

        using var response = await SendAsync(client, "/api/v1/platform/tenants?cursor=not-a-cursor");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_registry_cursor", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task MembershipBrowseTraversesEveryPageWithoutDuplicates()
    {
        var tenantId = Guid.CreateVersion7();
        var expected = new[]
        {
            Guid.Parse("10000000-0000-7000-8000-000000000001"),
            Guid.Parse("20000000-0000-7000-8000-000000000002"),
            Guid.Parse("30000000-0000-7000-8000-000000000003"),
        };
        await SeedMembershipTraversalAsync(tenantId, expected);

        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        var observed = new List<Guid>();
        string? cursor = null;
        for (var pageNumber = 0; pageNumber < 4; pageNumber++)
        {
            var path = $"/api/v1/platform/tenants/{tenantId:D}/memberships?limit=1" +
                (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");
            using var response = await SendAsync(client, path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Single(items);
            observed.Add(items[0].GetProperty("accountId").GetGuid());
            cursor = body.RootElement.GetProperty("nextCursor").ValueKind == JsonValueKind.Null
                ? null
                : body.RootElement.GetProperty("nextCursor").GetString();
            if (cursor is null) break;
        }

        Assert.Equal(expected, observed);
    }

    [Fact]
    public async Task StalePinnedPlatformModelFailsReadinessAndRegistryUse()
    {
        var legacyModelId = await environment.PublishAuthorizationModelAsync(
            await LegacyPlatformModelWithoutRegistryReadsAsync());
        using var factory = environment.CreateFactory(
            environment.DeviceCertificate,
            authorizationModelId: legacyModelId);
        using var client = CreateClient(factory);

        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);

        using var registry = await SendAsync(client, "/api/v1/platform/tenants");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, registry.StatusCode);
        using var problem = JsonDocument.Parse(await registry.Content.ReadAsStringAsync());
        Assert.Equal("authorization_unavailable", problem.RootElement.GetProperty("code").GetString());
    }


    private async Task AddPlatformPrincipalAsync(Guid principalId, Guid deviceId, string subject, X509Certificate2 certificate)
    {
        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO platform_administration.principals(id, issuer, subject, availability, created_at, disabled_at)
            VALUES (@principal, @issuer, @subject, 1, now(), NULL);
            INSERT INTO platform_administration.admin_devices(
                id, principal_id, certificate_fingerprint, display_name, availability, registered_at, revoked_at)
            VALUES (@device, @principal, @fingerprint, 'tenant registry reader', 1, now(), NULL);
            """;
        command.Parameters.AddWithValue("principal", principalId);
        command.Parameters.AddWithValue("device", deviceId);
        command.Parameters.AddWithValue("issuer", AdminApiTestEnvironment.Authority);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("fingerprint", certificate.GetCertHashString(HashAlgorithmName.SHA256));
        await command.ExecuteNonQueryAsync();
    }

    private async Task GrantAsync(Guid principalId, string relation)
    {
        using var openFga = new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = environment.OpenFgaApiUrl,
            StoreId = environment.StoreId,
            AuthorizationModelId = environment.ModelId,
            Credentials = new Credentials { Method = CredentialsMethod.None },
        });
        await openFga.Write(
            new ClientWriteRequest
            {
                Writes = [new ClientTupleKey
                {
                    User = $"user:{principalId:N}",
                    Relation = relation,
                    Object = "platform:root",
                }],
            },
            new ClientWriteOptions
            {
                StoreId = environment.StoreId,
                AuthorizationModelId = environment.ModelId,
            },
            CancellationToken.None);
    }

    private async Task SeedAsync(Guid tenantA, Guid tenantB, Guid accountA, Guid accountB, string suffix)
    {
        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO identity_access.accounts(id, availability, created_at, disabled_at)
            VALUES (@account_a, 1, now(), NULL), (@account_b, 2, now(), now())
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO tenancy.tenants(id, display_name, availability, created_at, suspended_at, revision)
            VALUES (@tenant_a, @name_a, 1, now(), NULL, 3), (@tenant_b, @name_b, 2, now(), now(), 4)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO tenancy.memberships(
                tenant_id, account_id, availability, created_at, revision,
                activated_at, suspended_at, removed_at, is_initial_owner)
            VALUES (@tenant_a, @account_a, 1, now(), 7, now(), NULL, NULL, true),
                   (@tenant_a, @account_b, 2, now(), 8, now(), now(), NULL, false)
            ON CONFLICT (tenant_id, account_id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("account_a", accountA);
        command.Parameters.AddWithValue("account_b", accountB);
        command.Parameters.AddWithValue("tenant_a", tenantA);
        command.Parameters.AddWithValue("tenant_b", tenantB);
        command.Parameters.AddWithValue("name_a", $"Registry tenant A {suffix}");
        command.Parameters.AddWithValue("name_b", $"Registry tenant B {suffix}");
        await command.ExecuteNonQueryAsync();
    }

    private async Task<(string Operation, short Outcome, string Reason)[]> AccessAuditsAsync()
    {
        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT operation, outcome, reason FROM platform_administration.access_audit_events ORDER BY occurred_at, id",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<(string Operation, short Outcome, string Reason)>();
        while (await reader.ReadAsync())
            values.Add((reader.GetString(0), reader.GetInt16(1), reader.GetString(2)));
        return values.ToArray();
    }

    private async Task SeedMembershipTraversalAsync(Guid tenantId, Guid[] accountIds)
    {
        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tenancy.tenants(id, display_name, availability, created_at, suspended_at, revision)
            VALUES (@tenant, 'pagination tenant', 1, now(), NULL, 1);
            INSERT INTO identity_access.accounts(id, availability, created_at, disabled_at)
            VALUES (@account_a, 1, now(), NULL), (@account_b, 1, now(), NULL), (@account_c, 1, now(), NULL)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO tenancy.memberships(
                tenant_id, account_id, availability, created_at, revision, activated_at, suspended_at, removed_at, is_initial_owner)
            VALUES (@tenant, @account_a, 1, now(), 1, now(), NULL, NULL, false),
                   (@tenant, @account_b, 1, now(), 1, now(), NULL, NULL, false),
                   (@tenant, @account_c, 1, now(), 1, now(), NULL, NULL, false);
            """;
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("account_a", accountIds[0]);
        command.Parameters.AddWithValue("account_b", accountIds[1]);
        command.Parameters.AddWithValue("account_c", accountIds[2]);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> LegacyPlatformModelWithoutRegistryReadsAsync()
    {
        var path = Path.Combine(
            AdminApiTestEnvironment.FindRepositoryRoot(),
            "infrastructure", "authorization", "openfga", "platform-authorization-model.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))?.AsObject()
            ?? throw new InvalidOperationException("Platform authorization model is not a JSON object.");
        var platform = root["type_definitions"]?.AsArray()
            .Select(node => node?.AsObject())
            .Single(node => node?["type"]?.GetValue<string>() == "platform")
            ?? throw new InvalidOperationException("Platform authorization type is missing.");
        foreach (var relation in new[] { "can_read_tenants", "can_read_accounts", "can_read_memberships" })
        {
            platform["relations"]?.AsObject().Remove(relation);
            platform["metadata"]?["relations"]?.AsObject().Remove(relation);
        }
        return root.ToJsonString();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string subject = AdminApiTestEnvironment.Subject)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", environment.CreateToken(subject));
        return await client.SendAsync(request);
    }

    private static HttpClient CreateClient(AdminApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
}
