using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Customers;
using Application.Orders;
using Application.Tenancy;

namespace Application.Invoices;

public abstract record InvoiceDebtorSelection
{
    private InvoiceDebtorSelection()
    {
    }

    public sealed record DefaultOrganization : InvoiceDebtorSelection;

    public sealed record AttributedProgram : InvoiceDebtorSelection;

    public sealed record Individual(Guid IndividualId) : InvoiceDebtorSelection;
}

public sealed record IssueInvoiceRequest(
    Guid OrderId,
    long ExpectedOrderRevision,
    InvoiceDebtorSelection? Debtor,
    string IdempotencyKey);

public enum InvoiceDebtorKind
{
    Organization = 1,
    Program = 2,
    Individual = 3,
}

public abstract record InvoiceDebtorFacts(InvoiceDebtorKind Kind)
{
    public sealed record Organization(
        Guid OrganizationId,
        string OrganizationDisplayName)
        : InvoiceDebtorFacts(InvoiceDebtorKind.Organization);

    public sealed record Program(
        Guid OrganizationId,
        string OrganizationDisplayName,
        Guid ProgramId,
        string ProgramDisplayName)
        : InvoiceDebtorFacts(InvoiceDebtorKind.Program);

    public sealed record Individual(
        Guid IndividualId,
        long ObservedRevision,
        string DisplayName)
        : InvoiceDebtorFacts(InvoiceDebtorKind.Individual);
}

public sealed record InvoiceIssuedLine(
    int Position,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record InvoiceSourceLine(
    int Position,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice,
    decimal LineTotal);

public sealed class InvoiceSourceOrderFacts
{
    private readonly ReadOnlyCollection<InvoiceSourceLine> retainedLines;

    public InvoiceSourceOrderFacts(
        Guid tenantId,
        Guid orderId,
        long revision,
        bool isCommitted,
        string currencyCode,
        CustomerOrderContext? customerContext,
        IEnumerable<InvoiceSourceLine> lines,
        decimal total)
    {
        ArgumentNullException.ThrowIfNull(lines);
        TenantId = tenantId;
        OrderId = orderId;
        Revision = revision;
        IsCommitted = isCommitted;
        CurrencyCode = currencyCode;
        CustomerContext = customerContext;
        retainedLines = Array.AsReadOnly(lines.ToArray());
        Total = total;
    }

    public Guid TenantId { get; }
    public Guid OrderId { get; }
    public long Revision { get; }
    public bool IsCommitted { get; }
    public string CurrencyCode { get; }
    public CustomerOrderContext? CustomerContext { get; }
    public IReadOnlyList<InvoiceSourceLine> Lines => retainedLines;
    public decimal Total { get; }
}

public enum InvoiceSourceOrderReadStatus
{
    Found = 1,
    Missing = 2,
    Invalid = 3,
    Unavailable = 4,
}

public sealed record InvoiceSourceOrderReadResult(
    InvoiceSourceOrderReadStatus Status,
    InvoiceSourceOrderFacts? SourceOrder = null);

public interface IInvoiceSourceOrderReader
{
    Task<InvoiceSourceOrderReadResult> ReadAsync(
        TenantContext tenantContext,
        Guid orderId,
        CancellationToken cancellationToken);
}

public sealed class InvoiceIssuedFacts
{
    private readonly ReadOnlyCollection<InvoiceIssuedLine> lines;

    public InvoiceIssuedFacts(
        Guid invoiceId,
        Guid tenantId,
        Guid sourceOrderId,
        long sourceOrderRevision,
        CustomerOrderContext? sourceCustomerContext,
        string currencyCode,
        IEnumerable<InvoiceIssuedLine> lines,
        decimal total,
        InvoiceDebtorFacts debtor,
        Guid issuedByAccountId,
        DateTimeOffset issuedAt,
        DateOnly businessDate,
        long organizationOrdinal)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(debtor);
        InvoiceIssueRules.RequireIdentity(invoiceId, "invoice_id_invalid");
        InvoiceIssueRules.RequireIdentity(tenantId, "tenant_id_invalid");
        InvoiceIssueRules.RequireIdentity(sourceOrderId, "order_id_invalid");
        InvoiceIssueRules.RequireIdentity(issuedByAccountId, "account_id_invalid");
        if (sourceOrderRevision < 1)
        {
            throw new InvoiceIssueValidationException(
                "source_order_revision_invalid",
                "Source Order revision must be positive.");
        }

        if (!InvoiceIssueRules.IsUtcTimestamp(issuedAt))
        {
            throw new InvoiceIssueValidationException(
                "issued_at_invalid",
                "Issued timestamp must be a non-default UTC timestamp.");
        }

        if (businessDate != DateOnly.FromDateTime(issuedAt.UtcDateTime))
        {
            throw new InvoiceIssueValidationException(
                "business_date_invalid",
                "Invoice business date must equal the UTC date of its issue timestamp.");
        }

        if (organizationOrdinal < 1)
        {
            throw new InvoiceIssueValidationException(
                "organization_ordinal_invalid",
                "Organization invoice ordinal must be positive.");
        }

        if (!string.Equals(currencyCode, InvoiceIssueRules.RequiredCurrencyCode, StringComparison.Ordinal))
        {
            throw new InvoiceIssueValidationException(
                "currency_invalid",
                "Issued invoice currency must be NPR.");
        }

        if (sourceCustomerContext is null || sourceCustomerContext.OrganizationId == Guid.Empty ||
            sourceCustomerContext.ProgramId == Guid.Empty)
        {
            throw new InvoiceIssueValidationException(
                "customer_context_invalid",
                "Retained customer attribution must contain an organization identity and valid optional program identity.");
        }

        var retainedLines = lines.ToArray();
        if (!InvoiceIssueRules.AreValidIssuedLines(retainedLines, total))
        {
            throw new InvoiceIssueValidationException(
                "issued_arithmetic_invalid",
                "Issued invoice lines or total violate the accepted decimal arithmetic contract.");
        }

        if (!InvoiceIssueRules.AreWithinRetainedBounds(retainedLines, debtor))
        {
            throw new InvoiceIssueValidationException(
                "resource_limit_exceeded",
                "Retained invoice facts exceed the supported resource bounds.");
        }

        if (!InvoiceIssueRules.HasValidRetainedLineText(retainedLines))
        {
            throw new InvoiceIssueValidationException(
                "issued_line_text_invalid",
                "Retained invoice line text is invalid.");
        }

        InvoiceIssueRules.ValidateDebtorFacts(debtor);
        if (debtor is InvoiceDebtorFacts.Organization organizationDebtor &&
            organizationDebtor.OrganizationId != sourceCustomerContext.OrganizationId ||
            debtor is InvoiceDebtorFacts.Program programDebtor &&
            (programDebtor.OrganizationId != sourceCustomerContext.OrganizationId ||
             sourceCustomerContext.ProgramId is not { } attributedProgramId ||
             attributedProgramId != programDebtor.ProgramId))
        {
            throw new InvoiceIssueValidationException(
                "debtor_organization_mismatch",
                "Organization or program debtor must use the committed Order organization anchor.");
        }

        InvoiceId = invoiceId;
        TenantId = tenantId;
        SourceOrderId = sourceOrderId;
        SourceOrderRevision = sourceOrderRevision;
        SourceCustomerContext = sourceCustomerContext;
        CurrencyCode = currencyCode;
        this.lines = Array.AsReadOnly(retainedLines);
        Total = total;
        Debtor = debtor;
        IssuedByAccountId = issuedByAccountId;
        IssuedAt = issuedAt;
        BusinessDate = businessDate;
        OrganizationOrdinal = organizationOrdinal;
    }

    public Guid InvoiceId { get; }
    public Guid TenantId { get; }
    public Guid SourceOrderId { get; }
    public long SourceOrderRevision { get; }
    public CustomerOrderContext? SourceCustomerContext { get; }
    public string CurrencyCode { get; }
    public IReadOnlyList<InvoiceIssuedLine> Lines => lines;
    public decimal Total { get; }
    public InvoiceDebtorFacts Debtor { get; }
    public Guid IssuedByAccountId { get; }
    public DateTimeOffset IssuedAt { get; }
    public DateOnly BusinessDate { get; }
    public long OrganizationOrdinal { get; }
}

