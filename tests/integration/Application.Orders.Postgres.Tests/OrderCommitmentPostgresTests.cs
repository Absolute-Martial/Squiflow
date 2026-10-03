using System.Text.Json;
using Application.Customers;
using Application.Orders;
using Application.Orders.Postgres.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
    [Fact]
    public async Task CommitmentFreezesAttributedFactsAndRetainsAnAtomicHistoricalReceipt()
    {
        await ApplyOrderSchemaAsync();
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var foreignTenant = Guid.NewGuid();
        var organization = Guid.NewGuid();
        var program = Guid.NewGuid();
        await SeedAuthorityRowsAsync(actor, tenant, foreignTenant);
        await SeedCustomerContextAsync(tenant, actor, organization, program);
        await using var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(source, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenant, actor);
        var foreign = await ResolveContextAsync(foreignTenant, actor);
        var intent = CreateIntent("Committed program materials", new CustomerOrderContext(organization, program));
        var original = (await store.CreateAsync(context, intent, "create", CancellationToken.None)).Order!;
        var commit = new CommitOrderDraft(store);
        var request = new CommitOrderDraftRequest(original.OrderId, 1);
        Assert.Equal(CommitOrderDraftStatus.NotFound,
            (await commit.ExecuteAsync(foreign, request, "commit", CancellationToken.None)).Status);
        var result = await commit.ExecuteAsync(context, request, "commit", CancellationToken.None);
        Assert.Equal(CommitOrderDraftStatus.Committed, result.Status);
        var expected = original with
        {
            State = OrderDraftState.Committed,
            Revision = 2,
            CommittedAt = new FixedTimeProvider().GetUtcNow(),
            CommittedByAccountId = actor,
        };
        Assert.Equivalent(expected, result.Order);
        Assert.Equivalent(expected, await store.FindAsync(context, original.OrderId, CancellationToken.None));
        var list = Assert.Single((await store.ListAsync(context, new ListOrderDraftsRequest(10, null), CancellationToken.None)).Items);
        Assert.Equal(OrderDraftState.Committed, list.State);
        Assert.Equal(expected.CommittedAt, list.CommittedAt);
        var replay = await commit.ExecuteAsync(context, request, "commit", CancellationToken.None);
        Assert.Equal(CommitOrderDraftStatus.Replayed, replay.Status);
        Assert.Equivalent(expected, replay.Order);
        Assert.Equal(CommitOrderDraftStatus.IdempotencyKeyConflict,
            (await commit.ExecuteAsync(context, request with { ExpectedRevision = 2 }, "commit", CancellationToken.None)).Status);
        Assert.Equal(CommitOrderDraftStatus.AlreadyCommitted,
            (await commit.ExecuteAsync(context, request with { ExpectedRevision = 2 }, "new-key", CancellationToken.None)).Status);
        Assert.Equal(AbandonOrderDraftStatus.AlreadyCommitted,
            (await store.AbandonAsync(context, new AbandonOrderDraftRequest(original.OrderId, 2), "abandon", new string('a', 64), CancellationToken.None)).Status);
        Assert.Equal(ReviseOrderDraftStatus.AlreadyCommitted,
            (await store.ReviseAsync(context, new ReviseOrderDraftRequest(original.OrderId, 2, "Changed", intent.CurrencyCode,
                [new OrderDraftLineInput("Changed", 1, "EA", 1)]), CreateIntent("Changed"), "revise", new string('b', 64), CancellationToken.None)).Status);
        Assert.Equivalent(original, (await store.CreateAsync(context, intent, "create", CancellationToken.None)).Order);
        var history = await store.ListHistoryAsync(context, new GetOrderDraftHistoryRequest(original.OrderId, 10), CancellationToken.None);
        Assert.Equal(2, history!.Items.Count);
        Assert.Equal(OrderDraftChange.Committed, history.Items[0].Change);
        Assert.Equal(actor, history.Items[0].ChangedByAccountId);
        Assert.Equivalent(expected, history.Items[0].Order);
        Assert.Equal(2, await CountReceiptsAsync(tenant));
        Assert.Equal(0, await CountReceiptsAsync(foreignTenant));
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var envelope = new NpgsqlCommand("SELECT response_json::text FROM orders.command_receipts WHERE tenant_id = @tenant AND operation = 'commit-order-draft'", admin);
        envelope.Parameters.AddWithValue("tenant", tenant);
        using var json = JsonDocument.Parse((string)(await envelope.ExecuteScalarAsync())!);
        Assert.Equal(3, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("order-committed", json.RootElement.GetProperty("resultType").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentCommitCommandsProduceOnlyOneEffect(bool sameKey)
    {
        await ApplyOrderSchemaAsync();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(actor, tenant);
        await using var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(source);
        var context = await ResolveContextAsync(tenant, actor);
        var original = (await store.CreateAsync(context, CreateIntent("Concurrent"), "create", CancellationToken.None)).Order!;
        var useCase = new CommitOrderDraft(store);
        var request = new CommitOrderDraftRequest(original.OrderId, 1);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            useCase.ExecuteAsync(context, request, sameKey ? "commit" : $"commit-{index}", CancellationToken.None)));
        Assert.Single(results, result => result.Status == CommitOrderDraftStatus.Committed);
        Assert.Equal(7, results.Count(result => result.Status == (sameKey ? CommitOrderDraftStatus.Replayed : CommitOrderDraftStatus.AlreadyCommitted)));
        Assert.Equal(2, (await store.FindAsync(context, original.OrderId, CancellationToken.None))!.Revision);
        Assert.Equal(2, await CountReceiptsAsync(tenant));
    }

    [Fact]
    public async Task RuntimeCannotRewriteCommittedFactsOrInsertAdditionalLines()
    {
        await ApplyOrderSchemaAsync();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(actor, tenant);
        var runtime = await CreateRuntimeRoleAsync();
        await using var source = new NpgsqlDataSourceBuilder(runtime).Build();
        var store = new PostgresOrderDraftStore(source);
        var context = await ResolveContextAsync(tenant, actor);
        var original = (await store.CreateAsync(context, CreateIntent("Frozen"), "create", CancellationToken.None)).Order!;
        var committed = (await new CommitOrderDraft(store).ExecuteAsync(context,
            new CommitOrderDraftRequest(original.OrderId, 1), "commit", CancellationToken.None)).Order!;
        await using var connection = new NpgsqlConnection(runtime);
        await connection.OpenAsync();
        await SetTenantContextAsync(connection, tenant);
        await using var update = new NpgsqlCommand("UPDATE orders.order_drafts SET total = total + 1 WHERE tenant_id = @tenant AND id = @id", connection);
        update.Parameters.AddWithValue("tenant", tenant); update.Parameters.AddWithValue("id", original.OrderId);
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync())).SqlState);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => InsertOrderLineAsync(connection, tenant, original.OrderId, 2, CancellationToken.None))).SqlState);
        await using var delete = new NpgsqlCommand("DELETE FROM orders.order_draft_lines WHERE tenant_id = @tenant AND order_id = @id", connection);
        delete.Parameters.AddWithValue("tenant", tenant); delete.Parameters.AddWithValue("id", original.OrderId);
        Assert.Equal(0, await delete.ExecuteNonQueryAsync());
        // Grant a deliberately broader mutation privilege to prove the row policy also
        // protects terminal lines if a future grant accidentally broadens this runtime role.
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var grant = new NpgsqlCommand("GRANT UPDATE ON orders.order_draft_lines TO application_orders_runtime", admin);
        await grant.ExecuteNonQueryAsync();
        await using var rewrite = new NpgsqlCommand("UPDATE orders.order_draft_lines SET unit_price = 999 WHERE tenant_id = @tenant AND order_id = @id", connection);
        rewrite.Parameters.AddWithValue("tenant", tenant); rewrite.Parameters.AddWithValue("id", original.OrderId);
        Assert.Equal(0, await rewrite.ExecuteNonQueryAsync());
        Assert.Equivalent(committed, await store.FindAsync(context, original.OrderId, CancellationToken.None));
    }

    [Fact]
    public async Task ReceiptFailureRollsBackCommitmentBeforeRetryCanSucceed()
    {
        await ApplyOrderSchemaAsync();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(actor, tenant);
        await using var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(source);
        var context = await ResolveContextAsync(tenant, actor);
        var created = (await store.CreateAsync(context, CreateIntent("Atomic"), "create", CancellationToken.None)).Order!;
        var original = (await store.FindAsync(context, created.OrderId, CancellationToken.None))!;
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using (var revoke = new NpgsqlCommand("REVOKE INSERT ON orders.command_receipts FROM application_orders_runtime", admin))
            await revoke.ExecuteNonQueryAsync();
        var commit = new CommitOrderDraft(store);
        var request = new CommitOrderDraftRequest(original.OrderId, 1);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
            (await Assert.ThrowsAsync<PostgresException>(() => commit.ExecuteAsync(context, request, "commit", CancellationToken.None))).SqlState);
        Assert.Equivalent(original, await store.FindAsync(context, original.OrderId, CancellationToken.None));
        Assert.Equal(1, await CountReceiptsAsync(tenant));
        await using (var grant = new NpgsqlCommand("GRANT INSERT ON orders.command_receipts TO application_orders_runtime", admin))
            await grant.ExecuteNonQueryAsync();
        Assert.Equal(CommitOrderDraftStatus.Committed, (await commit.ExecuteAsync(context, request, "commit", CancellationToken.None)).Status);
    }

    [Fact]
    public async Task CommitmentRollbackRefusesToDiscardRetainedAuthority()
    {
        await ApplyOrderSchemaAsync();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(actor, tenant);
        await using var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(source);
        var context = await ResolveContextAsync(tenant, actor);
        var original = (await store.CreateAsync(context, CreateIntent("Frozen"), "create", CancellationToken.None)).Order!;
        await new CommitOrderDraft(store).ExecuteAsync(context, new CommitOrderDraftRequest(original.OrderId, 1), "commit", CancellationToken.None);
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using (var ownership = new NpgsqlCommand("""
            CREATE ROLE application_orders_schema_owner LOGIN NOSUPERUSER NOBYPASSRLS PASSWORD 'local-schema-test-only';
            ALTER SCHEMA orders OWNER TO application_orders_schema_owner;
            ALTER TABLE orders.order_drafts OWNER TO application_orders_schema_owner;
            ALTER TABLE orders.order_draft_lines OWNER TO application_orders_schema_owner;
            ALTER TABLE orders.command_receipts OWNER TO application_orders_schema_owner;
            ALTER TABLE public.__orders_migrations OWNER TO application_orders_schema_owner;
            ALTER FUNCTION orders.protect_committed_order() OWNER TO application_orders_schema_owner;
            GRANT USAGE, CREATE ON SCHEMA public TO application_orders_schema_owner;
            GRANT USAGE ON SCHEMA tenancy TO application_orders_schema_owner;
            GRANT SELECT ON tenancy.tenants TO application_orders_schema_owner;
            """, admin))
            await ownership.ExecuteNonQueryAsync();
        var schemaIdentity = new NpgsqlConnectionStringBuilder(ConnectionString)
        { Username = "application_orders_schema_owner", Password = "local-schema-test-only" };
        await using var schema = CreateContext(schemaIdentity.ConnectionString);
        var error = await Assert.ThrowsAsync<PostgresException>(() => schema.GetService<IMigrator>().MigrateAsync("202609240002_OrderDraftRevision"));
        Assert.Contains("committed orders exist", error.MessageText, StringComparison.Ordinal);
        Assert.Contains("202610030001_OrderCommitment", await schema.Database.GetAppliedMigrationsAsync());
        Assert.Equal(OrderDraftState.Committed, (await store.FindAsync(context, original.OrderId, CancellationToken.None))!.State);
        Assert.Null(new OrderDraftRevision().TargetModel.FindEntityType("Application.Orders.Postgres.OrderDraftRow")!.FindProperty("CommittedAt"));
        Assert.NotNull(new OrderCommitment().TargetModel.FindEntityType("Application.Orders.Postgres.OrderDraftRow")!.FindProperty("CommittedAt"));
    }
}
