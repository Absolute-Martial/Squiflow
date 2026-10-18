using System.Text.Json.Serialization;

namespace Application.Web.Business;

public sealed record FinancialPage<T>(T[] Items, string? NextCursor);
public sealed record PaymentCommandReceipt([property: JsonRequired] Guid PaymentId, [property: JsonRequired] Guid EventId, [property: JsonRequired] long Revision);
public sealed record InvoiceCommandReceipt([property: JsonRequired] Guid InvoiceId);
public sealed record ReceivableRecord([property: JsonRequired] Guid InvoiceId, string DebtorKind, [property: JsonRequired] Guid DebtorId,
    string CurrencyCode, [property: JsonRequired] decimal OriginalAmount, [property: JsonRequired] DateOnly InvoiceDate,
    [property: JsonRequired] DateTimeOffset RecognizedAt, [property: JsonRequired] decimal AllocatedAmount,
    [property: JsonRequired] decimal OutstandingAmount, [property: JsonRequired] bool IsSettled);
public sealed record PaymentRecord([property: JsonRequired] Guid PaymentId, string DebtorKind, [property: JsonRequired] Guid DebtorId,
    string CurrencyCode, [property: JsonRequired] decimal Amount, string Channel, string Evidence, string? ExternalReference,
    [property: JsonRequired] Guid RecordedByAccountId, [property: JsonRequired] DateTimeOffset RecordedAt,
    [property: JsonRequired] long Revision, string State, [property: JsonRequired] decimal AllocatedAmount, [property: JsonRequired] decimal AvailableAmount);
public sealed record PaymentEventRecord([property: JsonRequired] Guid EventId, [property: JsonRequired] Guid PaymentId,
    [property: JsonRequired] long Revision, string Operation, string State, [property: JsonRequired] Guid ActorAccountId,
    [property: JsonRequired] DateTimeOffset RecordedAt, string? Evidence, Guid? ReceivableId, decimal? Amount, Guid? AllocationId);
public sealed record DebtorBalanceRecord(string DebtorKind, [property: JsonRequired] Guid DebtorId, string CurrencyCode,
    [property: JsonRequired] decimal RecognizedAmount, [property: JsonRequired] decimal OutstandingAmount, [property: JsonRequired] decimal ConfirmedUnappliedAmount);
public sealed record InvoiceDebtorRecord(string Kind, Guid? OrganizationId, string? OrganizationDisplayName, Guid? ProgramId,
    string? ProgramDisplayName, Guid? IndividualId, long? ObservedRevision, string? IndividualDisplayName);
public sealed record InvoiceRecord([property: JsonRequired] Guid InvoiceId, [property: JsonRequired] Guid SourceOrderId,
    [property: JsonRequired] long SourceOrderRevision, [property: JsonRequired] Guid OrganizationId, Guid? ProgramId,
    [property: JsonRequired] long OrganizationOrdinal, string CurrencyCode, OrderLine[] Lines, [property: JsonRequired] decimal Total,
    InvoiceDebtorRecord Debtor, [property: JsonRequired] Guid IssuedByAccountId, [property: JsonRequired] DateTimeOffset IssuedAt,
    [property: JsonRequired] DateOnly BusinessDate);

