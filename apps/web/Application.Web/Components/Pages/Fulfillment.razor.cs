using Application.Web.Business;
using Microsoft.AspNetCore.Components;

namespace Application.Web.Components.Pages;

public partial class Fulfillment
{
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Parameter] public Guid? OrderId { get; set; }
    private OperationsPage<FulfillmentListRecord>? inbox;
    private OperationsHistory<FulfillmentEventRecord>? history;
    private FulfillmentRecord? work;
    private FulfillmentReceipt? receipt;
    private string orderReference = "", recipient = "", disposition = "Stopped", dispositionReason = "", resolveReason = "";
    private string targetEvent = "", correctionReason = "", correctionConfirmation = "", replacementKind = "None", replacementRecipient = "", replacementDisposition = "Stopped";
    private decimal completionQuantity, pickupQuantity;
    private decimal? affectedQuantity, replacementQuantity;
    private long expectedRevision, orderRevision = 1, pendingRevision;
    private Guid pendingOrder;
    private bool startReviewed, completionReviewed, pickupReviewed, dispositionReviewed, resolutionReviewed;
    private bool Locked => Busy || PendingCommand is not null;
    private string Root => $"/workspace/{TenantId:D}/fulfillment";
    private static string Resource(Guid order) => $"orders/{order:D}/fulfillment";
    private bool VerificationAvailable => Configuration["Authentication:StrongAuthenticationAcr"] is not null;
    protected override void Reset()
    {
        inbox = null; history = null; work = null; receipt = null;
        orderReference = recipient = dispositionReason = resolveReason = targetEvent = correctionReason = correctionConfirmation = replacementRecipient = "";
        disposition = replacementDisposition = "Stopped"; replacementKind = "None";
        completionQuantity = pickupQuantity = 0; affectedQuantity = replacementQuantity = null;
        expectedRevision = pendingRevision = 0; orderRevision = 1; pendingOrder = Guid.Empty;
        startReviewed = completionReviewed = pickupReviewed = dispositionReviewed = resolutionReviewed = false;
    }
    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (OrderId is { } id) { orderReference = id.ToString("D"); await ReadAsync(id, cancellationToken); }
    }
    private bool Invalid(string message) { Message = message; Tone = "error"; return false; }
    private bool Target(out Guid id) => OperationsContracts.Id(orderReference, out id) || Invalid("Enter the committed Order reference.");
    private Task InboxAsync(string? cursor) => RunAsync(async ct =>
    {
        inbox = null;
        var result = await Api.GetAsync<OperationsPage<FulfillmentListRecord>>(TenantId, BusinessValidation.PagePath("fulfillment", cursor),
            page => OperationsContracts.Page(page, FulfillmentContracts.List, x => x.OrderId, cursor), ct);
        inbox = result.Value; Show(result);
    });
    private Task LoadWorkAsync() => RunAsync(async ct =>
    {
        work = null; history = null;
        if (Target(out var id)) await ReadAsync(id, ct);
    });
    private async Task ReadAsync(Guid id, CancellationToken ct)
    {
        var result = await Api.GetAsync<FulfillmentRecord>(TenantId, Resource(id), value => FulfillmentContracts.Record(value, id), ct);
        work = result.Value; Show(result);
        if (work is { } current) expectedRevision = current.Revision;
    }
    private Task HistoryAsync(long? before) => RunAsync(async ct =>
    {
        history = null;
        if (!Target(out var id)) return;
        var result = await Api.GetAsync<OperationsHistory<FulfillmentEventRecord>>(TenantId, OperationsContracts.HistoryPath(Resource(id), before),
            value => OperationsContracts.History(value, FulfillmentContracts.Event, x => x.Revision, before), ct);
        history = result.Value; Show(result);
    });
    private Task StartAsync() => RunAsync(async ct =>
    {
        if (!Target(out var id)) return;
        if (!startReviewed || orderRevision < 1 || !OperationsContracts.Revision(expectedRevision)) { Invalid("Review the committed Order revision and current fulfillment revision before starting."); return; }
        await SaveWorkAsync(id, "start", new { expectedRevision, expectedOrderRevision = orderRevision }, ct);
    });
    private Task CompleteAsync() => RunAsync(async ct =>
    {
        if (!Target(out var id)) return;
        if (!completionReviewed || !OperationsContracts.Quantity(completionQuantity)) { Invalid("Review the positive exact completed quantity in the retained unit."); return; }
        await SaveWorkAsync(id, "complete", new { expectedRevision, quantity = completionQuantity }, ct);
    });
    private Task PickupAsync() => RunAsync(async ct =>
    {
        if (!Target(out var id)) return;
        if (!pickupReviewed || !OperationsContracts.Quantity(pickupQuantity) || !OperationsContracts.Text(recipient, 128)) { Invalid("Review the exact pickup quantity and recipient label (up to 128 characters)."); return; }
        await SaveWorkAsync(id, "handoffs", new { expectedRevision, quantity = pickupQuantity, recipient }, ct);
    });
    private Task DispositionAsync() => RunAsync(async ct =>
    {
        if (!Target(out var id)) return;
        if (!dispositionReviewed || !FulfillmentContracts.Disposition(disposition) || !OperationsContracts.Text(dispositionReason, 512) ||
            affectedQuantity.HasValue && !OperationsContracts.Quantity(affectedQuantity.Value)) { Invalid("Review the disposition, reason and optional exact affected quantity."); return; }
        await SaveWorkAsync(id, "dispositions", new { expectedRevision, disposition, reason = dispositionReason, quantity = affectedQuantity }, ct);
    });
    private Task ResolveAsync() => RunAsync(async ct =>
    {
        if (!Target(out var id)) return;
        if (!resolutionReviewed || !OperationsContracts.Text(resolveReason, 512)) { Invalid("Review the active disposition and enter a resolution reason."); return; }
        await SaveWorkAsync(id, "resolve", new { expectedRevision, reason = resolveReason }, ct);
    });
    private Task CorrectAsync() => RunAsync(async ct =>
    {
        if (!Target(out var id)) return;
        if (!OperationsContracts.Id(targetEvent, out var target) || !OperationsContracts.Text(correctionReason, 512) || correctionConfirmation != "Correct fulfillment")
        { Invalid("Enter the target event, reason and exact correction confirmation."); return; }
        FulfillmentReplacement? replacement = null;
        if (replacementKind != "None")
        {
            replacement = new(replacementKind, replacementQuantity, replacementKind == "Handoff" ? replacementRecipient : null,
                replacementKind == "Disposition" ? replacementDisposition : null);
            if (!FulfillmentContracts.Replacement(replacement)) { Invalid("Review the replacement kind and its exact facts."); return; }
        }
        await SaveWorkAsync(id, "corrections", new { expectedRevision, targetEventId = target, reason = correctionReason, replacement }, ct);
    });
    private async Task SaveWorkAsync(Guid id, string operation, object payload, CancellationToken ct)
    {
        if (PendingCommand is not null) { Invalid("Resolve the saved request before another operation."); return; }
        if (!OperationsContracts.Revision(expectedRevision) || operation != "start" && expectedRevision == 0) { Invalid("Enter or load the current fulfillment revision."); return; }
        pendingOrder = id; pendingRevision = expectedRevision + 1;
        var result = await SaveAsync<FulfillmentReceipt>(Resource(id) + "/" + operation, payload,
            value => FulfillmentContracts.Receipt(value, pendingOrder, pendingRevision), ct);
        if (result is not null) Saved(result);
    }
    private void Saved(FulfillmentReceipt result)
    {
        receipt = result; expectedRevision = result.Revision; orderReference = result.OrderId.ToString("D");
        work = null; history = null; inbox = null;
        startReviewed = completionReviewed = pickupReviewed = dispositionReviewed = resolutionReviewed = false;
        correctionConfirmation = "";
    }
    private Task RetryAsync() => RunAsync(async ct =>
    {
        if (PendingCommand is not { } command) return;
        var result = await SaveAsync<FulfillmentReceipt>(command.Resource, new { }, value => FulfillmentContracts.Receipt(value, pendingOrder, pendingRevision), ct);
        if (result is not null) Saved(result);
    });
}
