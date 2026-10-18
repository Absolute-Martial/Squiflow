using System.Text.Json.Serialization;

namespace Application.Web.Business;

public sealed record TenantPermissionRecord(string PermissionId, string Relation, string DisplayName, [property: JsonRequired] bool DelegableByInitialOwner);
public sealed record TenantRoleRecord(Guid TenantId, Guid RoleId, string Name, int Availability, int Revision, string[] PermissionIds,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? RetiredAt);
public sealed record TenantAuthorizationRecord(Guid TenantId, int AuthorizationRevision, TenantPermissionRecord[] PermissionCatalog, TenantRoleRecord[] Roles);
public sealed record TenantProposalRecord(Guid ProposalId, Guid TenantId, Guid RequestedByAccountId, string IdempotencyKey, string RequestFingerprint,
    int Kind, int Status, int ExpectedAuthorizationRevision, int? AppliedAuthorizationRevision, Guid? TargetAccountId, string? PermissionId,
    Guid? RoleId, int? ExpectedRoleRevision, string? RoleName, string[] RequestedPermissions, string[] AppliedPermissions, int AttemptCount,
    string? FailureCode, DateTimeOffset RequestedAt, DateTimeOffset UpdatedAt);
public sealed record TenantOwnerReceipt(int Status, Guid? PreviousOwnerAccountId, Guid? CurrentOwnerAccountId, int? TenantRevision, int? AuthorizationRevision);
public sealed record TenantPolicyRecord(Guid TenantId, long Revision,
    [property: JsonRequired] bool RequireReferenceForProgramOrders, Guid? PublishedPolicyRevisionId,
    [property: JsonRequired] bool RequireApprovalForProgramOrders);
public sealed record PublishedPolicyRecord(Guid PolicyRevisionId, Guid TenantId, long Revision,
    [property: JsonRequired] bool RequireReferenceForProgramOrders, Guid PublishedByAccountId, DateTimeOffset PublishedAt,
    long ObservedAuthorizationRevision, string Version, [property: JsonRequired] bool RequireApprovalForProgramOrders);
public sealed record PolicyCommandReceipt(string Status, TenantPolicyRecord? Policy, PublishedPolicyRecord? PublishedPolicy);