public sealed class InvoiceIssueCandidate
{
    private readonly ReadOnlyCollection<InvoiceIssuedLine> lines;

    internal InvoiceIssueCandidate(
        Guid tenantId,
        Guid sourceOrderId,
        long sourceOrderRevision,
        CustomerOrderContext? sourceCustomerContext,
        IEnumerable<InvoiceIssuedLine> lines,
        decimal total,
        InvoiceDebtorFacts debtor,
        Guid issuedByAccountId)
    {
        TenantId = tenantId;
        SourceOrderId = sourceOrderId;
        SourceOrderRevision = sourceOrderRevision;
        SourceCustomerContext = sourceCustomerContext;
        CurrencyCode = InvoiceIssueRules.RequiredCurrencyCode;
        this.lines = Array.AsReadOnly(lines.ToArray());
        Total = total;
        Debtor = debtor;
        IssuedByAccountId = issuedByAccountId;
    }

    public Guid TenantId { get; }
    public Guid SourceOrderId { get; }
    public long SourceOrderRevision { get; }
    public CustomerOrderContext? SourceCustomerContext { get; }
    public string CurrencyCode { get; }
    public IReadOnlyList<InvoiceIssuedLine> Lines => lines;
    public decimal Total { get; }
    public InvoiceDebtorFacts Debtor { get; }
    public Guid IssuedByAccountId { get; }
}

public enum InvoiceIssueStatus
{
    Issued = 1,
    Replayed = 2,
    NotAuthorized = 3,
    SourceNotFound = 4,
    OrderNotCommitted = 5,
    OrderRevisionConflict = 6,
    UnsupportedCurrency = 7,
    InvalidDebtor = 8,
    InvalidCommittedOrderFacts = 9,
    IdempotencyKeyConflict = 10,
    DependencyUnavailable = 11,
    HistoricalReceiptInvalid = 12,
    OrderAlreadyInvoiced = 14,
    OutcomeUnknown = 15,
    ResourceLimit = 16,
}

