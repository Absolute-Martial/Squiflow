using Application.ApplicationEntry;
using Application.Entry.TestSupport;
using Application.Workstation.Authentication;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace Application.Workstation.Tests;

public sealed class TenantEntryScreenTests
{
    [AvaloniaFact]
    public async Task PickerBindingsAndKeyboardEntryRequireFreshWorkspaceAuthority()
    {
        using var protocol = new EntryProtocolFixture();
        using var http = new HttpClient(protocol, false);
        using var model = new TenantEntryViewModel(new SessionSource(), new TenantEntryClient(http));
        model.SetServiceAvailable(new("https://api.example.test"), true);
        await model.SignInCommand.ExecuteAsync(null);
        var panel = new TenantEntryPanel { DataContext = model };
        var window = new Window { Width = 500, Height = 850, Content = panel };
        window.Show();
        try
        {
            Assert.True(model.IsSignedIn); Assert.Null(model.SelectedTenant); Assert.False(model.HasWorkspace);
            var choice = panel.FindControl<ComboBox>("TenantChoice")!;
            Capture(window, "workstation-picker");
            choice.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(protocol.FirstTenant, model.SelectedTenant!.TenantId);
            var enter = panel.FindControl<Button>("EnterWorkspaceButton")!;
            Assert.True(enter.Focus());
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Assert.NotNull(model.EnterCommand.ExecutionTask);
            await model.EnterCommand.ExecutionTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("North workspace", model.WorkspaceName);
            Dispatcher.UIThread.RunJobs();
            Capture(window, "workstation-workspace");
            await model.RefreshCommand.ExecuteAsync(null);
            Assert.False(model.HasWorkspace);
            model.SelectedTenant = model.Tenants[1]; protocol.DenyWorkspace = true;
            await model.EnterCommand.ExecuteAsync(null);
            Assert.False(model.HasWorkspace);
            Assert.Contains("cannot enter", model.Message, StringComparison.Ordinal);
            model.SignOutCommand.Execute(null);
            Assert.False(model.IsSignedIn); Assert.Empty(model.Tenants); Assert.Equal("", model.AccountReference);
        }
        finally { window.Close(); }
    }

    private static void Capture(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var output = Environment.GetEnvironmentVariable("APPLICATION_UI_ARTIFACTS");
        if (output is null) return;
        Directory.CreateDirectory(output);
        frame.Save(Path.Combine(output, $"{name}.png"));
    }

    [AvaloniaFact]
    public async Task EmptyAndDisabledAccountsHaveExplicitStatesAndDifferentServiceCannotReceiveCredentials()
    {
        using var protocol = new EntryProtocolFixture { NoMemberships = true };
        using var http = new HttpClient(protocol, false);
        using var model = new TenantEntryViewModel(new SessionSource(), new TenantEntryClient(http));
        model.SetServiceAvailable(new("https://another.example.test"), true);
        Assert.False(model.SignInCommand.CanExecute(null));
        model.SetServiceAvailable(new("https://api.example.test"), true);
        await model.SignInCommand.ExecuteAsync(null);
        Assert.Empty(model.Tenants); Assert.Contains("No workspaces", model.Message, StringComparison.Ordinal);
        protocol.DisabledAccount = true;
        await model.RefreshCommand.ExecuteAsync(null);
        Assert.False(model.IsSignedIn); Assert.False(model.HasWorkspace); Assert.Empty(model.Tenants);
        Assert.Contains("cannot access an active application account", model.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task SignOutCannotBeUndoneByALateWorkspaceResponse()
    {
        using var protocol = new EntryProtocolFixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new DelayedWorkspace(protocol, entered, release));
        using var model = new TenantEntryViewModel(new SessionSource(), new TenantEntryClient(http));
        model.SetServiceAvailable(new("https://api.example.test"), true);
        await model.SignInCommand.ExecuteAsync(null);
        model.SelectedTenant = model.Tenants[0];
        var pending = model.EnterCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        model.SignOutCommand.Execute(null);
        release.SetResult();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(model.IsSignedIn); Assert.False(model.HasWorkspace); Assert.Empty(model.Tenants);
        Assert.Equal("", model.AccountReference);
    }

    private sealed class DelayedWorkspace(EntryProtocolFixture protocol, TaskCompletionSource entered, TaskCompletionSource release)
        : DelegatingHandler(protocol)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/workspace", StringComparison.Ordinal))
            {
                entered.SetResult();
                await release.Task;
                return await base.SendAsync(request, CancellationToken.None);
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class SessionSource : INativeSignIn
    {
        public Uri CoreApiOrigin { get; } = new("https://api.example.test");
        public Task<NativeCredential?> SignInAsync(CancellationToken cancellationToken) =>
            Task.FromResult<NativeCredential?>(new(EntryProtocolFixture.AccessToken, DateTimeOffset.UtcNow.AddMinutes(10)));
    }
}
