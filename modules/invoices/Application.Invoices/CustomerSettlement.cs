using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Tenancy;

namespace Application.Invoices;

public readonly record struct FinancialDebtor(InvoiceDebtorKind Kind, Guid Id)
{
    public static FinancialDebtor From(InvoiceDebtorFacts facts) => facts switch
    {
        InvoiceDebtorFacts.Organization value => new(value.Kind, value.OrganizationId),
        InvoiceDebtorFacts.Program value => new(value.Kind, value.ProgramId),
        InvoiceDebtorFacts.Individual value => new(value.Kind, value.IndividualId),
        _ => throw new FinancialValidationException(),
    };
}

public enum FinancialCapability { View = 1, Record = 2, Reconcile = 3, Allocate = 4, Correct = 5 }
public enum FinancialAccess { Allowed = 1, Denied = 2, Unavailable = 3 }
public sealed record FinancialAuthority(FinancialAccess Access, long AuthorizationRevision = 0);

public interface IFinancialAuthorityReader
{
    Task<FinancialAuthority> CheckAsync(TenantContext context, FinancialCapability permission, CancellationToken cancellationToken);
}

public enum CustomerPaymentChannel { Cash = 1, Bank = 2 }
public enum CustomerPaymentState { Pending = 1, Confirmed = 2, Rejected = 3, Disputed = 4, Reversed = 5 }
public enum CustomerPaymentOperation { Record = 1, Confirm = 2, Reject = 3, Dispute = 4, Reverse = 5, Allocate = 6, Release = 7 }

public sealed record CustomerPaymentRecord(FinancialDebtor Debtor, string CurrencyCode, decimal Amount,
    CustomerPaymentChannel Channel, string Evidence, string? ExternalReference = null);

public sealed record CustomerPaymentMutation(CustomerPaymentOperation Operation, long ExpectedRevision,
    string? Evidence = null, Guid? ReceivableId = null, decimal? Amount = null, Guid? AllocationId = null);

public sealed record ReceivableSnapshot(Guid InvoiceId, Guid TenantId, FinancialDebtor Debtor, string CurrencyCode,
    decimal OriginalAmount, DateOnly InvoiceDate, DateTimeOffset RecognizedAt, decimal AllocatedAmount)
{
    public decimal OutstandingAmount => OriginalAmount - AllocatedAmount;
    public bool IsSettled => OutstandingAmount == 0m;
}

public sealed record CustomerPaymentSnapshot(Guid PaymentId, Guid TenantId, FinancialDebtor Debtor, string CurrencyCode,
    decimal Amount, CustomerPaymentChannel Channel, string Evidence, string? ExternalReference,
    Guid RecordedByAccountId, DateTimeOffset RecordedAt, long Revision, CustomerPaymentState State,
    decimal AllocatedAmount, string? LastConfirmationEvidence)
{
    public decimal AvailableAmount => State == CustomerPaymentState.Confirmed ? Amount - AllocatedAmount : 0m;
}

public sealed record CustomerPaymentEvent(Guid EventId, Guid PaymentId, long Revision, CustomerPaymentOperation Operation,
    CustomerPaymentState State, Guid ActorAccountId, DateTimeOffset RecordedAt, long AuthorizationRevision,
    string? Evidence, Guid? ReceivableId, decimal? Amount, Guid? AllocationId);

public sealed record FinancialCommandReceipt(Guid PaymentId, Guid EventId, long Revision);
public sealed record FinancialPage<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record DebtorBalance(FinancialDebtor Debtor, string CurrencyCode, decimal RecognizedAmount,
    decimal OutstandingAmount, decimal ConfirmedUnappliedAmount);

public enum FinancialStatus
{
    Applied = 1, Replayed = 2, Found = 3, NotFound = 4, Denied = 5, Unavailable = 6, OutcomeUnknown = 7,
    RevisionConflict = 8, IdempotencyConflict = 9, InvalidTransition = 10, SelfConfirmation = 11,
    DuplicateReference = 12, DebtorMismatch = 13, InsufficientFunds = 14, OverAllocation = 15,
    ActiveAllocations = 16, HistoryInvalid = 17, ResourceLimit = 18, DebtorNotFound = 19,
}

public sealed record FinancialResult<T>(FinancialStatus Status, T? Value = default);

