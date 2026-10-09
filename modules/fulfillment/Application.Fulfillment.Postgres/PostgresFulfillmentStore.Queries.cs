using System.Data;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Fulfillment.Postgres;

public sealed partial class PostgresFulfillmentStore
{
    public async Task<FulfillmentSnapshot?> FindAsync(TenantContext context, Guid orderId, CancellationToken cancellationToken)
    {
        await RequireViewAsync(context, cancellationToken).ConfigureAwait(false);
        if (orderId == Guid.Empty) throw new FulfillmentValidationException("fulfillment_request_invalid");
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        return await ReadWorkAsync(connection, transaction, context.TenantId, orderId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FulfillmentPage> ListAsync(TenantContext context, FulfillmentListRequest request, CancellationToken cancellationToken)
    {
        await RequireViewAsync(context, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Limit is < 1 or > 50 || request.After is { } cursor &&
            (cursor.CreatedAt == default || cursor.CreatedAt.Offset != TimeSpan.Zero || cursor.OrderId == Guid.Empty))
            throw new FulfillmentValidationException("fulfillment_page_invalid");
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, transaction, "ListWorks", ("tenant", context.TenantId), ("limit", request.Limit + 1));
        command.Parameters.Add(new NpgsqlParameter("after_at", NpgsqlDbType.TimestampTz) { Value = (object?)request.After?.CreatedAt ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("after_order", NpgsqlDbType.Uuid) { Value = (object?)request.After?.OrderId ?? DBNull.Value });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<FulfillmentListItem>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var work = Deserialize<FulfillmentSnapshot>(reader.GetString(0)); RequireValidWork(work);
            if (reader.GetInt16(6) != 1) throw InvalidFacts();
            if (work.TenantId != context.TenantId || work.Revision != reader.GetInt64(1) || work.WorkId != reader.GetGuid(2) ||
                work.OrderId != reader.GetGuid(7) || work.CreatedAt != reader.GetFieldValue<DateTimeOffset>(3) ||
                work.UpdatedAt != reader.GetFieldValue<DateTimeOffset>(4) || !SameSource(work.Source, Deserialize<FulfillmentSourceFacts>(reader.GetString(5)))) throw InvalidFacts();
            items.Add(new(work.WorkId, work.OrderId, work.Revision, work.Stage, work.Source.OrderedQuantity,
                work.CompletedQuantity, work.HandedOffQuantity, work.Source.UnitCode, work.CreatedAt));
        }
        var hasMore = items.Count > request.Limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new(items.AsReadOnly(), hasMore ? new(items[^1].CreatedAt, items[^1].OrderId) : null);
    }

    public async Task<FulfillmentHistoryPage?> HistoryAsync(TenantContext context, Guid orderId, FulfillmentHistoryRequest request, CancellationToken cancellationToken)
    {
        await RequireViewAsync(context, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(request);
        if (orderId == Guid.Empty || request.Limit is < 1 or > 10 || request.BeforeRevision is < 2)
            throw new FulfillmentValidationException("fulfillment_history_invalid");
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        var work = await ReadWorkAsync(connection, transaction, context.TenantId, orderId, cancellationToken).ConfigureAwait(false);
        if (work is null) return null;
        await using var command = Command(connection, transaction, "History", ("tenant", context.TenantId), ("order", orderId), ("limit", request.Limit + 1));
        command.Parameters.Add(new NpgsqlParameter("before", NpgsqlDbType.Bigint) { Value = (object?)request.BeforeRevision ?? DBNull.Value });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<FulfillmentHistoryItem>();
        var expected = Math.Min(work.Revision, (request.BeforeRevision ?? checked(work.Revision + 1)) - 1);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var entry = ReadEvent(reader, work.Source, 7, 8, 9);
            if (entry.Revision != expected--) throw InvalidFacts();
            items.Add(new(entry, reader.GetBoolean(6)));
        }
        if (items.Count <= request.Limit && expected != 0) throw InvalidFacts();
        var hasMore = items.Count > request.Limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new(work.Revision, items.AsReadOnly(), hasMore ? items[^1].Event.Revision : null);
    }

    private async Task RequireViewAsync(TenantContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var authority = await authorityReader.ResolveAsync(context, ct).ConfigureAwait(false);
        if (!authority.CanView || authority.AuthorizationRevision < 1) throw new FulfillmentAccessDeniedException();
    }
}
