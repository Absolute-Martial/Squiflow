using Application.IdentityAccess;

namespace Application.PlatformAdministration;

public enum PlatformAdminAccessAuditOutcome
{
    Denied = 1,
    Succeeded = 2,
}

public sealed record PlatformAdminAccessAuditEntry(
    Guid Id,
    ExternalIdentity Administrator,
    AdminDeviceCertificateFingerprint? DeviceCertificateFingerprint,
    Guid? PrincipalId,
    Guid? DeviceId,
    string Operation,
    PlatformAdminAccessAuditOutcome Outcome,
    string Reason,
    DateTimeOffset OccurredAt);

public interface IPlatformAdminAccessAuditStore
{
    Task AppendAsync(PlatformAdminAccessAuditEntry entry, CancellationToken cancellationToken);
}
