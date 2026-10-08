using Application.Tenancy;
using Npgsql;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    private const string QuotationCreateOperation = "create-quotation-order";
    // The quotation owner serializes conversion; this writer owns Order SQL in its caller's transaction.
    public static async Task<OrderDraftSnapshot> CreateAcceptedQuotationAsync(TenantContext context, AcceptedQuotationOrder accepted,
        NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset createdAt, CancellationToken ct, OrderProgramPolicyFacts? programPolicy = null)
    {
        if (createdAt.Offset != TimeSpan.Zero || createdAt.Ticks % 10 != 0)
            throw new InvalidOperationException("The caller must supply a UTC Order creation instant at database precision.");
        accepted.RequireValid(context.TenantId);
        await using var session = await OrderTenantDbSession.BorrowAsync(connection, transaction, context.TenantId, ct).ConfigureAwait(false);
        var order = new OrderDraftSnapshot(Guid.CreateVersion7(), context.TenantId, context.AccountId, accepted.Summary, accepted.CurrencyCode,
            accepted.Total, 1, createdAt, accepted.Lines, CustomerContext: accepted.CustomerContext, QuotationOrigin: accepted.Origin, ProgramPolicy: programPolicy);
        await InsertOrderAsync(session, order, ct).ConfigureAwait(false);
        await InsertLinesAsync(session, order, ct).ConfigureAwait(false);
        await InsertProgramPolicyAsync(session, order, ct).ConfigureAwait(false);
        await using (var command = session.CreateCommand(OrderSql.InsertQuotationOrigin))
        {
            command.Parameters.AddWithValue("tenant_id", context.TenantId); command.Parameters.AddWithValue("order_id", order.OrderId);
            command.Parameters.AddWithValue("quotation_id", accepted.Origin.QuotationId);
            command.Parameters.AddWithValue("issued_revision_id", accepted.Origin.IssuedRevisionId);
            command.Parameters.AddWithValue("number", accepted.Origin.Number); command.Parameters.AddWithValue("revision_number", accepted.Origin.RevisionNumber);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(accepted))).ToLowerInvariant();
        if (!await TryInsertReceiptAsync(session, context.AccountId, QuotationCreateOperation, accepted.Origin.IssuedRevisionId.ToString("D"),
            fingerprint, order, createdAt, ct).ConfigureAwait(false))
            throw new InvalidOperationException("An accepted quotation already has an Order creation receipt.");
        return order;
    }
    private async Task RequireAcceptedQuotationFactsAsync(TenantContext context, OrderDraftSnapshot order, CancellationToken ct)
    {
        if (order.QuotationOrigin is not null && (commercialCommitGuard is null ||
            !await commercialCommitGuard.IsCompatibleAsync(context, order, ct).ConfigureAwait(false)))
            throw new InvalidOperationException("The quoted Order does not match its trusted accepted offer and conversion link.");
    }
    private static async Task<OrderQuotationOrigin?> ReadQuotationOriginAsync(OrderTenantDbSession session, Guid tenantId, Guid orderId, CancellationToken ct)
    {
        await using var command = session.CreateCommand(OrderSql.FindQuotationOrigin);
        command.Parameters.AddWithValue("tenant_id", tenantId); command.Parameters.AddWithValue("order_id", orderId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetInt64(3)) : null;
    }
}
