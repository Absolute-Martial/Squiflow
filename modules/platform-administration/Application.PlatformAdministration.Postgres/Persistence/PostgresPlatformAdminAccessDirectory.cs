using Application.IdentityAccess;
using Microsoft.EntityFrameworkCore;

namespace Application.PlatformAdministration.Postgres;

public sealed class PostgresPlatformAdminAccessDirectory(PlatformAdministrationDbContext database)
    : IPlatformAdminAccessDirectory
{
    public async Task<PlatformAdminAccess?> ResolveActiveAsync(
        ExternalIdentity administrator,
        AdminDeviceCertificateFingerprint deviceCertificateFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(administrator);
        ArgumentNullException.ThrowIfNull(deviceCertificateFingerprint);

        return await database.Principals
            .AsNoTracking()
            .Where(principal =>
                principal.Issuer == administrator.Issuer &&
                principal.Subject == administrator.Subject &&
                principal.Availability == PlatformPrincipalAvailability.Active)
            .Join(
                database.AdminDevices.AsNoTracking().Where(device =>
                    device.CertificateFingerprint == deviceCertificateFingerprint.Value &&
                    device.Availability == AdminDeviceAvailability.Active),
                principal => principal.Id,
                device => device.PrincipalId,
                (principal, device) => new PlatformAdminAccess(principal.Id, device.Id))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
