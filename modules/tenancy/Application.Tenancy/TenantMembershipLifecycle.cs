using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Application.Tenancy;

public enum MembershipLifecycleOperation
{
    Invite = 1,
    Activate = 2,
    Suspend = 3,
    Remove = 4,
    BootstrapOwner = 5,
}

public enum MembershipLifecycleStatus
{
    Invited = 1,
    Activated = 2,
    Suspended = 3,
    Removed = 4,
    Replayed = 5,
    IdempotencyKeyConflict = 6,
    TenantNotFound = 7,
    AccountNotFound = 8,
    MembershipNotFound = 9,
    MembershipAlreadyExists = 10,
    RevisionConflict = 11,
    InvalidTransition = 12,
    RevisionLimitReached = 13,
    OwnerBootstrapped = 14,
    InitialOwnerAlreadyExists = 15,
    InitialOwnerProtected = 16,
    AccountUnavailable = 17,
}

public sealed record TenantMembershipAdministrationActor
{
    private TenantMembershipAdministrationActor(Guid principalId, Guid deviceId)
    {
        PrincipalId = principalId;
        DeviceId = deviceId;
    }

    public Guid PrincipalId { get; }
    public Guid DeviceId { get; }

    public static TenantMembershipAdministrationActor Create(Guid principalId, Guid deviceId)
    {
        if (principalId == Guid.Empty)
            throw new ArgumentException("Platform principal identity cannot be empty.", nameof(principalId));
        if (deviceId == Guid.Empty)
            throw new ArgumentException("Admin device identity cannot be empty.", nameof(deviceId));
        return new TenantMembershipAdministrationActor(principalId, deviceId);
    }
}

public sealed record TenantMembershipLifecycleIntent
{
    public const int IdempotencyKeyLimit = 200;

    private TenantMembershipLifecycleIntent(
        MembershipLifecycleOperation operation,
        Guid tenantId,
        Guid accountId,
        int? expectedRevision,
        string idempotencyKey,
        string fingerprint)
    {
        Operation = operation;
        TenantId = tenantId;
        AccountId = accountId;
        ExpectedRevision = expectedRevision;
        IdempotencyKey = idempotencyKey;
        Fingerprint = fingerprint;
    }

    public MembershipLifecycleOperation Operation { get; }
    public Guid TenantId { get; }
    public Guid AccountId { get; }
    public int? ExpectedRevision { get; }
    public string IdempotencyKey { get; }
    public string Fingerprint { get; }

    public static TenantMembershipLifecycleIntent Invite(Guid tenantId, Guid accountId, string idempotencyKey) =>
        Create(MembershipLifecycleOperation.Invite, tenantId, accountId, null, idempotencyKey);

    public static TenantMembershipLifecycleIntent BootstrapOwner(
        Guid tenantId,
        Guid accountId,
        string idempotencyKey) =>
        Create(MembershipLifecycleOperation.BootstrapOwner, tenantId, accountId, null, idempotencyKey);

    public static TenantMembershipLifecycleIntent Transition(
        MembershipLifecycleOperation operation,
        Guid tenantId,
        Guid accountId,
        int expectedRevision,
        string idempotencyKey)
    {
        if (operation is MembershipLifecycleOperation.Invite or MembershipLifecycleOperation.BootstrapOwner)
            throw new ArgumentException("Creation operations do not use an expected revision.", nameof(operation));
        if (expectedRevision < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedRevision), "Expected revision must be positive.");
        return Create(operation, tenantId, accountId, expectedRevision, idempotencyKey);
    }

    private static TenantMembershipLifecycleIntent Create(
        MembershipLifecycleOperation operation,
        Guid tenantId,
        Guid accountId,
        int? expectedRevision,
        string idempotencyKey)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation), "Unsupported membership operation.");
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (idempotencyKey.Any(char.IsControl) ||
            idempotencyKey.Contains('\uFFFD', StringComparison.Ordinal) ||
            !HasWellFormedUtf16(idempotencyKey))
            throw new ArgumentException("Idempotency key contains invalid characters.", nameof(idempotencyKey));
        var key = idempotencyKey.Trim();
        if (key.Length > IdempotencyKeyLimit)
            throw new ArgumentOutOfRangeException(nameof(idempotencyKey), $"Idempotency key must be at most {IdempotencyKeyLimit} characters.");

        return new TenantMembershipLifecycleIntent(
            operation,
            tenantId,
            accountId,
            expectedRevision,
            key,
            ComputeFingerprint(operation, tenantId, accountId, expectedRevision));
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

    private static string ComputeFingerprint(
        MembershipLifecycleOperation operation,
        Guid tenantId,
        Guid accountId,
        int? expectedRevision)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var value in new[]
                 {
                     ((int)operation).ToString(System.Globalization.CultureInfo.InvariantCulture),
                     tenantId.ToString("N"),
                     accountId.ToString("N"),
                     expectedRevision?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                 })
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

