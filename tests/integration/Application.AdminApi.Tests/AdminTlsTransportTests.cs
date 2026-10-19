using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class AdminTlsTransportTests(AdminApiTestEnvironment environment)
{
    [Fact]
    public async Task ActualTlsCertificateControlsAdmissionAndRevocationDespiteAStillValidToken()
    {
        using var serverKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var serverCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var baseline = environment.CreateFactory(certificate: null);
        using var factory = baseline.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAdminClientCertificateProvider>();
            services.AddSingleton<IAdminClientCertificateProvider, ConnectionAdminClientCertificateProvider>();
        }));
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(serverCertificate)));
        factory.StartServer();
        var address = Assert.Single(factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses);
        var origin = new UriBuilder(address) { Host = "localhost" }.Uri;
        var serverFingerprint = serverCertificate.GetCertHashString(HashAlgorithmName.SHA256);
        var token = environment.CreateToken();

        using var registered = Client(environment.DeviceCertificate);
        using var ready = await registered.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        using var allowed = await AccessAsync(registered);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal("no-store", allowed.Headers.CacheControl?.ToString());

        using var missingCertificate = Client(certificate: null);
        using var forged = await AccessAsync(missingCertificate, forged: true);
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        using var problem = JsonDocument.Parse(await forged.Content.ReadAsStringAsync());
        Assert.Equal("admin_device_required", problem.RootElement.GetProperty("code").GetString());

        using var unknownKey = RSA.Create(2048);
        var unknownRequest = new CertificateRequest("CN=Application unregistered Admin TLS test", unknownKey,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var unknownCertificate = unknownRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var unknown = Client(unknownCertificate);
        using var rejected = await AccessAsync(unknown);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);

        await SetRegistrationAsync(active: false);
        try
        {
            using var revoked = await AccessAsync(registered);
            Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        }
        finally
        {
            await SetRegistrationAsync(active: true);
        }
        using var restored = await AccessAsync(registered);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);

        HttpClient Client(X509Certificate2? certificate)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, presented, _, errors) =>
                    presented?.GetCertHashString(HashAlgorithmName.SHA256) == serverFingerprint &&
                    (errors & SslPolicyErrors.RemoteCertificateNameMismatch) == 0,
                UseProxy = false,
            };
            if (certificate is not null) handler.ClientCertificates.Add(certificate);
            return new HttpClient(handler) { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(15) };
        }

        async Task<HttpResponseMessage> AccessAsync(HttpClient client, bool forged = false)
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, "/api/v1/platform/access");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (forged)
            {
                message.Headers.Add("X-Forwarded-Client-Cert", environment.DeviceCertificate.GetCertHashString(HashAlgorithmName.SHA256));
                message.Headers.Add("X-Client-Certificate", Convert.ToBase64String(environment.DeviceCertificate.RawData));
            }
            return await client.SendAsync(message);
        }
    }

    private async Task SetRegistrationAsync(bool active)
    {
        await using var owner = NpgsqlDataSource.Create(environment.OwnerConnectionString);
        await using var command = owner.CreateCommand("""
            UPDATE platform_administration.admin_devices SET availability = @availability,
                revoked_at = CASE WHEN @availability = 1 THEN NULL ELSE now() END WHERE id = @device
            """);
        command.Parameters.AddWithValue("availability", (short)(active ? 1 : 2));
        command.Parameters.AddWithValue("device", environment.DeviceId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}
