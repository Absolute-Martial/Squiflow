using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    public async Task<OrderDraftHistoryPage?> ListHistoryAsync(
        TenantContext tenantContext,
        GetOrderDraftHistoryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        await using var session = await OrderTenantDbSession
            .OpenAsync(dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        // Header revision and historical entries are read by one statement, from one snapshot.
        long? currentRevision = null;
        var entries = new List<OrderDraftHistoryEntry>(request.Limit + 1);
        await using (var command = session.CreateCommand(OrderSql.ListOrderHistory))
        {
            command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            command.Parameters.AddWithValue("order_id", request.OrderId);
            command.Parameters.AddWithValue("operations", new[] { CreateOperation, QuotationCreateOperation, ReviseOperation, AbandonOperation, CommitOperation, ProgramReferenceOperation });
            command.Parameters.Add(new NpgsqlParameter("before_revision", NpgsqlDbType.Bigint)
            {
                Value = (object?)request.BeforeRevision ?? DBNull.Value,
            });
            command.Parameters.AddWithValue("limit", request.Limit + 1);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                currentRevision = reader.GetInt64(reader.GetOrdinal("current_revision"));
                if (reader.IsDBNull(reader.GetOrdinal("order_id")))
                    continue;
                var operation = reader.GetString(reader.GetOrdinal("operation"));
                var receipt = new OrderCommandReceipt(
                    reader.GetGuid(reader.GetOrdinal("tenant_id")), reader.GetGuid(reader.GetOrdinal("order_id")),
                    reader.GetString(reader.GetOrdinal("fingerprint")), reader.GetString(reader.GetOrdinal("response_json")));
                var snapshot = ReadReceiptSnapshot(receipt, operation);
                var actor = reader.GetGuid(reader.GetOrdinal("account_id"));
                var recordedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
                var change = ToChange(operation);
                if (actor == Guid.Empty || snapshot.Revision > currentRevision.Value
                    || (change == OrderDraftChange.Created && (snapshot.Revision != 1 || actor != snapshot.CreatedByAccountId))
                    || (change != OrderDraftChange.Created && snapshot.Revision < 2)
                    || (change == OrderDraftChange.Committed && (actor != snapshot.CommittedByAccountId || recordedAt != snapshot.CommittedAt))
                    || (change == OrderDraftChange.Abandoned && (actor != snapshot.AbandonedByAccountId || recordedAt != snapshot.AbandonedAt)))
                    throw InvalidReceipt();
                entries.Add(new OrderDraftHistoryEntry(change, actor, recordedAt, snapshot));
            }
        }

        if (currentRevision is null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        // Complete the statement reader before profile validation uses this same transaction.
        foreach (var entry in entries)
        {
            await RequireRetainedProgramPolicyAsync(session, tenantContext.TenantId, entry.Order, cancellationToken).ConfigureAwait(false);
            await RequireAcceptedQuotationFactsAsync(tenantContext, entry.Order, cancellationToken).ConfigureAwait(false);
        }
        // Every successful current command stores its effect and receipt together. Do not
        // invent missing historical revisions or silently present contradictory receipts.
        var expectedNewest = request.BeforeRevision is { } before
            ? Math.Min(currentRevision.Value, before - 1)
            : currentRevision.Value;
        if ((entries.Count == 0 && expectedNewest > 0)
            || (entries.Count > 0 && entries[0].Order.Revision != expectedNewest))
            throw InvalidReceipt();
        for (var index = 1; index < entries.Count; index++)
            if (entries[index].Order.Revision != entries[index - 1].Order.Revision - 1)
                throw InvalidReceipt();

        if (entries.Count is > 0 && entries.Count <= request.Limit && entries[^1].Order.Revision != 1)
            throw InvalidReceipt();

        var hasMore = entries.Count > request.Limit;
        if (hasMore)
            entries.RemoveAt(entries.Count - 1);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new OrderDraftHistoryPage(currentRevision.Value, entries.AsReadOnly(),
            hasMore ? entries[^1].Order.Revision : null);
    }

    private static OrderDraftChange ToChange(string operation) => operation switch
    {
        CreateOperation => OrderDraftChange.Created,
        QuotationCreateOperation => OrderDraftChange.Created,
        ReviseOperation => OrderDraftChange.Revised,
        ProgramReferenceOperation => OrderDraftChange.ProgramReferenceUpdated,
        AbandonOperation => OrderDraftChange.Abandoned,
        CommitOperation => OrderDraftChange.Committed,
        _ => throw InvalidReceipt(),
    };
}