public sealed record TenantMembershipLifecycleSnapshot(
    Guid TenantId,
    Guid AccountId,
    MembershipAvailability Availability,
    int Revision,
    DateTimeOffset InvitedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? SuspendedAt,
    DateTimeOffset? RemovedAt,
    bool IsInitialOwner = false)
{
    public TenantMembershipLifecycleResult Transition(
        TenantMembershipLifecycleIntent intent,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (intent.TenantId != TenantId || intent.AccountId != AccountId)
            throw new ArgumentException("Intent does not identify this membership.", nameof(intent));
        if (Revision != intent.ExpectedRevision)
            return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.RevisionConflict, this);
        if (IsInitialOwner &&
            intent.Operation is MembershipLifecycleOperation.Suspend or MembershipLifecycleOperation.Remove)
            return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.InitialOwnerProtected, this);

        var target = intent.Operation switch
        {
            MembershipLifecycleOperation.Activate when Availability is MembershipAvailability.Invited or MembershipAvailability.Suspended =>
                MembershipAvailability.Active,
            MembershipLifecycleOperation.Suspend when Availability == MembershipAvailability.Active =>
                MembershipAvailability.Suspended,
            MembershipLifecycleOperation.Remove when Availability != MembershipAvailability.Removed =>
                MembershipAvailability.Removed,
            _ => (MembershipAvailability?)null,
        };
        if (target is null)
            return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.InvalidTransition, this);

        if (Revision == int.MaxValue)
            return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.RevisionLimitReached, this);
        var revision = Revision + 1;
        DateTimeOffset? activatedAt =
            target == MembershipAvailability.Active ? occurredAt : ActivatedAt;
        DateTimeOffset? suspendedAt =
            target == MembershipAvailability.Suspended ? occurredAt : null;
        DateTimeOffset? removedAt =
            target == MembershipAvailability.Removed ? occurredAt : null;

        var status = target.Value switch
        {
            MembershipAvailability.Active => MembershipLifecycleStatus.Activated,
            MembershipAvailability.Suspended => MembershipLifecycleStatus.Suspended,
            MembershipAvailability.Removed => MembershipLifecycleStatus.Removed,
            _ => throw new InvalidOperationException("Unexpected membership target state."),
        };
        return new TenantMembershipLifecycleResult(
            status,
            new TenantMembershipLifecycleSnapshot(
                TenantId, AccountId, target.Value, revision,
                InvitedAt, activatedAt, suspendedAt, removedAt, IsInitialOwner));
    }
}


public sealed record TenantMembershipLifecycleResult(
    MembershipLifecycleStatus Status,
    TenantMembershipLifecycleSnapshot? Membership,
    bool Replayed = false);

public interface ITenantMembershipLifecycleStore
{
    Task<TenantMembershipLifecycleResult> ExecuteAsync(
        TenantMembershipAdministrationActor actor,
        TenantMembershipLifecycleIntent intent,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);
}

public sealed class ManageTenantMembership(
    ITenantMembershipLifecycleStore store,
    TimeProvider timeProvider)
{
    public Task<TenantMembershipLifecycleResult> ExecuteAsync(
        TenantMembershipAdministrationActor actor,
        TenantMembershipLifecycleIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        return store.ExecuteAsync(actor, intent, timeProvider.GetUtcNow(), cancellationToken);
    }
}
