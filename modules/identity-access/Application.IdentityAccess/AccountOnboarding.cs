using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Application.IdentityAccess;

public enum OnboardAccountStatus
{
    Created = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
    IdentityAlreadyBound = 4,
}

public enum LinkExternalIdentityStatus
{
    Linked = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
    AccountNotFound = 4,
    AccountDisabled = 5,
    IdentityAlreadyLinked = 6,
    IdentityBoundElsewhere = 7,
}

public enum IdentityAdministrationReceiptStatus
{
    Missing = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
}

public sealed record IdentityAdministrationActor
{
    private IdentityAdministrationActor(Guid principalId, Guid deviceId)
    {
        PrincipalId = principalId;
        DeviceId = deviceId;
    }

    public Guid PrincipalId { get; }
    public Guid DeviceId { get; }

    public static IdentityAdministrationActor Create(Guid principalId, Guid deviceId)
    {
        if (principalId == Guid.Empty)
            throw new ArgumentException("Platform principal identity cannot be empty.", nameof(principalId));
        if (deviceId == Guid.Empty)
            throw new ArgumentException("Admin device identity cannot be empty.", nameof(deviceId));
        return new IdentityAdministrationActor(principalId, deviceId);
    }
}

public sealed record AccountOnboardingIntent
{
    public const int IdempotencyKeyLimit = 200;

    private AccountOnboardingIntent(ExternalIdentity identity, string idempotencyKey, string fingerprint)
    {
        Identity = identity;
        IdempotencyKey = idempotencyKey;
        Fingerprint = fingerprint;
    }

    public ExternalIdentity Identity { get; }
    public string IdempotencyKey { get; }
    public string Fingerprint { get; }

    public static AccountOnboardingIntent Create(ExternalIdentity identity, string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var key = NormalizeKey(idempotencyKey);
        return new AccountOnboardingIntent(identity, key, ComputeFingerprint(identity.Issuer, identity.Subject));
    }

    internal static string NormalizeKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > IdempotencyKeyLimit)
            throw new ArgumentOutOfRangeException(nameof(value), $"Idempotency key must be at most {IdempotencyKeyLimit} characters.");
        if (value.Any(char.IsControl) || !ExternalIdentity.HasWellFormedUtf16(value))
            throw new ArgumentException("Idempotency key contains invalid characters.", nameof(value));
        var normalized = value.Trim();
        return normalized;
    }

    internal static string ComputeFingerprint(params string[] values)
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

public sealed record ExternalIdentityLinkIntent
{
    private ExternalIdentityLinkIntent(Guid accountId, ExternalIdentity identity, string idempotencyKey, string fingerprint)
    {
        AccountId = accountId;
        Identity = identity;
        IdempotencyKey = idempotencyKey;
        Fingerprint = fingerprint;
    }

    public Guid AccountId { get; }
    public ExternalIdentity Identity { get; }
    public string IdempotencyKey { get; }
    public string Fingerprint { get; }

    public static ExternalIdentityLinkIntent Create(Guid accountId, ExternalIdentity identity, string idempotencyKey)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
        ArgumentNullException.ThrowIfNull(identity);
        var key = AccountOnboardingIntent.NormalizeKey(idempotencyKey);
        return new ExternalIdentityLinkIntent(
            accountId,
            identity,
            key,
            AccountOnboardingIntent.ComputeFingerprint(accountId.ToString("N"), identity.Issuer, identity.Subject));
    }
}

public sealed record AccountOnboardingSnapshot(
    Guid AccountId,
    ExternalIdentity Identity,
    AccountAvailability Availability,
    Guid ProvisionedByPrincipalId,
    Guid ProvisionedByDeviceId,
    DateTimeOffset CreatedAt);

public sealed record ExternalIdentityLinkSnapshot(
    Guid AccountId,
    ExternalIdentity Identity,
    Guid LinkedByPrincipalId,
    Guid LinkedByDeviceId,
    DateTimeOffset LinkedAt);

public sealed record OnboardAccountResult(OnboardAccountStatus Status, AccountOnboardingSnapshot? Account);
public sealed record LinkExternalIdentityResult(LinkExternalIdentityStatus Status, ExternalIdentityLinkSnapshot? Link);
public sealed record AccountOnboardingReceiptResult(
    IdentityAdministrationReceiptStatus Status,
    AccountOnboardingSnapshot? Account);
public sealed record ExternalIdentityLinkReceiptResult(
    IdentityAdministrationReceiptStatus Status,
    ExternalIdentityLinkSnapshot? Link);

public interface IAccountOnboardingStore
{
    Task<AccountOnboardingReceiptResult> FindOnboardingReceiptAsync(
        IdentityAdministrationActor actor,
        AccountOnboardingIntent intent,
        CancellationToken cancellationToken);

    Task<ExternalIdentityLinkReceiptResult> FindLinkReceiptAsync(
        IdentityAdministrationActor actor,
        ExternalIdentityLinkIntent intent,
        CancellationToken cancellationToken);

    Task<OnboardAccountResult> OnboardAsync(
        IdentityAdministrationActor actor,
        AccountOnboardingIntent intent,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<LinkExternalIdentityResult> LinkAsync(
        IdentityAdministrationActor actor,
        ExternalIdentityLinkIntent intent,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken);
}

public sealed class AccountOnboarding(IAccountOnboardingStore store, TimeProvider timeProvider)
{
    public Task<AccountOnboardingReceiptResult> FindOnboardingReceiptAsync(
        IdentityAdministrationActor actor,
        AccountOnboardingIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        return store.FindOnboardingReceiptAsync(actor, intent, cancellationToken);
    }

    public Task<ExternalIdentityLinkReceiptResult> FindLinkReceiptAsync(
        IdentityAdministrationActor actor,
        ExternalIdentityLinkIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        return store.FindLinkReceiptAsync(actor, intent, cancellationToken);
    }

    public Task<OnboardAccountResult> OnboardAsync(
        IdentityAdministrationActor actor,
        AccountOnboardingIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        return store.OnboardAsync(actor, intent, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<LinkExternalIdentityResult> LinkAsync(
        IdentityAdministrationActor actor,
        ExternalIdentityLinkIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        return store.LinkAsync(actor, intent, timeProvider.GetUtcNow(), cancellationToken);
    }
}
