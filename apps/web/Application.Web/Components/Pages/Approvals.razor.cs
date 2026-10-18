using Application.Web.Business;
using Microsoft.AspNetCore.Components;

namespace Application.Web.Components.Pages;

public partial class Approvals
{
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    private bool VerificationAvailable => Configuration["Authentication:StrongAuthenticationAcr"] is not null;
    [Parameter] public Guid? RequestId { get; set; }
    [Parameter] public Guid? OrderId { get; set; }
    private ApprovalPage? page;
    private ApprovalDetail? detail;
    private ApprovalReceipt? receipt;
    private ApprovalHistory? history;
    private string orderReference = "", assignee = "", requestReference = "", commandReference = "", reason = "", newAssignee = "", action = "approve", pendingAction = "";
    private long orderRevision, requestRevision;
    private long? pendingRevision;
    private bool recovery, submitConfirmed, actionConfirmed;
    private string? after;
    private bool Locked => Busy || PendingCommand is not null;
    private string Root => $"/workspace/{TenantId:D}/order-approvals";
    private string ActionLabel => action switch { "approve" => "Approve submitted subject", "reject" => "Reject approval request", "cancel" => "Cancel my approval request", "reassign" => "Reassign approval request", _ => "Owner cancel approval request" };
    private static bool Id(string text, out Guid id) => Guid.TryParse(text, out id) && id != Guid.Empty;
    private void Invalid(string message) { Message = message; Tone = "error"; }
    protected override void Reset()
    {
        page = null; detail = null; receipt = null; history = null; after = null;
        orderReference = assignee = requestReference = commandReference = reason = newAssignee = pendingAction = ""; action = "approve";
        orderRevision = requestRevision = 0; pendingRevision = null; recovery = submitConfirmed = actionConfirmed = false;
    }
    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        await ReadPageAsync(null, cancellationToken);
        if (RequestId is { } request) await ReadAsync(request, cancellationToken);
        if (OrderId is { } order)
        {
            orderReference = order.ToString("D");
            var result = await Api.GetAsync<ApprovalOrder>(TenantId, $"orders/{order:D}", row => ApprovalContracts.Order(row, order), cancellationToken);
            if (result.Value is { } value) orderRevision = value.Revision;
            Show(result);
        }
    }
    private Task BrowseAsync(string? cursor) => RunAsync(ct => ReadPageAsync(cursor, ct));
    private async Task ReadPageAsync(string? cursor, CancellationToken ct)
    {
        page = null; after = cursor;
        var resource = BusinessValidation.PagePath("order-approvals", cursor) + (recovery ? "&recovery=true" : "");
        var result = await Api.GetAsync<ApprovalPage>(TenantId, resource, row => BusinessValidation.Page(row.Items, row.NextCursor, ApprovalContracts.Summary, item => item.RequestId) &&
            (row.NextCursor is null || row.NextCursor != cursor), ct);
        page = result.Value; Show(result);
    }
    private Task FindAsync() => RunAsync(async ct =>
    {
        detail = null; history = null; receipt = null; requestRevision = 0;
        if (!Id(requestReference, out var id)) { Invalid("Enter an approval request reference."); return; }
        await ReadAsync(id, ct);
    });
    private async Task ReadAsync(Guid id, CancellationToken ct)
    {
        requestReference = commandReference = id.ToString("D");
        var result = await Api.GetAsync<ApprovalDetail>(TenantId, $"order-approvals/{id:D}", row => ApprovalContracts.Detail(row, id), ct);
        detail = result.Value;
        if (detail is { } value)
        {
            orderReference = value.OrderId.ToString("D"); orderRevision = value.Subject.Order.Revision; requestRevision = value.Revision;
            actionConfirmed = submitConfirmed = false;
        }
        Show(result);
    }
    private Task SubmitAsync() => RunAsync(async ct =>
    {
        if (!Id(orderReference, out var order) || !Id(assignee, out var assigned) || orderRevision < 1 || pendingRevision is < 1 || PendingCommand is null && !submitConfirmed)
        { Invalid("Enter the Order revision and independent assigned account, and confirm submission."); return; }
        pendingAction = "submit";
        var value = await SaveAsync<ApprovalReceipt>($"orders/{order:D}/approvals", new { expectedOrderRevision = orderRevision, assignedAccountId = assigned, expectedPendingRequestRevision = pendingRevision },
            row => ApprovalContracts.Receipt(row, order: order) && row.OrderRevision == orderRevision && row.State == ApprovalState.Pending, ct);
        if (value is not null) ApplyReceipt(value);
    });
    private void ClearConfirmation() => actionConfirmed = false;
    private Task TransitionAsync() => RunAsync(async ct =>
    {
        if (!Id(commandReference, out var request) || requestRevision < 1 || PendingCommand is null && !actionConfirmed ||
            !ApprovalContracts.ScalarText(reason, 512, action != "approve"))
        { Invalid("Enter the exact request revision, a required reason of up to 512 Unicode characters and confirm the action."); return; }
        Guid assigned = default;
        if (action == "reassign" && !Id(newAssignee, out assigned)) { Invalid("Enter a valid new assigned account reference."); return; }
        object payload = action == "reassign" ? new { expectedRequestRevision = requestRevision, assignedAccountId = assigned, reason } :
            (object)new { expectedRequestRevision = requestRevision, reason = string.IsNullOrWhiteSpace(reason) ? null : reason };
        pendingAction = action;
        var expectedState = action switch { "approve" => ApprovalState.Approved, "reject" => ApprovalState.Rejected, "reassign" => ApprovalState.Pending, _ => ApprovalState.Canceled };
        var knownOrder = detail?.RequestId == request ? detail.OrderId : (Guid?)null;
        var value = await SaveAsync<ApprovalReceipt>($"order-approvals/{request:D}/{action}", payload,
            row => ApprovalContracts.Receipt(row, request, knownOrder) && row.RequestRevision == requestRevision + 1 && row.State == expectedState, ct);
        if (value is not null) ApplyReceipt(value);
    });
    private void ApplyReceipt(ApprovalReceipt value)
    {
        receipt = value; requestReference = commandReference = value.RequestId.ToString("D"); orderReference = value.OrderId.ToString("D");
        orderRevision = value.OrderRevision; requestRevision = value.RequestRevision; actionConfirmed = submitConfirmed = false;
        detail = null; history = null; page = null;
    }
    private Task HistoryAsync(long? before) => RunAsync(async ct =>
    {
        history = null;
        if (!Id(requestReference, out var id)) { Invalid("Enter an approval request reference for its history."); return; }
        var result = await Api.GetAsync<ApprovalHistory>(TenantId, $"order-approvals/{id:D}/history?limit=10" + (before.HasValue ? $"&beforeRevision={before}" : ""),
            row => row.CurrentRevision > 0 && row.Items is { Length: <= 10 } && row.Items.All(item => item is not null && ApprovalContracts.Detail(item, id) && item.Revision <= row.CurrentRevision && (!before.HasValue || item.Revision < before)) &&
                row.Items.Select(item => item.Revision).Distinct().Count() == row.Items.Length &&
                (!row.NextRevision.HasValue || row.Items.Length > 0 && row.NextRevision == row.Items[^1].Revision && row.NextRevision > 0), ct);
        history = result.Value; Show(result);
    });
    private Task RetryAsync() => pendingAction == "submit" ? SubmitAsync() : TransitionAsync();
}
