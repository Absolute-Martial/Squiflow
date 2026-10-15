using Application.ApplicationEntry;
using Application.Workstation.Authentication;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Application.Workstation;

public sealed class TenantEntryViewModel : ObservableObject, IDisposable
{
    private readonly INativeSignIn signIn;
    private readonly TenantEntryClient client;
    private readonly TimeProvider time;
    private readonly DispatcherTimer expiry;
    private NativeCredential? credential;
    private TenantEntryPage? page;
    private TenantEntry? selectedTenant;
    private string workspaceName = "";
    private string accountReference = "";
    private string message = "Check the configured application service before signing in.";
    private bool serviceReady;
    private int revision;

    public TenantEntryViewModel(INativeSignIn signIn, TenantEntryClient client, TimeProvider? time = null)
    {
        this.signIn = signIn; this.client = client; this.time = time ?? TimeProvider.System;
        SignInCommand = new AsyncRelayCommand(SignInAsync, () => serviceReady && !IsBusy && !IsSignedIn);
        RefreshCommand = new AsyncRelayCommand(token => LoadAsync(null, token), () => IsSignedIn && !IsBusy);
        NextCommand = new AsyncRelayCommand(token => LoadAsync(page?.NextTenantId, token), () => IsSignedIn && page?.NextTenantId is not null && !IsBusy);
        EnterCommand = new AsyncRelayCommand(EnterAsync, () => IsSignedIn && SelectedTenant is not null && !IsBusy);
        SignOutCommand = new RelayCommand(() => Clear("You signed out of this application. Your identity-provider browser session may still be signed in."));
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        foreach (var command in new[] { SignInCommand, RefreshCommand, NextCommand, EnterCommand })
            command.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(IAsyncRelayCommand.IsRunning)) Changed(); };
        expiry = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        expiry.Tick += (_, _) => { if (credential is not null && !IsSignedIn) Clear(TenantEntryMessages.For(TenantEntryFailure.SignInRequired)); };
        expiry.Start();
    }

    public IAsyncRelayCommand SignInCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IAsyncRelayCommand EnterCommand { get; }
    public IRelayCommand SignOutCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public bool IsBusy => SignInCommand.IsRunning || RefreshCommand.IsRunning || NextCommand.IsRunning || EnterCommand.IsRunning;
    public bool IsIdle => !IsBusy;
    public bool IsSignedIn => credential is not null && credential.ExpiresAt > time.GetUtcNow();
    public bool ShowSignIn => !IsSignedIn;
    public bool HasNextPage => page?.NextTenantId is not null;
    public bool HasWorkspace => workspaceName.Length != 0;
    public bool ShowPicker => IsSignedIn && !HasWorkspace;
    public IReadOnlyList<TenantEntry> Tenants => page?.Items ?? [];
    public TenantEntry? SelectedTenant { get => selectedTenant; set { if (SetProperty(ref selectedTenant, value)) EnterCommand.NotifyCanExecuteChanged(); } }
    public string WorkspaceName { get => workspaceName; private set => SetProperty(ref workspaceName, value); }
    public string AccountReference { get => accountReference; private set => SetProperty(ref accountReference, value); }
    public string Message { get => message; private set => SetProperty(ref message, value); }

    public void SetServiceAvailable(Uri? origin, bool ready)
    {
        Clear("Check the configured application service before signing in.");
        serviceReady = ready && origin == signIn.CoreApiOrigin;
        Message = serviceReady ? "Sign in using your system browser." : "Sign-in requires the configured HTTPS application service.";
        Changed();
    }

    private async Task SignInAsync(CancellationToken cancellation)
    {
        var version = revision;
        Message = "Complete sign-in in your system browser. You can cancel here.";
        try
        {
            var result = await signIn.SignInAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (version != revision) return;
            if (result is null || result.ExpiresAt <= time.GetUtcNow()) { Message = "Sign-in was not completed. Try again or contact support."; return; }
            credential = result; Changed();
            await LoadAsync(null, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (version == revision) Clear("Sign-in canceled."); }
        finally { Changed(); }
    }

    private async Task LoadAsync(Guid? continuation, CancellationToken cancellation)
    {
        WorkspaceName = ""; AccountReference = ""; SelectedTenant = null; page = null; Changed();
        if (!IsSignedIn) { Clear(TenantEntryMessages.For(TenantEntryFailure.SignInRequired)); return; }
        var current = credential!;
        var version = revision;
        Message = "Checking your current account and workspaces…";
        try
        {
            var account = await client.AccountAsync(signIn.CoreApiOrigin, current.AccessToken, cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (version != revision) return;
            if (account.Value is null) { Fail(account.Failure); return; }
            var memberships = await client.TenantsAsync(signIn.CoreApiOrigin, current.AccessToken, continuation, cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (version != revision) return;
            if (!IsSignedIn) { Clear(TenantEntryMessages.For(TenantEntryFailure.SignInRequired)); return; }
            if (memberships.Value is null) { Fail(memberships.Failure); return; }
            AccountReference = account.Value.AccountId.ToString("D"); page = memberships.Value;
            Message = page.Items.Length == 0 ? "No workspaces are available on this page. Contact support if you need access." : "Choose a workspace. Your access is checked when you enter.";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (version == revision) Message = "Access check canceled. Try again."; }
        finally { Changed(); }
    }

    private async Task EnterAsync(CancellationToken cancellation)
    {
        WorkspaceName = ""; Changed();
        if (!IsSignedIn || SelectedTenant is not { } selected) return;
        var current = credential!; var version = revision;
        Message = "Checking current workspace access…";
        try
        {
            var account = await client.AccountAsync(signIn.CoreApiOrigin, current.AccessToken, cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (version != revision) return;
            if (account.Value is null) { Fail(account.Failure); return; }
            var result = await client.WorkspaceAsync(signIn.CoreApiOrigin, current.AccessToken, selected.TenantId, cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (version != revision) return;
            if (!IsSignedIn) { Clear(TenantEntryMessages.For(TenantEntryFailure.SignInRequired)); return; }
            if (result.Value is null) { Fail(result.Failure); return; }
            AccountReference = account.Value.AccountId.ToString("D"); WorkspaceName = result.Value.DisplayName; Message = "Workspace access confirmed.";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (version == revision) Message = "Access check canceled. Try again."; }
        finally { Changed(); }
    }

    private void Fail(TenantEntryFailure failure)
    {
        WorkspaceName = "";
        if (failure is TenantEntryFailure.SignInRequired or TenantEntryFailure.AccountUnavailable) Clear(TenantEntryMessages.For(failure));
        else Message = TenantEntryMessages.For(failure);
        Changed();
    }
    private void Cancel() { SignInCommand.Cancel(); RefreshCommand.Cancel(); NextCommand.Cancel(); EnterCommand.Cancel(); }
    private void Clear(string status)
    {
        revision++; Cancel(); credential = null; page = null; SelectedTenant = null; WorkspaceName = ""; AccountReference = ""; Message = status; Changed();
    }
    private void Changed()
    {
        foreach (var property in new[] { nameof(IsBusy), nameof(IsIdle), nameof(IsSignedIn), nameof(ShowSignIn), nameof(HasNextPage), nameof(HasWorkspace), nameof(ShowPicker), nameof(Tenants) }) OnPropertyChanged(property);
        SignInCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged(); NextCommand.NotifyCanExecuteChanged(); EnterCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
    }
    public void Dispose() { expiry.Stop(); Clear("Application closed."); GC.SuppressFinalize(this); }
}
