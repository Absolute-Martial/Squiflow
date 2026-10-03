using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.AdminApi.IdentityProvisioning;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class AccountOnboardingBoundaryTests(AdminApiTestEnvironment environment)
{
    [Fact]
    public async Task AccountOnboardingCreatesOneDurableBindingAndSafelyReplays()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        var key = Guid.NewGuid().ToString("N");
        var subject = $"onboard-{Guid.NewGuid():N}";

        using var created = await client.SendAsync(
            AccountRequest(environment.CreateToken(), key, subject));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var accountId = createdBody.RootElement.GetProperty("accountId").GetGuid();
        Assert.Equal(AdminApiTestEnvironment.Authority, createdBody.RootElement.GetProperty("issuer").GetString());
        Assert.Equal(subject, createdBody.RootElement.GetProperty("subject").GetString());
        Assert.Equal("active", createdBody.RootElement.GetProperty("availability").GetString());

        using var replayed = await client.SendAsync(
            AccountRequest(environment.CreateToken(), key, subject));
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        using var replayedBody = JsonDocument.Parse(await replayed.Content.ReadAsStringAsync());
        Assert.Equal(accountId, replayedBody.RootElement.GetProperty("accountId").GetGuid());

        using var conflict = await client.SendAsync(
            AccountRequest(environment.CreateToken(), key, $"changed-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var conflictBody = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
        Assert.Equal("idempotency_key_conflict", conflictBody.RootElement.GetProperty("code").GetString());

        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        Assert.Equal(1L, await CountAsync(
            connection,
            "SELECT count(*) FROM identity_access.external_identity_bindings WHERE issuer = @issuer AND subject = @subject",
            ("issuer", AdminApiTestEnvironment.Authority),
            ("subject", subject)));
        Assert.Equal(1L, await CountAsync(
            connection,
            "SELECT count(*) FROM identity_access.account_onboarding_receipts WHERE provisioned_by_principal_id = @principal AND idempotency_key = @key",
            ("principal", environment.PrincipalId),
            ("key", key)));
    }


    [Fact]
    public async Task CommittedAccountReplayAndConflictDoNotDependOnProviderAvailability()
    {
        var key = Guid.NewGuid().ToString("N");
        var subject = $"replay-{Guid.NewGuid():N}";
        Guid accountId;
        using (var factory = environment.CreateFactory(environment.DeviceCertificate))
        using (var client = CreateClient(factory))
        using (var created = await client.SendAsync(
                   AccountRequest(environment.CreateToken(), key, subject)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            accountId = body.RootElement.GetProperty("accountId").GetGuid();
        }

        using var unavailableFactory = environment.CreateFactory(
            environment.DeviceCertificate,
            identityVerifier: new ThrowingExternalIdentityVerifier(
                new AdminIdentityProviderUnavailableException("provider unavailable after commit")));
        using var unavailableClient = CreateClient(unavailableFactory);

        using var replay = await unavailableClient.SendAsync(
            AccountRequest(environment.CreateToken(), key, subject));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var replayBody = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(accountId, replayBody.RootElement.GetProperty("accountId").GetGuid());

        using var conflict = await unavailableClient.SendAsync(
            AccountRequest(environment.CreateToken(), key, $"changed-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var conflictBody = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
        Assert.Equal("idempotency_key_conflict", conflictBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task IdentityLinkAddsOneBindingAndCannotBeReclaimedByAnotherAccount()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        var firstSubject = $"primary-{Guid.NewGuid():N}";
        var firstKey = Guid.NewGuid().ToString("N");
        using var created = await client.SendAsync(
            AccountRequest(environment.CreateToken(), firstKey, firstSubject));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var accountId = createdBody.RootElement.GetProperty("accountId").GetGuid();

        var linkedSubject = $"linked-{Guid.NewGuid():N}";
        var linkKey = Guid.NewGuid().ToString("N");
        using var linked = await client.SendAsync(
            LinkRequest(environment.CreateToken(), accountId, linkKey, linkedSubject));
        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        using var linkedBody = JsonDocument.Parse(await linked.Content.ReadAsStringAsync());
        Assert.Equal(accountId, linkedBody.RootElement.GetProperty("accountId").GetGuid());
        Assert.Equal(linkedSubject, linkedBody.RootElement.GetProperty("subject").GetString());

        using var replayed = await client.SendAsync(
            LinkRequest(environment.CreateToken(), accountId, linkKey, linkedSubject));
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);

        using var reclaim = await client.SendAsync(
            AccountRequest(environment.CreateToken(), Guid.NewGuid().ToString("N"), linkedSubject));
        Assert.Equal(HttpStatusCode.Conflict, reclaim.StatusCode);
        using var reclaimBody = JsonDocument.Parse(await reclaim.Content.ReadAsStringAsync());
        Assert.Equal("external_identity_already_bound", reclaimBody.RootElement.GetProperty("code").GetString());

        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        Assert.Equal(2L, await CountAsync(
            connection,
            "SELECT count(*) FROM identity_access.external_identity_bindings WHERE account_id = @account",
            ("account", accountId)));
        Assert.Equal(1L, await CountAsync(
            connection,
            "SELECT count(*) FROM identity_access.identity_link_receipts WHERE linked_by_principal_id = @principal AND idempotency_key = @key",
            ("principal", environment.PrincipalId),
            ("key", linkKey)));
    }


    [Fact]
    public async Task CommittedIdentityLinkReplayAndConflictDoNotDependOnProviderAvailability()
    {
        Guid accountId;
        using (var factory = environment.CreateFactory(environment.DeviceCertificate))
        using (var client = CreateClient(factory))
        using (var created = await client.SendAsync(
                   AccountRequest(
                       environment.CreateToken(),
                       Guid.NewGuid().ToString("N"),
                       $"primary-{Guid.NewGuid():N}")))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            accountId = body.RootElement.GetProperty("accountId").GetGuid();
        }

        var linkKey = Guid.NewGuid().ToString("N");
        var linkedSubject = $"linked-replay-{Guid.NewGuid():N}";
        using (var factory = environment.CreateFactory(environment.DeviceCertificate))
        using (var client = CreateClient(factory))
        using (var linked = await client.SendAsync(
                   LinkRequest(environment.CreateToken(), accountId, linkKey, linkedSubject)))
        {
            Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        }

        using var unavailableFactory = environment.CreateFactory(
            environment.DeviceCertificate,
            identityVerifier: new ThrowingExternalIdentityVerifier(
                new AdminIdentityProviderUnavailableException("provider unavailable after commit")));
        using var unavailableClient = CreateClient(unavailableFactory);

        using var replay = await unavailableClient.SendAsync(
            LinkRequest(environment.CreateToken(), accountId, linkKey, linkedSubject));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        using var conflict = await unavailableClient.SendAsync(
            LinkRequest(
                environment.CreateToken(),
                accountId,
                linkKey,
                $"changed-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var conflictBody = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
        Assert.Equal("idempotency_key_conflict", conflictBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task RegisteredAdminDeviceAuthorityPrecedesIdentityLinkAccountValidation()
    {
        using var factory = environment.CreateFactory(certificate: null);
        using var client = CreateClient(factory);

        using var response = await client.SendAsync(
            LinkRequest(
                environment.CreateToken(),
                Guid.Empty,
                Guid.NewGuid().ToString("N"),
                $"subject-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("admin_device_required", problem.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData((int)ExternalIdentityVerification.NotFound, HttpStatusCode.NotFound, "external_identity_not_found")]
    [InlineData((int)ExternalIdentityVerification.NotInteractiveUser, HttpStatusCode.UnprocessableEntity, "external_identity_not_interactive")]
    public async Task ProviderIdentityRejectionCreatesNoLocalAccount(
        int providerResult,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var subject = $"rejected-{Guid.NewGuid():N}";
        var key = Guid.NewGuid().ToString("N");
        using var factory = environment.CreateFactory(
            environment.DeviceCertificate,
            identityVerifier: new FixedExternalIdentityVerifier(
                (ExternalIdentityVerification)providerResult));
        using var client = CreateClient(factory);

        using var response = await client.SendAsync(
            AccountRequest(environment.CreateToken(), key, subject));
        Assert.Equal(expectedStatus, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, problem.RootElement.GetProperty("code").GetString());

        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        Assert.Equal(0L, await CountAsync(
            connection,
            "SELECT count(*) FROM identity_access.external_identity_bindings WHERE issuer = @issuer AND subject = @subject",
            ("issuer", AdminApiTestEnvironment.Authority),
            ("subject", subject)));
        Assert.Equal(0L, await CountAsync(
            connection,
            "SELECT count(*) FROM identity_access.account_onboarding_receipts WHERE provisioned_by_principal_id = @principal AND idempotency_key = @key",
            ("principal", environment.PrincipalId),
            ("key", key)));
    }

    [Fact]
    public async Task ProviderFailureFailsClosedWithoutDisclosingProviderDetail()
    {
        const string providerDetail = "provider-internal-sensitive-detail";
        var subject = $"outage-{Guid.NewGuid():N}";
        var key = Guid.NewGuid().ToString("N");
        using var factory = environment.CreateFactory(
            environment.DeviceCertificate,
            identityVerifier: new ThrowingExternalIdentityVerifier(
                new AdminIdentityProviderUnavailableException(providerDetail)));
        using var client = CreateClient(factory);

        using var response = await client.SendAsync(
            AccountRequest(environment.CreateToken(), key, subject));
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("identity_provider_unavailable", problem.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(providerDetail, body, StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        Assert.Equal(0L, await CountAsync(
            connection,
            "SELECT count(*) FROM identity_access.external_identity_bindings WHERE issuer = @issuer AND subject = @subject",
            ("issuer", AdminApiTestEnvironment.Authority),
            ("subject", subject)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("""{"subject":null}""")]
    [InlineData("""{"subject":42}""")]
    [InlineData("""{"subject":"valid","extra":true}""")]
    [InlineData("{")]
    public async Task InvalidAccountOnboardingBodyCannotCreateDurableEffects(string body)
    {
        var key = Guid.NewGuid().ToString("N");
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        using var request = RawAccountRequest(environment.CreateToken(), key, body);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var connection = new NpgsqlConnection(environment.OwnerConnectionString);
        await connection.OpenAsync();
        Assert.Equal(0L, await CountAsync(
            connection,
            "SELECT count(*) FROM identity_access.account_onboarding_receipts WHERE provisioned_by_principal_id = @principal AND idempotency_key = @key",
            ("principal", environment.PrincipalId),
            ("key", key)));
    }

    [Fact]
    public async Task RuntimeCannotRewriteOrDeleteIdentityOnboardingFacts()
    {
        await using var connection = new NpgsqlConnection(environment.RuntimeConnectionString);
        await connection.OpenAsync();
        foreach (var sql in new[]
        {
            "UPDATE identity_access.accounts SET availability = 2",
            "DELETE FROM identity_access.accounts",
            "UPDATE identity_access.external_identity_bindings SET subject = 'tampered'",
            "DELETE FROM identity_access.external_identity_bindings",
            "UPDATE identity_access.account_onboarding_receipts SET subject = 'tampered'",
            "DELETE FROM identity_access.account_onboarding_receipts",
            "UPDATE identity_access.identity_link_receipts SET subject = 'tampered'",
            "DELETE FROM identity_access.identity_link_receipts",
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
    }

    private static HttpRequestMessage AccountRequest(string token, string key, string subject) =>
        RawAccountRequest(
            token,
            key,
            JsonSerializer.Serialize(new Dictionary<string, string> { ["subject"] = subject }));

    private static HttpRequestMessage RawAccountRequest(string token, string key, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/platform/accounts")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static HttpRequestMessage LinkRequest(
        string token,
        Guid accountId,
        string key,
        string subject)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/platform/accounts/{accountId:D}/identities")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new Dictionary<string, string> { ["subject"] = subject }),
                Encoding.UTF8,
                "application/json"),
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

    private static async Task<long> CountAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }
}
