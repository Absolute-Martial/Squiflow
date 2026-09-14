using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SquiFlow.ApplicationKernel.Presentation;

namespace SquiFlow.Workstation;

public sealed class MainWindow : Window
{
    private readonly ContentControl _workspaceHost = new();

    public MainWindow(IReadOnlyList<IWorkspaceContribution<Control>> contributions)
    {
        Title = "SquiFlow Workstation";
        Width = 1100;
        Height = 720;
        MinWidth = 800;
        MinHeight = 520;

        var navigation = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8
        };

        foreach (var contribution in contributions)
        {
            var button = new Button
            {
                Content = contribution.Metadata.Label,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            button.Click += (_, _) => _workspaceHost.Content = contribution.CreateView();
            navigation.Children.Add(button);
        }

        if (contributions.Count > 0)
        {
            _workspaceHost.Content = contributions[0].CreateView();
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("220,*"),
            RowDefinitions = new RowDefinitions("*,Auto")
        };

        Grid.SetColumn(navigation, 0);
        Grid.SetRow(navigation, 0);
        Grid.SetColumn(_workspaceHost, 1);
        Grid.SetRow(_workspaceHost, 0);

        var status = new TextBlock
        {
            Margin = new Thickness(12, 8),
            Text = "Phase 0 foundation · no SQLite business persistence or synchronization authority is implemented yet."
        };
        Grid.SetColumnSpan(status, 2);
        Grid.SetRow(status, 1);

        grid.Children.Add(navigation);
        grid.Children.Add(_workspaceHost);
        grid.Children.Add(status);
        Content = grid;
    }
}
