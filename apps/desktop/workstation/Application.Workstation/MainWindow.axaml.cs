using Avalonia;
using Avalonia.Controls;

namespace Application.Workstation;

public sealed partial class MainWindow : Window
{
    private bool? compactLayout;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => UpdateEntryLayout();
        SizeChanged += (_, _) => UpdateEntryLayout();
        Closed += (_, _) =>
        {
            if (DataContext is ConnectionViewModel viewModel)
            {
                viewModel.ConnectCommand.Cancel();
                viewModel.Access?.Dispose();
            }
        };
    }

    private void UpdateEntryLayout()
    {
        var breakpoint = (double)this.FindResource("Size.Compact")!;
        var panelWidth = (double)this.FindResource("Size.Panel")!;
        var compact = ClientSize.Width < breakpoint;
        if (compactLayout == compact) return;
        compactLayout = compact;
        EntryIntroduction.IsVisible = !compact;
        ConnectionPanel.Width = compact ? double.NaN : panelWidth;
        EntryLayout.ColumnDefinitions = compact
            ? new ColumnDefinitions("*")
            : new ColumnDefinitions { new(GridLength.Star), new(new GridLength(panelWidth)) };
        Grid.SetColumn(ConnectionPanel, compact ? 0 : 1);
    }
}
