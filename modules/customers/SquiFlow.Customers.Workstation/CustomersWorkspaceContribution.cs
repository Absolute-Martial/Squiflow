using Avalonia.Controls;
using SquiFlow.ApplicationKernel.Presentation;

namespace SquiFlow.Customers.Workstation;

public sealed class CustomersWorkspaceContribution : IWorkspaceContribution<Control>
{
    public WorkspaceContributionMetadata Metadata { get; } = new(
        new WorkspaceId("customers.workspace"),
        "Customers",
        Order: 100,
        CustomersModule.Id,
        RequiredFeature: CustomersModule.Feature,
        RequiredPermission: CustomersModule.ViewPermission);

    public Control CreateView() => new CustomerWorkspaceView();
}
