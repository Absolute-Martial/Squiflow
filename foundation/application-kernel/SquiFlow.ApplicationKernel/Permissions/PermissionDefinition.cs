using SquiFlow.ApplicationKernel.Hosting;
using SquiFlow.ApplicationKernel.Tenancy;

namespace SquiFlow.ApplicationKernel.Permissions;

public sealed record PermissionDefinition(
    PermissionId Id,
    ModuleId OwnerModuleId,
    string DisplayName,
    IReadOnlySet<HostKind> SupportedHosts,
    FeatureId? RequiredFeature = null)
{
    public bool Supports(HostKind host) => SupportedHosts.Contains(host);
}

public interface IApplicationAuthorization
{
    ValueTask<bool> IsAllowedAsync(
        TenantContext tenantContext,
        PermissionId permissionId,
        CancellationToken cancellationToken = default);
}
