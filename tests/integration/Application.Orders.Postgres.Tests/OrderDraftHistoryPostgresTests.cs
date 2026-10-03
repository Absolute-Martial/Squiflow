using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Orders;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
    [Fact]
    public async Task HistoryRetainsPricedRevisionsAndActorsWithoutDuplicatingRetriesOrFailedCommands()
    {
        await ApplyOrderSchemaAsync();
        var creator = Guid.CreateVersion7();
        var editor = Guid.CreateVersion7();
        var tenant = Guid.CreateVersion7();
        var otherTenant = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(creator, tenant, otherTenant);
        await SeedAdditionalAccountAsync(editor, tenant);
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var creatorContext = await ResolveContextAsync(tenant, creator);
        var editorContext = await ResolveContextAsync(tenant, editor);
        var created = await store.CreateAsync(creatorContext, CreateIntent("Original prices"), "create", CancellationToken.None);
        var original = created.Order!;
        var request = new ReviseOrderDraftRequest(original.OrderId, 1, "New prices", "USD",
            [new OrderDraftLineInput("Changed line", 3m, "EA", 7m)]);
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest(request.Summary, request.CurrencyCode, request.Lines));
        var revised = await store.ReviseAsync(editorContext, request, intent, "revise", new string('a', 64), CancellationToken.None);
        Assert.Equal(ReviseOrderDraftStatus.Revised, revised.Status);
        Assert.Equal(ReviseOrderDraftStatus.Replayed, (await store.ReviseAsync(editorContext, request, intent,
            "revise", new string('a', 64), CancellationToken.None)).Status);
        Assert.Equal(ReviseOrderDraftStatus.RevisionConflict, (await store.ReviseAsync(editorContext, request, intent,
            "stale", new string('b', 64), CancellationToken.None)).Status);
        var abandoned = await store.AbandonAsync(creatorContext, new AbandonOrderDraftRequest(original.OrderId, 2),
            "abandon", new string('c', 64), CancellationToken.None);
        Assert.Equal(AbandonOrderDraftStatus.Abandoned, abandoned.Status);
        Assert.Equal(AbandonOrderDraftStatus.Replayed, (await store.AbandonAsync(creatorContext,
            new AbandonOrderDraftRequest(original.OrderId, 2), "abandon", new string('c', 64), CancellationToken.None)).Status);

        var newest = await store.ListHistoryAsync(editorContext, new GetOrderDraftHistoryRequest(original.OrderId, 1), CancellationToken.None);
        Assert.NotNull(newest);
        Assert.Equal(3, newest.CurrentRevision);
        Assert.Equal(3, newest.NextBeforeRevision);
        var abandonedEntry = Assert.Single(newest.Items);
        Assert.Equal(OrderDraftChange.Abandoned, abandonedEntry.Change);
        Assert.Equal(creator, abandonedEntry.ChangedByAccountId);
        Assert.Equal(abandoned.Order!.AbandonedAt, abandonedEntry.RecordedAt);
        Assert.Equal(21m, abandonedEntry.Order.Total);

        var older = await store.ListHistoryAsync(editorContext,
            new GetOrderDraftHistoryRequest(original.OrderId, 1, newest.NextBeforeRevision), CancellationToken.None);
        Assert.NotNull(older);
        Assert.Equal(OrderDraftChange.Revised, Assert.Single(older.Items).Change);
        Assert.Equal(editor, older.Items[0].ChangedByAccountId);
        Assert.Equal(7m, older.Items[0].Order.Lines[0].UnitPrice);
        Assert.Equal(2, older.NextBeforeRevision);
        var oldest = await store.ListHistoryAsync(editorContext,
            new GetOrderDraftHistoryRequest(original.OrderId, 1, older.NextBeforeRevision), CancellationToken.None);
        Assert.NotNull(oldest);
        Assert.Null(oldest.NextBeforeRevision);
        Assert.Equal(OrderDraftChange.Created, Assert.Single(oldest.Items).Change);
        Assert.Equal(creator, oldest.Items[0].ChangedByAccountId);
        Assert.Equal(original.Total, oldest.Items[0].Order.Total);
        Assert.Equal(original.Lines, oldest.Items[0].Order.Lines);
        Assert.Equal(3, await CountReceiptsAsync(tenant));
        Assert.Equal(1, await CountOrdersAsync(tenant));

        var beforeCreation = await store.ListHistoryAsync(creatorContext,
            new GetOrderDraftHistoryRequest(original.OrderId, 1, 1), CancellationToken.None);
        Assert.NotNull(beforeCreation);
        Assert.Empty(beforeCreation.Items);
        Assert.Null(beforeCreation.NextBeforeRevision);
        var foreignContext = await ResolveContextAsync(otherTenant, creator);
        Assert.Null(await store.ListHistoryAsync(foreignContext, new GetOrderDraftHistoryRequest(original.OrderId), CancellationToken.None));
    }

    [Fact]
    public async Task HistoryReadsLegacySnapshotsAndRejectsUnsupportedEnvelopesWithoutChangingBusinessState()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.CreateVersion7();
        var tenant = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(account, tenant);
        await using var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(source, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenant, account);
        var created = await store.CreateAsync(context, CreateIntent("Legacy prices"), "legacy", CancellationToken.None);
        var snapshot = created.Order!;
        await ReplaceHistoryReceiptAsync(snapshot.OrderId, JsonSerializer.Serialize(snapshot, JsonSerializerOptions.Web));
        var history = await store.ListHistoryAsync(context, new GetOrderDraftHistoryRequest(snapshot.OrderId), CancellationToken.None);
        Assert.NotNull(history);
        Assert.Equal(snapshot.Total, Assert.Single(history.Items).Order.Total);
        var unknown = new JsonObject
        {
            ["schemaVersion"] = 999,
            ["operation"] = "create-order-draft",
            ["resultType"] = "order-draft-created",
            ["payload"] = JsonSerializer.SerializeToNode(snapshot, JsonSerializerOptions.Web),
        };
        await ReplaceHistoryReceiptAsync(snapshot.OrderId, unknown.ToJsonString());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ListHistoryAsync(context, new GetOrderDraftHistoryRequest(snapshot.OrderId), CancellationToken.None));
        Assert.DoesNotContain("Legacy prices", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, await CountOrdersAsync(tenant));
        Assert.Equal(1, await CountReceiptsAsync(tenant));
        Assert.Equal(snapshot.Total, (await store.FindAsync(context, snapshot.OrderId, CancellationToken.None))!.Total);
    }

    [Fact]
    public async Task RuntimeRoleCannotRewriteOrDeleteTheReceiptsUsedForHistory()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.CreateVersion7();
        var tenant = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(account, tenant);
        var runtimeConnection = await CreateRuntimeRoleAsync();
        await using var source = new NpgsqlDataSourceBuilder(runtimeConnection).Build();
        var context = await ResolveContextAsync(tenant, account);
        var store = new PostgresOrderDraftStore(source);
        var order = (await store.CreateAsync(context, CreateIntent("Retained"), "retained", CancellationToken.None)).Order!;
        await using var connection = new NpgsqlConnection(runtimeConnection);
        await connection.OpenAsync();
        await SetTenantContextAsync(connection, tenant);
        foreach (var sql in new[]
                 {
                     "UPDATE orders.command_receipts SET response_json = '{}'::jsonb WHERE tenant_id = @tenant_id",
                     "DELETE FROM orders.command_receipts WHERE tenant_id = @tenant_id",
                 })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("tenant_id", tenant);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
        var page = await store.ListHistoryAsync(context, new GetOrderDraftHistoryRequest(order.OrderId), CancellationToken.None);
        Assert.NotNull(page);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task MissingHistoricalRevisionsFailClosedInsteadOfInventingAnIncompleteHistory()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.CreateVersion7();
        var tenant = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(account, tenant);
        await using var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(source);
        var context = await ResolveContextAsync(tenant, account);
        var order = (await store.CreateAsync(context, CreateIntent("Original"), "first", CancellationToken.None)).Order!;
        var request = new ReviseOrderDraftRequest(order.OrderId, 1, "Replacement", "USD",
            [new OrderDraftLineInput("Replacement", 1m, "EA", 8m)]);
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest(request.Summary, request.CurrencyCode, request.Lines));
        Assert.Equal(ReviseOrderDraftStatus.Revised, (await store.ReviseAsync(context, request, intent,
            "second", new string('d', 64), CancellationToken.None)).Status);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM orders.command_receipts WHERE tenant_id = @tenant_id AND operation = 'create-order-draft'", connection);
        command.Parameters.AddWithValue("tenant_id", tenant);
        await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ListHistoryAsync(context, new GetOrderDraftHistoryRequest(order.OrderId), CancellationToken.None));
        Assert.Equal(2, (await store.FindAsync(context, order.OrderId, CancellationToken.None))!.Revision);
    }

    private async Task ReplaceHistoryReceiptAsync(Guid orderId, string json)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE orders.command_receipts SET response_json = @response::jsonb WHERE order_id = @order_id", connection);
        command.Parameters.AddWithValue("response", json);
        command.Parameters.AddWithValue("order_id", orderId);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task HistoryReadsCurrentRevisionAndReceiptsFromOneSnapshotDuringConcurrentRevision()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.CreateVersion7();
        var tenant = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(account, tenant);
        await using var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(source, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenant, account);
        var original = (await store.CreateAsync(context, CreateIntent("Original"), "original", CancellationToken.None)).Order!;
        var changed = original with
        {
            Summary = "Changed",
            Revision = 2,
            Total = 21m,
            Lines = [new OrderDraftLine(1, "Changed line", 3m, "EA", 7m, 21m)],
        };
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var transaction = await admin.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand("LOCK TABLE orders.command_receipts IN ACCESS EXCLUSIVE MODE", admin, transaction))
            await hold.ExecuteNonQueryAsync();

        var read = store.ListHistoryAsync(context, new GetOrderDraftHistoryRequest(original.OrderId), CancellationToken.None);
        // Observe an actual PostgreSQL lock wait, rather than relying on elapsed time.
        await using (var monitor = new NpgsqlConnection(ConnectionString))
        {
            await monitor.OpenAsync();
            using var waitBudget = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using var check = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE usename = 'application_orders_runtime' AND wait_event_type = 'Lock'
                  AND query LIKE '%orders.command_receipts%'
                """, monitor);
            while ((long)(await check.ExecuteScalarAsync(waitBudget.Token) ?? 0L) == 0) { }
        }

        await using (var change = new NpgsqlCommand("""
            UPDATE orders.order_drafts SET revision = 2, summary = 'Changed', total = 21
            WHERE tenant_id = @tenant_id AND id = @order_id;
            DELETE FROM orders.order_draft_lines WHERE tenant_id = @tenant_id AND order_id = @order_id;
            INSERT INTO orders.order_draft_lines
                (tenant_id, order_id, position, description, quantity, unit_code, unit_price, line_total)
            VALUES (@tenant_id, @order_id, 1, 'Changed line', 3, 'EA', 7, 21);
            """, admin, transaction))
        {
            change.Parameters.AddWithValue("tenant_id", tenant);
            change.Parameters.AddWithValue("order_id", original.OrderId);
            await change.ExecuteNonQueryAsync();
        }
        await InsertPersistedReceiptFixtureAsync(admin, tenant, account, original.OrderId,
            "revise-order-draft", "concurrent", new string('e', 64), JsonSerializer.Serialize(changed, JsonSerializerOptions.Web));
        await transaction.CommitAsync();
        var history = await read.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.NotNull(history);
        Assert.Equal(history.CurrentRevision, history.Items[0].Order.Revision);
        Assert.True(history.Items.Select(entry => entry.Order.Revision)
            .SequenceEqual(Enumerable.Range(1, checked((int)history.CurrentRevision)).Reverse().Select(value => (long)value)));
        Assert.Equal(2, (await store.FindAsync(context, original.OrderId, CancellationToken.None))!.Revision);
    }
}
