using Application.Customers.Postgres;
using Application.Invoices;
using Application.Invoices.Postgres;
using Application.Tenancy;
using Npgsql;

namespace Application.CoreApi.Composition;

internal sealed class FinancialDebtorReader : IFinancialDebtorReader
{
    public async Task<bool> ExistsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, FinancialDebtor debtor, CancellationToken cancellationToken) => debtor.Kind switch
        {
            InvoiceDebtorKind.Organization => await CustomerBillingTransactionReader.ReadOrganizationAsync(connection, transaction,
                context, debtor.Id, cancellationToken).ConfigureAwait(false) is not null,
            InvoiceDebtorKind.Program => await CustomerBillingTransactionReader.ReadProgramAsync(connection, transaction,
                context, debtor.Id, cancellationToken).ConfigureAwait(false) is not null,
            InvoiceDebtorKind.Individual => await CustomerBillingTransactionReader.ReadIndividualAsync(connection, transaction,
                context, debtor.Id, cancellationToken).ConfigureAwait(false) is not null,
            _ => false,
        };
}