public interface ICustomerSettlementStore
{
    Task<FinancialResult<FinancialCommandReceipt>> RecordAsync(TenantContext context, Guid paymentId,
        CustomerPaymentRecord record, string idempotencyKey, CancellationToken cancellationToken);
    Task<FinancialResult<FinancialCommandReceipt>> ExecuteAsync(TenantContext context, Guid paymentId,
        CustomerPaymentMutation mutation, string idempotencyKey, CancellationToken cancellationToken);
    Task<FinancialResult<ReceivableSnapshot>> ReadReceivableAsync(TenantContext context, Guid invoiceId, CancellationToken cancellationToken);
    Task<FinancialResult<CustomerPaymentSnapshot>> ReadPaymentAsync(TenantContext context, Guid paymentId, CancellationToken cancellationToken);
    Task<FinancialResult<FinancialPage<ReceivableSnapshot>>> BrowseReceivablesAsync(TenantContext context,
        FinancialDebtor? debtor, string? cursor, CancellationToken cancellationToken);
    Task<FinancialResult<FinancialPage<CustomerPaymentSnapshot>>> BrowsePaymentsAsync(TenantContext context,
        FinancialDebtor? debtor, string? cursor, CancellationToken cancellationToken);
    Task<FinancialResult<FinancialPage<CustomerPaymentEvent>>> ReadHistoryAsync(TenantContext context,
        Guid paymentId, string? cursor, CancellationToken cancellationToken);
    Task<FinancialResult<DebtorBalance>> ReadBalanceAsync(TenantContext context, FinancialDebtor debtor, CancellationToken cancellationToken);
}

public sealed class FinancialValidationException : ArgumentException
{
    public FinancialValidationException() : base("The financial request is invalid.") { }
}

public static class CustomerSettlementRules
{
    public const int PageSize = 50;
    public const int MaximumEventsPerPayment = 10000;
    public const int MaximumOrdinaryEventsPerPayment = 5000;
    public const decimal MaximumAmount = 999999999999999.9999m;

    public static FinancialCapability Permission(CustomerPaymentOperation operation) => operation switch
    {
        CustomerPaymentOperation.Record => FinancialCapability.Record,
        CustomerPaymentOperation.Confirm or CustomerPaymentOperation.Reject or CustomerPaymentOperation.Dispute => FinancialCapability.Reconcile,
        CustomerPaymentOperation.Allocate => FinancialCapability.Allocate,
        CustomerPaymentOperation.Reverse or CustomerPaymentOperation.Release => FinancialCapability.Correct,
        _ => throw new FinancialValidationException(),
    };

    public static void RequireDebtor(FinancialDebtor debtor)
    {
        if (debtor.Id == Guid.Empty || !Enum.IsDefined(debtor.Kind)) throw new FinancialValidationException();
    }

    public static bool IsAmount(decimal amount, bool allowZero = false) =>
        amount >= (allowZero ? 0m : 0.0001m) && amount <= MaximumAmount && ((decimal.GetBits(amount)[3] >> 16) & 255) <= 4;

    public static CustomerPaymentRecord Normalize(CustomerPaymentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        RequireDebtor(record.Debtor);
        if (record.CurrencyCode != "NPR" || !IsAmount(record.Amount) || !Enum.IsDefined(record.Channel))
            throw new FinancialValidationException();
        var reference = record.ExternalReference is null ? null : Text(record.ExternalReference, 200);
        if (record.Channel == CustomerPaymentChannel.Bank && reference is null) throw new FinancialValidationException();
        return record with { Evidence = Text(record.Evidence, 1000), ExternalReference = reference };
    }

    public static CustomerPaymentMutation Normalize(CustomerPaymentMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (!Enum.IsDefined(mutation.Operation) || mutation.Operation == CustomerPaymentOperation.Record ||
            mutation.ExpectedRevision is < 1 or > MaximumEventsPerPayment) throw new FinancialValidationException();
        if (mutation.Operation == CustomerPaymentOperation.Allocate)
        {
            if (mutation.ReceivableId is null || mutation.ReceivableId == Guid.Empty ||
                mutation.Amount is not { } amount || !IsAmount(amount) || mutation.AllocationId is not null || mutation.Evidence is not null)
                throw new FinancialValidationException();
            return mutation;
        }
        if (mutation.ReceivableId is not null || mutation.Amount is not null ||
            (mutation.Operation == CustomerPaymentOperation.Release
                ? mutation.AllocationId is null || mutation.AllocationId == Guid.Empty
                : mutation.AllocationId is not null)) throw new FinancialValidationException();
        return mutation with { Evidence = Text(mutation.Evidence, 1000) };
    }

    public static string Key(string value)
    {
        if (value is null || value.Length is < 1 or > 128 || value.Any(c => c is < '!' or > '~'))
            throw new FinancialValidationException();
        return value;
    }

