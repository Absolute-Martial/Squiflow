using System.Data;
using System.Text.Json;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Orders.Postgres;

public sealed class PostgresOrderDraftStore(
    NpgsqlDataSource dataSource,
    TimeProvider? timeProvider = null)
    : IOrderDraftStore
{
    private const string CreateOperation = "create-order-draft";
    private const string AbandonOperation = "abandon-order-draft";
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<CreateOrderDraftResult> CreateAsync(
        TenantContext tenantContext,
        OrderDraftIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        await using var connection = await dataSource
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var existing = await FindReceiptAsync(
                connection,
                transaction,
                tenantContext.TenantId,
                tenantContext.AccountId,
                CreateOperation,
                idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ToExistingResult(existing, intent.Fingerprint);
        }

        var createdAt = _timeProvider.GetUtcNow();
        var order = new OrderDraftSnapshot(
            Guid.CreateVersion7(),
            tenantContext.TenantId,
            tenantContext.AccountId,
            intent.Summary,
            intent.CurrencyCode,
            intent.Total,
            Revision: 1,
            createdAt,
            intent.Lines);

        await InsertOrderAsync(connection, transaction, order, cancellationToken).ConfigureAwait(false);
        await InsertLinesAsync(connection, transaction, order, cancellationToken).ConfigureAwait(false);

        if (await TryInsertReceiptAsync(
                connection,
                transaction,
                tenantContext.AccountId,
                CreateOperation,
                idempotencyKey,
                intent.Fingerprint,
                order,
                createdAt,
                cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new CreateOrderDraftResult(CreateOrderDraftStatus.Created, order);
        }

        // A concurrent command won the receipt's unique key. This transaction's order and
        // lines are intentionally rolled back after reading the winner's durable outcome.
        existing = await FindReceiptAsync(
                connection,
                transaction,
                tenantContext.TenantId,
                tenantContext.AccountId,
                CreateOperation,
                idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The order command receipt disappeared after a conflict.");
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ToExistingResult(existing, intent.Fingerprint);
    }

    public async Task<AbandonOrderDraftResult> AbandonAsync(
        TenantContext tenantContext,
        AbandonOrderDraftRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var receipt = await FindReceiptAsync(
            connection, transaction, tenantContext.TenantId, tenantContext.AccountId,
            AbandonOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ToExistingAbandonResult(receipt, fingerprint);
        }

        var abandonedAt = _timeProvider.GetUtcNow();
        var changed = await TryAbandonOrderAsync(
            connection, transaction, tenantContext, request, abandonedAt, cancellationToken)
            .ConfigureAwait(false);
        if (!changed)
        {
            // A concurrent winner may have committed while the conditional UPDATE waited.
            receipt = await FindReceiptAsync(
                connection, transaction, tenantContext.TenantId, tenantContext.AccountId,
                AbandonOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (receipt is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return ToExistingAbandonResult(receipt, fingerprint);
            }

            var current = await FindOrderStateAsync(
                connection, transaction, tenantContext.TenantId, request.OrderId, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            var status = current is null
                ? AbandonOrderDraftStatus.NotFound
                : OrderDraftLifecycle.AssessAbandon(
                    current.Value.State, current.Value.Revision, request.ExpectedRevision);
            if (status == AbandonOrderDraftStatus.Abandoned)
            {
                throw new InvalidOperationException("The draft remained eligible after its conditional update failed.");
            }

            return new AbandonOrderDraftResult(
                status, null);
        }

        var order = await FindOrderAsync(
            connection, transaction, tenantContext.TenantId, request.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The abandoned order disappeared before receipt creation.");
        if (await TryInsertReceiptAsync(
            connection, transaction, tenantContext.AccountId, AbandonOperation,
            idempotencyKey, fingerprint, order, abandonedAt, cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new AbandonOrderDraftResult(AbandonOrderDraftStatus.Abandoned, order);
        }

        receipt = await FindReceiptAsync(
            connection, transaction, tenantContext.TenantId, tenantContext.AccountId,
            AbandonOperation, idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The order command receipt disappeared after a conflict.");
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ToExistingAbandonResult(receipt, fingerprint);
    }

    private static async Task<bool> TryAbandonOrderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantContext tenantContext,
        AbandonOrderDraftRequest request,
        DateTimeOffset abandonedAt,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE orders.order_drafts
            SET state = 'abandoned',
                revision = revision + 1,
                abandoned_at = @abandoned_at,
                abandoned_by_account_id = @account_id
            WHERE tenant_id = @tenant_id
              AND id = @order_id
              AND state = 'draft'
              AND revision = @expected_revision
            RETURNING 1
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.AddWithValue("order_id", request.OrderId);
        command.Parameters.AddWithValue("expected_revision", request.ExpectedRevision);
        command.Parameters.AddWithValue("abandoned_at", abandonedAt);
        command.Parameters.AddWithValue("account_id", tenantContext.AccountId);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static async Task<(OrderDraftState State, long Revision)?> FindOrderStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT state, revision FROM orders.order_drafts WHERE tenant_id = @tenant_id AND id = @order_id",
            connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("order_id", orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (ReadState(reader.GetString(0)), reader.GetInt64(1))
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

        await using var connection = await dataSource
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var order = await FindOrderAsync(
                connection,
                transaction,
                tenantContext.TenantId,
                orderId,
                cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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

        await using var connection = await dataSource
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var items = await ListOrderHeadersAsync(
                connection,
                transaction,
                tenantContext.TenantId,
                request,
                cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

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

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.current_tenant', @tenant_id, true)",
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertOrderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OrderDraftSnapshot order,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO orders.order_drafts
                (tenant_id, id, created_by_account_id, summary, currency_code, total, revision, created_at)
            VALUES
                (@tenant_id, @id, @created_by_account_id, @summary, @currency_code, @total, @revision, @created_at)
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", order.TenantId);
        command.Parameters.AddWithValue("id", order.OrderId);
        command.Parameters.AddWithValue("created_by_account_id", order.CreatedByAccountId);
        command.Parameters.AddWithValue("summary", order.Summary);
        command.Parameters.AddWithValue("currency_code", order.CurrencyCode);
        command.Parameters.AddWithValue("total", order.Total);
        command.Parameters.AddWithValue("revision", order.Revision);
        command.Parameters.AddWithValue("created_at", order.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertLinesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OrderDraftSnapshot order,
        CancellationToken cancellationToken)
    {
        foreach (var line in order.Lines)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO orders.order_draft_lines
                    (tenant_id, order_id, position, description, quantity, unit_code, unit_price, line_total)
                VALUES
                    (@tenant_id, @order_id, @position, @description, @quantity, @unit_code, @unit_price, @line_total)
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("tenant_id", order.TenantId);
            command.Parameters.AddWithValue("order_id", order.OrderId);
            command.Parameters.AddWithValue("position", line.Position);
            command.Parameters.AddWithValue("description", line.Description);
            command.Parameters.AddWithValue("quantity", line.Quantity);
            command.Parameters.AddWithValue("unit_code", line.UnitCode);
            command.Parameters.AddWithValue("unit_price", line.UnitPrice);
            command.Parameters.AddWithValue("line_total", line.LineTotal);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<bool> TryInsertReceiptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string operation,
        string idempotencyKey,
        string fingerprint,
        OrderDraftSnapshot order,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO orders.command_receipts
                (tenant_id, account_id, operation, idempotency_key, fingerprint, order_id, response_json, created_at)
            VALUES
                (@tenant_id, @account_id, @operation, @idempotency_key, @fingerprint, @order_id, @response_json::jsonb, @created_at)
            ON CONFLICT (tenant_id, account_id, operation, idempotency_key) DO NOTHING
            RETURNING 1
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", order.TenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("order_id", order.OrderId);
        command.Parameters.AddWithValue("response_json", JsonSerializer.Serialize(order, SnapshotJsonOptions));
        command.Parameters.AddWithValue("created_at", createdAt);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static async Task<OrderCommandReceipt?> FindReceiptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid accountId,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT fingerprint, response_json::text
            FROM orders.command_receipts
            WHERE tenant_id = @tenant_id
              AND account_id = @account_id
              AND operation = @operation
              AND idempotency_key = @idempotency_key
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new OrderCommandReceipt(reader.GetString(0), reader.GetString(1));
    }

    private static async Task<OrderDraftSnapshot?> FindOrderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        OrderDraftHeader? header;
        await using (var command = new NpgsqlCommand(
            """
            SELECT id, tenant_id, created_by_account_id, summary, currency_code, total, revision, created_at,
                   state, abandoned_at, abandoned_by_account_id
            FROM orders.order_drafts
            WHERE tenant_id = @tenant_id AND id = @id
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("id", orderId);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            header = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? new OrderDraftHeader(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetString(3),
                    reader.GetString(4).TrimEnd(),
                    reader.GetDecimal(5),
                    reader.GetInt64(6),
                    reader.GetFieldValue<DateTimeOffset>(7),
                    ReadState(reader.GetString(8)),
                    reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
                    reader.IsDBNull(10) ? null : reader.GetGuid(10))
                : null;
        }

        if (header is null)
        {
            return null;
        }

        var lines = new List<OrderDraftLine>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT position, description, quantity, unit_code, unit_price, line_total
            FROM orders.order_draft_lines
            WHERE tenant_id = @tenant_id AND order_id = @order_id
            ORDER BY position
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("order_id", orderId);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new OrderDraftLine(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetDecimal(2),
                    reader.GetString(3),
                    reader.GetDecimal(4),
                    reader.GetDecimal(5)));
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
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        ListOrderDraftsRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id, summary, currency_code, total, revision, created_at, state, abandoned_at
            FROM orders.order_drafts
            WHERE tenant_id = @tenant_id
              AND (
                    @after_created_at IS NULL
                    OR created_at < @after_created_at
                    OR (created_at = @after_created_at AND id < @after_id)
                  )
            ORDER BY created_at DESC, id DESC
            LIMIT @limit
            """,
            connection,
            transaction);
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
            items.Add(new OrderDraftListItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2).TrimEnd(),
                reader.GetDecimal(3),
                reader.GetInt64(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                ReadState(reader.GetString(6)),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return items;
    }

    private static CreateOrderDraftResult ToExistingResult(
        OrderCommandReceipt receipt,
        string requestedFingerprint)
    {
        if (!string.Equals(receipt.Fingerprint, requestedFingerprint, StringComparison.Ordinal))
        {
            return new CreateOrderDraftResult(CreateOrderDraftStatus.IdempotencyKeyConflict, null);
        }

        var order = JsonSerializer.Deserialize<OrderDraftSnapshot>(receipt.ResponseJson, SnapshotJsonOptions)
            ?? throw new InvalidOperationException("The order command receipt does not contain a response.");
        return new CreateOrderDraftResult(CreateOrderDraftStatus.Replayed, order);
    }

    private static AbandonOrderDraftResult ToExistingAbandonResult(
        OrderCommandReceipt receipt,
        string requestedFingerprint)
    {
        if (!string.Equals(receipt.Fingerprint, requestedFingerprint, StringComparison.Ordinal))
        {
            return new AbandonOrderDraftResult(AbandonOrderDraftStatus.IdempotencyKeyConflict, null);
        }

        var order = JsonSerializer.Deserialize<OrderDraftSnapshot>(receipt.ResponseJson, SnapshotJsonOptions)
            ?? throw new InvalidOperationException("The order command receipt does not contain a response.");
        return new AbandonOrderDraftResult(AbandonOrderDraftStatus.Replayed, order);
    }

    private static OrderDraftState ReadState(string state) => state switch
    {
        "draft" => OrderDraftState.Draft,
        "abandoned" => OrderDraftState.Abandoned,
        _ => throw new InvalidOperationException("The order draft has an unsupported stored state."),
    };

    private sealed record OrderCommandReceipt(string Fingerprint, string ResponseJson);

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
