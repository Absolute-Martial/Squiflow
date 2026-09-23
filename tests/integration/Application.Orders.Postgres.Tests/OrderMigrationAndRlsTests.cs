using Application.Orders;
using Application.Orders.Postgres.Migrations;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed class OrderMigrationAndRlsTests : PostgresTestDatabase
{
    [Fact]
    public void MigrationTargetModelsKeepTheirHistoricalShape()
    {
        const string draftEntity = "Application.Orders.Postgres.OrderDraftRow";
        var initial = new InitialOrderDrafts().TargetModel.FindEntityType(draftEntity);
        var browse = new OrderDraftBrowseIndex().TargetModel.FindEntityType(draftEntity);
        var abandonment = new OrderDraftAbandonment().TargetModel.FindEntityType(draftEntity);

        Assert.NotNull(initial);
        Assert.NotNull(browse);
        Assert.NotNull(abandonment);
        Assert.Null(initial.FindProperty("State"));
        Assert.Null(browse.FindProperty("State"));
        Assert.Null(initial.FindProperty("AbandonedAt"));
        Assert.Null(browse.FindProperty("AbandonedAt"));
        Assert.NotNull(abandonment.FindProperty("State"));
        Assert.NotNull(abandonment.FindProperty("AbandonedAt"));
        Assert.NotNull(abandonment.FindProperty("AbandonedByAccountId"));

        static bool HasBrowseIndex(Microsoft.EntityFrameworkCore.Metadata.IEntityType entity) =>
            entity.GetIndexes().Any(index =>
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(["TenantId", "CreatedAt", "Id"]));

        Assert.False(HasBrowseIndex(initial));
        Assert.True(HasBrowseIndex(browse));
        Assert.True(HasBrowseIndex(abandonment));
    }

    [Fact]
    public async Task AppliedOrderMigrationMatchesTheOrderModel()
    {
        await ApplyOrderSchemaAsync();

        await using var context = CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
        await AssertCriticalSchemaGuardsAsync();
    }

    [Fact]
    public async Task CommandReceiptsCannotReferenceAnAbsentOrAnotherTenantsOrder()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var existingOrderId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantA, tenantB);
        await InsertOrderHeaderAsync(tenantA, existingOrderId, accountId);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);

        var orphan = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertCommandReceiptAsync(
                connection,
                tenantA,
                accountId,
                Guid.CreateVersion7(),
                "orphan-receipt",
                CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, orphan.SqlState);
        Assert.Equal("fk_command_receipts_order_drafts", orphan.ConstraintName);

        var crossTenant = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertCommandReceiptAsync(
                connection,
                tenantB,
                accountId,
                existingOrderId,
                "cross-tenant-receipt",
                CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, crossTenant.SqlState);
        Assert.Equal("fk_command_receipts_order_drafts", crossTenant.ConstraintName);
    }

    [Fact]
    public async Task RuntimeRoleCanOnlyCreateAndReadTheTransactionTenant()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantA, tenantB);

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var tenantAContext = await ResolveContextAsync(tenantA, accountId);
        var tenantBContext = await ResolveContextAsync(tenantB, accountId);
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            "Window sign order",
            "USD",
            [new OrderDraftLineInput("Printed panel", 2m, "EA", 12.5m)]));

        var created = await store.CreateAsync(
            tenantAContext,
            intent,
            "order-a-001",
            CancellationToken.None);

        var order = Assert.IsType<OrderDraftSnapshot>(created.Order);
        Assert.Equal(CreateOrderDraftStatus.Created, created.Status);
        Assert.Equal(tenantA, order.TenantId);
        Assert.Equal(25m, order.Total);

        var visibleToA = await store.FindAsync(tenantAContext, order.OrderId, CancellationToken.None);
        var hiddenFromB = await store.FindAsync(tenantBContext, order.OrderId, CancellationToken.None);
        Assert.NotNull(visibleToA);
        Assert.Equal(order.OrderId, visibleToA.OrderId);
        Assert.Equal(order.TenantId, visibleToA.TenantId);
        Assert.Equal(order.CreatedByAccountId, visibleToA.CreatedByAccountId);
        Assert.Equal(order.Summary, visibleToA.Summary);
        Assert.Equal(order.CurrencyCode, visibleToA.CurrencyCode);
        Assert.Equal(order.Total, visibleToA.Total);
        Assert.Equal(order.Revision, visibleToA.Revision);
        Assert.Equal(order.CreatedAt, visibleToA.CreatedAt);
        Assert.Equal(order.Lines, visibleToA.Lines);
        Assert.Null(hiddenFromB);

        await using var runtimeConnection = new NpgsqlConnection(runtimeConnectionString);
        await runtimeConnection.OpenAsync(CancellationToken.None);
        Assert.Equal(
            0,
            await CountRowsVisibleToRuntimeRoleAsync(
                runtimeConnection,
                "SELECT count(*) FROM orders.order_drafts"));
        Assert.Equal(
            0,
            await CountRowsVisibleToRuntimeRoleAsync(
                runtimeConnection,
                "SELECT count(*) FROM orders.order_draft_lines"));
        Assert.Equal(
            0,
            await CountRowsVisibleToRuntimeRoleAsync(
                runtimeConnection,
                "SELECT count(*) FROM orders.command_receipts"));

        await SetTenantContextAsync(runtimeConnection, tenantB);
        await using var crossTenantRead = runtimeConnection.CreateCommand();
        crossTenantRead.CommandText = "SELECT count(*) FROM orders.order_drafts WHERE id = @order_id";
        crossTenantRead.Parameters.AddWithValue("order_id", order.OrderId);
        var visibleAcrossTenantBoundary = (long)(await crossTenantRead.ExecuteScalarAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("The count query returned no value."));
        Assert.Equal(0, visibleAcrossTenantBoundary);

        await using var forbiddenDelete = runtimeConnection.CreateCommand();
        forbiddenDelete.CommandText = "DELETE FROM orders.order_drafts WHERE tenant_id = @tenant_id AND id = @order_id";
        forbiddenDelete.Parameters.AddWithValue("tenant_id", tenantA);
        forbiddenDelete.Parameters.AddWithValue("order_id", order.OrderId);
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenDelete.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);

        await using var forbiddenDdl = runtimeConnection.CreateCommand();
        forbiddenDdl.CommandText = "CREATE TABLE orders.runtime_must_not_create_tables (id uuid)";
        exception = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenDdl.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task RuntimeRoleRlsWithCheckRejectsMissingAndWrongTenantInsertsForEveryOrderTable()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var existingOrderId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantA, tenantB);
        await InsertOrderHeaderAsync(tenantA, existingOrderId, accountId);

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var runtimeConnection = new NpgsqlConnection(runtimeConnectionString);
        await runtimeConnection.OpenAsync(CancellationToken.None);

        await AssertRlsWithCheckRejectsMissingAndWrongTenantAsync(
            runtimeConnection,
            tenantB,
            connection => InsertOrderHeaderAsync(
                connection,
                tenantA,
                Guid.CreateVersion7(),
                accountId,
                CancellationToken.None));
        await AssertRlsWithCheckRejectsMissingAndWrongTenantAsync(
            runtimeConnection,
            tenantB,
            connection => InsertOrderLineAsync(
                connection,
                tenantA,
                existingOrderId,
                position: 1,
                CancellationToken.None));
        await AssertRlsWithCheckRejectsMissingAndWrongTenantAsync(
            runtimeConnection,
            tenantB,
            connection => InsertCommandReceiptAsync(
                connection,
                tenantA,
                accountId,
                existingOrderId,
                "rls-receipt",
                CancellationToken.None));
    }

    [Fact]
    public async Task TransactionLocalTenantContextDoesNotLeakFromAOneConnectionPoolAfterCommitOrRollback()
    {
        await ApplyOrderSchemaAsync();

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        var pooledConnectionString = new NpgsqlConnectionStringBuilder(runtimeConnectionString)
        {
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 1,
        }.ConnectionString;
        await using var dataSource = new NpgsqlDataSourceBuilder(pooledConnectionString).Build();

        await using (var connection = await dataSource.OpenConnectionAsync(CancellationToken.None))
        await using (var transaction = await connection.BeginTransactionAsync(CancellationToken.None))
        {
            await SetTransactionLocalTenantContextAsync(connection, transaction, Guid.CreateVersion7());
            await transaction.CommitAsync(CancellationToken.None);
        }

        await using (var checkoutAfterCommit = await dataSource.OpenConnectionAsync(CancellationToken.None))
        {
            Assert.Null(await ReadTenantContextAsync(checkoutAfterCommit));
        }

        await using (var connection = await dataSource.OpenConnectionAsync(CancellationToken.None))
        await using (var transaction = await connection.BeginTransactionAsync(CancellationToken.None))
        {
            await SetTransactionLocalTenantContextAsync(connection, transaction, Guid.CreateVersion7());
            await transaction.RollbackAsync(CancellationToken.None);
        }

        await using (var checkoutAfterRollback = await dataSource.OpenConnectionAsync(CancellationToken.None))
        {
            Assert.Null(await ReadTenantContextAsync(checkoutAfterRollback));
        }
    }

    [Fact]
    public async Task RepeatedCommandReturnsTheCommittedOutcomeAndRejectsChangedIntent()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var intent = CreateIntent("Window sign order");

        var created = await store.CreateAsync(context, intent, "stable-command-key", CancellationToken.None);
        var replayed = await store.CreateAsync(context, intent, "stable-command-key", CancellationToken.None);
        var conflict = await store.CreateAsync(
            context,
            CreateIntent("Changed order"),
            "stable-command-key",
            CancellationToken.None);

        Assert.Equal(CreateOrderDraftStatus.Created, created.Status);
        Assert.Equal(CreateOrderDraftStatus.Replayed, replayed.Status);
        Assert.Equal(created.Order?.OrderId, replayed.Order?.OrderId);
        Assert.Equal(CreateOrderDraftStatus.IdempotencyKeyConflict, conflict.Status);
        Assert.Null(conflict.Order);
        Assert.Equal(1, await CountOrdersAsync(tenantId));
        Assert.Equal(1, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task AbandonmentPreservesPricedDraftAndCreateReceiptWhileCurrentReadsShowNewState()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var intent = CreateIntent("Abandon priced draft");
        var created = await store.CreateAsync(context, intent, "create-key", CancellationToken.None);
        var original = Assert.IsType<OrderDraftSnapshot>(created.Order);

        var abandoned = await store.AbandonAsync(
            context, new AbandonOrderDraftRequest(original.OrderId, 1), "abandon-key",
            new string('a', 64), CancellationToken.None);
        var current = await store.FindAsync(context, original.OrderId, CancellationToken.None);
        var page = await store.ListAsync(context, new ListOrderDraftsRequest(10, null), CancellationToken.None);
        var creationReplay = await store.CreateAsync(context, intent, "create-key", CancellationToken.None);
        var abandonmentReplay = await store.AbandonAsync(
            context, new AbandonOrderDraftRequest(original.OrderId, 1), "abandon-key",
            new string('a', 64), CancellationToken.None);

        Assert.Equal(AbandonOrderDraftStatus.Abandoned, abandoned.Status);
        Assert.Equal(AbandonOrderDraftStatus.Replayed, abandonmentReplay.Status);
        Assert.Equal(OrderDraftState.Abandoned, current?.State);
        Assert.Equal(2, current?.Revision);
        Assert.Equal(accountId, current?.AbandonedByAccountId);
        Assert.Equal(new FixedTimeProvider().GetUtcNow(), current?.AbandonedAt);
        Assert.Equal(original.Total, current?.Total);
        Assert.Equal(original.Lines, current?.Lines);
        Assert.Equivalent(current, abandoned.Order);
        Assert.Equivalent(current, abandonmentReplay.Order);
        Assert.Equal(OrderDraftState.Abandoned, Assert.Single(page.Items).State);
        Assert.Equal(current?.AbandonedAt, page.Items[0].AbandonedAt);
        Assert.Equal(CreateOrderDraftStatus.Replayed, creationReplay.Status);
        Assert.Equivalent(original, creationReplay.Order);
        Assert.Equal(OrderDraftState.Draft, creationReplay.Order?.State);
        Assert.Equal(1, await CountOrdersAsync(tenantId));
        Assert.Equal(2, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task AbandonmentRejectsForeignAbsentStaleAndAlreadyAbandonedDraftsWithoutReceipts()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.CreateVersion7();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantA, tenantB);
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var contextA = await ResolveContextAsync(tenantA, accountId);
        var contextB = await ResolveContextAsync(tenantB, accountId);
        var created = await store.CreateAsync(contextA, CreateIntent("Scoped order"), "create-key", CancellationToken.None);
        var orderId = Assert.IsType<OrderDraftSnapshot>(created.Order).OrderId;
        var fingerprint = new string('a', 64);

        Assert.Equal(AbandonOrderDraftStatus.NotFound, (await store.AbandonAsync(
            contextB, new AbandonOrderDraftRequest(orderId, 1), "foreign-key", fingerprint,
            CancellationToken.None)).Status);
        Assert.Equal(AbandonOrderDraftStatus.NotFound, (await store.AbandonAsync(
            contextA, new AbandonOrderDraftRequest(Guid.CreateVersion7(), 1), "missing-key", fingerprint,
            CancellationToken.None)).Status);
        Assert.Equal(AbandonOrderDraftStatus.RevisionConflict, (await store.AbandonAsync(
            contextA, new AbandonOrderDraftRequest(orderId, 2), "stale-key", fingerprint,
            CancellationToken.None)).Status);
        Assert.Equal(1, await CountReceiptsAsync(tenantA));

        Assert.Equal(AbandonOrderDraftStatus.Abandoned, (await store.AbandonAsync(
            contextA, new AbandonOrderDraftRequest(orderId, 1), "winning-key", fingerprint,
            CancellationToken.None)).Status);
        Assert.Equal(AbandonOrderDraftStatus.AlreadyAbandoned, (await store.AbandonAsync(
            contextA, new AbandonOrderDraftRequest(orderId, 1), "later-key", fingerprint,
            CancellationToken.None)).Status);
        Assert.Equal(AbandonOrderDraftStatus.IdempotencyKeyConflict, (await store.AbandonAsync(
            contextA, new AbandonOrderDraftRequest(orderId, 1), "winning-key", new string('b', 64),
            CancellationToken.None)).Status);
        Assert.Equal(2, await CountReceiptsAsync(tenantA));
        Assert.Equal(0, await CountReceiptsAsync(tenantB));
    }

    [Fact]
    public async Task ConcurrentAbandonmentWithOneKeyReplaysWinnerAndDifferentKeyCannotMutateAgain()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var created = await store.CreateAsync(context, CreateIntent("Raced order"), "create-key", CancellationToken.None);
        var request = new AbandonOrderDraftRequest(Assert.IsType<OrderDraftSnapshot>(created.Order).OrderId, 1);
        var fingerprint = new string('a', 64);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<AbandonOrderDraftResult> SubmitAsync(string key, string intent)
        {
            await start.Task;
            return await store.AbandonAsync(context, request, key, intent, CancellationToken.None);
        }

        var first = SubmitAsync("same-key", fingerprint);
        var second = SubmitAsync("same-key", fingerprint);
        start.SetResult();
        var sameKeyResults = await Task.WhenAll(first, second);
        Assert.Single(sameKeyResults, result => result.Status == AbandonOrderDraftStatus.Abandoned);
        Assert.Single(sameKeyResults, result => result.Status == AbandonOrderDraftStatus.Replayed);
        Assert.Equivalent(sameKeyResults[0].Order, sameKeyResults[1].Order);
        Assert.Equal(AbandonOrderDraftStatus.AlreadyAbandoned, (await store.AbandonAsync(
            context, request, "different-key", fingerprint, CancellationToken.None)).Status);
        Assert.Equal(2, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task ConcurrentAbandonmentWithOneKeyAndDifferentIntentsReturnsOneKeyConflict()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var created = await store.CreateAsync(context, CreateIntent("Contended order"), "create-key", CancellationToken.None);
        var request = new AbandonOrderDraftRequest(Assert.IsType<OrderDraftSnapshot>(created.Order).OrderId, 1);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<AbandonOrderDraftResult> SubmitAsync(string fingerprint)
        {
            await start.Task;
            return await store.AbandonAsync(context, request, "shared-abandon-key", fingerprint, CancellationToken.None);
        }

        var first = SubmitAsync(new string('a', 64));
        var second = SubmitAsync(new string('b', 64));
        start.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, result => result.Status == AbandonOrderDraftStatus.Abandoned);
        Assert.Single(results, result => result.Status == AbandonOrderDraftStatus.IdempotencyKeyConflict);
        Assert.Equal(2, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task ConcurrentDifferentKeysHaveOneWinnerAndReceiptsStayScopedToTheCaller()
    {
        await ApplyOrderSchemaAsync();
        var firstAccountId = Guid.CreateVersion7();
        var secondAccountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(firstAccountId, tenantId);
        await SeedAdditionalAccountAsync(secondAccountId, tenantId);
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var firstContext = await ResolveContextAsync(tenantId, firstAccountId);
        var secondContext = await ResolveContextAsync(tenantId, secondAccountId);
        var created = await store.CreateAsync(
            firstContext, CreateIntent("Different key race"), "create-key", CancellationToken.None);
        var request = new AbandonOrderDraftRequest(Assert.IsType<OrderDraftSnapshot>(created.Order).OrderId, 1);
        var fingerprint = new string('a', 64);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<AbandonOrderDraftResult> SubmitAsync(TenantContext context, string key)
        {
            await start.Task;
            return await store.AbandonAsync(context, request, key, fingerprint, CancellationToken.None);
        }

        var first = SubmitAsync(firstContext, "first-key");
        var second = SubmitAsync(secondContext, "second-key");
        start.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, result => result.Status == AbandonOrderDraftStatus.Abandoned);
        Assert.Single(results, result => result.Status == AbandonOrderDraftStatus.AlreadyAbandoned);

        var winnerContext = results[0].Status == AbandonOrderDraftStatus.Abandoned
            ? firstContext : secondContext;
        var loserContext = results[0].Status == AbandonOrderDraftStatus.Abandoned
            ? secondContext : firstContext;
        var winnerKey = results[0].Status == AbandonOrderDraftStatus.Abandoned
            ? "first-key" : "second-key";
        Assert.Equal(AbandonOrderDraftStatus.Replayed, (await store.AbandonAsync(
            winnerContext, request, winnerKey, fingerprint, CancellationToken.None)).Status);
        Assert.Equal(AbandonOrderDraftStatus.AlreadyAbandoned, (await store.AbandonAsync(
            loserContext, request, winnerKey, fingerprint, CancellationToken.None)).Status);
        Assert.Equal(2, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task RuntimeRoleCannotAbandonWithoutTenantContextOrChangePriceAndDatabaseRejectsIncompleteState()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        var orderId = Guid.CreateVersion7();
        await InsertOrderHeaderAsync(tenantId, orderId, accountId);
        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var connection = new NpgsqlConnection(runtimeConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using (var noContext = connection.CreateCommand())
        {
            noContext.CommandText = """
                UPDATE orders.order_drafts
                SET state = 'abandoned', revision = 2,
                    abandoned_at = '2026-09-23T12:00:00Z', abandoned_by_account_id = @account_id
                WHERE tenant_id = @tenant_id AND id = @order_id
                """;
            noContext.Parameters.AddWithValue("account_id", accountId);
            noContext.Parameters.AddWithValue("tenant_id", tenantId);
            noContext.Parameters.AddWithValue("order_id", orderId);
            Assert.Equal(0, await noContext.ExecuteNonQueryAsync(CancellationToken.None));
        }

        await SetTenantContextAsync(connection, tenantId);
        await using (var priceUpdate = connection.CreateCommand())
        {
            priceUpdate.CommandText = "UPDATE orders.order_drafts SET total = 100 WHERE tenant_id = @tenant_id";
            priceUpdate.Parameters.AddWithValue("tenant_id", tenantId);
            var denied = await Assert.ThrowsAsync<PostgresException>(() =>
                priceUpdate.ExecuteNonQueryAsync(CancellationToken.None));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }

        await using (var incomplete = connection.CreateCommand())
        {
            incomplete.CommandText = """
                UPDATE orders.order_drafts SET state = 'abandoned', revision = 2
                WHERE tenant_id = @tenant_id AND id = @order_id
                """;
            incomplete.Parameters.AddWithValue("tenant_id", tenantId);
            incomplete.Parameters.AddWithValue("order_id", orderId);
            var rejected = await Assert.ThrowsAsync<PostgresException>(() =>
                incomplete.ExecuteNonQueryAsync(CancellationToken.None));
            Assert.Equal(PostgresErrorCodes.CheckViolation, rejected.SqlState);
            Assert.Equal("ck_order_drafts_lifecycle", rejected.ConstraintName);
        }
    }

    [Fact]
    public async Task ConcurrentCommandsWithOneKeyCommitOnlyOneOrder()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var intent = CreateIntent("Concurrent order");

        var results = await Task.WhenAll(
            store.CreateAsync(context, intent, "concurrent-command-key", CancellationToken.None),
            store.CreateAsync(context, intent, "concurrent-command-key", CancellationToken.None));

        Assert.Contains(results, result => result.Status == CreateOrderDraftStatus.Created);
        Assert.Contains(results, result => result.Status == CreateOrderDraftStatus.Replayed);
        Assert.Single(results.Select(result => result.Order?.OrderId).Distinct());
        Assert.Equal(1, await CountOrdersAsync(tenantId));
        Assert.Equal(1, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task ConcurrentCommandsWithOneKeyAndDifferentIntentsCommitOnlyTheWinningIntent()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<CreateOrderDraftResult> SubmitAsync(OrderDraftIntent intent)
        {
            await start.Task;
            return await store.CreateAsync(context, intent, "concurrent-intent-key", CancellationToken.None);
        }

        var firstSubmission = SubmitAsync(CreateIntent("First concurrent intent"));
        var secondSubmission = SubmitAsync(CreateIntent("Second concurrent intent"));
        start.SetResult();

        var results = await Task.WhenAll(firstSubmission, secondSubmission);

        Assert.Single(results, result => result.Status == CreateOrderDraftStatus.Created);
        Assert.Single(results, result => result.Status == CreateOrderDraftStatus.IdempotencyKeyConflict);
        Assert.Equal(1, await CountOrdersAsync(tenantId));
        Assert.Equal(1, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task RetryAfterTheCommittedCreateResponseIsLostReturnsTheStoredOutcome()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var intent = CreateIntent("Response-loss order");

        // The client never receives this committed result, then retries its same operation.
        await store.CreateAsync(context, intent, "lost-response-key", CancellationToken.None);

        var retry = await store.CreateAsync(context, intent, "lost-response-key", CancellationToken.None);

        Assert.Equal(CreateOrderDraftStatus.Replayed, retry.Status);
        Assert.NotNull(retry.Order);
        Assert.Equal(1, await CountOrdersAsync(tenantId));
        Assert.Equal(1, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task IdempotencyKeysAreScopedToTheAuthorizedAccountWithinTheTenant()
    {
        await ApplyOrderSchemaAsync();

        var firstAccountId = Guid.CreateVersion7();
        var secondAccountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(firstAccountId, tenantId);
        await SeedAdditionalAccountAsync(secondAccountId, tenantId);

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var firstContext = await ResolveContextAsync(tenantId, firstAccountId);
        var secondContext = await ResolveContextAsync(tenantId, secondAccountId);

        var firstCreated = await store.CreateAsync(
            firstContext,
            CreateIntent("First account order"),
            "shared-key",
            CancellationToken.None);
        var secondCreated = await store.CreateAsync(
            secondContext,
            CreateIntent("Second account order"),
            "shared-key",
            CancellationToken.None);
        var firstReplayed = await store.CreateAsync(
            firstContext,
            CreateIntent("First account order"),
            "shared-key",
            CancellationToken.None);
        var secondReplayed = await store.CreateAsync(
            secondContext,
            CreateIntent("Second account order"),
            "shared-key",
            CancellationToken.None);

        Assert.Equal(CreateOrderDraftStatus.Created, firstCreated.Status);
        Assert.Equal(CreateOrderDraftStatus.Created, secondCreated.Status);
        Assert.Equal(CreateOrderDraftStatus.Replayed, firstReplayed.Status);
        Assert.Equal(CreateOrderDraftStatus.Replayed, secondReplayed.Status);
        Assert.NotEqual(firstCreated.Order?.OrderId, secondCreated.Order?.OrderId);
        Assert.Equal(2, await CountOrdersAsync(tenantId));
        Assert.Equal(2, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task RuntimeRoleCanBrowseAnEmptyTenantWithoutElevatedDatabasePrivileges()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource);
        var context = await ResolveContextAsync(tenantId, accountId);

        var page = await store.ListAsync(
            context,
            new ListOrderDraftsRequest(10, After: null),
            CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);

        await using var runtimeConnection = new NpgsqlConnection(runtimeConnectionString);
        await runtimeConnection.OpenAsync(CancellationToken.None);
        await SetTenantContextAsync(runtimeConnection, tenantId);
        await using var forbiddenUpdate = runtimeConnection.CreateCommand();
        forbiddenUpdate.CommandText = "UPDATE orders.order_drafts SET total = 999 WHERE tenant_id = @tenant_id";
        forbiddenUpdate.Parameters.AddWithValue("tenant_id", tenantId);
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenUpdate.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task BrowseReturnsNewestFirstAndUsesAContinuationCursorOnlyWhenMoreRowsExist()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        var oldestOrderId = Guid.CreateVersion7();
        var middleOrderId = Guid.CreateVersion7();
        var newestOrderId = Guid.CreateVersion7();
        await InsertOrderHeaderAsync(
            tenantId,
            oldestOrderId,
            accountId,
            new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        await InsertOrderHeaderAsync(
            tenantId,
            middleOrderId,
            accountId,
            new DateTimeOffset(2026, 9, 23, 11, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        await InsertOrderHeaderAsync(
            tenantId,
            newestOrderId,
            accountId,
            new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource);
        var context = await ResolveContextAsync(tenantId, accountId);

        var firstPage = await store.ListAsync(
            context,
            new ListOrderDraftsRequest(2, After: null),
            CancellationToken.None);
        var secondPage = await store.ListAsync(
            context,
            new ListOrderDraftsRequest(2, firstPage.NextCursor),
            CancellationToken.None);

        Assert.Equal([newestOrderId, middleOrderId], firstPage.Items.Select(item => item.OrderId));
        Assert.Equal(
            new OrderDraftPageCursor(
                new DateTimeOffset(2026, 9, 23, 11, 0, 0, TimeSpan.Zero),
                middleOrderId),
            firstPage.NextCursor);
        Assert.Equal([oldestOrderId], secondPage.Items.Select(item => item.OrderId));
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task BrowseUsesOrderIdToContinueThroughRowsWithTheSameTimestampWithoutDuplicatesOrGaps()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        var createdAt = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        foreach (var orderId in Enumerable.Range(0, 5).Select(_ => Guid.CreateVersion7()))
        {
            await InsertOrderHeaderAsync(tenantId, orderId, accountId, createdAt, CancellationToken.None);
        }

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource);
        var context = await ResolveContextAsync(tenantId, accountId);

        var completePage = await store.ListAsync(
            context,
            new ListOrderDraftsRequest(50, After: null),
            CancellationToken.None);
        var firstPage = await store.ListAsync(
            context,
            new ListOrderDraftsRequest(2, After: null),
            CancellationToken.None);
        var secondPage = await store.ListAsync(
            context,
            new ListOrderDraftsRequest(2, firstPage.NextCursor),
            CancellationToken.None);
        var thirdPage = await store.ListAsync(
            context,
            new ListOrderDraftsRequest(2, secondPage.NextCursor),
            CancellationToken.None);

        var pagedOrderIds = firstPage.Items
            .Concat(secondPage.Items)
            .Concat(thirdPage.Items)
            .Select(item => item.OrderId)
            .ToArray();
        Assert.Equal(completePage.Items.Select(item => item.OrderId), pagedOrderIds);
        Assert.Equal(5, pagedOrderIds.Distinct().Count());
        Assert.NotNull(firstPage.NextCursor);
        Assert.NotNull(secondPage.NextCursor);
        Assert.Null(thirdPage.NextCursor);
    }

    [Fact]
    public async Task BrowseNeverRevealsAnotherTenantsRowsWhenGivenThatTenantsCursor()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantA, tenantB);
        var tenantAOrderIds = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() };
        var tenantBCursorOrderId = Guid.CreateVersion7();
        await InsertOrderHeaderAsync(
            tenantA,
            tenantAOrderIds[0],
            accountId,
            new DateTimeOffset(2026, 9, 23, 11, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        await InsertOrderHeaderAsync(
            tenantA,
            tenantAOrderIds[1],
            accountId,
            new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        var foreignCursor = new OrderDraftPageCursor(
            new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero),
            tenantBCursorOrderId);
        await InsertOrderHeaderAsync(
            tenantB,
            tenantBCursorOrderId,
            accountId,
            foreignCursor.CreatedAt,
            CancellationToken.None);

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource);
        var tenantAContext = await ResolveContextAsync(tenantA, accountId);

        var page = await store.ListAsync(
            tenantAContext,
            new ListOrderDraftsRequest(10, foreignCursor),
            CancellationToken.None);

        Assert.Equal(tenantAOrderIds.OrderByDescending(orderId => orderId), page.Items.Select(item => item.OrderId).OrderByDescending(orderId => orderId));
        Assert.DoesNotContain(page.Items, item => item.OrderId == tenantBCursorOrderId);
    }

    private async Task SeedAuthorityRowsAsync(Guid accountId, params Guid[] tenantIds)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO identity_access.accounts (id, availability, created_at)
            VALUES (@account_id, 1, '2026-09-22T12:00:00Z');
            """;
        command.Parameters.AddWithValue("account_id", accountId);
        await command.ExecuteNonQueryAsync(CancellationToken.None);

        foreach (var tenantId in tenantIds)
        {
            await using var tenantCommand = connection.CreateCommand();
            tenantCommand.CommandText = """
                INSERT INTO tenancy.tenants (id, display_name, availability, created_at)
                VALUES (@tenant_id, 'Tenant', 1, '2026-09-22T12:00:00Z')
                """;
            tenantCommand.Parameters.AddWithValue("tenant_id", tenantId);
            await tenantCommand.ExecuteNonQueryAsync(CancellationToken.None);

            await using var membershipCommand = connection.CreateCommand();
            membershipCommand.CommandText = """
                INSERT INTO tenancy.memberships (tenant_id, account_id, availability, created_at)
                VALUES (@tenant_id, @account_id, 1, '2026-09-22T12:00:00Z')
                """;
            membershipCommand.Parameters.AddWithValue("tenant_id", tenantId);
            membershipCommand.Parameters.AddWithValue("account_id", accountId);
            await membershipCommand.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static OrderDraftIntent CreateIntent(string summary) =>
        OrderDraftIntent.Create(new CreateOrderDraftRequest(
            summary,
            "USD",
            [new OrderDraftLineInput("Printed panel", 2m, "EA", 12.5m)]));

    private Task<long> CountOrdersAsync(Guid tenantId) =>
        CountRowsAsync("SELECT count(*) FROM orders.order_drafts WHERE tenant_id = @tenant_id", tenantId);

    private Task<long> CountReceiptsAsync(Guid tenantId) =>
        CountRowsAsync("SELECT count(*) FROM orders.command_receipts WHERE tenant_id = @tenant_id", tenantId);

    private async Task<long> CountRowsAsync(string query, Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.AddWithValue("tenant_id", tenantId);
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("The count query returned no value."));
    }

    private async Task AssertCriticalSchemaGuardsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);

        await using (var constraintCommand = connection.CreateCommand())
        {
            constraintCommand.CommandText = """
                SELECT conname
                FROM pg_constraint
                WHERE connamespace = 'orders'::regnamespace
                  AND contype = 'f'
                """;
            await using var reader = await constraintCommand.ExecuteReaderAsync(CancellationToken.None);
            var constraints = new HashSet<string>(StringComparer.Ordinal);
            while (await reader.ReadAsync(CancellationToken.None))
            {
                constraints.Add(reader.GetString(0));
            }

            foreach (var expectedConstraint in new[]
                     {
                         "fk_order_drafts_tenants_tenant_id",
                         "fk_order_drafts_accounts_created_by_account_id",
                         "fk_order_drafts_accounts_abandoned_by_account_id",
                         "fk_order_draft_lines_order_drafts",
                         "fk_command_receipts_accounts_account_id",
                         "fk_command_receipts_order_drafts",
                     })
            {
                Assert.Contains(expectedConstraint, constraints);
            }
        }

        await using var policyCommand = connection.CreateCommand();
        policyCommand.CommandText = """
            SELECT tablename, policyname, qual, with_check
            FROM pg_policies
            WHERE schemaname = 'orders'
            ORDER BY tablename
            """;
        await using var policyReader = await policyCommand.ExecuteReaderAsync(CancellationToken.None);
        var policies = new Dictionary<string, (string Name, string Using, string WithCheck)>(StringComparer.Ordinal);
        while (await policyReader.ReadAsync(CancellationToken.None))
        {
            policies.Add(
                policyReader.GetString(0),
                (policyReader.GetString(1), policyReader.GetString(2), policyReader.GetString(3)));
        }

        foreach (var table in new[] { "order_drafts", "order_draft_lines", "command_receipts" })
        {
            Assert.True(policies.TryGetValue(table, out var policy), $"Missing tenant RLS policy for {table}.");
            Assert.Equal($"{table}_tenant_isolation", policy.Name);
            Assert.Contains("app.current_tenant", policy.Using, StringComparison.Ordinal);
            Assert.Contains("app.current_tenant", policy.WithCheck, StringComparison.Ordinal);
        }
        await policyReader.DisposeAsync();

        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'orders'
              AND tablename = 'order_drafts'
              AND indexname = 'ix_order_drafts_tenant_created_at_id'
            """;
        var browseIndexDefinition = await indexCommand.ExecuteScalarAsync(CancellationToken.None) as string;
        Assert.NotNull(browseIndexDefinition);
        Assert.Contains(
            "tenant_id, created_at DESC, id DESC",
            browseIndexDefinition,
            StringComparison.Ordinal);
    }

    private static async Task AssertRlsWithCheckRejectsMissingAndWrongTenantAsync(
        NpgsqlConnection connection,
        Guid wrongTenantId,
        Func<NpgsqlConnection, Task> insert)
    {
        await ResetTenantContextAsync(connection);
        var missingContext = await Assert.ThrowsAsync<PostgresException>(() => insert(connection));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, missingContext.SqlState);

        await SetTenantContextAsync(connection, wrongTenantId);
        var wrongContext = await Assert.ThrowsAsync<PostgresException>(() => insert(connection));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, wrongContext.SqlState);
    }

    private async Task InsertOrderHeaderAsync(Guid tenantId, Guid orderId, Guid accountId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await InsertOrderHeaderAsync(connection, tenantId, orderId, accountId, CancellationToken.None);
    }

    private static async Task InsertOrderHeaderAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid orderId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await InsertOrderHeaderAsync(
            connection,
            tenantId,
            orderId,
            accountId,
            new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
            cancellationToken);
    }

    private async Task InsertOrderHeaderAsync(
        Guid tenantId,
        Guid orderId,
        Guid accountId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await InsertOrderHeaderAsync(connection, tenantId, orderId, accountId, createdAt, cancellationToken);
    }

    private static async Task InsertOrderHeaderAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid orderId,
        Guid accountId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO orders.order_drafts
                (tenant_id, id, created_by_account_id, summary, currency_code, total, revision, created_at)
            VALUES
                (@tenant_id, @id, @account_id, 'Database fixture order', 'USD', 0, 1, @created_at)
            """;
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("id", orderId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("created_at", createdAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOrderLineAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid orderId,
        int position,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO orders.order_draft_lines
                (tenant_id, order_id, position, description, quantity, unit_code, unit_price, line_total)
            VALUES
                (@tenant_id, @order_id, @position, 'Fixture line', 1, 'EA', 1, 1)
            """;
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("order_id", orderId);
        command.Parameters.AddWithValue("position", position);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCommandReceiptAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid accountId,
        Guid orderId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO orders.command_receipts
                (tenant_id, account_id, operation, idempotency_key, fingerprint, order_id, response_json, created_at)
            VALUES
                (@tenant_id, @account_id, 'fixture-order-command', @idempotency_key,
                 '0000000000000000000000000000000000000000000000000000000000000000',
                 @order_id, '{}'::jsonb, '2026-09-22T12:00:00Z')
            """;
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("order_id", orderId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private TenancyDbContext CreateTenancyContext()
    {
        var builder = new DbContextOptionsBuilder<TenancyDbContext>();
        PostgresTenancyOptions.Configure(builder, ConnectionString);
        return new TenancyDbContext(builder.Options);
    }

    private async Task<string> CreateRuntimeRoleAsync()
    {
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync(CancellationToken.None);
        await using var command = admin.CreateCommand();
        command.CommandText = """
            CREATE ROLE application_orders_runtime LOGIN PASSWORD 'local-runtime-test-only';
            GRANT CONNECT ON DATABASE application_tests TO application_orders_runtime;
            GRANT USAGE ON SCHEMA orders TO application_orders_runtime;
            GRANT SELECT, INSERT ON orders.order_drafts TO application_orders_runtime;
            GRANT UPDATE (state, revision, abandoned_at, abandoned_by_account_id)
                ON orders.order_drafts TO application_orders_runtime;
            GRANT SELECT, INSERT ON orders.order_draft_lines TO application_orders_runtime;
            GRANT SELECT, INSERT ON orders.command_receipts TO application_orders_runtime;
            """;
        await command.ExecuteNonQueryAsync(CancellationToken.None);

        var runtime = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "application_orders_runtime",
            Password = "local-runtime-test-only",
        };
        return runtime.ConnectionString;
    }

    private async Task SeedAdditionalAccountAsync(Guid accountId, Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO identity_access.accounts (id, availability, created_at)
            VALUES (@account_id, 1, '2026-09-22T12:00:00Z')
            """;
        command.Parameters.AddWithValue("account_id", accountId);
        await command.ExecuteNonQueryAsync(CancellationToken.None);

        await using var membershipCommand = connection.CreateCommand();
        membershipCommand.CommandText = """
            INSERT INTO tenancy.memberships (tenant_id, account_id, availability, created_at)
            VALUES (@tenant_id, @account_id, 1, '2026-09-22T12:00:00Z')
            """;
        membershipCommand.Parameters.AddWithValue("tenant_id", tenantId);
        membershipCommand.Parameters.AddWithValue("account_id", accountId);
        await membershipCommand.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task SetTenantContextAsync(NpgsqlConnection connection, Guid tenantId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        command.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task SetTransactionLocalTenantContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.current_tenant', @tenant_id, true)",
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task ResetTenantContextAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "RESET app.current_tenant";
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task<string?> ReadTenantContextAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT NULLIF(current_setting('app.current_tenant', true), '')";
        return await command.ExecuteScalarAsync(CancellationToken.None) as string;
    }

    private static async Task<long> CountRowsVisibleToRuntimeRoleAsync(
        NpgsqlConnection connection,
        string query)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("The count query returned no value."));
    }

    private async Task<TenantContext> ResolveContextAsync(Guid tenantId, Guid accountId)
    {
        await using var database = CreateTenancyContext();
        var resolver = new ResolveTenantContext(new PostgresTenantMembershipDirectory(database));
        return await resolver.ExecuteAsync(accountId, tenantId, CancellationToken.None)
            ?? throw new InvalidOperationException("The seeded active membership was not resolved.");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    }
}
