using Avalonia.Controls;

namespace Application.Workstation;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as ConnectionViewModel)?.ConnectCommand.Cancel();
    }
}
