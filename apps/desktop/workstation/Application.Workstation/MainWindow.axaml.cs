using Avalonia.Controls;

namespace Application.Workstation;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            if (DataContext is ConnectionViewModel viewModel)
            {
                viewModel.ConnectCommand.Cancel();
                viewModel.Access?.Dispose();
            }
        };
    }
}