public sealed record InvoiceIssueResult(
    InvoiceIssueStatus Status,
    InvoiceIssuedFacts? Invoice = null);

public enum InvoiceIssueAuthorityStatus
{
    Allowed = 1,
    Denied = 2,
    Unavailable = 3,
}

public interface IInvoiceIssueAuthority
{
    // Implementations must establish current active membership and the distinct
    // invoice-issue billing permission for this tenant/account on every call.
    Task<InvoiceIssueAuthorityStatus> CheckAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken);
}

public enum InvoiceIssueReceiptStatus
{
    Missing = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
    Unavailable = 4,
    HistoricalReceiptInvalid = 5,
    NotAuthorized = 6,
}

public sealed record InvoiceIssueReceiptResult(
    InvoiceIssueReceiptStatus Status,
    InvoiceIssuedFacts? Invoice = null);

public enum InvoiceIssueCommitStatus
{
    Issued = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
    Unavailable = 4,
    HistoricalReceiptInvalid = 5,
    NotAuthorized = 6,
    InvalidSource = 7,
    InvalidDebtor = 8,
    ResourceLimit = 9,
    OrderAlreadyInvoiced = 10,
    OutcomeUnknown = 11,
}

public sealed record InvoiceIssueCommitResult(
    InvoiceIssueCommitStatus Status,
    InvoiceIssuedFacts? Invoice = null);

public interface IInvoiceIssueStore
{
    // Receipt lookup is intentionally separate so exact replay can return retained
    // historical facts without resolving mutable current Order/Customer data.
    Task<InvoiceIssueReceiptResult> FindReceiptAsync(
        TenantContext tenantContext,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken);

    // A future durable adapter must atomically allocate invoice identity/reference,
    // establish the authoritative timestamp/business date under accepted policy,
    // persist the candidate, and persist the caller-scoped receipt. No such adapter
    // is introduced by the host-neutral slice.
    Task<InvoiceIssueCommitResult> CommitAsync(
        TenantContext tenantContext,
        InvoiceIssueCandidate candidate,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken);
}

public sealed class InvoiceIssueValidationException(string code, string message)
    : ArgumentException(message)
{
    public string Code { get; } = code;
}

public static class InvoiceIssueIntent
{
    public static string NormalizeIdempotencyKey(string value) =>
        InvoiceIssueRules.NormalizeIdempotencyKey(value);

    public static string Fingerprint(IssueInvoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        InvoiceIssueRules.ValidateRequest(request);
        return InvoiceIssueRules.Fingerprint(request);
    }
}

