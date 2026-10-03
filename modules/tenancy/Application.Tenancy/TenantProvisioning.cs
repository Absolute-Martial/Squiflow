using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Application.Tenancy;

public enum ProvisionTenantStatus
{
    Created = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
}

public sealed record TenantProvisioningActor
{
    private TenantProvisioningActor(Guid principalId, Guid deviceId)
    {
        PrincipalId = principalId;
        DeviceId = deviceId;
    }

    public Guid PrincipalId { get; }

    public Guid DeviceId { get; }

    public static TenantProvisioningActor Create(Guid principalId, Guid deviceId)
    {
        if (principalId == Guid.Empty)
        {
            throw new ArgumentException("Platform principal identity cannot be empty.", nameof(principalId));
        }

        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("Admin device identity cannot be empty.", nameof(deviceId));
        }

        return new TenantProvisioningActor(principalId, deviceId);
    }
}

public sealed record TenantProvisioningIntent
{
    public const int DisplayNameLimit = 200;
    public const int IdempotencyKeyLimit = 200;

    private TenantProvisioningIntent(string displayName, string idempotencyKey, string fingerprint)
    {
        DisplayName = displayName;
        IdempotencyKey = idempotencyKey;
        Fingerprint = fingerprint;
    }

    public string DisplayName { get; }

    public string IdempotencyKey { get; }

    public string Fingerprint { get; }

    public static TenantProvisioningIntent Create(string displayName, string idempotencyKey)
    {
        var normalizedDisplayName = RequireBounded(displayName, DisplayNameLimit, nameof(displayName))
            .Normalize(NormalizationForm.FormC);
        if (normalizedDisplayName.Length > DisplayNameLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(displayName),
                $"{nameof(displayName)} must be at most {DisplayNameLimit} characters.");
        }
        var normalizedIdempotencyKey = RequireBounded(
            idempotencyKey,
            IdempotencyKeyLimit,
            nameof(idempotencyKey));
        return new TenantProvisioningIntent(
            normalizedDisplayName,
            normalizedIdempotencyKey,
            ComputeFingerprint(normalizedDisplayName));
    }

    private static string RequireBounded(string value, int limit, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!HasWellFormedUtf16(value) || value.Any(char.IsControl))
        {
            throw new ArgumentException($"{parameterName} contains invalid characters.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > limit)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"{parameterName} must be at most {limit} characters.");
        }

        return normalized;
    }

    private static bool HasWellFormedUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (++index == value.Length || !char.IsLowSurrogate(value[index])) return false;
            }
            else if (char.IsLowSurrogate(value[index])) return false;
        }

        return true;
    }

    private static string ComputeFingerprint(string displayName)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var bytes = Encoding.UTF8.GetBytes(displayName);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

public sealed record TenantProvisioningSnapshot(
    Guid TenantId,
    string DisplayName,
    TenantAvailability Availability,
    Guid ProvisionedByPrincipalId,
    Guid ProvisionedByDeviceId,
    DateTimeOffset ActivatedAt);

public sealed record ProvisionTenantResult(
    ProvisionTenantStatus Status,
    TenantProvisioningSnapshot? Tenant);

public interface ITenantProvisioningStore
{
    Task<ProvisionTenantResult> ProvisionAsync(
        TenantProvisioningActor actor,
        TenantProvisioningIntent intent,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken);
}

public sealed class ProvisionTenant(
    ITenantProvisioningStore store,
    TimeProvider timeProvider)
{
    public Task<ProvisionTenantResult> ExecuteAsync(
        TenantProvisioningActor actor,
        TenantProvisioningIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        return store.ProvisionAsync(
            actor,
            intent,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
