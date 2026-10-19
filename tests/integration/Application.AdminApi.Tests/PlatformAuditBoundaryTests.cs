using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Application.IdentityAccess;
using Application.IdentityAccess.Postgres;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;
using Xunit;

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class PlatformAuditBoundaryTests(AdminApiTestEnvironment environment)
{
    [Fact]
    public async Task AccessReadsKeepTiesStableAcrossConcurrentAppendAndOmitPrivateIdentityFields()
    {
        await environment.ClearAccessAuditAsync();
        var ids = new[]
        {
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            Guid.Parse("30000000-0000-0000-0000-000000000003"),
        };
        foreach (var id in ids) await SeedDeniedAsync(id);
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        var route = $"/api/v1/platform/audit/access?limit=2&outcome=denied&principalId={environment.PrincipalId:D}";
        using var response = await SendAsync(client, route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var json = await response.Content.ReadAsStringAsync();
        using var first = JsonDocument.Parse(json);
        Assert.Equal("access_decision", first.RootElement.GetProperty("kind").GetString());
        Assert.Equal(ids.Reverse().Take(2), first.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        Assert.All(first.RootElement.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.Equal("denied", item.GetProperty("outcome").GetString());
            Assert.False(item.TryGetProperty("subject", out _));
            Assert.False(item.TryGetProperty("issuer", out _));
            Assert.False(item.TryGetProperty("certificateFingerprint", out _));
        });
        Assert.DoesNotContain("private-subject", json, StringComparison.Ordinal);
        var cursor = first.RootElement.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));
        await SeedDeniedAsync(Guid.Parse("40000000-0000-0000-0000-000000000004"));
        using var continued = await SendAsync(client, $"{route}&cursor={Uri.EscapeDataString(cursor!)}");
        using var second = JsonDocument.Parse(await continued.Content.ReadAsStringAsync());
        var last = Assert.Single(second.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(ids[0], last.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, second.RootElement.GetProperty("nextCursor").ValueKind);
        using var changedFilter = await SendAsync(client, $"/api/v1/platform/audit/access?cursor={Uri.EscapeDataString(cursor!)}");
        Assert.Equal(HttpStatusCode.BadRequest, changedFilter.StatusCode);
    }

    [Theory]
    [InlineData("limit=101")]
    [InlineData("limit=0")]
    [InlineData("limit=1&limit=2")]
    [InlineData("outcome=succeeded")]
    [InlineData("principalId=00000000-0000-0000-0000-000000000000")]
    [InlineData("cursor=invalid")]
    [InlineData("tenantId=not-supported")]
    public async Task InvalidAuditQueriesAreRejectedAfterAuthorization(string query)
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        using var response = await SendAsync(client, $"/api/v1/platform/audit/access?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var deniedFactory = environment.CreateFactory(certificate: null);
        using var deniedClient = CreateClient(deniedFactory);
        using var denied = await SendAsync(deniedClient, $"/api/v1/platform/audit/access?{query}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task CommittedHistoryIsSeparateFromAccessDecisionsAndNeverReappliesAnOldChange()
    {
        await using var source = NpgsqlDataSource.Create(environment.RuntimeConnectionString);
        var actor = IdentityAdministrationActor.Create(environment.PrincipalId, environment.DeviceId);
        var created = await new PostgresAccountOnboardingStore(source).OnboardAsync(actor,
            AccountOnboardingIntent.Create(ExternalIdentity.Create(AdminApiTestEnvironment.Authority,
                Guid.NewGuid().ToString("N")), Guid.NewGuid().ToString("N")), DateTimeOffset.UtcNow, CancellationToken.None);
        var account = created.Account!.AccountId;
        var store = new PostgresAccountLifecycleStore(source);
        foreach (var (operation, revision) in new[] { (AccountLifecycleOperation.Suspend, 1), (AccountLifecycleOperation.Reactivate, 2) })
        {
            var result = await store.ExecuteAsync(actor, AccountLifecycleIntent.Create(operation, account, revision,
                "Retained support reason", Guid.NewGuid().ToString("N")), DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.Equal(AccountLifecycleStatus.Changed, result.Status);
        }
        var stale = await store.ExecuteAsync(actor, AccountLifecycleIntent.Create(AccountLifecycleOperation.Suspend,
            account, 1, "Stale command", Guid.NewGuid().ToString("N")), DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(AccountLifecycleStatus.RevisionConflict, stale.Status);
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        var route = $"/api/v1/platform/accounts/{account:D}/history?limit=1";
        using var response = await SendAsync(client, route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var first = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("committed_account_transition", first.RootElement.GetProperty("kind").GetString());
        var newest = Assert.Single(first.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(3, newest.GetProperty("revision").GetInt32());
        Assert.Equal(actor.PrincipalId, newest.GetProperty("changedByPrincipalId").GetGuid());
        Assert.Equal(actor.DeviceId, newest.GetProperty("changedByDeviceId").GetGuid());
        Assert.False(newest.TryGetProperty("idempotencyKey", out _));
        var cursor = first.RootElement.GetProperty("nextCursor").GetString();
        using var continued = await SendAsync(client, $"{route}&cursor={Uri.EscapeDataString(cursor!)}");
        using var second = JsonDocument.Parse(await continued.Content.ReadAsStringAsync());
        Assert.Equal(2, Assert.Single(second.RootElement.GetProperty("items").EnumerateArray()).GetProperty("revision").GetInt32());
        Assert.Equal(JsonValueKind.Null, second.RootElement.GetProperty("nextCursor").ValueKind);
        using var wrongAccount = await SendAsync(client, $"/api/v1/platform/accounts/{Guid.NewGuid():D}/history?cursor={Uri.EscapeDataString(cursor!)}");
        Assert.Equal(HttpStatusCode.BadRequest, wrongAccount.StatusCode);
        using var wrongResource = await SendAsync(client, $"/api/v1/platform/audit/access?cursor={Uri.EscapeDataString(cursor!)}");
        Assert.Equal(HttpStatusCode.BadRequest, wrongResource.StatusCode);
        using var missing = await SendAsync(client, $"/api/v1/platform/accounts/{Guid.NewGuid():D}/history");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var empty = await SendAsync(client, $"/api/v1/platform/accounts/{Guid.Empty:D}/history");
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(AccountAvailability.Active, await new PostgresAccountDirectory(source).FindAvailabilityAsync(account, CancellationToken.None));
    }

    [Fact]
    public async Task RegistryPermissionDoesNotAuthorizeAuditAndAuditGrantDoesNotAuthorizeMutations()
    {
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=Application audit reader test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var principal = Guid.NewGuid();
        var subject = Guid.NewGuid().ToString("N");
        await using (var owner = NpgsqlDataSource.Create(environment.OwnerConnectionString))
        await using (var seed = owner.CreateCommand("""
            INSERT INTO platform_administration.principals(id, issuer, subject, availability, created_at)
            VALUES (@principal, @issuer, @subject, 1, now());
            INSERT INTO platform_administration.admin_devices(id, principal_id, certificate_fingerprint, display_name, availability, registered_at)
            VALUES (@device, @principal, @fingerprint, 'audit reader test', 1, now())
            """))
        {
            seed.Parameters.AddWithValue("principal", principal);
            seed.Parameters.AddWithValue("issuer", AdminApiTestEnvironment.Authority);
            seed.Parameters.AddWithValue("subject", subject);
            seed.Parameters.AddWithValue("device", Guid.NewGuid());
            seed.Parameters.AddWithValue("fingerprint", certificate.GetCertHashString(HashAlgorithmName.SHA256));
            await seed.ExecuteNonQueryAsync();
        }
        using var openFga = new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = environment.OpenFgaApiUrl,
            StoreId = environment.StoreId,
            AuthorizationModelId = environment.ModelId,
            Credentials = new Credentials { Method = CredentialsMethod.None },
        });
        async Task GrantAsync(string relation) => await openFga.Write(new ClientWriteRequest
        {
            Writes = [new ClientTupleKey { User = $"user:{principal:N}", Relation = relation, Object = "platform:root" }],
        }, new ClientWriteOptions { StoreId = environment.StoreId, AuthorizationModelId = environment.ModelId }, CancellationToken.None);
        await GrantAsync("can_read_accounts");
        using var factory = environment.CreateFactory(certificate);
        using var client = CreateClient(factory);
        using var denied = await SendAsync(client, "/api/v1/platform/audit/access?cursor=invalid", subject);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await GrantAsync("can_read_audit");
        using var allowed = await SendAsync(client, "/api/v1/platform/audit/access?limit=1", subject);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var mutation = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/platform/accounts/{Guid.NewGuid():D}/lifecycle/suspend");
        mutation.Headers.Authorization = new AuthenticationHeaderValue("Bearer", environment.CreateToken(subject));
        using var forbidden = await client.SendAsync(mutation);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task RuntimeCanOnlyReadTheBoundedSafeProjectionAndCannotEditAuditEvidence()
    {
        await using var source = NpgsqlDataSource.Create(environment.RuntimeConnectionString);
        foreach (var sql in new[]
        {
            "SELECT issuer, subject, certificate_fingerprint FROM platform_administration.access_audit_events LIMIT 1",
            "UPDATE platform_administration.access_audit_events SET outcome = outcome WHERE false",
            "DELETE FROM platform_administration.access_audit_events WHERE false",
        })
        {
            await using var command = source.CreateCommand(sql);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
        }
        await using var bounded = source.CreateCommand("""
            SELECT count(*) FROM platform_administration.read_access_audit(NULL, NULL, NULL, NULL, 102)
            """);
        Assert.Equal(0L, await bounded.ExecuteScalarAsync());
    }

    private async Task SeedDeniedAsync(Guid id)
    {
        await using var owner = NpgsqlDataSource.Create(environment.OwnerConnectionString);
        await using var command = owner.CreateCommand("""
            INSERT INTO platform_administration.access_audit_events
                (id, principal_id, device_id, issuer, subject, certificate_fingerprint, operation, outcome, reason, occurred_at)
            VALUES (@id, @principal, @device, 'https://private-issuer.example.test', 'private-subject', @fingerprint,
                    'platform_access', 1, 'authorization_denied', '2070-01-01T00:00:00Z')
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("principal", environment.PrincipalId);
        command.Parameters.AddWithValue("device", environment.DeviceId);
        command.Parameters.AddWithValue("fingerprint", environment.DeviceCertificate.GetCertHashString(HashAlgorithmName.SHA256));
        await command.ExecuteNonQueryAsync();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, string uri, string subject = AdminApiTestEnvironment.Subject)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", environment.CreateToken(subject));
        return await client.SendAsync(request);
    }

    private static HttpClient CreateClient(AdminApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
}
