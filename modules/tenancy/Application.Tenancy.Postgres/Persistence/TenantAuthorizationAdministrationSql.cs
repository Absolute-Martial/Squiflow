namespace Application.Tenancy.Postgres;

internal static class TenantAuthorizationAdministrationSql
{
    internal static readonly string GetAuthorizationRevision = Load(nameof(GetAuthorizationRevision));
    internal static readonly string IsInitialOwner = Load(nameof(IsInitialOwner));
    internal static readonly string InsertAuthorizationProposal = Load(nameof(InsertAuthorizationProposal));
    internal static readonly string CompleteAuthorizationProposal = Load(nameof(CompleteAuthorizationProposal));
    internal static readonly string ListCustomRoles = Load(nameof(ListCustomRoles));
    internal static readonly string FindCustomRole = Load(nameof(FindCustomRole));
    internal static readonly string ListCustomRoleAssignments = Load(nameof(ListCustomRoleAssignments));
    internal static readonly string TransferInitialOwner = Load(nameof(TransferInitialOwner));
    internal static readonly string UpdateAuthorizationProposalStatus = Load(nameof(UpdateAuthorizationProposalStatus));
    internal static readonly string UpsertDirectPermissionGrant = Load(nameof(UpsertDirectPermissionGrant));
    internal static readonly string InsertCustomRole = Load(nameof(InsertCustomRole));
    internal static readonly string ReviseCustomRole = Load(nameof(ReviseCustomRole));
    internal static readonly string RetireCustomRole = Load(nameof(RetireCustomRole));
    internal static readonly string UpsertCustomRoleAssignment = Load(nameof(UpsertCustomRoleAssignment));
    internal static readonly string LockTenant = Load(nameof(LockTenant));
    internal static readonly string EnsureAuthorizationState = Load(nameof(EnsureAuthorizationState));
    internal static readonly string LockAuthorizationState = Load(nameof(LockAuthorizationState));
    internal static readonly string HasActiveAuthorizationProposal = Load(nameof(HasActiveAuthorizationProposal));
    internal static readonly string IsActiveInitialOwner = Load(nameof(IsActiveInitialOwner));
    internal static readonly string IsActiveMembership = Load(nameof(IsActiveMembership));
    internal static readonly string IsDirectPermissionGrantActive = Load(nameof(IsDirectPermissionGrantActive));
    internal static readonly string IsCustomRoleAssignmentActive = Load(nameof(IsCustomRoleAssignmentActive));
    internal static readonly string ActiveCustomRoleNameExists = Load(nameof(ActiveCustomRoleNameExists));
    internal static readonly string FindCustomRoleForUpdate = Load(nameof(FindCustomRoleForUpdate));
    internal static readonly string FindInitialOwnerForUpdate = Load(nameof(FindInitialOwnerForUpdate));
    internal static readonly string FindMembershipForUpdate = Load(nameof(FindMembershipForUpdate));
    internal static readonly string FindOwnerTransferReceipt = Load(nameof(FindOwnerTransferReceipt));
    internal static readonly string InsertAuthorizationEvent = Load(nameof(InsertAuthorizationEvent));
    internal static readonly string FindAuthorizationProposal = Load(nameof(FindAuthorizationProposal));
    internal static readonly string FindAuthorizationProposalByIdempotencyForUpdate = Load(nameof(FindAuthorizationProposalByIdempotencyForUpdate));
    internal static readonly string FindAuthorizationProposalForUpdate = Load(nameof(FindAuthorizationProposalForUpdate));
    internal static readonly string CountActiveCustomRoles = Load(nameof(CountActiveCustomRoles));
    internal static readonly string CountActiveCustomRoleAssignments = Load(nameof(CountActiveCustomRoleAssignments));

    private static string Load(string name)
    {
        var resource = $"{typeof(TenantAuthorizationAdministrationSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(TenantAuthorizationAdministrationSql).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded Tenancy SQL resource: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
