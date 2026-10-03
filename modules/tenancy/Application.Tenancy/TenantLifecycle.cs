using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Application.Tenancy;

public enum TenantLifecycleOperation
{
    Suspend = 1,
    Reactivate = 2,
}

public enum TenantLifecycleStatus
{
    Suspended = 1,
    Reactivated = 2,
    IdempotencyKeyConflict = 3,
    TenantNotFound = 4,
    RevisionConflict = 5,
    InvalidTransition = 6,
    RevisionLimitReached = 7,
}

public sealed record TenantLifecycleIntent
{
    private TenantLifecycleIntent(
        TenantLifecycleOperation operation,
        Guid tenantId,
        int expectedRevision,
        string idempotencyKey,
        string fingerprint)
    {
        Operation = operation;
        TenantId = tenantId;
        ExpectedRevision = expectedRevision;
        IdempotencyKey = idempotencyKey;
        Fingerprint = fingerprint;
    }

    public TenantLifecycleOperation Operation { get; }
    public Guid TenantId { get; }
    public int ExpectedRevision { get; }
    public string IdempotencyKey { get; }
    public string Fingerprint { get; }

    public static TenantLifecycleIntent Create(
        TenantLifecycleOperation operation,
        Guid tenantId,
        int expectedRevision,
        string idempotencyKey)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedRevision, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (idempotencyKey.Length > TenantMembershipLifecycleIntent.IdempotencyKeyLimit ||
            idempotencyKey.Any(char.IsControl) ||
            idempotencyKey.Contains('\uFFFD', StringComparison.Ordinal))
            throw new ArgumentException("Idempotency key is invalid.", nameof(idempotencyKey));
        var key = idempotencyKey.Trim();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var value in new[]
                 {
                     ((int)operation).ToString(System.Globalization.CultureInfo.InvariantCulture),
                     tenantId.ToString("N"),
                     expectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                 })
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return new TenantLifecycleIntent(operation, tenantId, expectedRevision, key,
            Convert.ToHexString(hash.GetHashAndReset()));
    }
}

public sealed record TenantLifecycleSnapshot(
    Guid TenantId,
    TenantAvailability Availability,
    int Revision,
    DateTimeOffset? SuspendedAt);

public sealed record TenantLifecycleResult(
    TenantLifecycleStatus Status,
    TenantLifecycleSnapshot? Tenant,
    bool Replayed = false);

public interface ITenantLifecycleStore
{
    Task<TenantLifecycleResult> ExecuteAsync(
        TenantMembershipAdministrationActor actor,
        TenantLifecycleIntent intent,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);
}

public sealed class ManageTenantLifecycle(ITenantLifecycleStore store, TimeProvider timeProvider)
{
    public Task<TenantLifecycleResult> ExecuteAsync(
        TenantMembershipAdministrationActor actor,
        TenantLifecycleIntent intent,
        CancellationToken cancellationToken) =>
        store.ExecuteAsync(actor, intent, timeProvider.GetUtcNow(), cancellationToken);
}
