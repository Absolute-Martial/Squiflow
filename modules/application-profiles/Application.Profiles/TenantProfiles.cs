using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.PlatformAdministration;
using Application.Tenancy;

namespace Application.Profiles;

public sealed record TenantPolicyState(Guid TenantId, long Revision, bool RequireReferenceForProgramOrders, Guid? PublishedPolicyRevisionId);
public sealed record PublishedTenantPolicy(Guid PolicyRevisionId, Guid TenantId, long Revision, bool RequireReferenceForProgramOrders,
    Guid PublishedByAccountId, DateTimeOffset PublishedAt, long ObservedAuthorizationRevision);
public sealed record TenantProfileSnapshot(string Version, Guid ProfileId, Guid TenantId, string CatalogFingerprint, string SelectionFingerprint,
    IReadOnlyList<string> FeatureIds, PublishedTenantPolicy Policy, Guid PublishedByPrincipalId, Guid PublishedByDeviceId,
    DateTimeOffset PublishedAt, long ObservedAuthorizationRevision, bool IsLegacyBaseline);
public sealed record TenantProfileAuthority(Guid TenantId, long Revision, Guid? ActiveProfileId, Guid? LegacyBaselineProfileId);
public sealed record EditTenantPolicyRequest(long ExpectedRevision, bool RequireReferenceForProgramOrders);
public sealed record PublishTenantPolicyRequest(long ExpectedRevision);
public sealed record PublishTenantProfileRequest(Guid TenantId, long ExpectedAuthorityRevision, Guid PublishedPolicyRevisionId,
    long ObservedAuthorizationRevision, bool IsLegacyBaseline = false);
public sealed record ActivateTenantProfileRequest(Guid TenantId, long ExpectedAuthorityRevision, Guid ProfileId, bool AsLegacyBaseline = false);

public enum ProfileCommandStatus { Edited, PolicyPublished, ProfilePublished, Activated, LegacyBaselineSelected, Replayed, NotFound, RevisionConflict, IdempotencyKeyConflict, InvalidBaseline, BaselineAlreadySelected }
public sealed record ProfileCommandResult(ProfileCommandStatus Status, TenantPolicyState? PolicyState = null,
    PublishedTenantPolicy? PublishedPolicy = null, TenantProfileSnapshot? Profile = null, TenantProfileAuthority? Authority = null);

// Current membership/action authority belongs to the calling application boundary and is checked before every call, including replay.
public interface IProfileStore
{
    Task<TenantPolicyState?> GetPolicyAsync(TenantContext context, CancellationToken cancellationToken);
    Task<PublishedTenantPolicy?> GetPublishedPolicyAsync(TenantContext context, Guid policyRevisionId, CancellationToken cancellationToken);
    Task<ProfileCommandResult> EditPolicyAsync(TenantContext context, EditTenantPolicyRequest request, long observedAuthorizationRevision,
        string idempotencyKey, CancellationToken cancellationToken);
    Task<ProfileCommandResult> PublishPolicyAsync(TenantContext context, PublishTenantPolicyRequest request, long observedAuthorizationRevision,
        string idempotencyKey, CancellationToken cancellationToken);
    Task<TenantProfileAuthority?> GetAuthorityAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<TenantProfileSnapshot?> GetProfileAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken);
    Task<ProfileCommandResult> PublishProfileAsync(PlatformAdminAccess access, PublishTenantProfileRequest request,
        string idempotencyKey, CancellationToken cancellationToken);
    Task<ProfileCommandResult> ActivateProfileAsync(PlatformAdminAccess access, ActivateTenantProfileRequest request,
        string idempotencyKey, CancellationToken cancellationToken);
}

public sealed class ProfileValidationException(string code, string message) : ArgumentException(message)
{
    public string Code { get; } = code;
}

public static class TenantProfileRules
{
    public const string SupportedVersion = "tenant-profile/v1";
    public static void RequireIdentity(Guid value)
    {
        if (value == Guid.Empty) throw new ProfileValidationException("profile_identity_invalid", "A nonempty identity is required.");
    }
    public static void RequireExpectedRevision(long value)
    {
        if (value is < 0 or long.MaxValue) throw new ProfileValidationException("profile_revision_invalid", "Expected revision must be nonnegative and supported.");
    }
    public static void RequireAuthorizationRevision(long value)
    {
        if (value < 1) throw new ProfileValidationException("authorization_revision_invalid", "A positive observed authorization revision is required.");
    }
    public static string Key(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)) throw new ProfileValidationException("idempotency_key_invalid", "An Idempotency-Key is required.");
        var key = value.Trim();
        if (key.Length > 128 || key.Any(character => character < '!' || character > '~'))
            throw new ProfileValidationException("idempotency_key_invalid", "Idempotency-Key must contain 1 to 128 printable ASCII characters.");
        return key;
    }
    public static string Fingerprint(string operation, params string[] values)
    {
        var canonical = new StringBuilder("tenant-profile/v1:").Append(operation).Append(':');
        foreach (var value in values) canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }
    public static void ValidatePolicy(PublishedTenantPolicy policy, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (tenantId == Guid.Empty || policy.PolicyRevisionId == Guid.Empty || policy.PublishedByAccountId == Guid.Empty ||
            policy.TenantId != tenantId || policy.Revision < 1 || policy.ObservedAuthorizationRevision < 1)
            throw new InvalidOperationException("Stored tenant policy identity, revision or authorization observation is invalid.");
        RequireTime(policy.PublishedAt);
    }
    public static void ValidateProfile(TenantProfileSnapshot profile, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (tenantId == Guid.Empty || profile.ProfileId == Guid.Empty || profile.PublishedByPrincipalId == Guid.Empty ||
            profile.PublishedByDeviceId == Guid.Empty || profile.ObservedAuthorizationRevision < 1 ||
            profile.Version != SupportedVersion || profile.TenantId != tenantId)
            throw new InvalidOperationException("Stored tenant profile version or tenant identity is unsupported.");
        var selection = CommercialFeatureCatalog.Catalog.Compile([]);
        if (profile.CatalogFingerprint != selection.CatalogFingerprint || profile.SelectionFingerprint != selection.SelectionFingerprint ||
            profile.FeatureIds is null || !profile.FeatureIds.SequenceEqual(selection.EffectiveFeatureIds, StringComparer.Ordinal))
            throw new InvalidOperationException("Stored tenant profile catalog or selection is incompatible.");
        ValidatePolicy(profile.Policy, tenantId); RequireTime(profile.PublishedAt);
        if (profile.PublishedAt < profile.Policy.PublishedAt || profile.IsLegacyBaseline && profile.Policy.RequireReferenceForProgramOrders)
            throw new InvalidOperationException("Stored tenant profile policy or publication time is inconsistent.");
    }
    public static void RequireTime(DateTimeOffset at)
    {
        if (at == default || at.Offset != TimeSpan.Zero || at.Ticks % 10 != 0)
            throw new InvalidOperationException("Stored profile time is unsupported.");
    }
}
