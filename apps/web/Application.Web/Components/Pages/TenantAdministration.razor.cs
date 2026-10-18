using Application.Web.Authentication;
using Application.Web.Business;
using Microsoft.AspNetCore.Components;

namespace Application.Web.Components.Pages;

public partial class TenantAdministration
{
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    private TenantAuthorizationRecord? catalog;
    private TenantProposalRecord? proposal;
    private TenantOwnerReceipt? ownerReceipt;
    private string target = "", permission = "", permissionAction = "grant", roleAction = "create", roleName = "", roleReference = "";
    private string assignmentRole = "", assignmentAccount = "", assignmentAction = "assign", proposalReference = "", transferTarget = "", transferConfirmation = "", pendingAction = "";
    private int expectedAuthorization = 1, expectedRole = 1, expectedTenant = 1;
    private HashSet<string> selectedPermissions = new(StringComparer.Ordinal);
    private bool permissionConfirmed, roleConfirmed, assignmentConfirmed, reconcileConfirmed, transferred;
    private bool Locked => Busy || PendingCommand is not null || transferred;
    private bool ProposalOpen => proposal?.Status is 1 or 4;
    private bool ChangesLocked => Locked || ProposalOpen || catalog is null;
    private bool VerificationAvailable => !string.IsNullOrWhiteSpace(Configuration["Authentication:StrongAuthenticationAcr"]) && !Locked;
    private static bool Id(string text, out Guid id) => Guid.TryParse(text, out id) && id != Guid.Empty;
    private void Invalid(string message) { Message = message; Tone = "error"; }
    protected override void Reset()
    {
        catalog = null; proposal = null; ownerReceipt = null; transferred = false;
        target = permission = roleName = assignmentRole = assignmentAccount = proposalReference = transferTarget = transferConfirmation = pendingAction = "";
        roleReference = Guid.NewGuid().ToString("D"); permissionAction = "grant"; roleAction = "create"; assignmentAction = "assign";
        expectedAuthorization = expectedRole = expectedTenant = 1;
        selectedPermissions.Clear(); permissionConfirmed = roleConfirmed = assignmentConfirmed = reconcileConfirmed = false;
    }
    protected override Task LoadAsync(CancellationToken cancellationToken) => ReadCatalogAsync(cancellationToken);
    private Task ReloadAsync() => RunAsync(ReadCatalogAsync);
    private async Task ReadCatalogAsync(CancellationToken ct)
    {
        catalog = null; permission = "";
        var result = await Api.GetAsync<TenantAuthorizationRecord>(TenantId, "authorization", x => TenantAdministrationContracts.Authorization(x, TenantId), ct);
        catalog = result.Value;
        if (catalog is { } current) expectedAuthorization = current.AuthorizationRevision;
        permissionConfirmed = roleConfirmed = assignmentConfirmed = false;
        Show(result);
    }
    private void SelectRole(TenantRoleRecord role)
    {
        roleAction = "revise"; roleReference = role.RoleId.ToString("D"); roleName = role.Name; expectedRole = role.Revision;
        selectedPermissions = new(role.PermissionIds, StringComparer.Ordinal); roleConfirmed = false;
    }
    private void ClearConfirmations() => permissionConfirmed = roleConfirmed = assignmentConfirmed = false;
    private void NewRole()
    {
        roleAction = "create"; roleReference = Guid.NewGuid().ToString("D"); roleName = ""; expectedRole = 1; selectedPermissions.Clear(); roleConfirmed = false;
    }
    private void TogglePermission(string id, ChangeEventArgs args)
    {
        if (args.Value is true) selectedPermissions.Add(id); else selectedPermissions.Remove(id);
        roleConfirmed = false;
    }
    private bool CanChange() => catalog is not null && expectedAuthorization > 0 && !ProposalOpen && !transferred;
    private async Task ProposeAsync(string resource, object payload, int kind, CancellationToken ct, Guid? role = null, Guid? account = null, string? directPermission = null, HttpMethod? method = null)
    {
        var value = await SaveAsync<TenantProposalRecord>(resource, payload, x => TenantAdministrationContracts.Proposal(x, TenantId) &&
            x.ExpectedAuthorizationRevision == expectedAuthorization && x.Kind == kind && x.RoleId == role && x.TargetAccountId == account &&
            x.PermissionId == directPermission && x.IdempotencyKey == PendingCommand?.Key, ct, method);
        if (value is not null) ApplyProposal(value);
    }
    private void ApplyProposal(TenantProposalRecord value)
    {
        proposal = value; proposalReference = value.ProposalId.ToString("D"); catalog = null;
        permissionConfirmed = roleConfirmed = assignmentConfirmed = reconcileConfirmed = false;
        Message = value.Status switch
        {
            1 => "Proposal saved as Pending. Review it and explicitly reconcile; access has not been confirmed.",
            2 => "Proposal Applied. Reload current administration before another change. Removing one grant may leave access from another source.",
            3 => "Proposal Failed. Review the saved evidence and reload current administration before another change.",
            _ => "Proposal Uncertain. Keep its reference and review the retained outcome before any further action."
        };
        Tone = value.Status == 2 ? "ready" : value.Status == 3 ? "error" : "warning";
    }
    private Task PermissionAsync() => RunAsync(async ct =>
    {
        if (PendingCommand is null && (!CanChange() || !permissionConfirmed) || !Id(target, out var account) ||
            !TenantAdministrationContracts.Permission(permission) || permissionAction is not ("grant" or "revoke"))
        { Invalid("Review the active member, compiled permission, current authorization revision and confirmation."); return; }
        if (PendingCommand is null && !catalog!.PermissionCatalog.Any(p => p.PermissionId == permission && p.DelegableByInitialOwner))
        { Invalid("Choose a delegable permission from the current catalog."); return; }
        pendingAction = "permission";
        await ProposeAsync($"authorization/permissions/{permissionAction}", new { targetAccountId = account, permissionId = permission, expectedAuthorizationRevision = expectedAuthorization },
            permissionAction == "grant" ? 1 : 2, ct, account: account, directPermission: permission);
    });
    private Task RoleAsync() => RunAsync(async ct =>
    {
        if (PendingCommand is null && (!CanChange() || !roleConfirmed) || !Id(roleReference, out var role) || expectedRole < 1 ||
            roleAction is not ("create" or "revise" or "retire") || roleAction != "retire" &&
            (!OperationsContracts.Text(roleName, 128) || selectedPermissions.Count is < 1 or > 32 || selectedPermissions.Any(p => !TenantAdministrationContracts.Permission(p))))
        { Invalid("Review the role reference, name, exact revisions, 1–32 permissions and confirmation."); return; }
        if (PendingCommand is null && roleAction != "retire" && selectedPermissions.Any(id => !catalog!.PermissionCatalog.Any(p => p.PermissionId == id && p.DelegableByInitialOwner)))
        { Invalid("Choose only delegable permissions from the current catalog."); return; }
        var expectedAuthorizationRevision = expectedAuthorization;
        var permissionIds = selectedPermissions.Order(StringComparer.Ordinal).ToArray();
        object payload = roleAction switch
        {
            "create" => new { roleId = role, name = roleName, permissionIds, expectedAuthorizationRevision },
            "revise" => new { name = roleName, permissionIds, expectedRoleRevision = expectedRole, expectedAuthorizationRevision },
            _ => new { expectedRoleRevision = expectedRole, expectedAuthorizationRevision }
        };
        pendingAction = "role";
        await ProposeAsync(roleAction == "create" ? "authorization/roles" : $"authorization/roles/{role:D}" + (roleAction == "retire" ? "/retire" : ""),
            payload, roleAction == "create" ? 3 : roleAction == "revise" ? 4 : 5, ct, role: role, method: roleAction == "revise" ? HttpMethod.Put : null);
    });
    private Task AssignmentAsync() => RunAsync(async ct =>
    {
        if (PendingCommand is null && (!CanChange() || !assignmentConfirmed) || !Id(assignmentRole, out var role) || !Id(assignmentAccount, out var account) || assignmentAction is not ("assign" or "remove"))
        { Invalid("Review the role, active member account, current authorization revision and confirmation."); return; }
        pendingAction = "assignment";
        await ProposeAsync($"authorization/roles/{role:D}/assignments/{account:D}/{assignmentAction}", new { expectedAuthorizationRevision = expectedAuthorization },
            assignmentAction == "assign" ? 6 : 7, ct, role, account);
    });
    private Task LookupAsync() => RunAsync(async ct =>
    {
        proposal = null;
        if (!Id(proposalReference, out var id)) { Invalid("Enter a saved proposal reference."); return; }
        var result = await Api.GetAsync<TenantProposalRecord>(TenantId, $"authorization/proposals/{id:D}", x => TenantAdministrationContracts.Proposal(x, TenantId, id), ct);
        if (result.Value is { } value) ApplyProposal(value); else Show(result);
    });
    private Task ReconcileAsync() => RunAsync(async ct =>
    {
        if (proposal is not { Status: 1 or 4 } current || PendingCommand is null && !reconcileConfirmed)
        { Invalid("Load a Pending or Uncertain proposal and confirm one bounded reconciliation attempt."); return; }
        pendingAction = "reconcile";
        var value = await SaveAsync<TenantProposalRecord>($"authorization/proposals/{current.ProposalId:D}/reconcile", new { },
            x => TenantAdministrationContracts.Proposal(x, TenantId, current.ProposalId) && x.RequestFingerprint == current.RequestFingerprint, ct);
        if (value is not null) ApplyProposal(value);
    });
    private Task TransferAsync() => RunAsync(async ct =>
    {
        if (PendingCommand is null && (!CanChange() || !string.Equals(transferConfirmation, transferTarget, StringComparison.Ordinal)) ||
            !Id(transferTarget, out var account) || expectedTenant < 1)
        { Invalid("Enter the active successor, exact tenant revision and matching account confirmation."); return; }
        pendingAction = "transfer";
        var value = await SaveAsync<TenantOwnerReceipt>("authorization/owner-transfer", new { targetAccountId = account, expectedTenantRevision = expectedTenant },
            x => TenantAdministrationContracts.Owner(x, account, expectedTenant), ct);
        if (value is not null)
        {
            ownerReceipt = value; transferred = true; catalog = null; proposal = null; transferConfirmation = "";
            Message = "Owner handoff saved. Your previous Owner designation no longer authorizes administration. The successor must reload with their own current session."; Tone = "ready";
        }
    });
    private Task RetryAsync() => pendingAction switch { "permission" => PermissionAsync(), "role" => RoleAsync(), "assignment" => AssignmentAsync(), "reconcile" => ReconcileAsync(), _ => TransferAsync() };
}
