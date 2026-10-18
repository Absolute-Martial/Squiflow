using System.Globalization;
using Application.Web.Business;
using Microsoft.AspNetCore.Components;

namespace Application.Web.Components.Pages;

public partial class Billing
{
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Parameter] public Guid? OrderId { get; set; }
    [Parameter] public Guid? PaymentId { get; set; }
    [Parameter] public Guid? InvoiceId { get; set; }
    private FinancialPage<ReceivableRecord>? receivables;
    private FinancialPage<PaymentRecord>? payments;
    private FinancialPage<PaymentEventRecord>? history;
    private PaymentRecord? payment;
    private InvoiceRecord? invoice;
    private OrderRecord? sourceOrder;
    private ReceivableRecord? receivable;
    private string directoryKind = "Organization", directoryDebtor = "";
    private (string Kind, Guid Id)? directoryFilter;
    private DebtorBalanceRecord? balance;
    private PaymentCommandReceipt? lastPaymentReceipt;
    private InvoiceCommandReceipt? lastInvoiceReceipt;
    private Guid newPaymentId = Guid.NewGuid();
    private string recordKind = "Organization", recordDebtor = "", recordChannel = "Cash", recordEvidence = "", recordBankReference = "";
    private decimal recordAmount;
    private bool recordReviewed, confirmReviewed, reviewReviewed, allocationReviewed, invoiceReviewed;
    private string operationPayment = "", confirmEvidence = "", reviewEvidence = "", allocationInvoice = "", correctionReason = "", releaseAllocation = "", correctionConfirmation = "";
    private long operationRevision = 1;
    private decimal allocationAmount;
    private string invoiceOrder = "", invoiceKind = "Organization", invoiceIndividual = "", invoiceReference = "";
    private long invoiceOrderRevision = 1;
    private string balanceKind = "Organization", balanceDebtor = "";
    private string pendingOperation = "";
    private Guid pendingPaymentId;
    private long pendingRevision;
    private string Root => $"/workspace/{TenantId:D}/billing";
    private bool Locked => Busy || PendingCommand is not null;
    private bool VerificationAvailable => Configuration["Authentication:StrongAuthenticationAcr"] is not null;
    private static string Money(decimal amount) => amount.ToString("0.0000", CultureInfo.InvariantCulture);

    protected override void Reset()
    {
        receivables = null; payments = null; history = null; payment = null; invoice = null; balance = null;
        lastPaymentReceipt = null; lastInvoiceReceipt = null; newPaymentId = Guid.NewGuid();
        sourceOrder = null; receivable = null; directoryFilter = null; directoryKind = "Organization"; directoryDebtor = "";
        recordKind = balanceKind = invoiceKind = "Organization"; recordChannel = "Cash";
        recordDebtor = recordEvidence = recordBankReference = operationPayment = confirmEvidence = reviewEvidence = allocationInvoice =
            correctionReason = releaseAllocation = correctionConfirmation = invoiceOrder = invoiceIndividual = invoiceReference = balanceDebtor = pendingOperation = "";
        recordAmount = allocationAmount = 0; operationRevision = invoiceOrderRevision = 1;
        recordReviewed = confirmReviewed = reviewReviewed = allocationReviewed = invoiceReviewed = false;
        pendingPaymentId = Guid.Empty; pendingRevision = 0;
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (PaymentId is { } id) { operationPayment = id.ToString("D"); await ReadPaymentAsync(id, cancellationToken); }
        else if (InvoiceId is { } invoiceId) { invoiceReference = invoiceId.ToString("D"); await ReadInvoiceAsync(invoiceId, false, cancellationToken); }
        else if (OrderId is { } orderId) { invoiceOrder = orderId.ToString("D"); await ReadSourceOrderAsync(orderId, cancellationToken); }
    }

    private static string PagePath(string resource, string? cursor) => resource + (cursor is null ? "" : "?cursor=" + cursor);
    private bool Invalid(string message)
    {
        Message = message; Tone = "error"; return false;
    }

    private Task ReceivablesAsync(string? cursor) => RunAsync(async ct =>
    {
        receivables = null;
        var result = await Api.GetAsync<FinancialPage<ReceivableRecord>>(TenantId, DirectoryPath("receivables", cursor),
            value => FinancialContracts.Page(value, FinancialContracts.Receivable, row => row.InvoiceId, cursor), ct);
        receivables = result.Value; Show(result);
    });

    private Task PaymentsAsync(string? cursor) => RunAsync(async ct =>
    {
        payments = null;
        var result = await Api.GetAsync<FinancialPage<PaymentRecord>>(TenantId, DirectoryPath("customer-payments", cursor),
            value => FinancialContracts.Page(value, row => FinancialContracts.Payment(row), row => row.PaymentId, cursor), ct);
        payments = result.Value; Show(result);
    });

    private Task LoadPaymentAsync() => RunAsync(async ct =>
    {
        payment = null; history = null;
        if (!FinancialContracts.Id(operationPayment, out var id)) { Invalid("Enter the payment's saved reference."); return; }
        await ReadPaymentAsync(id, ct);
    });

    private async Task ReadPaymentAsync(Guid id, CancellationToken ct)
    {
        var result = await Api.GetAsync<PaymentRecord>(TenantId, $"customer-payments/{id:D}", value => FinancialContracts.Payment(value, id), ct);
        payment = result.Value; Show(result);
        if (payment is { } current) operationRevision = current.Revision;
    }

    private Task HistoryAsync(string? cursor) => RunAsync(async ct =>
    {
        history = null;
        if (!FinancialContracts.Id(operationPayment, out var id)) { Invalid("Enter the payment's saved reference."); return; }
        var result = await Api.GetAsync<FinancialPage<PaymentEventRecord>>(TenantId, PagePath($"customer-payments/{id:D}/history", cursor),
            value => FinancialContracts.Page(value, row => FinancialContracts.Event(row, id), row => row.EventId, cursor) &&
                value.Items.Zip(value.Items.Skip(1)).All(pair => pair.First.Revision < pair.Second.Revision), ct);
        history = result.Value; Show(result);
    });

    private Task RecordAsync() => RunAsync(async ct =>
    {
        if (!recordReviewed || !FinancialContracts.Kind(recordKind) || !FinancialContracts.Id(recordDebtor, out var debtor) ||
            !FinancialContracts.Money(recordAmount) || recordAmount <= 0 || recordChannel is not ("Cash" or "Bank") ||
            !BusinessValidation.Text(recordEvidence, 1000) || recordChannel == "Bank" && !BusinessValidation.Text(recordBankReference, 200))
        { Invalid("Review the exact debtor, positive NPR amount and required receipt evidence before recording."); return; }
        await SavePaymentAsync(newPaymentId, 1, $"customer-payments/{newPaymentId:D}", new
        {
            debtorKind = recordKind,
            debtorId = debtor,
            currencyCode = "NPR",
            amount = recordAmount,
            channel = recordChannel,
            evidence = recordEvidence,
            externalReference = recordChannel == "Bank" ? recordBankReference : null,
        }, ct, HttpMethod.Put);
    });

    private bool ExistingPayment(out Guid id)
    {
        id = Guid.Empty;
        return FinancialContracts.Id(operationPayment, out id) && operationRevision is > 0 and < long.MaxValue ||
            Invalid("Load the current payment, or enter its exact reference and current saved revision.");
    }

    private Task EvidenceAsync(string operation, string evidence) => RunAsync(async ct =>
    {
        if (!ExistingPayment(out var id)) return;
        if (!(operation == "confirm" ? confirmReviewed : reviewReviewed) || !BusinessValidation.Text(evidence, 1000))
        { Invalid("Review the payment and enter the received-funds or decision evidence."); return; }
        await SavePaymentAsync(id, operationRevision + 1, $"customer-payments/{id:D}/{operation}", new { expectedRevision = operationRevision, evidence }, ct);
    });

    private Task AllocateAsync() => RunAsync(async ct =>
    {
        if (!ExistingPayment(out var id)) return;
        if (!allocationReviewed || !FinancialContracts.Id(allocationInvoice, out var receivable) || !FinancialContracts.Money(allocationAmount) || allocationAmount <= 0)
        { Invalid("Review the invoice reference and positive exact allocation amount."); return; }
        await SavePaymentAsync(id, operationRevision + 1, $"customer-payments/{id:D}/allocations",
            new { expectedRevision = operationRevision, receivableId = receivable, amount = allocationAmount }, ct);
    });

    private Task ReverseAsync() => CorrectAsync(false);
    private Task ReleaseAsync() => CorrectAsync(true);
    private Task CorrectAsync(bool release) => RunAsync(async ct =>
    {
        if (!ExistingPayment(out var id)) return;
        if (!BusinessValidation.Text(correctionReason, 1000) || correctionConfirmation != (release ? "Release allocation" : "Reverse payment"))
        { Invalid("Enter a correction reason and type the exact correction confirmation."); return; }
        var resource = $"customer-payments/{id:D}/reverse";
        if (release)
        {
            if (!FinancialContracts.Id(releaseAllocation, out var allocation)) { Invalid("Enter the original allocation event reference."); return; }
            resource = $"customer-payments/{id:D}/allocations/{allocation:D}/release";
        }
        await SavePaymentAsync(id, operationRevision + 1, resource, new { expectedRevision = operationRevision, reason = correctionReason }, ct);
    });

    private async Task SavePaymentAsync(Guid id, long revision, string resource, object payload, CancellationToken ct, HttpMethod? method = null)
    {
        if (PendingCommand is not null) { Invalid("Resolve the saved request before starting another payment operation."); return; }
        pendingOperation = "payment"; pendingPaymentId = id; pendingRevision = revision;
        var result = await SaveAsync<PaymentCommandReceipt>(resource, payload, value => FinancialContracts.Receipt(value, id) && value.Revision == revision, ct, method);
        if (result is not null) PaymentSaved(result);
    }

    private void PaymentSaved(PaymentCommandReceipt receipt)
    {
        lastPaymentReceipt = receipt; operationPayment = receipt.PaymentId.ToString("D"); operationRevision = receipt.Revision;
        payment = null; history = null; payments = null; receivables = null; receivable = null; balance = null;
        recordReviewed = confirmReviewed = reviewReviewed = allocationReviewed = false; correctionConfirmation = "";
        if (receipt.PaymentId == newPaymentId) { newPaymentId = Guid.NewGuid(); recordAmount = 0; recordEvidence = recordBankReference = ""; }
    }

    private Task IssueAsync() => RunAsync(async ct =>
    {
        if (!invoiceReviewed || !FinancialContracts.Id(invoiceOrder, out var order) || invoiceOrderRevision < 1 || !FinancialContracts.Kind(invoiceKind))
        { Invalid("Review the exact committed Order reference, revision and debtor choice."); return; }
        object debtor = new { kind = invoiceKind };
        if (invoiceKind == "Individual")
        {
            if (!FinancialContracts.Id(invoiceIndividual, out var individual)) { Invalid("Enter the exact individual debtor reference."); return; }
            debtor = new { kind = invoiceKind, individualId = individual };
        }
        if (PendingCommand is not null) { Invalid("Resolve the saved request before issuing another invoice."); return; }
        pendingOperation = "invoice";
        var saved = await SaveAsync<InvoiceCommandReceipt>($"orders/{order:D}/invoice", new { expectedOrderRevision = invoiceOrderRevision, debtor },
            value => value.InvoiceId != Guid.Empty, ct);
        if (saved is not null) { lastInvoiceReceipt = saved; invoiceReviewed = false; invoice = null; receivable = null; receivables = null; }
    });

    private Task FindInvoiceAsync() => RunAsync(async ct =>
    {
        invoice = null; receivable = null;
        if (!FinancialContracts.Id(invoiceReference, out var id)) { Invalid("Enter the issued invoice reference."); return; }
        await ReadInvoiceAsync(id, false, ct);
    });

    private Task FindInvoiceByOrderAsync() => RunAsync(async ct =>
    {
        invoice = null; receivable = null;
        if (!FinancialContracts.Id(invoiceOrder, out var id)) { Invalid("Enter the committed Order reference."); return; }
        await ReadInvoiceAsync(id, true, ct);
    });

    private async Task ReadInvoiceAsync(Guid id, bool byOrder, CancellationToken ct)
    {
        var result = await Api.GetAsync<InvoiceRecord>(TenantId, byOrder ? $"orders/{id:D}/invoice" : $"invoices/{id:D}",
            value => FinancialContracts.Invoice(value, byOrder ? null : id, byOrder ? id : null), ct);
        invoice = result.Value; receivable = null; Show(result);
    }

    private void ClearInvoiceReview() { invoiceReviewed = false; sourceOrder = null; }
    private Task LoadInvoiceOrderAsync() => RunAsync(async ct =>
    {
        ClearInvoiceReview();
        if (!FinancialContracts.Id(invoiceOrder, out var id)) { Invalid("Enter a committed Order reference."); return; }
        await ReadSourceOrderAsync(id, ct);
    });
    private async Task ReadSourceOrderAsync(Guid id, CancellationToken ct)
    {
        sourceOrder = null;
        var result = await Api.GetAsync<OrderRecord>(TenantId, $"orders/{id:D}", value => OrderContracts.Record(value, id), ct);
        Show(result);
        if (result.Value is not { } value) return;
        if (value.State != "committed") { Invalid("This Order is not committed. Open its saved record and complete admission before billing."); return; }
        sourceOrder = value; invoiceOrderRevision = value.Revision; invoiceReviewed = false;
    }
    private Task ReceivableAsync() => RunAsync(async ct =>
    {
        receivable = null;
        if (invoice is not { } current) return;
        var result = await Api.GetAsync<ReceivableRecord>(TenantId, $"receivables/{current.InvoiceId:D}",
            value => FinancialContracts.Receivable(value) && value.InvoiceId == current.InvoiceId, ct);
        receivable = result.Value; Show(result);
    });
    private void UseReceivable()
    {
        if (Locked || receivable is not { } current) return;
        recordKind = balanceKind = directoryKind = current.DebtorKind;
        recordDebtor = balanceDebtor = directoryDebtor = current.DebtorId.ToString("D");
        allocationInvoice = current.InvoiceId.ToString("D");
        recordReviewed = allocationReviewed = false;
        Message = "Debtor and invoice references copied. Enter actual payment evidence and review the allocation separately."; Tone = "neutral";
    }
    private string DirectoryPath(string resource, string? cursor)
    {
        var filter = directoryFilter is { } debtor ? $"debtorKind={debtor.Kind}&debtorId={debtor.Id:D}" : "";
        if (cursor is not null) filter += (filter.Length > 0 ? "&" : "") + "cursor=" + Uri.EscapeDataString(cursor);
        return resource + (filter.Length > 0 ? "?" + filter : "");
    }
    private void ApplyDirectoryFilter()
    {
        if (Locked) return;
        if (string.IsNullOrWhiteSpace(directoryDebtor)) directoryFilter = null;
        else
        {
            if (!FinancialContracts.Kind(directoryKind) || !FinancialContracts.Id(directoryDebtor, out var id)) { Invalid("Choose a supported debtor kind and exact saved debtor reference."); return; }
            directoryFilter = (directoryKind, id);
        }
        receivables = null; payments = null;
        Message = "Directory filter applied. Refresh receivables and payments to load current results."; Tone = "neutral";
    }

    private Task BalanceAsync() => RunAsync(async ct =>
    {
        balance = null;
        if (!FinancialContracts.Kind(balanceKind) || !FinancialContracts.Id(balanceDebtor, out var id)) { Invalid("Choose the exact debtor kind and enter its reference."); return; }
        var kind = balanceKind;
        var result = await Api.GetAsync<DebtorBalanceRecord>(TenantId, $"debtors/{kind}/{id:D}/balance", value => FinancialContracts.Balance(value, kind, id), ct);
        balance = result.Value; Show(result);
    });

    private Task RetryAsync() => RunAsync(async ct =>
    {
        if (PendingCommand is not { } command) return;
        if (pendingOperation == "invoice")
        {
            var result = await SaveAsync<InvoiceCommandReceipt>(command.Resource, new { }, value => value.InvoiceId != Guid.Empty, ct, command.Method);
            if (result is not null) { lastInvoiceReceipt = result; invoiceReviewed = false; invoice = null; receivable = null; receivables = null; }
        }
        else if (pendingOperation == "payment")
        {
            var result = await SaveAsync<PaymentCommandReceipt>(command.Resource, new { },
                value => FinancialContracts.Receipt(value, pendingPaymentId) && value.Revision == pendingRevision, ct, command.Method);
            if (result is not null) PaymentSaved(result);
        }
    });
}
