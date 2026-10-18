using System.Globalization;
using Application.Web.Business;
using Microsoft.AspNetCore.Components;

namespace Application.Web.Components.Pages;

public partial class Quotations
{
    [Parameter] public Guid? QuotationId { get; set; }
    private QuotationRecord? quote;
    private QuotationCommandReceipt? commandReceipt;
    private QuotationHistory? history;
    private QuotationResponseFact? historicalResponse;
    private string reference = "", actionReference = "", issuedReference = "", summary = "", currency = "", validUntil = "", terms = "";
    private string individual = "", organization = "", program = "", mode = "manual", evidence = "", claim = "", action = "issue", pendingAction = "";
    private long version;
    private bool wholesale, replaceConfirmed, actionConfirmed;
    private readonly List<QuotationLineEntry> lines = [new()];
    private bool Locked => Busy || PendingCommand is not null;
    private string Root => $"/workspace/{TenantId:D}/quotations";
    private string ActionLabel => action switch { "issue" => "Issue quotation", "accept" => "Record acceptance", "reject" => "Record rejection", "expire" => "Record expiry", _ => "Convert accepted quotation" };
    protected override void Reset()
    {
        quote = null; commandReceipt = null; history = null; historicalResponse = null;
        reference = actionReference = issuedReference = summary = currency = validUntil = terms = individual = organization = program = evidence = claim = pendingAction = "";
        mode = "manual"; action = "issue"; version = 0; wholesale = replaceConfirmed = actionConfirmed = false; lines.Clear(); lines.Add(new());
    }
    protected override Task LoadAsync(CancellationToken cancellationToken) => QuotationId is { } id ? ReadAsync(id, cancellationToken) : Task.CompletedTask;
    private void Invalid(string message) { Message = message; Tone = "error"; }
    private static bool Id(string value, out Guid id) => Guid.TryParse(value, out id) && id != Guid.Empty;
    private static bool OptionalId(string text, out Guid? id)
    {
        id = null; if (string.IsNullOrWhiteSpace(text)) return true;
        if (!Id(text, out var value)) return false;
        id = value; return true;
    }
    private Task FindAsync() => RunAsync(async ct =>
    {
        quote = null; history = null; commandReceipt = null; historicalResponse = null;
        if (!Id(reference, out var id)) { Invalid("Enter a valid quotation reference."); return; }
        await ReadAsync(id, ct);
    });
    private async Task ReadAsync(Guid id, CancellationToken ct)
    {
        reference = actionReference = id.ToString("D");
        var result = await Api.GetAsync<QuotationRecord>(TenantId, $"quotations/{id:D}", row => QuotationContracts.Record(row, TenantId, id), ct);
        if (result.Value is { } value) SelectQuote(value);
        Show(result);
    }
    private void SelectQuote(QuotationRecord value)
    {
        quote = value; reference = actionReference = value.QuotationId.ToString("D"); version = value.Version;
        issuedReference = value.CurrentIssued?.RevisionId.ToString("D") ?? "";
        replaceConfirmed = actionConfirmed = false;
        var offer = value.Draft ?? value.CurrentIssued?.Offer;
        if (offer is null) return;
        summary = offer.Summary; currency = offer.CurrencyCode; validUntil = offer.ValidUntil.ToString("O", CultureInfo.InvariantCulture); terms = offer.Terms ?? "";
        individual = offer.CustomerId?.ToString("D") ?? ""; organization = offer.CustomerContext?.OrganizationId.ToString("D") ?? "";
        program = offer.CustomerContext?.ProgramId?.ToString("D") ?? ""; wholesale = offer.WholesaleApplicable; mode = offer.Mode;
        lines.Clear(); lines.AddRange(offer.Lines.Select(row => new QuotationLineEntry
        {
            Description = row.Description,
            UnitCode = row.UnitCode,
            Quantity = row.Quantity,
            UnitPrice = row.UnitPrice,
            Item = row.ItemId?.ToString("D") ?? "",
            Unit = row.UnitId?.ToString("D") ?? "",
            OverridePrice = row.Override?.UnitPrice,
            OverrideReason = row.Override?.Reason ?? ""
        }));
    }
    private Task DraftAsync() => RunAsync(async ct =>
    {
        if (quote is not null && PendingCommand is null && !replaceConfirmed) return;
        if (!BusinessValidation.Text(summary, 200) || !CatalogPricingContracts.Currency(currency) || !QuotationContracts.Instant(validUntil, out _) ||
            !BusinessValidation.Text(terms, 2000, false) || !OptionalId(individual, out var customer) || !OptionalId(organization, out var org) ||
            !OptionalId(program, out var prog) || prog.HasValue && !org.HasValue || lines.Count is < 1 or > 100)
        { Invalid("Check the summary, currency, explicit expiry timestamp, terms and customer references."); return; }
        var entered = new List<QuotationLineInput>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0 || !CatalogPricingContracts.Money(line.Quantity)) { Invalid("Enter positive quantities with up to four decimal places."); return; }
            if (mode == "manual")
            {
                if (!BusinessValidation.Text(line.Description, 300) || !BusinessValidation.Text(line.UnitCode, 16) || !CatalogPricingContracts.Money(line.UnitPrice))
                { Invalid("Check each supplied description, unit code and unit price."); return; }
                entered.Add(new(line.Quantity, line.Description, line.UnitCode, line.UnitPrice));
            }
            else
            {
                if (!Id(line.Item, out var item) || !Id(line.Unit, out var unit) || line.ConversionRevision is < 1 ||
                    line.OverridePrice.HasValue && (!CatalogPricingContracts.Money(line.OverridePrice.Value) || !BusinessValidation.Text(line.OverrideReason, 500)))
                { Invalid("Check Catalog references, non-base conversion revisions and any override price/reason."); return; }
                entered.Add(new(line.Quantity, ItemId: item, UnitId: unit, ConversionRevision: line.ConversionRevision,
                    OverridePrice: line.OverridePrice, OverrideReason: line.OverridePrice.HasValue ? line.OverrideReason : null));
            }
        }
        var existing = quote;
        var payload = new QuotationDraftInput(mode, summary, currency, validUntil, entered.ToArray(), existing?.Version,
            string.IsNullOrWhiteSpace(terms) ? null : terms, customer, org.HasValue ? new(org.Value, prog) : null, wholesale);
        pendingAction = "draft";
        var saved = await SaveAsync<QuotationRecord>(existing is null ? "quotations" : $"quotations/{existing.QuotationId:D}/draft", payload,
            row => QuotationContracts.Record(row, TenantId, existing?.QuotationId) && row.Draft is not null && row.Version == (existing?.Version ?? 0) + 1,
            ct, existing is null ? HttpMethod.Post : HttpMethod.Put);
        if (saved is not null) { SelectQuote(saved); commandReceipt = null; history = null; historicalResponse = null; }
    });
    private void ChooseCustomer(OrderCustomer customer)
    {
        organization = customer.OrganizationId.ToString("D"); program = customer.ProgramId?.ToString("D") ?? ""; replaceConfirmed = false;
    }
    private void ChooseItem(QuotationLineEntry line, CatalogItem item)
    {
        line.Item = item.ItemId.ToString("D"); line.Unit = item.BaseUnitId.ToString("D"); line.ConversionRevision = null;
        line.OverridePrice = null; line.OverrideReason = ""; replaceConfirmed = false;
    }
    private void ClearConfirmation() => actionConfirmed = false;
    private Task ActAsync() => RunAsync(async ct =>
    {
        if (!Id(actionReference, out var id) || version < 1 || PendingCommand is null && !actionConfirmed)
        { Invalid("Enter a quotation reference, expected version and confirm the exact action."); return; }
        pendingAction = action;
        if (action == "issue")
        {
            var saved = await SaveAsync<QuotationRecord>($"quotations/{id:D}/issue", new { expectedVersion = version },
                row => QuotationContracts.Record(row, TenantId, id) && row.CurrentIssued is not null && row.Version == version + 1, ct);
            if (saved is not null) { SelectQuote(saved); commandReceipt = null; }
            return;
        }
        if (!Id(issuedReference, out var revision) || action != "convert" && (!BusinessValidation.Text(evidence, 2000) || action != "expire" && !BusinessValidation.Text(claim, 300)))
        { Invalid("Enter the exact issued revision and the evidence and customer claim required by this action."); return; }
        object payload = action == "convert" ? new { issuedRevisionId = revision, expectedVersion = version } :
            (object)new { issuedRevisionId = revision, expectedVersion = version, evidence, customerClaim = action == "expire" ? null : claim };
        var receipt = await SaveAsync<QuotationCommandReceipt>($"quotations/{id:D}/{action}", payload, row => QuotationContracts.Command(row, TenantId, id, revision, action), ct);
        if (receipt is not null)
        {
            commandReceipt = receipt; version = receipt.Version; actionConfirmed = false;
            if (quote?.QuotationId == id && quote.CurrentIssued?.RevisionId == revision)
                quote = quote with { Version = receipt.Version, CurrentResponse = receipt.Response, Conversion = receipt.Conversion };
        }
    });
    private Task HistoryAsync(long after) => RunAsync(async ct =>
    {
        history = null;
        if (!Id(actionReference, out var id)) { Invalid("Enter a quotation reference for its history."); return; }
        var result = await Api.GetAsync<QuotationHistory>(TenantId, $"quotations/{id:D}/issued?limit=1&afterRevision={after}", row =>
            row.Items is { Length: <= 1 } && row.Items.All(item => item is not null && QuotationContracts.Issued(item) && item.RevisionNumber > after) &&
            row.Items.Select(item => item.RevisionId).Distinct().Count() == row.Items.Length &&
            row.Items.Select(item => item.RevisionNumber).SequenceEqual(row.Items.Select(item => item.RevisionNumber).Order()) &&
            (!row.NextAfterRevision.HasValue || row.Items.Length > 0 && row.NextAfterRevision == row.Items[^1].RevisionNumber), ct);
        history = result.Value; Show(result);
    });
    private void SelectIssued(QuotationIssued value) { issuedReference = value.RevisionId.ToString("D"); historicalResponse = null; actionConfirmed = false; }
    private Task ReadResponseAsync() => RunAsync(async ct =>
    {
        historicalResponse = null;
        if (!Id(actionReference, out var id) || !Id(issuedReference, out var revision)) { Invalid("Enter quotation and issued revision references."); return; }
        var result = await Api.GetAsync<QuotationResponseFact>(TenantId, $"quotations/{id:D}/issued/{revision:D}/response", row => QuotationContracts.Response(row, id, revision), ct);
        historicalResponse = result.Value; Show(result);
    });
    private Task RetryAsync() => pendingAction == "draft" ? DraftAsync() : ActAsync();
    private sealed class QuotationLineEntry
    {
        public string Description { get; set; } = "";
        public string UnitCode { get; set; } = "";
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public string Item { get; set; } = "";
        public string Unit { get; set; } = "";
        public long? ConversionRevision { get; set; }
        public decimal? OverridePrice { get; set; }
        public string OverrideReason { get; set; } = "";
    }
}
