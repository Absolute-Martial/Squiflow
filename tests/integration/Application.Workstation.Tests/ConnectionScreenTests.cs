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
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Application.Workstation.Tests.TestApplicationBuilder))]

namespace Application.Workstation.Tests;

public static class TestApplicationBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().WithInterFont().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
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
            address.SelectAll();
            Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(address.SelectionForegroundBrush).Color);
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
    public async Task EditingTheServiceAddressClearsThePreviousConnectionResult()
    {
        var handler = new EntryHandler();
        using var http = new HttpClient(handler);
        var model = new ConnectionViewModel(new ApplicationEntryClient(http), "https://api.example.com");
        await model.ConnectCommand.ExecuteAsync(null);
        Assert.Equal("Fieldwork", model.ApplicationName);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.Address = "https://api.example.com";
            Assert.Equal("Fieldwork", model.ApplicationName);
            window.FindControl<TextBox>("ServiceAddressBox")!.Text = "https://another.example.com";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("https://another.example.com", model.Address);
            Assert.Equal("Application connection", window.Title);
            Assert.Equal(string.Empty, model.LegalName);
            Assert.Equal("Connection not checked", window.FindControl<TextBlock>("ConnectionHeading")!.Text);
            Assert.Contains("address changed", model.Message, StringComparison.Ordinal);
            Assert.Equal(2, handler.Calls);
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var output = Environment.GetEnvironmentVariable("APPLICATION_UI_ARTIFACTS");
            if (output is not null)
            {
                Directory.CreateDirectory(output);
                frame.Save(Path.Combine(output, "workstation-address-changed.png"));
            }
            await model.ConnectCommand.ExecuteAsync(null);
            Assert.Equal("Service available", model.Heading);
            Assert.Equal(4, handler.Calls);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task EditingTheAddressDuringACheckCannotRestoreItsPreviousResultOrStatus()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new DelayedEntryHandler(entered, release));
        var model = new ConnectionViewModel(new ApplicationEntryClient(http), "https://api.example.com");
        var pending = model.ConnectCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            model.Address = "https://another.example.com";
        }
        finally { release.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Application connection", model.ApplicationName);
        Assert.Equal("Connection not checked", model.Heading);
        Assert.Contains("address changed", model.Message, StringComparison.Ordinal);
        Assert.True(model.ConnectCommand.CanExecute(null));
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
            Assert.Contains("busy", window.FindControl<Button>("ConnectButton")!.Classes);
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
    public async Task EntryReflowsBetweenCompactAndWideLayoutsWithoutShrinkingControls()
    {
        using var http = new HttpClient(new EntryHandler("Fieldwork " + string.Concat(Enumerable.Repeat("North ", 18))));
        var model = new ConnectionViewModel(new ApplicationEntryClient(http), "https://api.example.com");
        await model.ConnectCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            foreach (var (width, height) in new[] { (1120, 820), (520, 560), (780, 640), (1120, 820) })
            {
                window.Width = width;
                window.Height = height;
                Dispatcher.UIThread.RunJobs();
                var compact = width < 960;
                Assert.Equal(!compact, window.FindControl<StackPanel>("EntryIntroduction")!.IsVisible);
                Assert.Equal(compact ? 1 : 2, window.FindControl<Grid>("EntryLayout")!.ColumnDefinitions.Count);
                var panel = window.FindControl<Border>("ConnectionPanel")!;
                if (!compact) Assert.Equal(440, panel.Bounds.Width);
                var panelPosition = panel.TranslatePoint(default, window)!.Value;
                Assert.InRange(panelPosition.X + panel.Bounds.Width, 0, width);
                var button = window.FindControl<Button>("ConnectButton")!;
                var address = window.FindControl<TextBox>("ServiceAddressBox")!;
                Assert.True(button.Bounds.Width >= 300);
                Assert.True(button.Bounds.Height >= 48);
                Assert.True(address.Bounds.Width >= 300);
                Assert.True(address.Bounds.Height >= 48);
                Assert.True(button.Focus());
                button.BringIntoView();
                Dispatcher.UIThread.RunJobs();
                var position = button.TranslatePoint(default, window)!.Value;
                Assert.InRange(position.Y, 0, height - button.Bounds.Height);
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                var output = Environment.GetEnvironmentVariable("APPLICATION_UI_ARTIFACTS");
                if (output is not null)
                {
                    Directory.CreateDirectory(output);
                    frame.Save(Path.Combine(output, $"workstation-{width}.png"));
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class EntryHandler(string name = "Fieldwork") : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpStatusCode Readiness { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(request.RequestUri!.AbsolutePath == "/api/v1/application/bootstrap"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(BrandProfile.Create(name, name, "Fieldwork Services",
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

    private sealed class DelayedEntryHandler(TaskCompletionSource entered, TaskCompletionSource release) : DelegatingHandler(new EntryHandler())
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await release.Task;
            return await base.SendAsync(request, CancellationToken.None);
        }
    }
}
