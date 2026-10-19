using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
public sealed class AccountLifecycleBoundaryTests(AdminApiTestEnvironment environment)
{
    [Fact]
    public async Task SuspendAndReactivatePersistUnderRuntimeRoleAndReplayOriginalEvidence()
    {
        await using var source = NpgsqlDataSource.Create(environment.RuntimeConnectionString);
        var actor = IdentityAdministrationActor.Create(environment.PrincipalId, environment.DeviceId);
        var identity = ExternalIdentity.Create(AdminApiTestEnvironment.Authority, Guid.NewGuid().ToString("N"));
        var created = await new PostgresAccountOnboardingStore(source).OnboardAsync(actor,
            AccountOnboardingIntent.Create(identity, Guid.NewGuid().ToString("N")), DateTimeOffset.UtcNow, CancellationToken.None);
        var account = created.Account!.AccountId;
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        var key = Guid.NewGuid().ToString("N");
        using var suspended = await SendAsync(client, account, "suspend", key, "{\"expectedRevision\":1,\"reason\":\"Support request\"}");
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        Assert.Equal("no-store", suspended.Headers.CacheControl?.ToString());
        using var body = JsonDocument.Parse(await suspended.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetProperty("revision").GetInt32());
        Assert.Equal("disabled", body.RootElement.GetProperty("availability").GetString());
        Assert.Equal(AccountAvailability.Disabled, await new PostgresAccountDirectory(source).FindAvailabilityAsync(account, CancellationToken.None));
        using var reactivated = await SendAsync(client, account, "reactivate", Guid.NewGuid().ToString("N"), "{\"reason\":\"Restored\",\"expectedRevision\":2}");
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        using var replayed = await SendAsync(client, account, "suspend", key, "{\"expectedRevision\":1,\"reason\":\"Support request\"}");
        using var replay = JsonDocument.Parse(await replayed.Content.ReadAsStringAsync());
        Assert.True(replay.RootElement.GetProperty("replayed").GetBoolean());
        Assert.Equal(2, replay.RootElement.GetProperty("revision").GetInt32());
        Assert.Equal(AccountAvailability.Active, await new PostgresAccountDirectory(source).FindAvailabilityAsync(account, CancellationToken.None));
        using var changedReplay = await SendAsync(client, account, "suspend", key, "{\"expectedRevision\":1,\"reason\":\"Changed intent\"}");
        Assert.Equal(HttpStatusCode.Conflict, changedReplay.StatusCode);
        using var stale = await SendAsync(client, account, "suspend", Guid.NewGuid().ToString("N"), "{\"expectedRevision\":1,\"reason\":\"Stale\"}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var receipt = source.CreateCommand("SELECT changed_by_device_id FROM identity_access.account_lifecycle_receipts WHERE changed_by_principal_id = @principal AND idempotency_key = @key");
        receipt.Parameters.AddWithValue("principal", actor.PrincipalId);
        receipt.Parameters.AddWithValue("key", key);
        Assert.Equal(actor.DeviceId, await receipt.ExecuteScalarAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"expectedRevision\":1}")]
    [InlineData("{\"expectedRevision\":0,\"reason\":\"No\"}")]
    [InlineData("{\"expectedRevision\":1,\"reason\":\"\"}")]
    [InlineData("{\"expectedRevision\":1,\"reason\":false}")]
    [InlineData("{\"expectedRevision\":1,\"expectedRevision\":2,\"reason\":\"Duplicate\"}")]
    [InlineData("{\"expectedRevision\":1,\"reason\":\"A\",\"reason\":\"B\"}")]
    [InlineData("{\"expectedRevision\":1,\"reason\":\"A\",\"extra\":1}")]
    public async Task InvalidBodyCannotReachAccountMutation(string body)
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        using var response = await SendAsync(client, Guid.NewGuid(), "suspend", Guid.NewGuid().ToString("N"), body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeviceAndDistinctAccountPermissionPrecedeBodyOrAccountDisclosure()
    {
        using (var noDevice = environment.CreateFactory(certificate: null))
        using (var client = CreateClient(noDevice))
        using (var response = await SendAsync(client, Guid.NewGuid(), "suspend", "denied", "{"))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Application account reader test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var principal = Guid.NewGuid();
        var subject = Guid.NewGuid().ToString("N");
        await using (var owner = new NpgsqlConnection(environment.OwnerConnectionString))
        {
            await owner.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                INSERT INTO platform_administration.principals(id, issuer, subject, availability, created_at, disabled_at)
                VALUES (@principal, @issuer, @subject, 1, now(), NULL);
                INSERT INTO platform_administration.admin_devices(id, principal_id, certificate_fingerprint, display_name, availability, registered_at, revoked_at)
                VALUES (@device, @principal, @fingerprint, 'account reader test', 1, now(), NULL)
                """, owner);
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
        await openFga.Write(new ClientWriteRequest
        {
            Writes = [new ClientTupleKey { User = $"user:{principal:N}", Relation = "can_read_accounts", Object = "platform:root" }],
        }, new ClientWriteOptions { StoreId = environment.StoreId, AuthorizationModelId = environment.ModelId }, CancellationToken.None);
        using var factory = environment.CreateFactory(certificate);
        using var reader = CreateClient(factory);
        using var denied = await SendAsync(reader, Guid.NewGuid(), "suspend", "reader", "{", subject);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task MissingTargetsInvalidTransitionsAndRevisionExhaustionLeaveNoTransitionReceipt()
    {
        await using var source = NpgsqlDataSource.Create(environment.RuntimeConnectionString);
        var actor = IdentityAdministrationActor.Create(environment.PrincipalId, environment.DeviceId);
        var created = await new PostgresAccountOnboardingStore(source).OnboardAsync(actor,
            AccountOnboardingIntent.Create(ExternalIdentity.Create(AdminApiTestEnvironment.Authority,
                Guid.NewGuid().ToString("N")), Guid.NewGuid().ToString("N")), DateTimeOffset.UtcNow, CancellationToken.None);
        var account = created.Account!.AccountId;
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        using var missing = await SendAsync(client, Guid.NewGuid(), "suspend", Guid.NewGuid().ToString("N"),
            "{\"expectedRevision\":1,\"reason\":\"Missing target\"}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var invalid = await SendAsync(client, account, "reactivate", Guid.NewGuid().ToString("N"),
            "{\"expectedRevision\":1,\"reason\":\"Already active\"}");
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);

        await using (var owner = NpgsqlDataSource.Create(environment.OwnerConnectionString))
        await using (var seed = owner.CreateCommand("UPDATE identity_access.accounts SET revision = 2147483647 WHERE id = @account"))
        {
            seed.Parameters.AddWithValue("account", account);
            Assert.Equal(1, await seed.ExecuteNonQueryAsync());
        }
        using var exhausted = await SendAsync(client, account, "suspend", Guid.NewGuid().ToString("N"),
            "{\"expectedRevision\":2147483647,\"reason\":\"Revision limit\"}");
        Assert.Equal(HttpStatusCode.Conflict, exhausted.StatusCode);
        using var problem = JsonDocument.Parse(await exhausted.Content.ReadAsStringAsync());
        Assert.Equal("revision_limit_reached", problem.RootElement.GetProperty("code").GetString());
        using var receiptCount = source.CreateCommand("SELECT count(*) FROM identity_access.account_lifecycle_receipts WHERE account_id = @account");
        receiptCount.Parameters.AddWithValue("account", account);
        Assert.Equal(0L, await receiptCount.ExecuteScalarAsync());
        Assert.Equal(AccountAvailability.Active, await new PostgresAccountDirectory(source).FindAvailabilityAsync(account, CancellationToken.None));
    }

    [Fact]
    public async Task EmptyAccountIdentityIsRejectedAfterDeviceAuthorization()
    {
        foreach (var certificate in new[] { environment.DeviceCertificate, null })
        {
            using var factory = environment.CreateFactory(certificate);
            using var client = CreateClient(factory);
            using var response = await SendAsync(client, Guid.Empty, "suspend", "empty-account", "{}");
            Assert.Equal(certificate is null ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task RuntimeCannotRewriteIdentityOrRetainedLifecycleEvidence()
    {
        await using var source = NpgsqlDataSource.Create(environment.RuntimeConnectionString);
        foreach (var sql in new[]
        {
            "UPDATE identity_access.accounts SET created_at = created_at WHERE false",
            "UPDATE identity_access.accounts SET id = id WHERE false",
            "UPDATE identity_access.account_lifecycle_receipts SET reason = reason WHERE false",
            "DELETE FROM identity_access.account_lifecycle_receipts WHERE false",
        })
        {
            await using var command = source.CreateCommand(sql);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, Guid account, string operation, string key, string body,
        string subject = AdminApiTestEnvironment.Subject)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/platform/accounts/{account:D}/lifecycle/{operation}")
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", environment.CreateToken(subject));
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static HttpClient CreateClient(AdminApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
}
