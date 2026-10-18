using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Application.Invoices.Postgres;

public interface IInvoiceIssueTransactionalAuthority
{
    Task<InvoiceIssueAuthorityStatus> CheckAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, CancellationToken cancellationToken);

    Task<InvoiceIssueAuthorityStatus> CheckViewAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, CancellationToken cancellationToken);
}

public enum InvoiceIssueDebtorObservationStatus { Valid = 1, Invalid = 2, Unavailable = 3 }

public interface IInvoiceIssueTransactionalSourceReader
{
    Task<InvoiceSourceOrderReadResult> ReadOrderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, Guid orderId, CancellationToken cancellationToken);

    Task<InvoiceIssueDebtorObservationStatus> ValidateDebtorAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, InvoiceIssueCandidate candidate, CancellationToken cancellationToken);
}

public sealed class InvoicesDbContext(DbContextOptions<InvoicesDbContext> options) : DbContext(options);

public static class InvoicesPostgresRegistration
{
    public static InvoicesDbContext CreateContext(string connectionString) => new(new DbContextOptionsBuilder<InvoicesDbContext>()
        .UseNpgsql(connectionString, provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "invoices")).Options);

    public static IServiceCollection AddInvoicesPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PostgresInvoiceIssueStore>();
        services.AddScoped<IInvoiceIssueStore>(provider => provider.GetRequiredService<PostgresInvoiceIssueStore>());
        services.AddScoped<IInvoiceIssuedFactsReader>(provider => provider.GetRequiredService<PostgresInvoiceIssueStore>());
        services.AddScoped<ICustomerSettlementStore, PostgresCustomerSettlementStore>();
        return services;
    }
}
