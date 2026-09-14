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

public sealed class EffectivePermissionSnapshot
{
    private readonly HashSet<PermissionId> _allowed;

    public EffectivePermissionSnapshot(long revision, IEnumerable<PermissionId> allowed)
    {
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        ArgumentNullException.ThrowIfNull(allowed);
        Revision = revision;
        _allowed = allowed.ToHashSet();
    }

    public long Revision { get; }
    public bool Allows(PermissionId permissionId) => _allowed.Contains(permissionId);
}

public interface IApplicationAuthorization
{
    ValueTask<bool> IsAllowedAsync(
        TenantContext tenantContext,
        PermissionId permissionId,
        CancellationToken cancellationToken = default);
}