public sealed class IssueInvoice(
    IInvoiceIssueAuthority authority,
    IInvoiceIssueStore invoiceStore,
    IInvoiceSourceOrderReader sourceOrderReader,
    GetCustomerOrganization getOrganization,
    GetCustomerProgram getProgram,
    GetCustomerIndividual getIndividual)
{
    public async Task<InvoiceIssueResult> ExecuteAsync(
        TenantContext tenantContext,
        IssueInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        InvoiceIssueRules.ValidateRequest(request);
        var normalizedKey = InvoiceIssueIntent.NormalizeIdempotencyKey(request.IdempotencyKey);
        var fingerprint = InvoiceIssueIntent.Fingerprint(request);

        var authorityStatus = await authority.CheckAsync(tenantContext, cancellationToken)
            .ConfigureAwait(false);
        if (authorityStatus != InvoiceIssueAuthorityStatus.Allowed)
        {
            return authorityStatus switch
            {
                InvoiceIssueAuthorityStatus.Denied => new(InvoiceIssueStatus.NotAuthorized),
                InvoiceIssueAuthorityStatus.Unavailable => new(InvoiceIssueStatus.DependencyUnavailable),
                _ => throw new InvalidOperationException("Invoice issue authority returned an unsupported status."),
            };
        }

        var receipt = await invoiceStore
            .FindReceiptAsync(tenantContext, normalizedKey, fingerprint, cancellationToken)
            .ConfigureAwait(false);
        var receiptOutcome = MapReceipt(receipt, tenantContext);
        if (receiptOutcome is not null)
        {
            return receiptOutcome;
        }

        var sourceResult = await sourceOrderReader
            .ReadAsync(tenantContext, request.OrderId, cancellationToken)
            .ConfigureAwait(false);
        if (sourceResult.Status == InvoiceSourceOrderReadStatus.Missing)
        {
            return new(InvoiceIssueStatus.SourceNotFound);
        }

        if (sourceResult.Status == InvoiceSourceOrderReadStatus.Unavailable)
        {
            return new(InvoiceIssueStatus.DependencyUnavailable);
        }

        if (sourceResult.Status == InvoiceSourceOrderReadStatus.Invalid || sourceResult.SourceOrder is null)
        {
            return new(InvoiceIssueStatus.InvalidCommittedOrderFacts);
        }

        if (!Enum.IsDefined(sourceResult.Status))
        {
            throw new InvalidOperationException("Invoice source reader returned an unsupported status.");
        }

        var order = sourceResult.SourceOrder;
        if (order.TenantId != tenantContext.TenantId || order.OrderId != request.OrderId)
        {
            return new(InvoiceIssueStatus.SourceNotFound);
        }

        if (!order.IsCommitted)
        {
            return new(InvoiceIssueStatus.OrderNotCommitted);
        }

        if (order.Revision != request.ExpectedOrderRevision)
        {
            return new(InvoiceIssueStatus.OrderRevisionConflict);
        }

        if (!string.Equals(order.CurrencyCode, InvoiceIssueRules.RequiredCurrencyCode, StringComparison.Ordinal))
        {
            return new(InvoiceIssueStatus.UnsupportedCurrency);
        }

        if (order.CustomerContext is null || order.CustomerContext.OrganizationId == Guid.Empty)
        {
            return new(InvoiceIssueStatus.InvalidDebtor);
        }

        if (order.CustomerContext.ProgramId == Guid.Empty)
        {
            return new(InvoiceIssueStatus.InvalidCommittedOrderFacts);
        }

        if (InvoiceIssueRules.ExceedsSourceBounds(order))
        {
            return new(InvoiceIssueStatus.ResourceLimit);
        }

        if (!InvoiceIssueRules.TryBuildLines(order, out var issuedLines, out var total))
        {
            return new(InvoiceIssueStatus.InvalidCommittedOrderFacts);
        }

        var debtorResult = await ResolveDebtorAsync(
            tenantContext,
            order.CustomerContext,
            request.Debtor,
            cancellationToken).ConfigureAwait(false);
        if (debtorResult.Result is not null)
        {
            return debtorResult.Result;
        }

        try
        {
            InvoiceIssueRules.ValidateDebtorFacts(debtorResult.Debtor!);
        }
        catch (InvoiceIssueValidationException)
        {
            return new(InvoiceIssueStatus.InvalidDebtor);
        }

        if (debtorResult.Debtor is InvoiceDebtorFacts.Organization organizationDebtor &&
            organizationDebtor.OrganizationId != order.CustomerContext.OrganizationId ||
            debtorResult.Debtor is InvoiceDebtorFacts.Program programDebtor &&
            (programDebtor.OrganizationId != order.CustomerContext.OrganizationId ||
             order.CustomerContext.ProgramId != programDebtor.ProgramId))
        {
            return new(InvoiceIssueStatus.InvalidDebtor);
        }

        var candidate = new InvoiceIssueCandidate(
            tenantContext.TenantId,
            order.OrderId,
            order.Revision,
            order.CustomerContext,
            issuedLines,
            total,
            debtorResult.Debtor!,
            tenantContext.AccountId);

        var commit = await invoiceStore
            .CommitAsync(tenantContext, candidate, normalizedKey, fingerprint, cancellationToken)
            .ConfigureAwait(false);
        return MapCommit(commit, candidate);
    }

    private static InvoiceIssueResult? MapReceipt(
        InvoiceIssueReceiptResult receipt,
        TenantContext tenantContext) =>
        receipt.Status switch
        {
            InvoiceIssueReceiptStatus.Missing => null,
            InvoiceIssueReceiptStatus.Replayed => new(
                InvoiceIssueStatus.Replayed,
                InvoiceIssueRules.RequireRetainedInvoice(receipt.Invoice, tenantContext)),
            InvoiceIssueReceiptStatus.IdempotencyKeyConflict => new(InvoiceIssueStatus.IdempotencyKeyConflict),
            InvoiceIssueReceiptStatus.Unavailable => new(InvoiceIssueStatus.DependencyUnavailable),
            InvoiceIssueReceiptStatus.HistoricalReceiptInvalid => new(InvoiceIssueStatus.HistoricalReceiptInvalid),
            InvoiceIssueReceiptStatus.NotAuthorized => new(InvoiceIssueStatus.NotAuthorized),
            _ => throw new InvalidOperationException("Invoice issue receipt lookup returned an unsupported status."),
        };

    private static InvoiceIssueResult MapCommit(
        InvoiceIssueCommitResult commit,
        InvoiceIssueCandidate candidate) =>
        commit.Status switch
        {
            InvoiceIssueCommitStatus.Issued => new(
                InvoiceIssueStatus.Issued,
                InvoiceIssueRules.RequireCommittedInvoice(commit.Invoice, candidate)),
            InvoiceIssueCommitStatus.Replayed => new(
                InvoiceIssueStatus.Replayed,
                InvoiceIssueRules.RequireRetainedInvoice(commit.Invoice, candidate.TenantId)),
            InvoiceIssueCommitStatus.IdempotencyKeyConflict => new(InvoiceIssueStatus.IdempotencyKeyConflict),
            InvoiceIssueCommitStatus.Unavailable => new(InvoiceIssueStatus.DependencyUnavailable),
            InvoiceIssueCommitStatus.HistoricalReceiptInvalid => new(InvoiceIssueStatus.HistoricalReceiptInvalid),
            InvoiceIssueCommitStatus.NotAuthorized => new(InvoiceIssueStatus.NotAuthorized),
            InvoiceIssueCommitStatus.InvalidSource => new(InvoiceIssueStatus.InvalidCommittedOrderFacts),
            InvoiceIssueCommitStatus.InvalidDebtor => new(InvoiceIssueStatus.InvalidDebtor),
            InvoiceIssueCommitStatus.ResourceLimit => new(InvoiceIssueStatus.ResourceLimit),
            InvoiceIssueCommitStatus.OrderAlreadyInvoiced => new(InvoiceIssueStatus.OrderAlreadyInvoiced),
            InvoiceIssueCommitStatus.OutcomeUnknown => new(InvoiceIssueStatus.OutcomeUnknown),
            _ => throw new InvalidOperationException("Invoice issue persistence returned an unsupported status."),
        };

    private async Task<(InvoiceDebtorFacts? Debtor, InvoiceIssueResult? Result)> ResolveDebtorAsync(
        TenantContext tenantContext,
        CustomerOrderContext? customerContext,
        InvoiceDebtorSelection? selection,
        CancellationToken cancellationToken)
    {
        switch (selection)
        {
            case null:
            case InvoiceDebtorSelection.DefaultOrganization:
                if (customerContext is null)
                {
                    return (null, new InvoiceIssueResult(InvoiceIssueStatus.InvalidDebtor));
                }

                var organization = await getOrganization
                    .ExecuteAsync(tenantContext, customerContext.OrganizationId, cancellationToken)
                    .ConfigureAwait(false);
                return organization is null ||
                    organization.TenantId != tenantContext.TenantId ||
                    organization.OrganizationId != customerContext.OrganizationId
                    ? (null, new InvoiceIssueResult(InvoiceIssueStatus.InvalidDebtor))
                    : (new InvoiceDebtorFacts.Organization(
                        organization.OrganizationId,
                        organization.DisplayName), null);

            case InvoiceDebtorSelection.AttributedProgram:
                if (customerContext?.ProgramId is not { } programId)
                {
                    return (null, new InvoiceIssueResult(InvoiceIssueStatus.InvalidDebtor));
                }

                var program = await getProgram.ExecuteAsync(tenantContext, programId, cancellationToken)
                    .ConfigureAwait(false);
                if (program is null || program.TenantId != tenantContext.TenantId ||
                    program.ProgramId != programId || program.OrganizationId != customerContext.OrganizationId)
                {
                    return (null, new InvoiceIssueResult(InvoiceIssueStatus.InvalidDebtor));
                }

                var programOrganization = await getOrganization
                    .ExecuteAsync(tenantContext, customerContext.OrganizationId, cancellationToken)
                    .ConfigureAwait(false);
                return programOrganization is null ||
                    programOrganization.TenantId != tenantContext.TenantId ||
                    programOrganization.OrganizationId != customerContext.OrganizationId
                    ? (null, new InvoiceIssueResult(InvoiceIssueStatus.InvalidDebtor))
                    : (new InvoiceDebtorFacts.Program(
                        programOrganization.OrganizationId,
                        programOrganization.DisplayName,
                        program.ProgramId,
                        program.DisplayName), null);

            case InvoiceDebtorSelection.Individual individualSelection:
                var individual = await getIndividual
                    .ExecuteAsync(tenantContext, individualSelection.IndividualId, cancellationToken)
                    .ConfigureAwait(false);
                if (individual is null || individual.TenantId != tenantContext.TenantId ||
                    individual.IndividualId != individualSelection.IndividualId)
                {
                    return (null, new InvoiceIssueResult(InvoiceIssueStatus.InvalidDebtor));
                }

                if (individual.RedirectTargetIndividualId is not null)
                {
                    return (null, new InvoiceIssueResult(InvoiceIssueStatus.InvalidDebtor));
                }

                if (individual.Availability == CustomerIndividualAvailability.Inactive)
                {
                    return (null, new InvoiceIssueResult(InvoiceIssueStatus.InvalidDebtor));
                }

                if (individual.Availability != CustomerIndividualAvailability.Active)
                {
                    throw new InvalidOperationException("Customer individual query returned an unsupported availability.");
                }

                return (new InvoiceDebtorFacts.Individual(
                    individual.IndividualId,
                    individual.Revision,
                    individual.DisplayName), null);

            default:
                throw new InvalidOperationException("Invoice debtor selection has an unsupported kind.");
        }
    }
}

