using System.Globalization;
using Application.Web.Business;
using Microsoft.AspNetCore.Components;

namespace Application.Web.Components.Pages;

public partial class Purchasing
{
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Parameter] public Guid? SupplierId { get; set; }
    [Parameter] public Guid? PurchaseId { get; set; }
    private OperationsPage<SupplierRecord>? suppliers;
    private OperationsPage<PurchaseRecord>? requests;
    private OperationsHistory<SupplierEventRecord>? supplierHistory;
    private OperationsHistory<PurchaseEventRecord>? purchaseHistory;
    private SupplierRecord? supplier;
    private PurchaseRecord? purchase;
    private SupplierBalanceRecord? balance;
    private PurchasingReceipt? receipt;
    private string receiptKind = "";
    private Guid newSupplier = Guid.NewGuid(), newPurchase = Guid.NewGuid(), pendingId;
    private string pendingKind = "";
    private long pendingRevision, supplierRevision = 1, purchaseRevision = 1, linkedOrderRevision = 1;
    private string supplierReference = "", supplierName = "", supplierContact = "", purchaseReference = "", requestSupplier = "", requestUnit = "", description = "", linkedOrder = "";
    private int linkedLine = 1;
    private decimal requestQuantity, receiveQuantity, receiveCost, paymentAmount, refundAmount;
    private string payableReceipt = "", refundPayment = "", paymentMethod = "Cash", refundMethod = "Cash", paymentReference = "", refundReference = "", cancelReason = "", cancelConfirmation = "";
    private string retireConfirmation = "";
    private string correctionTarget = "", correctionReason = "", correctionConfirmation = "", replacementKind = "None", replacementReceipt = "", replacementPayment = "", replacementMethod = "Cash", replacementReference = "";
    private decimal? replacementQuantity, replacementAmount;
    private bool supplierReviewed, requestReviewed, receiptReviewed, paymentReviewed, refundReviewed;
    private bool Locked => Busy || PendingCommand is not null;
    private bool VerificationAvailable => Configuration["Authentication:StrongAuthenticationAcr"] is not null;
    private string Root => $"/workspace/{TenantId:D}/purchasing";
    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string SupplierPath(Guid id) => $"purchasing/suppliers/{id:D}";
    private static string PurchasePath(Guid id) => $"purchasing/requests/{id:D}";
    protected override void Reset()
    {
        suppliers = null; requests = null; supplierHistory = null; purchaseHistory = null; supplier = null; purchase = null; balance = null; receipt = null;
        newSupplier = Guid.NewGuid(); newPurchase = Guid.NewGuid(); pendingId = Guid.Empty; pendingRevision = 0;
        supplierRevision = purchaseRevision = linkedOrderRevision = 1; linkedLine = 1;
        supplierReference = supplierName = supplierContact = purchaseReference = requestSupplier = requestUnit = description = linkedOrder =
            payableReceipt = refundPayment = paymentReference = refundReference = cancelReason = cancelConfirmation = correctionTarget = correctionReason =
            correctionConfirmation = retireConfirmation = replacementReceipt = replacementPayment = replacementReference = pendingKind = receiptKind = "";
        paymentMethod = refundMethod = replacementMethod = "Cash"; replacementKind = "None";
        requestQuantity = receiveQuantity = receiveCost = paymentAmount = refundAmount = 0; replacementQuantity = replacementAmount = null;
        supplierReviewed = requestReviewed = receiptReviewed = paymentReviewed = refundReviewed = false;
    }
    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (SupplierId is { } supplierId) { supplierReference = supplierId.ToString("D"); await ReadSupplierAsync(supplierId, cancellationToken); }
        else if (PurchaseId is { } purchaseId) { purchaseReference = purchaseId.ToString("D"); await ReadPurchaseAsync(purchaseId, cancellationToken); }
    }
    private bool Invalid(string message) { Message = message; Tone = "error"; return false; }
    private bool SupplierTarget(out Guid id) => OperationsContracts.Id(supplierReference, out id) || Invalid("Enter the saved supplier reference.");
    private bool PurchaseTarget(out Guid id) => OperationsContracts.Id(purchaseReference, out id) || Invalid("Enter the saved purchasing request reference.");
    private Task SuppliersAsync(string? cursor) => RunAsync(async ct =>
    {
        suppliers = null;
        var result = await Api.GetAsync<OperationsPage<SupplierRecord>>(TenantId, BusinessValidation.PagePath("purchasing/suppliers", cursor),
            value => OperationsContracts.Page(value, x => PurchasingContracts.Supplier(x), x => x.SupplierId, cursor), ct);
        suppliers = result.Value; Show(result);
    });
    private Task RequestsAsync(string? cursor) => RunAsync(async ct =>
    {
        requests = null;
        var result = await Api.GetAsync<OperationsPage<PurchaseRecord>>(TenantId, "purchasing/requests?limit=3" + (cursor is null ? "" : "&after=" + cursor),
            value => value.Items is { Length: <= 3 } && OperationsContracts.Page(value, x => PurchasingContracts.Purchase(x), x => x.PurchaseId, cursor), ct);
        requests = result.Value; Show(result);
    });
    private Task LoadSupplierAsync() => RunAsync(async ct => { supplier = null; supplierHistory = null; balance = null; if (SupplierTarget(out var id)) await ReadSupplierAsync(id, ct); });
    private async Task ReadSupplierAsync(Guid id, CancellationToken ct)
    {
        var result = await Api.GetAsync<SupplierRecord>(TenantId, SupplierPath(id), value => PurchasingContracts.Supplier(value, id), ct);
        supplier = result.Value; Show(result);
        if (supplier is { } current) { supplierRevision = current.Revision; supplierName = current.Name; supplierContact = current.Contact ?? ""; requestSupplier = current.SupplierId.ToString("D"); }
    }
    private Task LoadPurchaseAsync() => RunAsync(async ct => { purchase = null; purchaseHistory = null; if (PurchaseTarget(out var id)) await ReadPurchaseAsync(id, ct); });
    private async Task ReadPurchaseAsync(Guid id, CancellationToken ct)
    {
        var result = await Api.GetAsync<PurchaseRecord>(TenantId, PurchasePath(id), value => PurchasingContracts.Purchase(value, id), ct);
        purchase = result.Value; Show(result); if (purchase is { } current) purchaseRevision = current.Revision;
    }
    private Task SupplierHistoryAsync(long? before) => RunAsync(async ct =>
    {
        supplierHistory = null; if (!SupplierTarget(out var id)) return;
        var result = await Api.GetAsync<OperationsHistory<SupplierEventRecord>>(TenantId, OperationsContracts.HistoryPath(SupplierPath(id), before),
            value => OperationsContracts.History(value, x => PurchasingContracts.SupplierEvent(x, id), x => x.Revision, before), ct);
        supplierHistory = result.Value; Show(result);
    });
    private Task PurchaseHistoryAsync(long? before) => RunAsync(async ct =>
    {
        purchaseHistory = null; if (!PurchaseTarget(out var id)) return;
        var result = await Api.GetAsync<OperationsHistory<PurchaseEventRecord>>(TenantId, OperationsContracts.HistoryPath(PurchasePath(id), before),
            value => OperationsContracts.History(value, PurchasingContracts.Event, x => x.Revision, before), ct);
        purchaseHistory = result.Value; Show(result);
    });
    private Task BalanceAsync() => RunAsync(async ct =>
    {
        balance = null; if (!SupplierTarget(out var id)) return;
        var result = await Api.GetAsync<SupplierBalanceRecord>(TenantId, SupplierPath(id) + "/balance", value => PurchasingContracts.Balance(value, id), ct);
        balance = result.Value; Show(result);
    });
    private Task SupplierSaveAsync(bool create) => RunAsync(async ct =>
    {
        var id = newSupplier;
        if (!create && !SupplierTarget(out id)) return;
        if (!supplierReviewed || !OperationsContracts.Text(supplierName, 128) || !OperationsContracts.Text(supplierContact, 512, false)) { Invalid("Review the supplier name and optional contact before saving."); return; }
        await SaveOperationAsync("supplier", id, create ? 0 : supplierRevision, SupplierPath(id) + (create ? "" : "/revise"),
            new { expectedRevision = create ? 0 : supplierRevision, name = supplierName, contact = string.IsNullOrWhiteSpace(supplierContact) ? null : supplierContact }, ct);
    });
    private Task RetireAsync() => RunAsync(async ct =>
    {
        if (!SupplierTarget(out var id)) return;
        if (retireConfirmation != "Retire supplier") { Invalid("Type Retire supplier to confirm retirement of this exact revision."); return; }
        await SaveOperationAsync("supplier", id, supplierRevision, SupplierPath(id) + "/retire", new { expectedRevision = supplierRevision }, ct);
    });
    private Task RequestAsync() => RunAsync(async ct =>
    {
        if (!requestReviewed || !OperationsContracts.Id(requestSupplier, out var supplierId) || !OperationsContracts.Id(requestUnit, out var unitId) ||
            !OperationsContracts.Text(description, 512) || !OperationsContracts.Quantity(requestQuantity)) { Invalid("Review the supplier, active unit, service description and exact requested quantity."); return; }
        PurchaseOrderLink? order = null;
        if (!string.IsNullOrWhiteSpace(linkedOrder))
        {
            if (!OperationsContracts.Id(linkedOrder, out var orderId) || linkedLine < 1 || linkedOrderRevision < 1) { Invalid("Enter the committed Order, line and revision together, or leave the Order reference blank."); return; }
            order = new(orderId, linkedLine, linkedOrderRevision);
        }
        await SaveOperationAsync("purchase", newPurchase, 0, PurchasePath(newPurchase), new { expectedRevision = 0, supplierId, unitId, description, quantity = requestQuantity, order }, ct);
    });
    private Task ReceiveAsync() => RunAsync(async ct =>
    {
        if (!PurchaseTarget(out var id)) return;
        if (!receiptReviewed || !OperationsContracts.Quantity(receiveQuantity) || !OperationsContracts.Money(receiveCost, true)) { Invalid("Review the actual receipt quantity and positive exact two-decimal NPR total cost."); return; }
        await SaveOperationAsync("purchase", id, purchaseRevision, PurchasePath(id) + "/receipts", new { expectedRevision = purchaseRevision, quantity = receiveQuantity, cost = receiveCost }, ct);
    });
    private Task PayAsync(bool refund) => RunAsync(async ct =>
    {
        if (!PurchaseTarget(out var id)) return;
        var amount = refund ? refundAmount : paymentAmount; var method = refund ? refundMethod : paymentMethod; var reference = refund ? refundReference : paymentReference;
        if (!(refund ? refundReviewed : paymentReviewed) || !OperationsContracts.Id(refund ? refundPayment : payableReceipt, out var target) ||
            !OperationsContracts.Money(amount, true) || !PurchasingContracts.Method(method) || !OperationsContracts.Text(reference, 128, false)) { Invalid("Review the exact receipt/payment reference, actual amount, method and optional reference."); return; }
        object body = refund ? new { expectedRevision = purchaseRevision, paymentId = target, amount, method, reference = string.IsNullOrWhiteSpace(reference) ? null : reference } :
            new { expectedRevision = purchaseRevision, receiptId = target, amount, method, reference = string.IsNullOrWhiteSpace(reference) ? null : reference };
        await SaveOperationAsync("purchase", id, purchaseRevision, PurchasePath(id) + (refund ? "/refunds" : "/payments"), body, ct);
    });
    private Task CancelAsync() => RunAsync(async ct =>
    {
        if (!PurchaseTarget(out var id)) return;
        if (cancelConfirmation != "Cancel remainder" || !OperationsContracts.Text(cancelReason, 512)) { Invalid("Enter a reason and type Cancel remainder to confirm."); return; }
        await SaveOperationAsync("purchase", id, purchaseRevision, PurchasePath(id) + "/cancel", new { expectedRevision = purchaseRevision, reason = cancelReason }, ct);
    });
    private Task CorrectAsync() => RunAsync(async ct =>
    {
        if (!PurchaseTarget(out var id)) return;
        if (!OperationsContracts.Id(correctionTarget, out var target) || !OperationsContracts.Text(correctionReason, 512) || correctionConfirmation != "Correct supplier entry") { Invalid("Enter the original event, reason and exact financial correction confirmation."); return; }
        PurchasingFactRecord? replacement = null;
        if (replacementKind != "None")
        {
            Guid? receiptId = null, paymentId = null;
            if (replacementKind == "Payment") { if (!OperationsContracts.Id(replacementReceipt, out var value)) { Invalid("Enter the replacement payable receipt reference."); return; } receiptId = value; }
            if (replacementKind == "Refund") { if (!OperationsContracts.Id(replacementPayment, out var value)) { Invalid("Enter the replacement supplier payment reference."); return; } paymentId = value; }
            replacement = new(replacementKind, replacementKind == "Receipt" ? replacementQuantity : null, replacementAmount, receiptId, paymentId,
                replacementKind == "Receipt" ? null : replacementMethod, replacementKind == "Receipt" || string.IsNullOrWhiteSpace(replacementReference) ? null : replacementReference);
            if (!PurchasingContracts.Fact(replacement) || replacementAmount is null || replacementKind == "Receipt" && replacementQuantity is null) { Invalid("Review the exact replacement receipt, payment or refund facts."); return; }
        }
        await SaveOperationAsync("purchase", id, purchaseRevision, PurchasePath(id) + "/corrections", new { expectedRevision = purchaseRevision, targetEventId = target, reason = correctionReason, replacement }, ct);
    });
    private async Task SaveOperationAsync(string kind, Guid id, long revision, string resource, object payload, CancellationToken ct)
    {
        if (PendingCommand is not null) { Invalid("Resolve the saved request before another operation."); return; }
        if (!OperationsContracts.Revision(revision) || revision == 0 && id != newSupplier && id != newPurchase) { Invalid("Enter or load the current saved revision."); return; }
        pendingKind = kind; pendingId = id; pendingRevision = revision + 1;
        var result = await SaveAsync<PurchasingReceipt>(resource, payload, value => PurchasingContracts.Receipt(value, pendingId, pendingRevision), ct);
        if (result is not null) Saved(result);
    }
    private void Saved(PurchasingReceipt result)
    {
        receipt = result; receiptKind = pendingKind;
        suppliers = null; requests = null; supplierHistory = null; purchaseHistory = null; supplier = null; purchase = null; balance = null;
        if (pendingKind == "supplier") { supplierReference = result.Id.ToString("D"); supplierRevision = result.Revision; requestSupplier = supplierReference; if (result.Id == newSupplier) newSupplier = Guid.NewGuid(); }
        else { purchaseReference = result.Id.ToString("D"); purchaseRevision = result.Revision; if (result.Id == newPurchase) newPurchase = Guid.NewGuid(); }
        supplierReviewed = requestReviewed = receiptReviewed = paymentReviewed = refundReviewed = false; correctionConfirmation = cancelConfirmation = retireConfirmation = "";
    }
    private Task RetryAsync() => RunAsync(async ct =>
    {
        if (PendingCommand is not { } command) return;
        var result = await SaveAsync<PurchasingReceipt>(command.Resource, new { }, value => PurchasingContracts.Receipt(value, pendingId, pendingRevision), ct);
        if (result is not null) Saved(result);
    });
}
