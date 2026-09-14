using Avalonia.Controls;
using SquiFlow.ApplicationKernel;
using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Hosting;
using SquiFlow.ApplicationKernel.Modules;
using SquiFlow.ApplicationKernel.Permissions;
using SquiFlow.ApplicationKernel.Presentation;
using SquiFlow.Customers;
using SquiFlow.Customers.Workstation;

namespace SquiFlow.Workstation;

internal static class WorkstationComposition
{
    public static IReadOnlyList<IWorkspaceContribution<Control>> CreateWorkspaceContributions()
    {
        var moduleGraph = ModuleGraph.Build([CustomersModule.Descriptor]);
        var workstationModules = moduleGraph.ForHost(HostKind.Workstation);

        var featureDefinitions = workstationModules.SelectMany(module => module.Features).ToArray();
        var featureSnapshot = FeatureSnapshotBuilder.Publish(
            featureDefinitions,
            HostKind.Workstation,
            platformAllowed: [CustomersModule.Feature],
            tenantRequested: [CustomersModule.Feature],
            allowedChannels: new HashSet<ReleaseChannel> { ReleaseChannel.Stable },
            revision: 1);

        // Phase 0 proves snapshot-aware UX composition only. This snapshot is not server authorization.
        var permissionSnapshot = new EffectivePermissionSnapshot(
            revision: 1,
            allowed: [CustomersModule.ViewPermission]);

        IWorkspaceContribution<Control>[] candidates =
        [
            new CustomersWorkspaceContribution()
        ];

        var duplicates = candidates
            .GroupBy(contribution => contribution.Metadata.WorkspaceId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicates is not null)
        {
            throw new InvalidOperationException($"Duplicate workspace id '{duplicates.Key}'.");
        }

        var moduleIds = workstationModules.Select(module => module.Id).ToHashSet();
        return candidates
            .Where(contribution => moduleIds.Contains(contribution.Metadata.OwnerModuleId))
            .Where(contribution => contribution.Metadata.RequiredFeature is not FeatureId feature || featureSnapshot.IsEnabled(feature))
            .Where(contribution => contribution.Metadata.RequiredPermission is not PermissionId permission || permissionSnapshot.Allows(permission))
            .OrderBy(contribution => contribution.Metadata.Order)
            .ToArray();
    }
}
