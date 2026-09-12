using Avalonia.Controls;
using SquiFlow.ApplicationKernel.Authorization;
using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Presentation;

namespace SquiFlow.Workstation;

public partial class MainWindow : Window
{
    private readonly IReadOnlyList<IWorkspaceContribution<Control>> _contributions;

    public MainWindow(
        IEnumerable<IWorkspaceContribution<Control>> contributions,
        IFeatureSnapshotAccessor features,
        EffectivePermissionSnapshot permissions)
    {
        InitializeComponent();

        _contributions = contributions
            .Where(contribution => IsVisible(contribution.Metadata, features.Current, permissions))
            .OrderBy(contribution => contribution.Metadata.Order)
            .ThenBy(contribution => contribution.Metadata.Label, StringComparer.Ordinal)
            .ToArray();

        foreach (var contribution in _contributions)
        {
            var button = new Button
            {
                Content = contribution.Metadata.Label,
                Tag = contribution
            };
            button.Classes.Add("nav-item");
            button.Click += NavigationClicked;
            NavigationItems.Children.Add(button);
        }

        if (_contributions.Count > 0)
        {
            Activate(_contributions[0]);
        }
    }

    private static bool IsVisible(
        WorkspaceContributionMetadata metadata,
        EffectiveFeatureSnapshot features,
        EffectivePermissionSnapshot permissions)
    {
        if (metadata.RequiredFeature is { } featureId && !features.IsEnabled(featureId))
        {
            return false;
        }

        if (metadata.RequiredPermission is { } permissionId && !permissions.Allows(permissionId))
        {
            return false;
        }

        return true;
    }

    private void NavigationClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (sender is Button { Tag: IWorkspaceContribution<Control> contribution })
        {
            Activate(contribution);
        }
    }

    private void Activate(IWorkspaceContribution<Control> contribution) =>
        WorkspaceHost.Content = contribution.CreateView();
}
