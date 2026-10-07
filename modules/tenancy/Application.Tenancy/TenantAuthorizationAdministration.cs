using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Application.Tenancy;

public enum TenantAuthorizationProposalKind
{
    GrantPermission = 1,
    RevokePermission = 2,
    CreateRole = 3,
    ReviseRole = 4,
    RetireRole = 5,
    AssignRole = 6,
    UnassignRole = 7,
}

public enum TenantAuthorizationProposalStatus
{
    Pending = 1,
    Applied = 2,
    Failed = 3,
    Uncertain = 4,
}

public enum TenantRoleAvailability
{
    Pending = 1,
    Active = 2,
    PendingRevision = 3,
    PendingRetirement = 4,
    Retired = 5,
}

public enum TenantRoleAssignmentAvailability
{
    Active = 1,
    Removed = 2,
}

public sealed record TenantPermissionDefinition(
    string PermissionId,
    string Relation,
    string DisplayName,
    bool DelegableByInitialOwner = true);

public static class TenantPermissionCatalog
{
    public const int MaximumRolePermissions = 32;
    public const int MaximumCustomRolesPerTenant = 50;
    public const int MaximumAssignmentsPerRole = 50;

    private static readonly TenantPermissionDefinition[] Definitions =
    [
        new("workspace.view", "workspace_viewer", "View workspace"),
        new("orders.create", "order_creator", "Create orders"),
        new("orders.view", "order_viewer", "View orders"),
        new("orders.abandon", "order_abandoner", "Abandon orders"),
        new("orders.edit", "order_editor", "Edit orders"),
        new("orders.commit", "order_committer", "Commit orders"),
        new("orders.manual_price", "manual_pricer", "Apply manual order prices"),
        new("customers.organizations.create", "organization_creator", "Create customer organizations"),
        new("customers.organizations.view", "organization_viewer", "View customer organizations"),
        new("customers.programs.create", "program_creator", "Create customer programs"),
        new("customers.programs.view", "program_viewer", "View customer programs"),
        new("customers.individuals.create", "individual_creator", "Create individual customers"),
        new("customers.individuals.view", "individual_viewer", "View individual customers"),
        new("customers.individuals.availability", "individual_availability_editor", "Change individual customer availability"),
        new("customers.individuals.contact.edit", "individual_contact_editor", "Edit individual customer contact fields"),
        new("customers.representatives.view", "representative_viewer", "View customer representative relationships"),
        new("customers.representatives.manage", "representative_manager", "Manage customer representative relationships"),
        new("customers.duplicates.resolve", "customer_duplicate_resolver", "Review and resolve customer duplicates"),
        new("customers.duplicates.consolidate", "customer_duplicate_consolidator", "Consolidate customer identities"),
        new("customers.import", "customer_importer", "Plan and accept customer imports"),
        new("catalog.view", "catalog_viewer", "View catalog items and units"),
        new("catalog.manage", "catalog_editor", "Manage catalog items and units"),
        new("pricing.view", "pricing_viewer", "View published prices and policy"),
        new("pricing.drafts.edit", "pricing_draft_editor", "Create pricing drafts"),
        new("pricing.publish", "pricing_publisher", "Publish price revisions and policy"),
        new("pricing.retire", "pricing_retirer", "Retire published price revisions"),
        new("pricing.override", "pricing_overrider", "Override a selected price with a reason"),
        new("pricing.override_beyond_policy", "pricing_exception_overrider", "Override outside the current policy envelope"),
    ];

    public static IReadOnlyList<TenantPermissionDefinition> All { get; } = Array.AsReadOnly(Definitions);

