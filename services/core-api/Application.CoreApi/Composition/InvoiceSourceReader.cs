using System.Text.Json;
using Application.Customers;
using Application.Customers.Postgres;
using Application.Invoices;
using Application.Invoices.Postgres;
using Application.Orders.Postgres;
using Application.Tenancy;
using Npgsql;

namespace Application.CoreApi.Composition;

internal sealed class InvoiceSourceReader(NpgsqlDataSource source, PostgresOrderDraftStore orders)
    : IInvoiceSourceOrderReader, IInvoiceIssueTransactionalSourceReader
{
    public async Task<InvoiceSourceOrderReadResult> ReadAsync(TenantContext context, Guid orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var result = await ReadOrderAsync(connection, transaction, context, orderId, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(InvoiceSourceOrderReadStatus.Unavailable);
        }
    }

    public async Task<InvoiceSourceOrderReadResult> ReadOrderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await orders.ReadFinancialSourceInTransactionAsync(connection, transaction, context, orderId,
                cancellationToken).ConfigureAwait(false);
            return result is null ? new(InvoiceSourceOrderReadStatus.Missing) : new(InvoiceSourceOrderReadStatus.Found,
                new(result.TenantId, result.OrderId, result.Revision, result.IsCommitted, result.CurrencyCode, result.CustomerContext,
                    result.Lines.Select(line => new InvoiceSourceLine(line.Position, line.Description, line.Quantity,
                        line.UnitCode, line.UnitPrice, line.LineTotal)), result.Total));
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or JsonException)
        {
            return new(InvoiceSourceOrderReadStatus.Invalid);
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(InvoiceSourceOrderReadStatus.Unavailable);
        }
    }

    public async Task<InvoiceIssueDebtorObservationStatus> ValidateDebtorAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, TenantContext context, InvoiceIssueCandidate candidate, CancellationToken cancellationToken)
    {
        try
        {
            InvoiceDebtorObservedFacts? observed = null;
            switch (candidate.Debtor)
            {
                case InvoiceDebtorFacts.Organization debtor:
                    var organization = await CustomerBillingTransactionReader.ReadOrganizationAsync(connection, transaction,
                        context, debtor.OrganizationId, cancellationToken).ConfigureAwait(false);
                    if (organization is not null)
                        observed = new InvoiceDebtorObservedFacts.Organization(organization.TenantId, organization.OrganizationId,
                            organization.DisplayName);
                    break;
                case InvoiceDebtorFacts.Program debtor:
                    var parent = await CustomerBillingTransactionReader.ReadOrganizationAsync(connection, transaction,
                        context, debtor.OrganizationId, cancellationToken).ConfigureAwait(false);
                    var program = await CustomerBillingTransactionReader.ReadProgramAsync(connection, transaction,
                        context, debtor.ProgramId, cancellationToken).ConfigureAwait(false);
                    if (parent is not null && program is not null && parent.OrganizationId == program.OrganizationId)
                        observed = new InvoiceDebtorObservedFacts.Program(program.TenantId, parent.OrganizationId,
                            parent.DisplayName, program.ProgramId, program.DisplayName);
                    break;
                case InvoiceDebtorFacts.Individual debtor:
                    var individual = await CustomerBillingTransactionReader.ReadIndividualAsync(connection, transaction,
                        context, debtor.IndividualId, cancellationToken).ConfigureAwait(false);
                    if (individual is not null)
                        observed = new InvoiceDebtorObservedFacts.Individual(individual.TenantId, individual.IndividualId,
                            individual.Revision, individual.DisplayName, individual.Availability == CustomerIndividualAvailability.Active);
                    break;
            }
            return InvoiceIssueDebtorValidation.ValidateObservedCandidate(context, candidate, observed) == InvoiceIssueDebtorValidationStatus.Valid
                ? InvoiceIssueDebtorObservationStatus.Valid : InvoiceIssueDebtorObservationStatus.Invalid;
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or JsonException)
        {
            return InvoiceIssueDebtorObservationStatus.Invalid;
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return InvoiceIssueDebtorObservationStatus.Unavailable;
        }
    }
}
