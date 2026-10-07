using Application.Orders;
using Application.Orders.Postgres;
using Application.Quotations.Postgres;
using Application.Tenancy;
using Npgsql;

namespace Application.CoreApi.Composition;

internal sealed class QuotationOrderWriter : IQuotationOrderWriter
{
    public Task<OrderDraftSnapshot> CreateAsync(TenantContext context, AcceptedQuotationOrder accepted,
        NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset createdAt, CancellationToken ct) =>
        PostgresOrderDraftStore.CreateAcceptedQuotationAsync(context, accepted, connection, transaction, createdAt, ct);
}
