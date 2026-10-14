using System.Net;
using System.Net.Http.Json;
using Application.ApplicationEntry;
using Application.Branding;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Application.Workstation.Tests.TestApplicationBuilder))]

namespace Application.Workstation.Tests;

public static class TestApplicationBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class ConnectionScreenTests
{
    [AvaloniaFact]
    public async Task KeyboardEntryAndDefaultActionRenderRealApplicationResults()
    {
        using var http = new HttpClient(new EntryHandler());
        var model = new ConnectionViewModel(new ApplicationEntryClient(http));
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            var address = window.FindControl<TextBox>("ServiceAddressBox")!;
            Assert.True(address.Focus());
            window.KeyTextInput("https://api.example.com");
            Assert.Equal("https://api.example.com", model.Address);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Assert.NotNull(model.ConnectCommand.ExecutionTask);
            await model.ConnectCommand.ExecutionTask.WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Fieldwork", window.Title);
            Assert.Equal("Service available", window.FindControl<TextBlock>("ConnectionHeading")!.Text);
            var statusPeer = new TextBlockAutomationPeer(window.FindControl<TextBlock>("ConnectionHeading")!);
            Assert.Equal("Service available", statusPeer.GetName());
            Assert.Equal(AutomationLiveSetting.Polite, statusPeer.GetLiveSetting());
            Assert.Contains("does not sign you in", window.FindControl<TextBlock>("ConnectionMessage")!.Text, StringComparison.Ordinal);
            Assert.True(window.FindControl<Button>("ConnectButton")!.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task InvalidAddressesAreRejectedWithoutContactingTheService()
    {
        var handler = new EntryHandler();
        using var http = new HttpClient(handler);
        var model = new ConnectionViewModel(new ApplicationEntryClient(http), "http://remote.example.com");
        await model.ConnectCommand.ExecuteAsync(null);
        Assert.Equal("Check the service address", model.Heading);
        Assert.Equal(0, handler.Calls);
    }

    [AvaloniaFact]
    public async Task NewFailedChecksClearPreviousBrandingAndDoNotReportSuccess()
    {
        var handler = new EntryHandler();
        using var http = new HttpClient(handler);
        var model = new ConnectionViewModel(new ApplicationEntryClient(http), "https://api.example.com");
        await model.ConnectCommand.ExecuteAsync(null);
        Assert.Equal("Fieldwork", model.ApplicationName);
        handler.Readiness = HttpStatusCode.InternalServerError;
        await model.ConnectCommand.ExecuteAsync(null);
        Assert.Equal("Application connection", model.ApplicationName);
        Assert.Equal(string.Empty, model.LegalName);
        Assert.Equal("Unable to connect", model.Heading);
    }

    [AvaloniaFact]
    public async Task PendingChecksDisableDuplicateActionsAndCancelWithoutFalseSuccess()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new BlockingHandler(entered));
        var model = new ConnectionViewModel(new ApplicationEntryClient(http), "https://api.example.com");
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            var pending = model.ConnectCommand.ExecuteAsync(null);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.FindControl<Button>("ConnectButton")!.IsEffectivelyEnabled);
            Assert.False(model.ConnectCommand.CanExecute(null));
            Assert.True(window.FindControl<TextBox>("ServiceAddressBox")!.IsReadOnly);
            Assert.True(model.CancelCommand.CanExecute(null));
            model.CancelCommand.Execute(null);
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("Check canceled", model.Heading);
            Assert.Equal("Application connection", model.ApplicationName);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ConnectionControlsRemainVisibleAtTheSupportedMinimumWindowSize()
    {
        using var http = new HttpClient(new EntryHandler());
        var window = new MainWindow { DataContext = new ConnectionViewModel(new ApplicationEntryClient(http)), Width = 780, Height = 640 };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var button = window.FindControl<Button>("ConnectButton")!;
            Assert.True(button.Bounds.Width > 100);
            Assert.True(button.Bounds.Height > 30);
            Assert.True(window.FindControl<TextBox>("ServiceAddressBox")!.Bounds.Width > 100);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class EntryHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpStatusCode Readiness { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(request.RequestUri!.AbsolutePath == "/api/v1/application/bootstrap"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(BrandProfile.Create("Fieldwork", "Fieldwork", "Fieldwork Services",
                        "default", "/logo.svg", "/favicon.svg", "/support", "/privacy", "/terms"))
                }
                : new HttpResponseMessage(Readiness));
        }
    }

    private sealed class BlockingHandler(TaskCompletionSource entered) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new(HttpStatusCode.OK);
        }
    }
}
