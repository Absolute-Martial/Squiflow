using Application.Tenancy;

namespace Application.Invoices;

public enum InvoiceReadStatus
{
    Found = 1,
    Missing = 2,
    NotAuthorized = 3,
    Unavailable = 4,
    HistoricalInvalid = 5,
}

public sealed record InvoiceReadResult(InvoiceReadStatus Status, InvoiceIssuedFacts? Invoice = null);

public interface IInvoiceViewAuthority
{
    Task<InvoiceIssueAuthorityStatus> CheckAsync(TenantContext context, CancellationToken cancellationToken);
}

public interface IInvoiceIssuedFactsReader
{
    // Each lookup must establish current active membership and the independent invoice-view
    // permission before disclosing retained facts; source Order/Customer queries cannot rebuild them.
    Task<InvoiceReadResult> FindByIdAsync(TenantContext context, Guid invoiceId, CancellationToken cancellationToken);
    Task<InvoiceReadResult> FindByOrderAsync(TenantContext context, Guid orderId, CancellationToken cancellationToken);
}
