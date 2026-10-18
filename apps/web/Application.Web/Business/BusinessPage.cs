using Application.ApplicationEntry;
using Application.Web.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Application.Web.Business;

public abstract class BusinessPage : ComponentBase, IDisposable
{
    [Parameter] public Guid TenantId { get; set; }
    [Inject] protected BusinessApiClient Api { get; set; } = null!;
    [Inject] private TenantEntryClient EntryClient { get; set; } = null!;
    [Inject] private ApplicationEntryClient PublicClient { get; set; } = null!;
    [Inject] private Uri ServiceAddress { get; set; } = null!;
    [Inject] private AuthenticationStateProvider Authentication { get; set; } = null!;
    [Inject] private WebSessionTickets Tickets { get; set; } = null!;
    protected TenantEntry? Workspace { get; private set; }
    protected PublicApplicationIdentity? Identity { get; private set; }
    protected bool Busy => busy || !interactive;
    private bool busy;
    private bool interactive;
    protected string Message { get; set; } = "Checking workspace access…";
    protected string Tone { get; set; } = "neutral";
    protected BusinessCommand? PendingCommand { get; private set; }
    private CancellationTokenSource lifetime = new();
    private bool disposed;

    protected override async Task OnParametersSetAsync()
    {
        await lifetime.CancelAsync();
        lifetime.Dispose();
        lifetime = new();
        Workspace = null;
        Identity = null;
        PendingCommand = null;
        busy = false;
        Reset();
        await RunAsync(async ct =>
        {
            Message = "Checking workspace access…";
            var state = await Authentication.GetAuthenticationStateAsync();
            var token = Tickets.AccessToken(state.User.FindFirst(WebSessionTickets.SessionClaim)?.Value);
            if (token is null) { Show(new BusinessResult<object>(null, BusinessFailure.SignInRequired)); return; }
            if (TenantId == Guid.Empty) { Show(new BusinessResult<object>(null, BusinessFailure.Denied)); return; }
            var entry = await EntryClient.WorkspaceAsync(ServiceAddress, token, TenantId, ct);
            ct.ThrowIfCancellationRequested();
            if (entry.Value is null) { Message = TenantEntryMessages.For(entry.Failure); Tone = "error"; return; }
            var branding = await PublicClient.CheckAsync(ServiceAddress, ct);
            ct.ThrowIfCancellationRequested();
            Identity = branding.Application;
            Workspace = entry.Value;
            Message = "Workspace access confirmed. Choose an action below.";
            Tone = "ready";
            await LoadAsync(ct);
        });
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender) return;
        interactive = true;
        StateHasChanged();
    }

    protected virtual void Reset() { }
    protected virtual Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (busy || disposed) return;
        var current = lifetime;
        busy = true;
        Message = "Working…";
        Tone = "neutral";
        StateHasChanged();
        try { await action(current.Token); }
        catch (OperationCanceledException) when (current.IsCancellationRequested) { }
        finally { if (ReferenceEquals(current, lifetime)) busy = false; }
    }

    protected void Show<T>(BusinessResult<T> result) where T : class
    {
        Message = result.Message;
        Tone = result.Value is not null ? "ready" : result.Unknown ? "warning" : "error";
        if (result.Failure == BusinessFailure.SignInRequired) { Workspace = null; Reset(); }
    }

    protected async Task<T?> SaveAsync<T>(string resource, object payload, Func<T, bool> validate, CancellationToken ct, HttpMethod? method = null) where T : class
    {
        if (PendingCommand is not null && (PendingCommand.Resource != resource || PendingCommand.TenantId != TenantId))
        {
            Message = "Resolve the saved request before starting another operation.";
            Tone = "warning";
            return null;
        }
        var recovering = PendingCommand is not null;
        try { PendingCommand ??= BusinessCommand.Create(TenantId, resource, payload, method); }
        catch (ArgumentException) { Show(new BusinessResult<T>(null, BusinessFailure.InvalidRequest)); return null; }
        var result = await Api.SendAsync(PendingCommand, validate, ct);
        ct.ThrowIfCancellationRequested();
        if (!result.Unknown && (!recovering || result.Value is not null)) PendingCommand = null;
        Show(result);
        if (recovering && result.Value is null && !result.Unknown)
        {
            Message = "The earlier save outcome is still unknown. " + Message + " Check the current record before creating a replacement.";
            Tone = "warning";
        }
        return result.Value;
    }

    public void Dispose()
    {
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