public static class FinancialContracts
{
    public static bool Kind(string? value) => value is "Organization" or "Program" or "Individual";
    public static bool State(string? value) => value is "Pending" or "Confirmed" or "Rejected" or "Disputed" or "Reversed";
    public static bool Money(decimal value) => value is >= 0 and <= 999999999999999.9999m &&
        ((decimal.GetBits(value)[3] >> 16) & 0xff) <= 4;
    public static bool Id(string value, out Guid id) => Guid.TryParse(value, out id) && id != Guid.Empty;
    public static bool Debtor(string? kind, Guid id) => Kind(kind) && id != Guid.Empty;
    public static bool Receipt(PaymentCommandReceipt value, Guid id) => value.PaymentId == id && value.EventId != Guid.Empty && value.Revision > 0;
    public static bool Receivable(ReceivableRecord value) => value.InvoiceId != Guid.Empty && Debtor(value.DebtorKind, value.DebtorId) &&
        value.CurrencyCode == "NPR" && Money(value.OriginalAmount) && Money(value.AllocatedAmount) && Money(value.OutstandingAmount) &&
        value.AllocatedAmount <= value.OriginalAmount && value.OutstandingAmount <= value.OriginalAmount &&
        value.InvoiceDate != default && value.RecognizedAt != default;
    public static bool Payment(PaymentRecord value, Guid? id = null) => value.PaymentId != Guid.Empty && (!id.HasValue || value.PaymentId == id) &&
        Debtor(value.DebtorKind, value.DebtorId) && value.CurrencyCode == "NPR" && Money(value.Amount) && value.Amount > 0 &&
        value.Channel is "Cash" or "Bank" && BusinessValidation.Text(value.Evidence, 1000) &&
        BusinessValidation.Text(value.ExternalReference, 200, value.Channel == "Bank") && value.RecordedByAccountId != Guid.Empty &&
        value.RecordedAt != default && value.Revision > 0 && State(value.State) && Money(value.AllocatedAmount) && Money(value.AvailableAmount) &&
        value.AllocatedAmount <= value.Amount && value.AvailableAmount <= value.Amount;
    public static bool Event(PaymentEventRecord value, Guid payment) => value.EventId != Guid.Empty && value.PaymentId == payment && value.Revision > 0 &&
        value.Operation is "Record" or "Confirm" or "Reject" or "Dispute" or "Reverse" or "Allocate" or "Release" && State(value.State) &&
        value.ActorAccountId != Guid.Empty && value.RecordedAt != default && BusinessValidation.Text(value.Evidence, 1000, false) &&
        (!value.Amount.HasValue || Money(value.Amount.Value)) && value.ReceivableId != Guid.Empty && value.AllocationId != Guid.Empty;
    public static bool Cursor(string? value) => value is null || value.Length is > 0 and <= 160 && !value.Contains("//", StringComparison.Ordinal) && (value.StartsWith("r1/", StringComparison.Ordinal) || value.StartsWith("p1/", StringComparison.Ordinal) || value.StartsWith("h1/", StringComparison.Ordinal)) && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-');
    public static bool Page<T>(FinancialPage<T> value, Func<T, bool> validate, Func<T, Guid> id, string? previous) where T : class =>
        value.Items is { Length: <= 50 } && Cursor(value.NextCursor) && (value.NextCursor is null || value.Items.Length > 0 && value.NextCursor != previous) &&
        value.Items.All(item => item is not null && validate(item)) && value.Items.Select(id).Distinct().Count() == value.Items.Length;
    private static bool Aggregate(decimal amount) => amount >= 0 && ((decimal.GetBits(amount)[3] >> 16) & 0xff) <= 4;
    public static bool Balance(DebtorBalanceRecord value, string kind, Guid id) => value.DebtorKind == kind && value.DebtorId == id &&
        value.CurrencyCode == "NPR" && Aggregate(value.RecognizedAmount) && Aggregate(value.OutstandingAmount) && Aggregate(value.ConfirmedUnappliedAmount);
    public static bool Invoice(InvoiceRecord value, Guid? id = null, Guid? order = null) => value.InvoiceId != Guid.Empty && value.SourceOrderId != Guid.Empty &&
        (!id.HasValue || value.InvoiceId == id) && (!order.HasValue || value.SourceOrderId == order) && value.SourceOrderRevision > 0 &&
        value.OrganizationId != Guid.Empty && value.OrganizationOrdinal > 0 && value.CurrencyCode == "NPR" && Money(value.Total) &&
        value.IssuedByAccountId != Guid.Empty && value.IssuedAt != default && value.BusinessDate != default && value.Debtor is { } debtor && Kind(debtor.Kind) &&
        (debtor.Kind == "Individual" ? debtor.IndividualId.HasValue && debtor.IndividualId != Guid.Empty && BusinessValidation.Text(debtor.IndividualDisplayName, 200) :
            debtor.OrganizationId == value.OrganizationId && BusinessValidation.Text(debtor.OrganizationDisplayName, 200) &&
            (debtor.Kind != "Program" || debtor.ProgramId.HasValue && debtor.ProgramId == value.ProgramId && BusinessValidation.Text(debtor.ProgramDisplayName, 200))) &&
        value.Lines is { Length: > 0 and <= 100 } && value.Lines.Select((line, i) => line is not null && line.Position == i + 1 &&
            BusinessValidation.Text(line.Description, 300) && BusinessValidation.Text(line.UnitCode, 64) && line.Quantity > 0 && Money(line.UnitPrice) && Money(line.LineTotal)).All(x => x);
}