internal static class InvoiceIssueRules
{
    internal const string RequiredCurrencyCode = "NPR";
    private const decimal MaximumDecimal19Scale4 = 999_999_999_999_999.9999m;

    internal static void ValidateRequest(IssueInvoiceRequest request)
    {
        RequireIdentity(request.OrderId, "order_id_invalid");
        if (request.ExpectedOrderRevision < 1)
        {
            throw new InvoiceIssueValidationException(
                "expected_order_revision_invalid",
                "Expected Order revision must be positive.");
        }

        if (request.Debtor is InvoiceDebtorSelection.Individual individual)
        {
            RequireIdentity(individual.IndividualId, "individual_id_invalid");
        }
    }

    internal static string NormalizeIdempotencyKey(string value)
    {
        if (value is null)
        {
            throw new InvoiceIssueValidationException(
                "idempotency_key_invalid",
                "Idempotency key is required.");
        }

        if (!HasWellFormedUtf16(value))
        {
            throw new InvoiceIssueValidationException(
                "idempotency_key_invalid",
                "Idempotency key contains malformed text.");
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length is < 1 or > 128 || normalized.Any(char.IsControl))
        {
            throw new InvoiceIssueValidationException(
                "idempotency_key_invalid",
                "Idempotency key must contain 1 to 128 non-control characters.");
        }

        return normalized;
    }

