using Application.Orders;
using Application.Orders.Postgres;
using Application.Quotations.Postgres;
using Application.Tenancy;
using Npgsql;

namespace Application.CoreApi.Composition;

internal sealed class QuotationOrderWriter(IOrderProfilePolicySource profilePolicySource) : IQuotationOrderWriter
{
    public async Task<OrderDraftSnapshot> CreateAsync(TenantContext context, AcceptedQuotationOrder accepted,
        NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset createdAt, CancellationToken ct) =>
        await PostgresOrderDraftStore.CreateAcceptedQuotationAsync(context, accepted, connection, transaction, createdAt, ct,
            await profilePolicySource.ResolveActiveAsync(connection, transaction, context.TenantId, ct).ConfigureAwait(false)
                ?? throw new OrderProfileUnavailableException()).ConfigureAwait(false);
}