    public static string Fingerprint(Guid paymentId, CustomerPaymentRecord record) => Hash(paymentId, 1,
        ((int)record.Debtor.Kind).ToString(CultureInfo.InvariantCulture), record.Debtor.Id.ToString("D"), record.CurrencyCode,
        record.Amount.ToString("G29", CultureInfo.InvariantCulture), ((int)record.Channel).ToString(CultureInfo.InvariantCulture),
        record.Evidence, record.ExternalReference);

    public static string Fingerprint(Guid paymentId, CustomerPaymentMutation mutation) => Hash(paymentId, (int)mutation.Operation,
        mutation.ExpectedRevision.ToString(CultureInfo.InvariantCulture), mutation.Evidence,
        mutation.ReceivableId?.ToString("D"), mutation.Amount?.ToString("G29", CultureInfo.InvariantCulture), mutation.AllocationId?.ToString("D"));

    public static FinancialStatus CanApply(CustomerPaymentSnapshot payment, CustomerPaymentMutation mutation, Guid actor)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(mutation);
        if (payment.Revision != mutation.ExpectedRevision) return FinancialStatus.RevisionConflict;
        if (payment.Revision >= MaximumEventsPerPayment) return FinancialStatus.ResourceLimit;
        // Reserve enough retained events to release every allocation and reverse the receipt.
        if (payment.Revision >= MaximumOrdinaryEventsPerPayment &&
            mutation.Operation is not (CustomerPaymentOperation.Release or CustomerPaymentOperation.Reverse)) return FinancialStatus.ResourceLimit;
        if (payment.State is CustomerPaymentState.Rejected or CustomerPaymentState.Reversed) return FinancialStatus.InvalidTransition;
        return mutation.Operation switch
        {
            CustomerPaymentOperation.Confirm when payment.RecordedByAccountId == actor => FinancialStatus.SelfConfirmation,
            CustomerPaymentOperation.Confirm when payment.State is CustomerPaymentState.Pending or CustomerPaymentState.Disputed &&
                mutation.Evidence != payment.LastConfirmationEvidence => FinancialStatus.Applied,
            CustomerPaymentOperation.Reject when payment.State == CustomerPaymentState.Pending => FinancialStatus.Applied,
            CustomerPaymentOperation.Dispute when payment.State is CustomerPaymentState.Pending or CustomerPaymentState.Confirmed => FinancialStatus.Applied,
            CustomerPaymentOperation.Reverse when payment.State is CustomerPaymentState.Confirmed or CustomerPaymentState.Disputed =>
                payment.AllocatedAmount == 0m ? FinancialStatus.Applied : FinancialStatus.ActiveAllocations,
            CustomerPaymentOperation.Allocate when payment.State == CustomerPaymentState.Confirmed =>
                mutation.Amount <= payment.AvailableAmount ? FinancialStatus.Applied : FinancialStatus.InsufficientFunds,
            CustomerPaymentOperation.Release => FinancialStatus.Applied,
            _ => FinancialStatus.InvalidTransition,
        };
    }

    public static CustomerPaymentState NextState(CustomerPaymentState state, CustomerPaymentOperation operation) => operation switch
    {
        CustomerPaymentOperation.Record => CustomerPaymentState.Pending,
        CustomerPaymentOperation.Confirm => CustomerPaymentState.Confirmed,
        CustomerPaymentOperation.Reject => CustomerPaymentState.Rejected,
        CustomerPaymentOperation.Dispute => CustomerPaymentState.Disputed,
        CustomerPaymentOperation.Reverse => CustomerPaymentState.Reversed,
        CustomerPaymentOperation.Allocate or CustomerPaymentOperation.Release => state,
        _ => throw new FinancialValidationException(),
    };

    private static string Text(string? value, int maximum)
    {
        if (value is null || value.Trim().Length is < 1 || value.Trim().Length > maximum || value.Any(char.IsControl))
            throw new FinancialValidationException();
        return value.Trim();
    }

    private static string Hash(Guid paymentId, int operation, params string?[] fields)
    {
        if (paymentId == Guid.Empty) throw new FinancialValidationException();
        var builder = new StringBuilder("v1:customer-settlement:").Append(paymentId.ToString("D")).Append(':').Append(operation.ToString(CultureInfo.InvariantCulture));
        foreach (var field in fields) builder.Append('|').Append(field?.Length.ToString(CultureInfo.InvariantCulture) ?? "-").Append(':').Append(field);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