    internal static string Fingerprint(IssueInvoiceRequest request)
    {
        var canonical = new StringBuilder("v1:issue-invoice:")
            .Append(request.OrderId.ToString("N", CultureInfo.InvariantCulture))
            .Append(':')
            .Append(request.ExpectedOrderRevision.ToString(CultureInfo.InvariantCulture))
            .Append(':');

        switch (request.Debtor)
        {
            case null:
            case InvoiceDebtorSelection.DefaultOrganization:
                canonical.Append("organization");
                break;
            case InvoiceDebtorSelection.AttributedProgram:
                canonical.Append("program");
                break;
            case InvoiceDebtorSelection.Individual individual:
                canonical.Append("individual:")
                    .Append(individual.IndividualId.ToString("N", CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException("Invoice debtor selection has an unsupported kind.");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    internal static bool AreValidIssuedLines(
        IReadOnlyList<InvoiceIssuedLine> lines,
        decimal total)
    {
        if (lines.Count == 0 || total < 0m || !HasSupportedPrecisionAndRange(total))
        {
            return false;
        }

        var positions = new HashSet<int>();
        var sum = 0m;
        try
        {
            foreach (var line in lines)
            {
                if (line is null || line.Position < 1 || !positions.Add(line.Position) ||
                    line.Quantity <= 0m || line.UnitPrice < 0m ||
                    !IsValidLineText(line.Description, 300) || !IsValidLineText(line.UnitCode, 64) ||
                    !HasSupportedPrecisionAndRange(line.Quantity) ||
                    !HasSupportedPrecisionAndRange(line.UnitPrice) ||
                    !HasSupportedPrecisionAndRange(line.LineTotal))
                {
                    return false;
                }

                var calculated = Math.Round(
                    checked(line.Quantity * line.UnitPrice),
                    4,
                    MidpointRounding.ToEven);
                if (!HasSupportedPrecisionAndRange(calculated) || calculated != line.LineTotal)
                {
                    return false;
                }

                sum = checked(sum + calculated);
                if (!HasSupportedPrecisionAndRange(sum))
                {
                    return false;
                }
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        return sum == total;
    }

    internal static void ValidateDebtorFacts(InvoiceDebtorFacts debtor)
    {
        var expectedKind = debtor switch
        {
            InvoiceDebtorFacts.Organization => InvoiceDebtorKind.Organization,
            InvoiceDebtorFacts.Program => InvoiceDebtorKind.Program,
            InvoiceDebtorFacts.Individual => InvoiceDebtorKind.Individual,
            _ => throw new InvoiceIssueValidationException("debtor_invalid", "Retained debtor facts have an unsupported kind."),
        };
        if (debtor.Kind != expectedKind)
        {
            throw new InvoiceIssueValidationException("debtor_kind_invalid", "Retained debtor kind does not match its facts.");
        }

        switch (debtor)
        {
            case InvoiceDebtorFacts.Organization organization:
                RequireIdentity(organization.OrganizationId, "organization_id_invalid");
                RequireRetainedDisplayName(organization.OrganizationDisplayName);
                return;
            case InvoiceDebtorFacts.Program program:
                RequireIdentity(program.OrganizationId, "organization_id_invalid");
                RequireIdentity(program.ProgramId, "program_id_invalid");
                RequireRetainedDisplayName(program.OrganizationDisplayName);
                RequireRetainedDisplayName(program.ProgramDisplayName);
                return;
            case InvoiceDebtorFacts.Individual individual:
                RequireIdentity(individual.IndividualId, "individual_id_invalid");
                if (individual.ObservedRevision < 1)
                {
                    throw new InvoiceIssueValidationException(
                        "individual_revision_invalid",
                        "Retained individual revision must be positive.");
                }
                RequireRetainedDisplayName(individual.DisplayName);
                return;
            default:
                throw new InvoiceIssueValidationException(
                    "debtor_invalid",
                    "Retained debtor facts have an unsupported kind.");
        }
    }

    internal static bool TryBuildLines(
        InvoiceSourceOrderFacts order,
        out InvoiceIssuedLine[] lines,
        out decimal total)
    {
        lines = [];
        total = 0m;
        if (order.Lines.Count == 0 || order.Total < 0m || !HasSupportedPrecisionAndRange(order.Total))
        {
            return false;
        }

        var positions = new HashSet<int>();
        var projected = new InvoiceIssuedLine[order.Lines.Count];
        try
        {
            for (var index = 0; index < order.Lines.Count; index++)
            {
                var source = order.Lines[index];
                if (source is null || source.Position < 1 || !positions.Add(source.Position) ||
                    !IsValidLineText(source.Description, 300) ||
                    !IsValidLineText(source.UnitCode, 64) ||
                    source.Quantity <= 0m || source.UnitPrice < 0m ||
                    !HasSupportedPrecisionAndRange(source.Quantity) ||
                    !HasSupportedPrecisionAndRange(source.UnitPrice) ||
                    !HasSupportedPrecisionAndRange(source.LineTotal))
                {
                    return false;
                }

                var lineTotal = Math.Round(
                    checked(source.Quantity * source.UnitPrice),
                    4,
                    MidpointRounding.ToEven);
                if (!HasSupportedPrecisionAndRange(lineTotal) || lineTotal != source.LineTotal)
                {
                    return false;
                }

                total = checked(total + lineTotal);
                if (!HasSupportedPrecisionAndRange(total))
                {
                    return false;
                }

                projected[index] = new InvoiceIssuedLine(
                    source.Position,
                    source.Description,
                    source.Quantity,
                    source.UnitCode,
                    source.UnitPrice,
                    lineTotal);
            }
        }
        catch (OverflowException)
        {
            lines = [];
            total = 0m;
            return false;
        }

        if (total != order.Total)
        {
            lines = [];
            total = 0m;
            return false;
        }

        lines = projected;
        return true;
    }

    internal static bool ExceedsSourceBounds(InvoiceSourceOrderFacts order) =>
        order.Lines.Count > 100 || order.Lines.Any(line => line is not null &&
            (line.Description?.Length > 300 || line.UnitCode?.Length > 64));

    internal static bool AreWithinRetainedBounds(
        IReadOnlyList<InvoiceIssuedLine> lines,
        InvoiceDebtorFacts debtor) =>
        lines.Count is >= 1 and <= 100 &&
        lines.All(line => line is not null && line.Description?.Length <= 300 && line.UnitCode?.Length <= 64) &&
        debtor switch
        {
            InvoiceDebtorFacts.Organization organization => organization.OrganizationDisplayName is { Length: <= 200 },
            InvoiceDebtorFacts.Program program =>
                program.OrganizationDisplayName is { Length: <= 200 } &&
                program.ProgramDisplayName is { Length: <= 200 },
            InvoiceDebtorFacts.Individual individual => individual.DisplayName is { Length: <= 200 },
            _ => false,
        };

    internal static bool HasValidRetainedLineText(IReadOnlyList<InvoiceIssuedLine> lines) =>
        lines.All(line => line is not null &&
            IsValidLineText(line.Description, 300) &&
            IsValidLineText(line.UnitCode, 64));

    internal static InvoiceIssuedFacts RequireRetainedInvoice(
        InvoiceIssuedFacts? invoice,
        TenantContext tenantContext) =>
        RequireRetainedInvoice(invoice, tenantContext.TenantId);

    internal static InvoiceIssuedFacts RequireRetainedInvoice(
        InvoiceIssuedFacts? invoice,
        Guid tenantId)
    {
        if (invoice is null || invoice.TenantId != tenantId)
        {
            throw new InvalidOperationException("The invoice issue store returned an invalid historical receipt.");
        }

        return invoice;
    }

    internal static InvoiceIssuedFacts RequireCommittedInvoice(
        InvoiceIssuedFacts? invoice,
        InvoiceIssueCandidate candidate)
    {
        if (invoice is null ||
            invoice.TenantId != candidate.TenantId ||
            invoice.SourceOrderId != candidate.SourceOrderId ||
            invoice.SourceOrderRevision != candidate.SourceOrderRevision ||
            invoice.SourceCustomerContext != candidate.SourceCustomerContext ||
            !string.Equals(invoice.CurrencyCode, candidate.CurrencyCode, StringComparison.Ordinal) ||
            invoice.Total != candidate.Total ||
            invoice.Debtor != candidate.Debtor ||
            invoice.IssuedByAccountId != candidate.IssuedByAccountId ||
            !invoice.Lines.SequenceEqual(candidate.Lines))
        {
            throw new InvalidOperationException("The invoice issue store changed authoritative issue facts.");
        }

        return invoice;
    }

    private static void RequireRetainedDisplayName(string value)
    {
        if (!IsValidLineText(value, 200))
        {
            throw new InvoiceIssueValidationException(
                "debtor_display_name_invalid",
                "Retained debtor display name is invalid.");
        }
    }

    internal static void RequireIdentity(Guid identity, string code)
    {
        if (identity == Guid.Empty)
        {
            throw new InvoiceIssueValidationException(code, "Identity cannot be empty.");
        }
    }

    internal static bool IsUtcTimestamp(DateTimeOffset timestamp) =>
        timestamp != default && timestamp.Offset == TimeSpan.Zero;

    private static bool IsValidLineText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        HasWellFormedUtf16(value) &&
        !value.Any(char.IsControl);

    internal static bool HasWellFormedUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[++index]))
                {
                    return false;
                }
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSupportedPrecisionAndRange(decimal value) =>
        value <= MaximumDecimal19Scale4 &&
        value >= -MaximumDecimal19Scale4 &&
        GetScale(value) <= 4;

    private static int GetScale(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0x7F;

}

public enum InvoiceIssueSourceValidationStatus
{
    Valid = 1,
    Invalid = 2,
}

public static class InvoiceIssueSourceValidation
{
    public static InvoiceIssueSourceValidationStatus Validate(
        TenantContext tenantContext,
        InvoiceIssueCandidate candidate,
        InvoiceSourceOrderFacts source)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(source);

        if (source.TenantId != tenantContext.TenantId ||
            source.TenantId != candidate.TenantId ||
            source.OrderId != candidate.SourceOrderId ||
            source.Revision != candidate.SourceOrderRevision ||
            !source.IsCommitted ||
            !string.Equals(source.CurrencyCode, candidate.CurrencyCode, StringComparison.Ordinal) ||
            source.CustomerContext is null ||
            source.CustomerContext != candidate.SourceCustomerContext ||
            InvoiceIssueRules.ExceedsSourceBounds(source) ||
            !InvoiceIssueRules.TryBuildLines(source, out var lines, out var total) ||
            total != candidate.Total ||
            !lines.SequenceEqual(candidate.Lines))
        {
            return InvoiceIssueSourceValidationStatus.Invalid;
        }

        return InvoiceIssueSourceValidationStatus.Valid;
    }
}

public enum InvoiceIssueDebtorValidationStatus
{
    Valid = 1,
    Invalid = 2,
}

public abstract record InvoiceDebtorObservedFacts
{
    private InvoiceDebtorObservedFacts()
    {
    }

    public sealed record Organization(Guid TenantId, Guid OrganizationId, string DisplayName)
        : InvoiceDebtorObservedFacts;

    public sealed record Program(
        Guid TenantId,
        Guid OrganizationId,
        string OrganizationDisplayName,
        Guid ProgramId,
        string DisplayName)
        : InvoiceDebtorObservedFacts;

    public sealed record Individual(
        Guid TenantId,
        Guid IndividualId,
        long Revision,
        string DisplayName,
        bool IsActive)
        : InvoiceDebtorObservedFacts;
}

public static class InvoiceIssueDebtorValidation
{
    public static InvoiceIssueDebtorValidationStatus ValidateObservedCandidate(
        TenantContext tenantContext,
        InvoiceIssueCandidate candidate,
        InvoiceDebtorObservedFacts? observed)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(candidate);
        var sourceOrganizationId = candidate.SourceCustomerContext?.OrganizationId;
        return (candidate.Debtor, observed) switch
        {
            (InvoiceDebtorFacts.Organization debtor, InvoiceDebtorObservedFacts.Organization current) =>
                current.TenantId == tenantContext.TenantId &&
                current.OrganizationId == sourceOrganizationId &&
                current.OrganizationId == debtor.OrganizationId &&
                string.Equals(current.DisplayName, debtor.OrganizationDisplayName, StringComparison.Ordinal)
                    ? InvoiceIssueDebtorValidationStatus.Valid
                    : InvoiceIssueDebtorValidationStatus.Invalid,
            (InvoiceDebtorFacts.Program debtor, InvoiceDebtorObservedFacts.Program current) =>
                current.TenantId == tenantContext.TenantId &&
                current.OrganizationId == sourceOrganizationId &&
                current.OrganizationId == debtor.OrganizationId &&
                candidate.SourceCustomerContext?.ProgramId == current.ProgramId &&
                current.ProgramId == debtor.ProgramId &&
                string.Equals(current.OrganizationDisplayName, debtor.OrganizationDisplayName, StringComparison.Ordinal) &&
                string.Equals(current.DisplayName, debtor.ProgramDisplayName, StringComparison.Ordinal)
                    ? InvoiceIssueDebtorValidationStatus.Valid
                    : InvoiceIssueDebtorValidationStatus.Invalid,
            (InvoiceDebtorFacts.Individual debtor, InvoiceDebtorObservedFacts.Individual current) =>
                current.TenantId == tenantContext.TenantId &&
                current.IsActive &&
                current.IndividualId == debtor.IndividualId &&
                current.Revision == debtor.ObservedRevision &&
                string.Equals(current.DisplayName, debtor.DisplayName, StringComparison.Ordinal)
                    ? InvoiceIssueDebtorValidationStatus.Valid
                    : InvoiceIssueDebtorValidationStatus.Invalid,
            _ => InvoiceIssueDebtorValidationStatus.Invalid,
        };
    }
}
