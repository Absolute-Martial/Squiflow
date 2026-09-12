using Avalonia.Controls;
using SquiFlow.ApplicationKernel.Presentation;

namespace SquiFlow.Customers.Workstation;

public sealed class CustomersWorkstationContribution : IWorkspaceContribution<Control>
{
    public WorkspaceContributionMetadata Metadata { get; } = new(
        NavigationId: "customers",
        Label: "Customers",
        Route: "/customers",
        Order: 100,
        ModuleId: CustomersModule.Id,
        RequiredFeature: CustomersModule.Feature,
        RequiredPermission: CustomersModule.ViewPermission);

    public Control CreateView() => new CustomerWorkspaceView();
}