    public static bool TryGet(string permissionId, out TenantPermissionDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.PermissionId, permissionId, StringComparison.Ordinal))!;
        return definition is not null;
    }

    public static string NormalizePermission(string permissionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionId);
        var normalized = permissionId.Trim();
        if (!TryGet(normalized, out _))
        {
            throw new ArgumentException("The tenant permission identifier is not supported.", nameof(permissionId));
        }

        return normalized;
    }

    public static string[] NormalizeRolePermissions(IEnumerable<string> permissionIds)
    {
        ArgumentNullException.ThrowIfNull(permissionIds);
        var values = permissionIds
            .Select(NormalizePermission)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (values.Length is < 1 or > MaximumRolePermissions)
        {
            throw new ArgumentOutOfRangeException(
                nameof(permissionIds),
                $"A custom role must contain from 1 through {MaximumRolePermissions} supported permissions.");
        }

        return values;
    }
}

public sealed record TenantAuthorizationActor
{
    private TenantAuthorizationActor(Guid accountId)
    {
        AccountId = accountId;
    }

    public Guid AccountId { get; }

    public static TenantAuthorizationActor Create(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
        }

        return new TenantAuthorizationActor(accountId);
    }
}

public sealed record TenantAuthorizationProposalIntent
{
    public const int IdempotencyKeyLimit = 200;
    public const int RoleNameLimit = 100;

    private TenantAuthorizationProposalIntent(
        TenantAuthorizationProposalKind kind,
        Guid tenantId,
        int expectedAuthorizationRevision,
        string idempotencyKey,
        Guid? targetAccountId,
        string? permissionId,
        Guid? roleId,
        int? expectedRoleRevision,
        string? roleName,
        string[] requestedPermissions,
        string fingerprint)
    {
        Kind = kind;
        TenantId = tenantId;
        ExpectedAuthorizationRevision = expectedAuthorizationRevision;
        IdempotencyKey = idempotencyKey;
        TargetAccountId = targetAccountId;
        PermissionId = permissionId;
        RoleId = roleId;
        ExpectedRoleRevision = expectedRoleRevision;
        RoleName = roleName;
        RequestedPermissions = requestedPermissions;
        Fingerprint = fingerprint;
    }

    public TenantAuthorizationProposalKind Kind { get; }
    public Guid TenantId { get; }
    public int ExpectedAuthorizationRevision { get; }
    public string IdempotencyKey { get; }
    public Guid? TargetAccountId { get; }
    public string? PermissionId { get; }
    public Guid? RoleId { get; }
    public int? ExpectedRoleRevision { get; }
    public string? RoleName { get; }
    public IReadOnlyList<string> RequestedPermissions { get; }
    public string Fingerprint { get; }

    public static TenantAuthorizationProposalIntent PermissionChange(
        TenantAuthorizationProposalKind kind,
        Guid tenantId,
        Guid targetAccountId,
        string permissionId,
        int expectedAuthorizationRevision,
        string idempotencyKey)
    {
        if (kind is not TenantAuthorizationProposalKind.GrantPermission and
            not TenantAuthorizationProposalKind.RevokePermission)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ValidateTenantAndRevision(tenantId, expectedAuthorizationRevision);
        if (targetAccountId == Guid.Empty)
        {
            throw new ArgumentException("Target account identity cannot be empty.", nameof(targetAccountId));
        }

        var permission = TenantPermissionCatalog.NormalizePermission(permissionId);
        var key = NormalizeKey(idempotencyKey);
        return Create(kind, tenantId, expectedAuthorizationRevision, key, targetAccountId,
            permission, null, null, null, [],
            [((int)kind).ToString(CultureInfo.InvariantCulture), tenantId.ToString("N"),
             targetAccountId.ToString("N"), permission,
             expectedAuthorizationRevision.ToString(CultureInfo.InvariantCulture)]);
    }

