using Application.Tenancy;
using Npgsql;

namespace Application.Invoices.Postgres;

public interface IFinancialTransactionalAuthorityReader
{
    Task<FinancialAuthority> CheckAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, FinancialCapability permission, CancellationToken cancellationToken);
}

public interface IFinancialDebtorReader
{
    Task<bool> ExistsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, FinancialDebtor debtor, CancellationToken cancellationToken);
}
