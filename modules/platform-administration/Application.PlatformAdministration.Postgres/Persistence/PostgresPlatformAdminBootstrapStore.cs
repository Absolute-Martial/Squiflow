using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.PlatformAdministration.Postgres;

public sealed class PostgresPlatformAdminBootstrapStore(PlatformAdministrationDbContext database)
    : IPlatformAdminBootstrapStore
{
    private const long BootstrapAdvisoryLockKey = 7_063_918_401;

    public async Task<PlatformAdminBootstrapPreparation> PrepareAsync(
        PlatformAdminBootstrapIntent intent,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        await using var transaction = await database.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await AcquireBootstrapLockAsync(cancellationToken).ConfigureAwait(false);

        var existing = await database.BootstrapStates
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            EnsureSameIntent(existing, intent);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new PlatformAdminBootstrapPreparation(ToSnapshot(existing), Existing: true);
        }

        var principalId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var bootstrapId = Guid.NewGuid();
        database.Principals.Add(new PlatformPrincipalRow
        {
            Id = principalId,
            Issuer = intent.Administrator.Issuer,
            Subject = intent.Administrator.Subject,
            Availability = PlatformPrincipalAvailability.Active,
            CreatedAt = now,
        });
        database.AdminDevices.Add(new AdminDeviceRow
        {
            Id = deviceId,
            PrincipalId = principalId,
            CertificateFingerprint = intent.DeviceCertificateFingerprint.Value,
            DisplayName = intent.DeviceName,
            Availability = AdminDeviceAvailability.Active,
            RegisteredAt = now,
        });
        var bootstrap = new PlatformAdminBootstrapRow
        {
            BootstrapId = bootstrapId,
            SingletonKey = 1,
            PrincipalId = principalId,
            DeviceId = deviceId,
            IdempotencyKey = intent.IdempotencyKey,
            IntentFingerprint = intent.IntentFingerprint,
            Status = PlatformAdminBootstrapStatus.PendingAuthorization,
            PreparedAt = now,
        };
        database.BootstrapStates.Add(bootstrap);
        database.AuditEvents.Add(new PlatformAdminAuditEventRow
        {
            Id = Guid.NewGuid(),
            BootstrapId = bootstrapId,
            PrincipalId = principalId,
            DeviceId = deviceId,
            EventKind = PlatformAdminAuditEventKind.BootstrapPrepared,
            Outcome = PlatformAdminAuditOutcome.Pending,
            OccurredAt = now,
        });

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new PlatformAdminBootstrapPreparation(ToSnapshot(bootstrap), Existing: false);
    }

    public async Task<PlatformAdminBootstrapSnapshot> CompleteAsync(
        Guid bootstrapId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(bootstrapId, Guid.Empty);
        await using var transaction = await database.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await AcquireBootstrapLockAsync(cancellationToken).ConfigureAwait(false);

        var bootstrap = await database.BootstrapStates
            .SingleOrDefaultAsync(row => row.BootstrapId == bootstrapId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Platform Admin bootstrap state does not exist.");

        if (bootstrap.Status == PlatformAdminBootstrapStatus.Completed)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ToSnapshot(bootstrap);
        }

        bootstrap.Status = PlatformAdminBootstrapStatus.Completed;
        bootstrap.CompletedAt = now;
        database.AuditEvents.Add(new PlatformAdminAuditEventRow
        {
            Id = Guid.NewGuid(),
            BootstrapId = bootstrap.BootstrapId,
            PrincipalId = bootstrap.PrincipalId,
            DeviceId = bootstrap.DeviceId,
            EventKind = PlatformAdminAuditEventKind.BootstrapCompleted,
            Outcome = PlatformAdminAuditOutcome.Succeeded,
            OccurredAt = now,
        });
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(bootstrap);
    }

    private async Task AcquireBootstrapLockAsync(CancellationToken cancellationToken)
    {
        _ = await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({BootstrapAdvisoryLockKey})",
            cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureSameIntent(
        PlatformAdminBootstrapRow existing,
        PlatformAdminBootstrapIntent intent)
    {
        if (!string.Equals(existing.IdempotencyKey, intent.IdempotencyKey, StringComparison.Ordinal) ||
            !string.Equals(existing.IntentFingerprint, intent.IntentFingerprint, StringComparison.Ordinal))
        {
            throw new PlatformAdminBootstrapConflictException(
                "Platform Admin bootstrap has already been prepared with a different intent.");
        }
    }

    private static PlatformAdminBootstrapSnapshot ToSnapshot(PlatformAdminBootstrapRow row) =>
        new(
            row.BootstrapId,
            row.PrincipalId,
            row.DeviceId,
            row.Status,
            row.PreparedAt,
            row.CompletedAt);
}
