using System.Globalization;

namespace Application.IdentityAccess;

public enum AccountLifecycleOperation { Suspend = 1, Reactivate = 2 }
public enum AccountLifecycleStatus
{
    Changed = 1,
    AccountNotFound = 2,
    RevisionConflict = 3,
    InvalidTransition = 4,
    IdempotencyKeyConflict = 5,
    RevisionLimitReached = 6,
}

public sealed record AccountLifecycleIntent
{
    private AccountLifecycleIntent(AccountLifecycleOperation operation, Guid accountId, int expectedRevision,
        string reason, string key, string fingerprint)
    {
        Operation = operation;
        AccountId = accountId;
        ExpectedRevision = expectedRevision;
        Reason = reason;
        IdempotencyKey = key;
        Fingerprint = fingerprint;
    }

    public AccountLifecycleOperation Operation { get; }
    public Guid AccountId { get; }
    public int ExpectedRevision { get; }
    public string Reason { get; }
    public string IdempotencyKey { get; }
    public string Fingerprint { get; }
    public AccountAvailability TargetAvailability => Operation == AccountLifecycleOperation.Suspend
        ? AccountAvailability.Disabled : AccountAvailability.Active;

    public static AccountLifecycleIntent Create(AccountLifecycleOperation operation, Guid accountId,
        int expectedRevision, string reason, string idempotencyKey)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (accountId == Guid.Empty) throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedRevision, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 500 || reason.Any(char.IsControl) || !ExternalIdentity.HasWellFormedUtf16(reason))
            throw new ArgumentException("The account lifecycle reason is invalid.", nameof(reason));
        var key = AccountOnboardingIntent.NormalizeKey(idempotencyKey);
        var normalizedReason = reason.Trim();
        return new(operation, accountId, expectedRevision, normalizedReason, key,
            AccountOnboardingIntent.ComputeFingerprint("account-lifecycle/v1", operation.ToString(),
                accountId.ToString("N"), expectedRevision.ToString(CultureInfo.InvariantCulture), normalizedReason));
    }

    public AccountLifecycleStatus Evaluate(AccountAvailability currentAvailability, int currentRevision)
    {
        if (ExpectedRevision != currentRevision) return AccountLifecycleStatus.RevisionConflict;
        if (currentAvailability == TargetAvailability) return AccountLifecycleStatus.InvalidTransition;
        if (!Enum.IsDefined(currentAvailability)) throw new InvalidOperationException("Unknown account availability.");
        return currentRevision == int.MaxValue
            ? AccountLifecycleStatus.RevisionLimitReached : AccountLifecycleStatus.Changed;
    }
}

public sealed record AccountLifecycleSnapshot(Guid AccountId, AccountAvailability Availability, int Revision,
    DateTimeOffset? DisabledAt, Guid ChangedByPrincipalId, Guid ChangedByDeviceId, string Reason, DateTimeOffset ChangedAt);
public sealed record AccountLifecycleResult(AccountLifecycleStatus Status, AccountLifecycleSnapshot? Account, bool Replayed = false);

public interface IAccountLifecycleStore
{
    Task<AccountLifecycleResult> ExecuteAsync(IdentityAdministrationActor actor, AccountLifecycleIntent intent,
        DateTimeOffset changedAt, CancellationToken cancellationToken);
}

public sealed class ManageAccountAvailability(IAccountLifecycleStore store, TimeProvider timeProvider)
{
    public Task<AccountLifecycleResult> ExecuteAsync(IdentityAdministrationActor actor, AccountLifecycleIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        return store.ExecuteAsync(actor, intent, timeProvider.GetUtcNow(), cancellationToken);
    }
}