    public static TenantAuthorizationProposalIntent CreateRole(
        Guid tenantId,
        Guid roleId,
        string roleName,
        IEnumerable<string> permissionIds,
        int expectedAuthorizationRevision,
        string idempotencyKey)
    {
        ValidateTenantAndRevision(tenantId, expectedAuthorizationRevision);
        ValidateRoleId(roleId);
        var name = NormalizeRoleName(roleName);
        var permissions = TenantPermissionCatalog.NormalizeRolePermissions(permissionIds);
        var key = NormalizeKey(idempotencyKey);
        return Create(TenantAuthorizationProposalKind.CreateRole, tenantId, expectedAuthorizationRevision,
            key, null, null, roleId, null, name, permissions,
            ["3", tenantId.ToString("N"), roleId.ToString("N"), name,
             string.Join('\n', permissions), expectedAuthorizationRevision.ToString(CultureInfo.InvariantCulture)]);
    }

    public static TenantAuthorizationProposalIntent ReviseRole(
        Guid tenantId,
        Guid roleId,
        int expectedRoleRevision,
        string roleName,
        IEnumerable<string> permissionIds,
        int expectedAuthorizationRevision,
        string idempotencyKey)
    {
        ValidateTenantAndRevision(tenantId, expectedAuthorizationRevision);
        ValidateRoleId(roleId);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedRoleRevision, 1);
        var name = NormalizeRoleName(roleName);
        var permissions = TenantPermissionCatalog.NormalizeRolePermissions(permissionIds);
        var key = NormalizeKey(idempotencyKey);
        return Create(TenantAuthorizationProposalKind.ReviseRole, tenantId, expectedAuthorizationRevision,
            key, null, null, roleId, expectedRoleRevision, name, permissions,
            ["4", tenantId.ToString("N"), roleId.ToString("N"),
             expectedRoleRevision.ToString(CultureInfo.InvariantCulture), name,
             string.Join('\n', permissions), expectedAuthorizationRevision.ToString(CultureInfo.InvariantCulture)]);
    }

    public static TenantAuthorizationProposalIntent RetireRole(
        Guid tenantId,
        Guid roleId,
        int expectedRoleRevision,
        int expectedAuthorizationRevision,
        string idempotencyKey)
    {
        ValidateTenantAndRevision(tenantId, expectedAuthorizationRevision);
        ValidateRoleId(roleId);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedRoleRevision, 1);
        var key = NormalizeKey(idempotencyKey);
        return Create(TenantAuthorizationProposalKind.RetireRole, tenantId, expectedAuthorizationRevision,
            key, null, null, roleId, expectedRoleRevision, null, [],
            ["5", tenantId.ToString("N"), roleId.ToString("N"),
             expectedRoleRevision.ToString(CultureInfo.InvariantCulture),
             expectedAuthorizationRevision.ToString(CultureInfo.InvariantCulture)]);
    }

    public static TenantAuthorizationProposalIntent RoleAssignment(
        TenantAuthorizationProposalKind kind,
        Guid tenantId,
        Guid roleId,
        Guid targetAccountId,
        int expectedAuthorizationRevision,
        string idempotencyKey)
    {
        if (kind is not TenantAuthorizationProposalKind.AssignRole and
            not TenantAuthorizationProposalKind.UnassignRole)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ValidateTenantAndRevision(tenantId, expectedAuthorizationRevision);
        ValidateRoleId(roleId);
        if (targetAccountId == Guid.Empty)
        {
            throw new ArgumentException("Target account identity cannot be empty.", nameof(targetAccountId));
        }

        var key = NormalizeKey(idempotencyKey);
        return Create(kind, tenantId, expectedAuthorizationRevision, key, targetAccountId,
            null, roleId, null, null, [],
            [((int)kind).ToString(CultureInfo.InvariantCulture), tenantId.ToString("N"),
             roleId.ToString("N"), targetAccountId.ToString("N"),
             expectedAuthorizationRevision.ToString(CultureInfo.InvariantCulture)]);
    }

    private static TenantAuthorizationProposalIntent Create(
        TenantAuthorizationProposalKind kind,
        Guid tenantId,
        int expectedAuthorizationRevision,
        string idempotencyKey,
        Guid? targetAccountId,
        string? permissionId,
        Guid? roleId,
        int? expectedRoleRevision,
        string? roleName,
        string[] permissions,
        IEnumerable<string> fingerprintParts) =>
        new(kind, tenantId, expectedAuthorizationRevision, idempotencyKey, targetAccountId,
            permissionId, roleId, expectedRoleRevision, roleName, permissions,
            ComputeFingerprint(fingerprintParts));

    private static void ValidateTenantAndRevision(Guid tenantId, int expectedAuthorizationRevision)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(expectedAuthorizationRevision, 1);
    }

    private static void ValidateRoleId(Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identity cannot be empty.", nameof(roleId));
        }
    }

    private static string NormalizeRoleName(string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        var value = roleName.Trim().Normalize(NormalizationForm.FormC);
        if (value.Length > RoleNameLimit || value.Any(char.IsControl))
        {
            throw new ArgumentException($"Role name must be plain text up to {RoleNameLimit} characters.", nameof(roleName));
        }

        return value;
    }

    private static string NormalizeKey(string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var key = idempotencyKey.Trim();
        if (key.Length > IdempotencyKeyLimit || key.Any(char.IsControl) || key.Contains('\uFFFD', StringComparison.Ordinal))
        {
            throw new ArgumentException("Idempotency key is invalid.", nameof(idempotencyKey));
        }

        return key;
    }

    private static string ComputeFingerprint(IEnumerable<string> parts)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var part in parts)
        {
            var bytes = Encoding.UTF8.GetBytes(part);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

public sealed record TenantAuthorizationProposal(
    Guid ProposalId,
    Guid TenantId,
    Guid RequestedByAccountId,
    string IdempotencyKey,
    string RequestFingerprint,
    TenantAuthorizationProposalKind Kind,
    TenantAuthorizationProposalStatus Status,
    int ExpectedAuthorizationRevision,
    int? AppliedAuthorizationRevision,
    Guid? TargetAccountId,
    string? PermissionId,
    Guid? RoleId,
    int? ExpectedRoleRevision,
    string? RoleName,
    IReadOnlyList<string> RequestedPermissions,
    IReadOnlyList<string> AppliedPermissions,
    int AttemptCount,
    string? FailureCode,
    DateTimeOffset RequestedAt,
    DateTimeOffset UpdatedAt);

public enum TenantAuthorizationProposalResultStatus
{
    Created = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
    TenantNotFound = 4,
    TenantUnavailable = 5,
    ActorNotInitialOwner = 6,
    AuthorizationRevisionConflict = 7,
    TargetMembershipUnavailable = 8,
    RoleNotFound = 9,
    RoleRevisionConflict = 10,
    RoleAlreadyExists = 11,
    RoleRetired = 12,
    RoleAssignmentConflict = 13,
    DirectPermissionConflict = 14,
}

public sealed record TenantAuthorizationProposalResult(
    TenantAuthorizationProposalResultStatus Status,
    TenantAuthorizationProposal? Proposal);

public sealed record TenantCustomRole(
    Guid TenantId,
    Guid RoleId,
    string Name,
    TenantRoleAvailability Availability,
    int Revision,
    IReadOnlyList<string> PermissionIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? RetiredAt);

public sealed record TenantRoleAssignment(
    Guid TenantId,
    Guid RoleId,
    Guid AccountId,
    TenantRoleAssignmentAvailability Availability,
    int Revision,
    DateTimeOffset AssignedAt,
    DateTimeOffset? RemovedAt);

public sealed record TenantOwnerTransferIntent
{
    private TenantOwnerTransferIntent(
        Guid tenantId,
        Guid targetAccountId,
        int expectedTenantRevision,
        string idempotencyKey,
        string fingerprint)
    {
        TenantId = tenantId;
        TargetAccountId = targetAccountId;
        ExpectedTenantRevision = expectedTenantRevision;
        IdempotencyKey = idempotencyKey;
        Fingerprint = fingerprint;
    }

    public Guid TenantId { get; }
    public Guid TargetAccountId { get; }
    public int ExpectedTenantRevision { get; }
    public string IdempotencyKey { get; }
    public string Fingerprint { get; }

    public static TenantOwnerTransferIntent Create(
        Guid tenantId,
        Guid targetAccountId,
        int expectedTenantRevision,
        string idempotencyKey)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        if (targetAccountId == Guid.Empty) throw new ArgumentException("Target account identity cannot be empty.", nameof(targetAccountId));
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedTenantRevision, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var key = idempotencyKey.Trim();
        if (key.Length > TenantAuthorizationProposalIntent.IdempotencyKeyLimit || key.Any(char.IsControl))
            throw new ArgumentException("Idempotency key is invalid.", nameof(idempotencyKey));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('|', tenantId.ToString("N"), targetAccountId.ToString("N"),
                expectedTenantRevision.ToString(CultureInfo.InvariantCulture)))));
        return new TenantOwnerTransferIntent(tenantId, targetAccountId, expectedTenantRevision, key, fingerprint);
    }
}

