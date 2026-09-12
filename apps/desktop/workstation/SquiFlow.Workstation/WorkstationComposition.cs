using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SquiFlow.ApplicationKernel;
using SquiFlow.ApplicationKernel.Authorization;
using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Modules;
using SquiFlow.ApplicationKernel.Presentation;
using SquiFlow.Customers;
using SquiFlow.Customers.Workstation;

namespace SquiFlow.Workstation;

internal static class WorkstationComposition
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        var graph = ModuleGraph.Build([CustomersModule.Descriptor]);
        var workstationModules = graph.ForHost(HostKind.Workstation);
        var featureDefinitions = workstationModules.SelectMany(module => module.Features).ToArray();

        var featureSnapshot = FeatureSnapshotBuilder.Publish(
            featureDefinitions,
            platformAllowed: [CustomersModule.Feature],
            tenantRequested: [CustomersModule.Feature],
            revision: 1);

        var permissionSnapshot = new EffectivePermissionSnapshot(
            revision: 1,
            allowed: [CustomersModule.ViewPermission]);

        services.AddSingleton(graph);
        services.AddSingleton<IFeatureSnapshotAccessor>(new FixedFeatureSnapshotAccessor(featureSnapshot));
        services.AddSingleton(permissionSnapshot);
        services.AddSingleton<IWorkspaceContribution<Control>, CustomersWorkstationContribution>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private sealed class FixedFeatureSnapshotAccessor(EffectiveFeatureSnapshot current)
        : IFeatureSnapshotAccessor
    {
        public EffectiveFeatureSnapshot Current { get; } = current;
    }
}
