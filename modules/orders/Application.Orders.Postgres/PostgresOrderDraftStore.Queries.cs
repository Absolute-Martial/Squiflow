using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    private static async Task<(OrderDraftState State, long Revision)?> FindOrderStateAsync(
        OrderTenantDbSession session,
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(OrderSql.FindOrderState);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("order_id", orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (ReadState(reader.GetString(reader.GetOrdinal("state"))),
                reader.GetInt64(reader.GetOrdinal("revision")))
            : null;
    }

    public async Task<OrderDraftSnapshot?> FindAsync(
        TenantContext tenantContext,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order identity cannot be empty.", nameof(orderId));
        }

        await using var session = await OrderTenantDbSession
            .OpenAsync(dataSource, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var order = await FindOrderAsync(
                session,
                tenantContext.TenantId,
                orderId,
                cancellationToken)
            .ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return order;
    }

    public async Task<OrderDraftPage> ListAsync(
        TenantContext tenantContext,
        ListOrderDraftsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Page size must be between 1 and 50.");
        }

        await using var session = await OrderTenantDbSession
            .OpenAsync(dataSource, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var items = await ListOrderHeadersAsync(
                session,
                tenantContext.TenantId,
                request,
                cancellationToken)
            .ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);

        var hasMore = items.Count > request.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var nextCursor = hasMore
            ? new OrderDraftPageCursor(items[^1].CreatedAt, items[^1].OrderId)
            : null;
        return new OrderDraftPage(items, nextCursor);
    }

    private static async Task<OrderDraftSnapshot?> FindOrderAsync(
        OrderTenantDbSession session,
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        OrderDraftHeader? header;
        await using (var command = session.CreateCommand(OrderSql.FindOrder))
        {
            command.Parameters.AddWithValue("id", orderId);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            header = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? ReadHeader(reader)
                : null;
        }

        if (header is null)
        {
            return null;
        }

        var lines = new List<OrderDraftLine>();
        await using (var command = session.CreateCommand(OrderSql.FindLines))
        {
            command.Parameters.AddWithValue("order_id", orderId);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(ReadLine(reader));
            }
        }

        return new OrderDraftSnapshot(
            header.OrderId,
            header.TenantId,
            header.CreatedByAccountId,
            header.Summary,
            header.CurrencyCode,
            header.Total,
            header.Revision,
            header.CreatedAt,
            lines,
            header.State,
            header.AbandonedAt,
            header.AbandonedByAccountId);
    }

    private static async Task<List<OrderDraftListItem>> ListOrderHeadersAsync(
        OrderTenantDbSession session,
        Guid tenantId,
        ListOrderDraftsRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(OrderSql.ListOrderHeaders);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.Add(new NpgsqlParameter("after_created_at", NpgsqlDbType.TimestampTz)
        {
            Value = (object?)request.After?.CreatedAt ?? DBNull.Value,
        });
        command.Parameters.Add(new NpgsqlParameter("after_id", NpgsqlDbType.Uuid)
        {
            Value = (object?)request.After?.OrderId ?? DBNull.Value,
        });
        command.Parameters.AddWithValue("limit", request.Limit + 1);

        var items = new List<OrderDraftListItem>(request.Limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(ReadListItem(reader));
        }

        return items;
    }

    private static OrderDraftHeader ReadHeader(NpgsqlDataReader reader)
    {
        var abandonedAt = reader.GetOrdinal("abandoned_at");
        var abandonedByAccountId = reader.GetOrdinal("abandoned_by_account_id");
        return new OrderDraftHeader(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetGuid(reader.GetOrdinal("tenant_id")),
            reader.GetGuid(reader.GetOrdinal("created_by_account_id")),
            reader.GetString(reader.GetOrdinal("summary")),
            reader.GetString(reader.GetOrdinal("currency_code")).TrimEnd(),
            reader.GetDecimal(reader.GetOrdinal("total")),
            reader.GetInt64(reader.GetOrdinal("revision")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            ReadState(reader.GetString(reader.GetOrdinal("state"))),
            reader.IsDBNull(abandonedAt) ? null : reader.GetFieldValue<DateTimeOffset>(abandonedAt),
            reader.IsDBNull(abandonedByAccountId) ? null : reader.GetGuid(abandonedByAccountId));
    }

    private static OrderDraftLine ReadLine(NpgsqlDataReader reader) => new(
        reader.GetInt32(reader.GetOrdinal("position")),
        reader.GetString(reader.GetOrdinal("description")),
        reader.GetDecimal(reader.GetOrdinal("quantity")),
        reader.GetString(reader.GetOrdinal("unit_code")),
        reader.GetDecimal(reader.GetOrdinal("unit_price")),
        reader.GetDecimal(reader.GetOrdinal("line_total")));

    private static OrderDraftListItem ReadListItem(NpgsqlDataReader reader)
    {
        var abandonedAt = reader.GetOrdinal("abandoned_at");
        return new OrderDraftListItem(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("summary")),
            reader.GetString(reader.GetOrdinal("currency_code")).TrimEnd(),
            reader.GetDecimal(reader.GetOrdinal("total")),
            reader.GetInt64(reader.GetOrdinal("revision")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            ReadState(reader.GetString(reader.GetOrdinal("state"))),
            reader.IsDBNull(abandonedAt) ? null : reader.GetFieldValue<DateTimeOffset>(abandonedAt));
    }

    private static OrderDraftState ReadState(string state) => state switch
    {
        "draft" => OrderDraftState.Draft,
        "abandoned" => OrderDraftState.Abandoned,
        _ => throw new InvalidOperationException("The order draft has an unsupported stored state."),
    };

    private sealed record OrderDraftHeader(
        Guid OrderId,
        Guid TenantId,
        Guid CreatedByAccountId,
        string Summary,
        string CurrencyCode,
        decimal Total,
        long Revision,
        DateTimeOffset CreatedAt,
        OrderDraftState State,
        DateTimeOffset? AbandonedAt,
        Guid? AbandonedByAccountId);
}
