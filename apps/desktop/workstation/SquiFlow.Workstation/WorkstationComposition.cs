using Avalonia.Controls;
using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Hosting;
using SquiFlow.ApplicationKernel.Modules;
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

        var definitions = workstationModules.SelectMany(module => module.Features).ToArray();
        var snapshot = FeatureSnapshotBuilder.Publish(
            definitions,
            HostKind.Workstation,
            platformAllowed: [CustomersModule.Feature],
            tenantRequested: [CustomersModule.Feature],
            allowedChannels: new HashSet<ReleaseChannel> { ReleaseChannel.Stable },
            revision: 1);

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
            .Where(contribution => contribution.Metadata.RequiredFeature is not FeatureId feature || snapshot.IsEnabled(feature))
            .OrderBy(contribution => contribution.Metadata.Order)
            .ToArray();
    }
}
