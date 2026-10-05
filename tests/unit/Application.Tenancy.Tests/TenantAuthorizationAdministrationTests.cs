using Application.Tenancy;
using Xunit;

namespace Application.Tenancy.Tests;

public sealed class TenantAuthorizationAdministrationTests
{
    [Fact]
    public void PermissionCatalogUsesStableUniqueBusinessPermissionIdsAndRelations()
    {
        Assert.NotEmpty(TenantPermissionCatalog.All);
        Assert.Equal(
            TenantPermissionCatalog.All.Count,
            TenantPermissionCatalog.All.Select(permission => permission.PermissionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            TenantPermissionCatalog.All.Count,
            TenantPermissionCatalog.All.Select(permission => permission.Relation).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(TenantPermissionCatalog.All, permission =>
            permission.PermissionId.Contains("admin", StringComparison.OrdinalIgnoreCase) ||
            permission.PermissionId.Contains("role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RolePermissionsAreNormalizedDeduplicatedAndOrdered()
    {
        var permissions = TenantPermissionCatalog.NormalizeRolePermissions(
            ["orders.view", "workspace.view", "orders.view"]);

        Assert.Equal(["orders.view", "workspace.view"], permissions);
    }

    [Fact]
    public void RolePermissionsRejectUnknownAndOverLimitValues()
    {
        Assert.Throws<ArgumentException>(() =>
            TenantPermissionCatalog.NormalizeRolePermissions(["roles.manage"]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TenantPermissionCatalog.NormalizeRolePermissions([]));
    }

    [Fact]
    public void PermissionProposalFingerprintIgnoresIdempotencyKeyButIncludesSemanticIntent()
    {
        var tenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var baseline = TenantAuthorizationProposalIntent.PermissionChange(
            TenantAuthorizationProposalKind.GrantPermission,
            tenantId,
            accountId,
            "orders.view",
            3,
            "first");
        var replay = TenantAuthorizationProposalIntent.PermissionChange(
            TenantAuthorizationProposalKind.GrantPermission,
            tenantId,
            accountId,
            "orders.view",
            3,
            "second");
        var revoke = TenantAuthorizationProposalIntent.PermissionChange(
            TenantAuthorizationProposalKind.RevokePermission,
            tenantId,
            accountId,
            "orders.view",
            3,
            "third");

        Assert.Equal(baseline.Fingerprint, replay.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, revoke.Fingerprint);
        Assert.Matches("^[0-9A-F]{64}$", baseline.Fingerprint);
    }

    [Fact]
    public void CustomRoleFingerprintIsStableAcrossPermissionOrder()
    {
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var first = TenantAuthorizationProposalIntent.CreateRole(
            tenantId,
            roleId,
            "Operations",
            ["workspace.view", "orders.view"],
            1,
            "one");
        var second = TenantAuthorizationProposalIntent.CreateRole(
            tenantId,
            roleId,
            "Operations",
            ["orders.view", "workspace.view"],
            1,
            "two");

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(["orders.view", "workspace.view"], first.RequestedPermissions);
    }

    [Fact]
    public void RoleAssignmentCannotUseAnotherProposalKindOrEmptyIdentifiers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TenantAuthorizationProposalIntent.RoleAssignment(
                TenantAuthorizationProposalKind.CreateRole,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                "key"));
        Assert.Throws<ArgumentException>(() =>
            TenantAuthorizationProposalIntent.RoleAssignment(
                TenantAuthorizationProposalKind.AssignRole,
                Guid.NewGuid(),
                Guid.Empty,
                Guid.NewGuid(),
                1,
                "key"));
    }

    [Fact]
    public void OwnerTransferFingerprintBindsTargetAndTenantRevisionButNotIdempotencyKey()
    {
        var tenantId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var first = TenantOwnerTransferIntent.Create(tenantId, targetId, 4, "owner-transfer-a");
        var replay = TenantOwnerTransferIntent.Create(tenantId, targetId, 4, "owner-transfer-b");
        var changed = TenantOwnerTransferIntent.Create(tenantId, targetId, 5, "owner-transfer-c");

        Assert.Equal(first.Fingerprint, replay.Fingerprint);
        Assert.NotEqual(first.Fingerprint, changed.Fingerprint);
    }
}
