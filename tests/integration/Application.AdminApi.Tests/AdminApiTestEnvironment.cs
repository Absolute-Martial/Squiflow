using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Application.AdminApi;
using Application.AdminApi.IdentityProvisioning;
using Application.IdentityAccess;
using Application.IdentityAccess.Postgres;
using Application.PlatformAdministration;
using Application.PlatformAdministration.Postgres;
using Application.Tenancy.Postgres;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;
using OpenFga.Sdk.Model;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.AdminApi.Tests;

public sealed class AdminApiTestEnvironment : IAsyncLifetime
{
    internal const string Authority = "https://admin-issuer.example.test";
    internal const string Audience = "application-admin-api";
    internal const string Subject = "platform-admin-subject";
    internal const string RuntimeRole = "application_admin_api_test";
    internal const string RuntimePassword = "local-admin-api-test-only";
    private const ushort OpenFgaPort = 8080;

    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithDatabase("application_admin_api_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    private readonly IContainer _openFga =
        new ContainerBuilder(new DockerImage(repository: "openfga/openfga", tag: "v1.21.0"))
            .WithCommand("run", "--playground-enabled=false")
            .WithPortBinding(OpenFgaPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(OpenFgaPort).ForPath("/healthz")))
            .Build();
    private readonly RSA _signingRsa = RSA.Create(2048);
    private readonly RSA _deviceRsa = RSA.Create(2048);

