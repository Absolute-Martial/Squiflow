namespace SquiFlow.ApplicationKernel
{
    public enum HostKind { Workstation, Guard, CoreApi, TenantWeb, Worker, AdminApi, AdminWeb }

    public readonly record struct ModuleId(string Value) { public override string ToString() => Value; }
    public readonly record struct FeatureId(string Value) { public override string ToString() => Value; }
    public readonly record struct PermissionId(string Value) { public override string ToString() => Value; }
    public readonly record struct SettingKey(string Value) { public override string ToString() => Value; }
    public readonly record struct TenantId(string Value) { public override string ToString() => Value; }
    public readonly record struct SubjectId(string Value) { public override string ToString() => Value; }
}

namespace SquiFlow.ApplicationKernel.Tenancy
{
    public sealed record TenantContext(TenantId TenantId, SubjectId SubjectId);
}

namespace SquiFlow.ApplicationKernel.Authorization
{
    using SquiFlow.ApplicationKernel.Tenancy;

    public sealed record PermissionDefinition(
        PermissionId Id,
        ModuleId ModuleId,
        string DisplayName,
        FeatureId? RequiredFeature = null);

    public interface IPermissionEvaluator
    {
        ValueTask<bool> IsAllowedAsync(
            TenantContext tenantContext,
            PermissionId permissionId,
            CancellationToken cancellationToken = default);
    }

    public sealed class EffectivePermissionSnapshot
    {
        private readonly HashSet<PermissionId> _allowed;

        public EffectivePermissionSnapshot(long revision, IEnumerable<PermissionId> allowed)
        {
            Revision = revision;
            _allowed = allowed.ToHashSet();
        }

        public long Revision { get; }
        public bool Allows(PermissionId permissionId) => _allowed.Contains(permissionId);
    }
}

namespace SquiFlow.ApplicationKernel.Presentation
{
    public sealed record WorkspaceContributionMetadata(
        string NavigationId,
        string Label,
        string Route,
        int Order,
        ModuleId ModuleId,
        FeatureId? RequiredFeature = null,
        PermissionId? RequiredPermission = null);

    public interface IWorkspaceContribution<out TView>
    {
        WorkspaceContributionMetadata Metadata { get; }
        TView CreateView();
    }
}
