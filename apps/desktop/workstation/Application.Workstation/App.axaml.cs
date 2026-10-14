using Application.ApplicationEntry;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Application.Workstation;

public sealed partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var httpClient = ApplicationEntryClient.CreateHttpClient();
            var viewModel = new ConnectionViewModel(new ApplicationEntryClient(httpClient),
                Environment.GetEnvironmentVariable("APPLICATION_CORE_API_URL") ?? string.Empty);
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.Exit += (_, _) =>
            {
                viewModel.ConnectCommand.Cancel();
                httpClient.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
