using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Application.IdentityAccess;

namespace Application.PlatformAdministration;

public enum PlatformAdminBootstrapStatus
{
    PendingAuthorization = 1,
    Completed = 2,
}

public enum PlatformAdminBootstrapExecutionKind
{
    Created = 1,
    Resumed = 2,
    Replayed = 3,
}

public enum PlatformPrincipalAvailability
{
    Active = 1,
    Disabled = 2,
}

public enum AdminDeviceAvailability
{
    Active = 1,
    Revoked = 2,
}

public enum PlatformAdminAuditEventKind
{
    BootstrapPrepared = 1,
    BootstrapCompleted = 2,
}

public enum PlatformAdminAuditOutcome
{
    Pending = 1,
    Succeeded = 2,
}

public sealed record AdminDeviceCertificateFingerprint
{
    public const int HexLength = 64;

    private AdminDeviceCertificateFingerprint(string value) => Value = value;

    public string Value { get; }

    public static AdminDeviceCertificateFingerprint Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != HexLength || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                $"Admin device certificate fingerprint must contain exactly {HexLength} hexadecimal characters.",
                nameof(value));
        }

        return new AdminDeviceCertificateFingerprint(value.ToUpperInvariant());
    }
}

public sealed record PlatformAdminBootstrapIntent
{
    public const int DeviceNameLimit = 200;
    public const int IdempotencyKeyLimit = 200;

    private PlatformAdminBootstrapIntent(
        ExternalIdentity administrator,
        AdminDeviceCertificateFingerprint deviceCertificateFingerprint,
        string deviceName,
        string idempotencyKey,
        string intentFingerprint)
    {
        Administrator = administrator;
        DeviceCertificateFingerprint = deviceCertificateFingerprint;
        DeviceName = deviceName;
        IdempotencyKey = idempotencyKey;
        IntentFingerprint = intentFingerprint;
    }

    public ExternalIdentity Administrator { get; }

    public AdminDeviceCertificateFingerprint DeviceCertificateFingerprint { get; }

    public string DeviceName { get; }

    public string IdempotencyKey { get; }

    public string IntentFingerprint { get; }

    public static PlatformAdminBootstrapIntent Create(
        ExternalIdentity administrator,
        AdminDeviceCertificateFingerprint deviceCertificateFingerprint,
        string deviceName,
        string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(administrator);
        ArgumentNullException.ThrowIfNull(deviceCertificateFingerprint);

        var normalizedDeviceName = NormalizeDisplayValue(deviceName, DeviceNameLimit, nameof(deviceName));
        ValidateIdempotencyKey(idempotencyKey);

        return new PlatformAdminBootstrapIntent(
            administrator,
            deviceCertificateFingerprint,
            normalizedDeviceName,
            idempotencyKey,
            ComputeIntentFingerprint(
                administrator.Issuer,
                administrator.Subject,
                deviceCertificateFingerprint.Value,
                normalizedDeviceName));
    }

    private static string NormalizeDisplayValue(string value, int limit, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > limit || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Value must be at most {limit} characters and contain no control characters.",
                parameterName);
        }

        return normalized;
    }

    private static void ValidateIdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Length > IdempotencyKeyLimit ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Idempotency key must be exact, at most {IdempotencyKeyLimit} characters and contain no control characters.",
                nameof(value));
        }
    }

    private static string ComputeIntentFingerprint(params string[] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

public sealed record PlatformAdminBootstrapSnapshot(
    Guid BootstrapId,
    Guid PrincipalId,
    Guid DeviceId,
    PlatformAdminBootstrapStatus Status,
    DateTimeOffset PreparedAt,
    DateTimeOffset? CompletedAt);

public sealed record PlatformAdminBootstrapPreparation(
    PlatformAdminBootstrapSnapshot Snapshot,
    bool Existing);

public sealed record PlatformAdminBootstrapOutcome(
    PlatformAdminBootstrapSnapshot Snapshot,
    PlatformAdminBootstrapExecutionKind ExecutionKind);

public interface IPlatformAdminBootstrapStore
{
    Task<PlatformAdminBootstrapPreparation> PrepareAsync(
        PlatformAdminBootstrapIntent intent,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<PlatformAdminBootstrapSnapshot> CompleteAsync(
        Guid bootstrapId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public interface IInitialPlatformAdministratorProvisioner
{
    Task EnsureAdministratorAsync(Guid platformPrincipalId, CancellationToken cancellationToken);
}

public sealed class PlatformAdminBootstrapCoordinator(
    IPlatformAdminBootstrapStore store,
    IInitialPlatformAdministratorProvisioner administratorProvisioner,
    TimeProvider timeProvider)
{
    public async Task<PlatformAdminBootstrapOutcome> ExecuteAsync(
        PlatformAdminBootstrapIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var preparation = await store.PrepareAsync(
            intent,
            timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false);

        if (preparation.Snapshot.Status == PlatformAdminBootstrapStatus.Completed)
        {
            return new PlatformAdminBootstrapOutcome(
                preparation.Snapshot,
                PlatformAdminBootstrapExecutionKind.Replayed);
        }

        await administratorProvisioner.EnsureAdministratorAsync(
            preparation.Snapshot.PrincipalId,
            cancellationToken).ConfigureAwait(false);

        var completed = await store.CompleteAsync(
            preparation.Snapshot.BootstrapId,
            timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false);

        return new PlatformAdminBootstrapOutcome(
            completed,
            preparation.Existing
                ? PlatformAdminBootstrapExecutionKind.Resumed
                : PlatformAdminBootstrapExecutionKind.Created);
    }
}

public sealed class PlatformAdminBootstrapConflictException(string message) : Exception(message);

public sealed class PlatformAuthorizationProviderUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