public static class TenantAdministrationContracts
{
    public static bool Permission(string? value) => value is { Length: > 0 and <= 128 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
    private static bool Permissions(string[]? values) => values is { Length: <= 32 } && values.All(Permission) && values.Distinct(StringComparer.Ordinal).Count() == values.Length;
    public static bool Role(TenantRoleRecord value, Guid tenant) => value.TenantId == tenant && value.RoleId != Guid.Empty &&
        value.Revision > 0 && value.Availability is >= 1 and <= 5 && OperationsContracts.Text(value.Name, 128) &&
        Permissions(value.PermissionIds) && value.PermissionIds.Length > 0 && value.CreatedAt != default && value.UpdatedAt >= value.CreatedAt &&
        (value.Availability != 5 || value.RetiredAt.HasValue);
    public static bool Authorization(TenantAuthorizationRecord value, Guid tenant) => value.TenantId == tenant && value.AuthorizationRevision > 0 &&
        value.PermissionCatalog is { Length: > 0 and <= 512 } && value.PermissionCatalog.All(p => p is not null && Permission(p.PermissionId) &&
            Permission(p.Relation) && OperationsContracts.Text(p.DisplayName, 256)) &&
        value.PermissionCatalog.Select(p => p.PermissionId).Distinct(StringComparer.Ordinal).Count() == value.PermissionCatalog.Length &&
        value.Roles is { Length: <= 4096 } && value.Roles.All(r => r is not null && Role(r, tenant) &&
            r.PermissionIds.All(id => value.PermissionCatalog.Any(p => p.PermissionId == id))) &&
        value.Roles.Select(r => r.RoleId).Distinct().Count() == value.Roles.Length;
    public static bool Proposal(TenantProposalRecord value, Guid tenant, Guid? id = null) => value.TenantId == tenant &&
        value.ProposalId != Guid.Empty && (!id.HasValue || value.ProposalId == id) && value.RequestedByAccountId != Guid.Empty &&
        BusinessValidation.Text(value.IdempotencyKey, 128) && value.RequestFingerprint is { Length: 64 } && value.RequestFingerprint.All(char.IsAsciiHexDigit) &&
        value.Kind is >= 1 and <= 7 && value.Status is >= 1 and <= 4 && value.ExpectedAuthorizationRevision > 0 &&
        (value.Status != 2 || value.AppliedAuthorizationRevision == (long)value.ExpectedAuthorizationRevision + 1) &&
        (value.TargetAccountId is null || value.TargetAccountId != Guid.Empty) && (value.RoleId is null || value.RoleId != Guid.Empty) &&
        (value.PermissionId is null || Permission(value.PermissionId)) && (value.ExpectedRoleRevision is null or > 0) &&
        OperationsContracts.Text(value.RoleName, 128, false) && Permissions(value.RequestedPermissions) && Permissions(value.AppliedPermissions) &&
        value.AttemptCount is >= 0 and <= 20 && BusinessValidation.Text(value.FailureCode, 256, false) &&
        value.RequestedAt != default && value.UpdatedAt >= value.RequestedAt && (value.Kind switch
        {
            1 or 2 => value.TargetAccountId.HasValue && value.PermissionId is not null && value.RoleId is null,
            3 or 4 => value.RoleId.HasValue && value.TargetAccountId is null && value.PermissionId is null &&
                OperationsContracts.Text(value.RoleName, 128) && value.RequestedPermissions.Length > 0 && (value.Kind == 3 || value.ExpectedRoleRevision > 0),
            5 => value.RoleId.HasValue && value.ExpectedRoleRevision > 0 && value.TargetAccountId is null,
            _ => value.RoleId.HasValue && value.TargetAccountId.HasValue && value.PermissionId is null
        });
    public static bool Owner(TenantOwnerReceipt value, Guid target, int expected) => value.Status is 1 or 2 &&
        value.PreviousOwnerAccountId is { } previous && previous != Guid.Empty && previous != target && value.CurrentOwnerAccountId == target &&
        value.TenantRevision == (long)expected + 1 && value.AuthorizationRevision > 0;
    public static bool Policy(TenantPolicyRecord value, Guid tenant) => value.TenantId == tenant && value.Revision >= 0 && value.Revision < long.MaxValue &&
        (value.PublishedPolicyRevisionId is null || value.PublishedPolicyRevisionId != Guid.Empty);
    public static bool Published(PublishedPolicyRecord value, Guid tenant) => value.TenantId == tenant && value.PolicyRevisionId != Guid.Empty &&
        value.Revision > 0 && value.PublishedByAccountId != Guid.Empty && value.PublishedAt != default && value.ObservedAuthorizationRevision > 0 &&
        value.Version is "tenant-policy/v1" or "tenant-policy/v2" && (value.Version != "tenant-policy/v1" || !value.RequireApprovalForProgramOrders);
    public static bool PolicyReceipt(PolicyCommandReceipt value, Guid tenant, bool publish, long expected, bool reference, bool approval) =>
        value.Status is "Edited" or "PolicyPublished" or "Replayed" && (publish
            ? value.Status != "Edited" && value.PublishedPolicy is { } published && Published(published, tenant) && published.Revision == expected + 1 &&
                published.RequireReferenceForProgramOrders == reference && published.RequireApprovalForProgramOrders == approval &&
                value.Policy is { } head && Policy(head, tenant) && head.Revision == published.Revision && head.PublishedPolicyRevisionId == published.PolicyRevisionId &&
                head.RequireReferenceForProgramOrders == reference && head.RequireApprovalForProgramOrders == approval
            : value.Status != "PolicyPublished" && value.PublishedPolicy is null && value.Policy is { } policy && Policy(policy, tenant) && policy.Revision == expected + 1 &&
                policy.RequireReferenceForProgramOrders == reference && policy.RequireApprovalForProgramOrders == approval);
    public static string ProposalState(int state) => state switch { 1 => "Pending", 2 => "Applied", 3 => "Failed", 4 => "Uncertain", _ => "Unsupported" };
    public static string RoleState(int state) => state switch { 1 => "Pending", 2 => "Active", 3 => "Pending revision", 4 => "Pending retirement", 5 => "Retired", _ => "Unsupported" };
}
