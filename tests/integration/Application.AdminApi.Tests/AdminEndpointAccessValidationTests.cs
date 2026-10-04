using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Application.AdminApi;
using Application.AdminApi.Authorization;
using Application.IdentityAccess;
using Application.PlatformAdministration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Application.AdminApi.Tests;

public sealed class AdminEndpointAccessValidationTests
{
    [Fact]
    public void ClassificationAuthenticationAndPermissionMustAgree()
    {
        var protectedAccess = new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration);
        var permission = new AdminEndpointPermissionMetadata(PlatformAdminPermission.ReadTenants);
        var auditOperation = new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.RegistryTenantsBrowse);
        var publicAccess = new AdminEndpointAccessMetadata(AdminEndpointAccess.PublicHealth);

        Assert.Throws<InvalidOperationException>(() => Validate());
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess, new AuthorizeAttribute()));
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess, permission, new AuthorizeAttribute()));
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess, auditOperation, new AuthorizeAttribute()));
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess, permission, auditOperation));
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess, permission, auditOperation, new AuthorizeAttribute(), new AllowAnonymousAttribute()));
        Assert.Throws<InvalidOperationException>(() => Validate(publicAccess, permission));
        Assert.Throws<InvalidOperationException>(() => Validate(publicAccess, auditOperation));
        Assert.Throws<InvalidOperationException>(() => Validate(new AdminEndpointAccessMetadata((AdminEndpointAccess)int.MaxValue)));

        Validate(protectedAccess, permission, auditOperation, new AuthorizeAttribute());
        Validate(publicAccess);
    }

    [Fact]
    public async Task RuntimeAuthorizationUsesTheValidatedEndpointPermissionDeclaration()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Admin permission metadata test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        var principalId = Guid.CreateVersion7();
        var deviceId = Guid.CreateVersion7();
        var authorization = new RecordingPlatformAdminAuthorization();
        var audit = new RecordingAuditStore();
        var authorizer = new PlatformAdminRequestAuthorizer(
            new FixedCertificateProvider(certificate),
            new FixedAccessDirectory(principalId, deviceId),
            audit,
            authorization,
            TimeProvider.System);
        using var services = new ServiceCollection()
            .AddSingleton(authorizer)
            .BuildServiceProvider();
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/test"),
            0,
            new EndpointMetadataCollection(
                new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration),
                new AdminEndpointPermissionMetadata(PlatformAdminPermission.ReadAccounts),
                new AdminEndpointAuditMetadata(AdminEndpointAuditOperation.RegistryAccountsBrowse),
                new AuthorizeAttribute()),
            "test route");
        var context = new DefaultHttpContext();
        context.RequestServices = services;
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("iss", "https://issuer.example.test"),
                new Claim("sub", "admin-permission-test"),
            ],
            "test"));
        context.SetEndpoint(endpoint);
        PlatformAdminRequestAccess? middlewareAccess = null;
        var application = new ApplicationBuilder(services);
        application.UseAdminApiPlatformAuthorization();
        application.Run(httpContext =>
        {
            middlewareAccess = AdminApiPlatformAuthorization.GetRequiredAccess(httpContext);
            return Task.CompletedTask;
        });

        await application.Build()(context);

        Assert.Equal(PlatformAdminPermission.ReadAccounts, authorization.ObservedPermission);
        Assert.Equal(principalId, middlewareAccess?.PrincipalId);
        Assert.Equal(deviceId, middlewareAccess?.DeviceId);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal("registry_accounts_browse", entry.Operation);
        Assert.Equal(PlatformAdminAccessAuditOutcome.Succeeded, entry.Outcome);
    }

    private static void Validate(params object[] metadata)
    {
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/test"),
            0,
            new EndpointMetadataCollection(metadata),
            "test route");
        AdminApiEndpointAccessValidation.Validate([endpoint]);
    }

    private sealed class FixedCertificateProvider(X509Certificate2 certificate) : IAdminClientCertificateProvider
    {
        public Task<X509Certificate2?> GetAsync(HttpContext context, CancellationToken cancellationToken) =>
            Task.FromResult<X509Certificate2?>(certificate);
    }

    private sealed class FixedAccessDirectory(Guid principalId, Guid deviceId) : IPlatformAdminAccessDirectory
    {
        public Task<PlatformAdminAccess?> ResolveActiveAsync(
            ExternalIdentity administrator,
            AdminDeviceCertificateFingerprint deviceCertificateFingerprint,
            CancellationToken cancellationToken) =>
            Task.FromResult<PlatformAdminAccess?>(new PlatformAdminAccess(principalId, deviceId));
    }

    private sealed class RecordingAuditStore : IPlatformAdminAccessAuditStore
    {
        internal List<PlatformAdminAccessAuditEntry> Entries { get; } = [];

        public Task AppendAsync(PlatformAdminAccessAuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPlatformAdminAuthorization : IPlatformAdminAuthorization
    {
        internal PlatformAdminPermission? ObservedPermission { get; private set; }

        public Task<bool> CanAccessAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Record(PlatformAdminPermission.Access);

        public Task<bool> CanProvisionTenantAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Record(PlatformAdminPermission.ProvisionTenant);

        public Task<bool> CanOnboardAccountAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Record(PlatformAdminPermission.OnboardAccount);

        public Task<bool> CanLinkIdentityAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Record(PlatformAdminPermission.LinkIdentity);

        public Task<bool> CanManageMembershipsAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Record(PlatformAdminPermission.ManageMemberships);

        public Task<bool> CanManageTenantLifecycleAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Record(PlatformAdminPermission.ManageTenantLifecycle);

        public Task<bool> CanReadTenantsAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Record(PlatformAdminPermission.ReadTenants);

        public Task<bool> CanReadAccountsAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Record(PlatformAdminPermission.ReadAccounts);

        public Task<bool> CanReadMembershipsAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Record(PlatformAdminPermission.ReadMemberships);

        public Task<bool> IsReadyAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        private Task<bool> Record(PlatformAdminPermission permission)
        {
            ObservedPermission = permission;
            return Task.FromResult(true);
        }
    }
}
