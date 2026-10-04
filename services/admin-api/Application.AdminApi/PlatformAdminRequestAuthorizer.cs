using System.Security.Cryptography;
using Application.AdminApi.Authorization;
using Application.IdentityAccess;
using Application.PlatformAdministration;

namespace Application.AdminApi;

internal enum PlatformAdminPermission
{
    Access = 1,
    ProvisionTenant = 2,
    OnboardAccount = 3,
    LinkIdentity = 4,
    ManageMemberships = 5,
    ManageTenantLifecycle = 6,
    ReadTenants = 7,
    ReadAccounts = 8,
    ReadMemberships = 9,
}

internal sealed record PlatformAdminRequestAccess(Guid PrincipalId, Guid DeviceId);

internal sealed record PlatformAdminRequestAuthorization(
    PlatformAdminRequestAccess? Access,
    IResult? Failure);

internal sealed class PlatformAdminRequestAuthorizer(
    IAdminClientCertificateProvider certificates,
    IPlatformAdminAccessDirectory directory,
    IPlatformAdminAccessAuditStore audit,
    IPlatformAdminAuthorization authorization,
    TimeProvider timeProvider)
{
    internal Task<PlatformAdminRequestAuthorization> AuthorizeAsync(
        HttpContext context,
        string auditOperation) =>
        AuthorizeCoreAsync(context, auditOperation);

    private async Task<PlatformAdminRequestAuthorization> AuthorizeCoreAsync(
        HttpContext context,
        string auditOperation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditOperation);
        var permission = DeclaredPermission(context);

        if (!context.Request.IsHttps)
        {
            return Denied(
                StatusCodes.Status403Forbidden,
                "admin_tls_required",
                "Platform Admin access requires TLS.");
        }

        var issuerClaims = context.User.FindAll("iss").Select(claim => claim.Value).ToArray();
        var subjectClaims = context.User.FindAll("sub").Select(claim => claim.Value).ToArray();
        if (issuerClaims.Length != 1 || subjectClaims.Length != 1)
        {
            return Denied(
                StatusCodes.Status403Forbidden,
                "platform_admin_access_denied",
                "Platform Admin access denied.");
        }

        ExternalIdentity administrator;
        try
        {
            administrator = ExternalIdentity.Create(issuerClaims[0], subjectClaims[0]);
        }
        catch (ArgumentException)
        {
            return Denied(
                StatusCodes.Status403Forbidden,
                "platform_admin_access_denied",
                "Platform Admin access denied.");
        }

        var certificate = await certificates
            .GetAsync(context, context.RequestAborted)
            .ConfigureAwait(false);
        if (certificate is null)
        {
            await AppendAuditAsync(
                administrator,
                null,
                null,
                null,
                auditOperation,
                PlatformAdminAccessAuditOutcome.Denied,
                "admin_device_required",
                context.RequestAborted).ConfigureAwait(false);
            return Denied(
                StatusCodes.Status403Forbidden,
                "admin_device_required",
                "A registered Admin device certificate is required.");
        }

        var fingerprint = AdminDeviceCertificateFingerprint.Create(
            certificate.GetCertHashString(HashAlgorithmName.SHA256));
        var access = await directory.ResolveActiveAsync(
            administrator,
            fingerprint,
            context.RequestAborted).ConfigureAwait(false);
        if (access is null)
        {
            await AppendAuditAsync(
                administrator,
                fingerprint,
                null,
                null,
                auditOperation,
                PlatformAdminAccessAuditOutcome.Denied,
                "admin_device_not_active",
                context.RequestAborted).ConfigureAwait(false);
            return Denied(
                StatusCodes.Status403Forbidden,
                "platform_admin_access_denied",
                "Platform Admin access denied.");
        }

        bool allowed;
        try
        {
            allowed = permission switch
            {
                PlatformAdminPermission.Access => await authorization
                    .CanAccessAsync(access.PrincipalId, context.RequestAborted)
                    .ConfigureAwait(false),
                PlatformAdminPermission.ProvisionTenant => await authorization
                    .CanProvisionTenantAsync(access.PrincipalId, context.RequestAborted)
                    .ConfigureAwait(false),
                PlatformAdminPermission.OnboardAccount => await authorization
                    .CanOnboardAccountAsync(access.PrincipalId, context.RequestAborted)
                    .ConfigureAwait(false),
                PlatformAdminPermission.LinkIdentity => await authorization
                    .CanLinkIdentityAsync(access.PrincipalId, context.RequestAborted)
                    .ConfigureAwait(false),
                PlatformAdminPermission.ManageMemberships => await authorization
                    .CanManageMembershipsAsync(access.PrincipalId, context.RequestAborted)
                    .ConfigureAwait(false),
                PlatformAdminPermission.ManageTenantLifecycle => await authorization
                    .CanManageTenantLifecycleAsync(access.PrincipalId, context.RequestAborted)
                    .ConfigureAwait(false),
                PlatformAdminPermission.ReadTenants => await authorization
                    .CanReadTenantsAsync(access.PrincipalId, context.RequestAborted)
                    .ConfigureAwait(false),
                PlatformAdminPermission.ReadAccounts => await authorization
                    .CanReadAccountsAsync(access.PrincipalId, context.RequestAborted)
                    .ConfigureAwait(false),
                PlatformAdminPermission.ReadMemberships => await authorization
                    .CanReadMembershipsAsync(access.PrincipalId, context.RequestAborted)
                    .ConfigureAwait(false),
                _ => throw new InvalidOperationException("Unknown Platform Admin permission."),
            };
        }
        catch (AdminAuthorizationProviderUnavailableException)
        {
            await AppendAuditAsync(
                administrator,
                fingerprint,
                access.PrincipalId,
                access.DeviceId,
                auditOperation,
                PlatformAdminAccessAuditOutcome.Denied,
                "authorization_unavailable",
                context.RequestAborted).ConfigureAwait(false);
            throw;
        }

        if (!allowed)
        {
            await AppendAuditAsync(
                administrator,
                fingerprint,
                access.PrincipalId,
                access.DeviceId,
                auditOperation,
                PlatformAdminAccessAuditOutcome.Denied,
                "authorization_denied",
                context.RequestAborted).ConfigureAwait(false);
            return Denied(
                StatusCodes.Status403Forbidden,
                "platform_admin_access_denied",
                "Platform Admin access denied.");
        }

        await AppendAuditAsync(
            administrator,
            fingerprint,
            access.PrincipalId,
            access.DeviceId,
            auditOperation,
            PlatformAdminAccessAuditOutcome.Succeeded,
            "authorized",
            context.RequestAborted).ConfigureAwait(false);
        return new PlatformAdminRequestAuthorization(
            new PlatformAdminRequestAccess(access.PrincipalId, access.DeviceId),
            null);
    }

    private static PlatformAdminPermission DeclaredPermission(HttpContext context)
    {
        var permissions = context.GetEndpoint()?.Metadata
            .GetOrderedMetadata<AdminEndpointPermissionMetadata>();
        if (permissions is null || permissions.Count != 1 || !Enum.IsDefined(permissions[0].Permission))
        {
            throw new InvalidOperationException(
                "Protected Admin API authorization requires exactly one validated endpoint permission declaration.");
        }

        return permissions[0].Permission;
    }

    private Task AppendAuditAsync(
        ExternalIdentity administrator,
        AdminDeviceCertificateFingerprint? fingerprint,
        Guid? principalId,
        Guid? deviceId,
        string operation,
        PlatformAdminAccessAuditOutcome outcome,
        string reason,
        CancellationToken cancellationToken) =>
        audit.AppendAsync(
            new PlatformAdminAccessAuditEntry(
                Guid.CreateVersion7(),
                administrator,
                fingerprint,
                principalId,
                deviceId,
                operation,
                outcome,
                reason,
                timeProvider.GetUtcNow()),
            cancellationToken);

    private static PlatformAdminRequestAuthorization Denied(
        int status,
        string code,
        string title) =>
        new(
            null,
            Results.Problem(
                statusCode: status,
                title: title,
                extensions: new Dictionary<string, object?> { ["code"] = code }));
}