    internal RsaSecurityKey SigningKey { get; private set; } = null!;
    internal X509Certificate2 DeviceCertificate { get; private set; } = null!;
    internal Guid PrincipalId { get; private set; }
    internal Guid DeviceId { get; private set; }
    internal string StoreId { get; private set; } = string.Empty;
    internal string ModelId { get; private set; } = string.Empty;
    internal string OpenFgaApiUrl =>
        $"http://127.0.0.1:{_openFga.GetMappedPublicPort(OpenFgaPort)}";
    internal string OwnerConnectionString => _database.GetConnectionString();
    internal string RuntimeConnectionString => new NpgsqlConnectionStringBuilder(OwnerConnectionString)
    {
        Username = RuntimeRole,
        Password = RuntimePassword,
    }.ConnectionString;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _openFga.StartAsync());

        SigningKey = new RsaSecurityKey(_signingRsa) { KeyId = "admin-api-test-key" };
        var certificateRequest = new CertificateRequest(
            "CN=Application Admin API Test Device",
            _deviceRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        DeviceCertificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));

        await using (var context = IdentityAccessPostgresMigrations.CreateContext(OwnerConnectionString))
        {
            await context.Database.MigrateAsync();
        }

        await using (var context = TenancyPostgresMigrations.CreateContext(OwnerConnectionString))
        {
            await context.Database.MigrateAsync();
        }

        await using (var context =
                     PlatformAdministrationPostgresMigrations.CreateContext(OwnerConnectionString))
        {
            await context.Database.MigrateAsync();
            Assert.False(context.Database.HasPendingModelChanges());

            var store = new PostgresPlatformAdminBootstrapStore(context);
            var intent = PlatformAdminBootstrapIntent.Create(
                ExternalIdentity.Create(Authority, Subject),
                AdminDeviceCertificateFingerprint.Create(
                    DeviceCertificate.GetCertHashString(HashAlgorithmName.SHA256)),
                "admin-api-test-device",
                "admin-api-test-bootstrap");
            var prepared = await store.PrepareAsync(
                intent,
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            var completed = await store.CompleteAsync(
                prepared.Snapshot.BootstrapId,
                DateTimeOffset.UtcNow.AddMilliseconds(1),
                CancellationToken.None);
            PrincipalId = completed.PrincipalId;
            DeviceId = completed.DeviceId;
        }

        await CreatePlatformAuthorizationAsync();
        await ProvisionRuntimeRoleAsync();
    }

    public async Task DisposeAsync()
    {
        DeviceCertificate.Dispose();
        _deviceRsa.Dispose();
        _signingRsa.Dispose();
        await _openFga.DisposeAsync();
        await _database.DisposeAsync();
    }

    internal AdminApiFactory CreateFactory(
        X509Certificate2? certificate,
        string? openFgaApiUrl = null,
        IExternalIdentityVerifier? identityVerifier = null,
        string? authorizationModelId = null) =>
        new(
            this,
            certificate,
            openFgaApiUrl ?? OpenFgaApiUrl,
            identityVerifier ?? new FixedExternalIdentityVerifier(ExternalIdentityVerification.Verified),
            authorizationModelId ?? ModelId);

    internal async Task<string> PublishAuthorizationModelAsync(string modelJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelJson);
        using var administrativeClient = new HttpClient { BaseAddress = new Uri(OpenFgaApiUrl) };
        using var modelContent = new StringContent(modelJson, Encoding.UTF8, "application/json");
        using var modelResponse = await administrativeClient.PostAsync(
            $"/stores/{StoreId}/authorization-models",
            modelContent);
        modelResponse.EnsureSuccessStatusCode();
        using var modelBody = JsonDocument.Parse(await modelResponse.Content.ReadAsStringAsync());
        return modelBody.RootElement.GetProperty("authorization_model_id").GetString()
            ?? throw new InvalidOperationException("OpenFGA did not return an authorization model ID.");
    }

    internal string CreateToken(string subject = Subject)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: Authority,
            audience: Audience,
            claims: [new Claim("sub", subject)],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal async Task ClearAccessAuditAsync()
    {
        await using var connection = new NpgsqlConnection(OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "TRUNCATE TABLE platform_administration.access_audit_events",
            connection);
        _ = await command.ExecuteNonQueryAsync();
    }

    internal async Task<(short Outcome, string Reason, Guid? PrincipalId, Guid? DeviceId)>
        LatestAuditAsync()
    {
        await using var connection = new NpgsqlConnection(OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT outcome, reason, principal_id, device_id
            FROM platform_administration.access_audit_events
            ORDER BY occurred_at DESC, id DESC
            LIMIT 1
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (
            reader.GetInt16(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3));
    }

    private async Task CreatePlatformAuthorizationAsync()
    {
        using var administrativeClient = new HttpClient { BaseAddress = new Uri(OpenFgaApiUrl) };
        using var storeResponse = await administrativeClient.PostAsJsonAsync(
            "/stores",
            new { name = "platform-admin-api-tests" });
        storeResponse.EnsureSuccessStatusCode();
        using var storeBody = JsonDocument.Parse(await storeResponse.Content.ReadAsStringAsync());
        StoreId = storeBody.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("OpenFGA did not return a store ID.");

        var modelJson = await File.ReadAllTextAsync(Path.Combine(
            FindRepositoryRoot(),
            "infrastructure",
            "authorization",
            "openfga",
            "platform-authorization-model.json"));
        using var modelContent = new StringContent(modelJson, Encoding.UTF8, "application/json");
        using var modelResponse = await administrativeClient.PostAsync(
            $"/stores/{StoreId}/authorization-models",
            modelContent);
        modelResponse.EnsureSuccessStatusCode();
        using var modelBody = JsonDocument.Parse(await modelResponse.Content.ReadAsStringAsync());
        ModelId = modelBody.RootElement.GetProperty("authorization_model_id").GetString()
            ?? throw new InvalidOperationException("OpenFGA did not return an authorization model ID.");

        using var openFga = new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = OpenFgaApiUrl,
            StoreId = StoreId,
            AuthorizationModelId = ModelId,
            Credentials = new Credentials { Method = CredentialsMethod.None },
        });
        await openFga.Write(
            new ClientWriteRequest
            {
                Writes =
                [
                    new ClientTupleKey
                    {
                        User = $"user:{PrincipalId:N}",
                        Relation = "administrator",
                        Object = "platform:root",
                    },
                ],
            },
            new ClientWriteOptions
            {
                StoreId = StoreId,
                AuthorizationModelId = ModelId,
            },
            CancellationToken.None);
    }

    private async Task ProvisionRuntimeRoleAsync()
    {
        await using var connection = new NpgsqlConnection(OwnerConnectionString);
        await connection.OpenAsync();

        await using (var create = new NpgsqlCommand(
            $"""
            CREATE ROLE {RuntimeRole} LOGIN PASSWORD '{RuntimePassword}'
                NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
            """,
            connection))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using (var setRole = new NpgsqlCommand(
            "SELECT set_config('app.provision_admin_api_role', @role, false)",
            connection))
        {
            setRole.Parameters.AddWithValue("role", RuntimeRole);
            _ = await setRole.ExecuteScalarAsync();
        }

        var script = await File.ReadAllTextAsync(Path.Combine(
            FindRepositoryRoot(),
            "deploy",
            "database",
            "grant-admin-api.sql"));
        await using var grant = new NpgsqlCommand(script, connection);
        _ = await grant.ExecuteNonQueryAsync();
    }

    internal static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Application.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Cannot locate Application.slnx from test output.");
    }
}