public enum TenantOwnerTransferStatus
{
    Transferred = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
    TenantNotFound = 4,
    TenantRevisionConflict = 5,
    ActorNotInitialOwner = 6,
    TargetMembershipUnavailable = 7,
    TargetAlreadyInitialOwner = 8,
    AuthorizationChangeInProgress = 9,
}

public sealed record TenantOwnerTransferResult(
    TenantOwnerTransferStatus Status,
    Guid? PreviousOwnerAccountId,
    Guid? CurrentOwnerAccountId,
    int? TenantRevision,
    int? AuthorizationRevision);

public interface ITenantAuthorizationAdministrationStore
{
    Task<int?> GetAuthorizationRevisionAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<bool> IsInitialOwnerAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    Task<TenantAuthorizationProposalResult> ProposeAsync(
        TenantAuthorizationActor actor,
        TenantAuthorizationProposalIntent intent,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken);

    Task<TenantAuthorizationProposal?> FindProposalAsync(
        Guid tenantId,
        Guid proposalId,
        CancellationToken cancellationToken);

    Task<TenantAuthorizationProposal?> MarkAttemptAsync(
        Guid tenantId,
        Guid proposalId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);

    Task<TenantAuthorizationProposal?> MarkUncertainAsync(
        Guid tenantId,
        Guid proposalId,
        string failureCode,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);

