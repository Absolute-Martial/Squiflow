using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Hosting;
using SquiFlow.ApplicationKernel.Permissions;
using SquiFlow.ApplicationKernel.Settings;

namespace SquiFlow.ApplicationKernel.Modules;

public sealed record ModuleDescriptor(
    ModuleId Id,
    Version Version,
    IReadOnlyCollection<ModuleId> Dependencies,
    IReadOnlySet<HostKind> SupportedHosts,
    IReadOnlyCollection<FeatureDefinition> Features,
    IReadOnlyCollection<PermissionDefinition> Permissions,
    IReadOnlyCollection<ISettingDefinition> Settings)
{
    public bool Supports(HostKind host) => SupportedHosts.Contains(host);
}