internal sealed class AdminApiFactory : WebApplicationFactory<Program>
{
    private readonly AdminApiTestEnvironment _environment;
    private readonly X509Certificate2? _certificate;
    private readonly IExternalIdentityVerifier _identityVerifier;

    internal AdminApiFactory(
        AdminApiTestEnvironment environment,
        X509Certificate2? certificate,
        string openFgaApiUrl,
        IExternalIdentityVerifier identityVerifier,
        string authorizationModelId)
    {
        _environment = environment;
        _certificate = certificate;
        _identityVerifier = identityVerifier;
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__PlatformAdministration",
            environment.RuntimeConnectionString);
        Environment.SetEnvironmentVariable("AllowedHosts", "localhost");
        Environment.SetEnvironmentVariable("AdminApi__MaximumConcurrentRequests", "4");
        Environment.SetEnvironmentVariable("AdminApi__ProtectedRequestTimeoutSeconds", "30");
        Environment.SetEnvironmentVariable("Authentication__Authority", AdminApiTestEnvironment.Authority);
        Environment.SetEnvironmentVariable("Authentication__Audience", AdminApiTestEnvironment.Audience);
        Environment.SetEnvironmentVariable("Authentication__BackchannelTimeoutSeconds", "2");
        Environment.SetEnvironmentVariable("Authentication__ClockSkewSeconds", "0");
        Environment.SetEnvironmentVariable("Authorization__PlatformOpenFga__ApiUrl", openFgaApiUrl);
        Environment.SetEnvironmentVariable("Authorization__PlatformOpenFga__StoreId", environment.StoreId);
        Environment.SetEnvironmentVariable(
            "Authorization__PlatformOpenFga__AuthorizationModelId",
            authorizationModelId);
        Environment.SetEnvironmentVariable("Authorization__PlatformOpenFga__RequestTimeoutSeconds", "1");
        Environment.SetEnvironmentVariable("Authorization__PlatformOpenFga__MaximumRetries", "0");
        Environment.SetEnvironmentVariable(
            "Authorization__PlatformOpenFga__MinimumRetryDelayMilliseconds",
            "1");
        Environment.SetEnvironmentVariable("Authorization__PlatformOpenFga__CredentialMethod", "None");
        Environment.SetEnvironmentVariable(
            "IdentityProvisioning__Zitadel__ApiUrl",
            AdminApiTestEnvironment.Authority);
        Environment.SetEnvironmentVariable(
            "IdentityProvisioning__Zitadel__ApiToken",
            "admin-api-test-identity-provider-token");
        Environment.SetEnvironmentVariable(
            "IdentityProvisioning__Zitadel__RequestTimeoutSeconds",
            "1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAdminClientCertificateProvider>();
            services.AddSingleton<IAdminClientCertificateProvider>(
                new FixedAdminClientCertificateProvider(_certificate));
            services.RemoveAll<IExternalIdentityVerifier>();
            services.AddSingleton(_identityVerifier);
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    var metadata = new OpenIdConnectConfiguration
                    {
                        Issuer = AdminApiTestEnvironment.Authority,
                    };
                    metadata.SigningKeys.Add(_environment.SigningKey);
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                });
        });
    }
}

internal sealed class FixedAdminClientCertificateProvider(X509Certificate2? certificate)
    : IAdminClientCertificateProvider
{
    public Task<X509Certificate2?> GetAsync(
        HttpContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(certificate);
}

internal sealed class FixedExternalIdentityVerifier(ExternalIdentityVerification result)
    : IExternalIdentityVerifier
{
    public Task<ExternalIdentityVerification> VerifyAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken) =>
        Task.FromResult(result);
}

internal sealed class ThrowingExternalIdentityVerifier(Exception exception)
    : IExternalIdentityVerifier
{
    public Task<ExternalIdentityVerification> VerifyAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken) =>
        Task.FromException<ExternalIdentityVerification>(exception);
}