    Task<TenantAuthorizationProposal?> MarkFailedAsync(
        Guid tenantId,
        Guid proposalId,
        string failureCode,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);

    Task<TenantAuthorizationProposal?> CompleteAsync(
        Guid tenantId,
        Guid proposalId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantCustomRole>> ListRolesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<TenantCustomRole?> FindRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantRoleAssignment>> ListRoleAssignmentsAsync(
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken);

    Task<TenantOwnerTransferResult> TransferInitialOwnerAsync(
        TenantAuthorizationActor actor,
        TenantOwnerTransferIntent intent,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);
}

public sealed class TenantAuthorizationAdministration(
    ITenantAuthorizationAdministrationStore store,
    TimeProvider timeProvider)
{
    public Task<TenantAuthorizationProposalResult> ProposeAsync(
        TenantAuthorizationActor actor,
        TenantAuthorizationProposalIntent intent,
        CancellationToken cancellationToken) =>
        store.ProposeAsync(actor, intent, timeProvider.GetUtcNow(), cancellationToken);

    public Task<TenantOwnerTransferResult> TransferOwnerAsync(
        TenantAuthorizationActor actor,
        TenantOwnerTransferIntent intent,
        CancellationToken cancellationToken) =>
        store.TransferInitialOwnerAsync(actor, intent, timeProvider.GetUtcNow(), cancellationToken);
}
