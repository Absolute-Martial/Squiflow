using Application.IdentityAccess;

namespace Application.PlatformAdministration;

public sealed record PlatformAdminAccess(Guid PrincipalId, Guid DeviceId);

public interface IPlatformAdminAccessDirectory
{
    Task<PlatformAdminAccess?> ResolveActiveAsync(
        ExternalIdentity administrator,
        AdminDeviceCertificateFingerprint deviceCertificateFingerprint,
        CancellationToken cancellationToken);
}
