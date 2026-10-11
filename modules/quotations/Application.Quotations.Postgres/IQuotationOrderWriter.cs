using Application.Orders;
using Application.Tenancy;
using Npgsql;

namespace Application.Quotations.Postgres;

public interface IQuotationOrderWriter
{
    Task<OrderDraftSnapshot> CreateAsync(TenantContext context, AcceptedQuotationOrder accepted,
        NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset createdAt, CancellationToken ct);
}
