using Application.ApplicationEntry;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Application.Workstation.Authentication;

namespace Application.Workstation;

public sealed partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var httpClient = ApplicationEntryClient.CreateHttpClient();
            var login = NativeLoginConfiguration.ReadEnvironment();
            var access = login is null ? null : new TenantEntryViewModel(new NativeSignIn(login), new TenantEntryClient(httpClient));
            var viewModel = new ConnectionViewModel(new ApplicationEntryClient(httpClient),
                Environment.GetEnvironmentVariable("APPLICATION_CORE_API_URL") ?? string.Empty, access);
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.Exit += (_, _) =>
            {
                viewModel.ConnectCommand.Cancel();
                access?.Dispose();
                httpClient.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
