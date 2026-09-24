using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Customers;
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
        var attribution = new OrderDraftCustomerAttribution().TargetModel.FindEntityType(draftEntity);
        var revision = new OrderDraftRevision().TargetModel.FindEntityType(draftEntity);

        Assert.NotNull(initial);
        Assert.NotNull(browse);
        Assert.NotNull(abandonment);
        Assert.NotNull(attribution);
        Assert.NotNull(revision);
        Assert.Null(initial.FindProperty("State"));
        Assert.Null(browse.FindProperty("State"));
        Assert.Null(initial.FindProperty("AbandonedAt"));
        Assert.Null(browse.FindProperty("AbandonedAt"));
        Assert.NotNull(abandonment.FindProperty("State"));
        Assert.NotNull(abandonment.FindProperty("AbandonedAt"));
        Assert.NotNull(abandonment.FindProperty("AbandonedByAccountId"));
        Assert.Null(abandonment.FindProperty("CustomerOrganizationId"));
        Assert.Null(abandonment.FindProperty("CustomerProgramId"));
        Assert.NotNull(attribution.FindProperty("CustomerOrganizationId"));
        Assert.NotNull(attribution.FindProperty("CustomerProgramId"));
        Assert.Contains("revision >= 1", revision.GetCheckConstraints()
            .Single(constraint => constraint.Name == "ck_order_drafts_lifecycle").Sql, StringComparison.Ordinal);

        static bool HasBrowseIndex(Microsoft.EntityFrameworkCore.Metadata.IEntityType entity) =>
            entity.GetIndexes().Any(index =>
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(["TenantId", "CreatedAt", "Id"]));

        Assert.False(HasBrowseIndex(initial));
        Assert.True(HasBrowseIndex(browse));
        Assert.True(HasBrowseIndex(abandonment));
        Assert.True(HasBrowseIndex(attribution));
        Assert.True(HasBrowseIndex(revision));
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
    public async Task AttributedDraftPersistsCustomerContextAndReplaysTheVersionedReceipt()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var organizationId = Guid.CreateVersion7();
        var programId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await SeedCustomerContextAsync(tenantId, accountId, organizationId, programId);

        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var tenant = await ResolveContextAsync(tenantId, accountId);
        var context = new CustomerOrderContext(organizationId, programId);
        var intent = CreateIntent("Program materials", context);

        var created = await store.CreateAsync(tenant, intent, "attributed-create", CancellationToken.None);
        var replayed = await store.CreateAsync(tenant, intent, "attributed-create", CancellationToken.None);
        var found = await store.FindAsync(tenant, created.Order!.OrderId, CancellationToken.None);
        var page = await store.ListAsync(
            tenant, new ListOrderDraftsRequest(10, null), CancellationToken.None);

        Assert.Equal(CreateOrderDraftStatus.Created, created.Status);
        Assert.Equal(CreateOrderDraftStatus.Replayed, replayed.Status);
        Assert.Equal(context, created.Order.CustomerContext);
        Assert.Equal(context, replayed.Order?.CustomerContext);
        Assert.Equal(context, found?.CustomerContext);
        Assert.Equal(context, Assert.Single(page.Items).CustomerContext);
        Assert.Equal(1, await CountOrdersAsync(tenantId));

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var receipt = connection.CreateCommand();
        receipt.CommandText = """
            SELECT response_json::text FROM orders.command_receipts
            WHERE tenant_id = @tenant_id AND idempotency_key = 'attributed-create'
            """;
        receipt.Parameters.AddWithValue("tenant_id", tenantId);
        using var envelope = JsonDocument.Parse((string)(await receipt.ExecuteScalarAsync(CancellationToken.None))!);
        Assert.Equal(2, envelope.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task RevisionReplacesPricedContentAndReplaysItsCommittedSnapshot()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.CreateVersion7();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantA, tenantB);
        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var contextA = await ResolveContextAsync(tenantA, accountId);
        var contextB = await ResolveContextAsync(tenantB, accountId);
        var created = await store.CreateAsync(
            contextA, CreateIntent("Original"), "create-key", CancellationToken.None);
        var original = Assert.IsType<OrderDraftSnapshot>(created.Order);
        var request = new ReviseOrderDraftRequest(
            original.OrderId, 1, "Revised", "USD",
            [new OrderDraftLineInput("Replacement", 3m, "EA", 7m)]);
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            request.Summary, request.CurrencyCode, request.Lines, request.CustomerContext));
        var fingerprint = new string('a', 64);

        Assert.Equal(ReviseOrderDraftStatus.NotFound, (await store.ReviseAsync(
            contextB, request, intent, "foreign-key", fingerprint, CancellationToken.None)).Status);
        var revised = await store.ReviseAsync(
            contextA, request, intent, "revise-key", fingerprint, CancellationToken.None);
        var replayed = await store.ReviseAsync(
            contextA, request, intent, "revise-key", fingerprint, CancellationToken.None);
        Assert.Equal(ReviseOrderDraftStatus.Revised, revised.Status);
        Assert.Equal(ReviseOrderDraftStatus.Replayed, replayed.Status);
        Assert.Equal(2, revised.Order?.Revision);
        Assert.Equal(21m, revised.Order?.Total);
        Assert.Equal("Replacement", Assert.Single(revised.Order!.Lines).Description);
        Assert.Equivalent(revised.Order, replayed.Order);
        Assert.Equal(ReviseOrderDraftStatus.IdempotencyKeyConflict, (await store.ReviseAsync(
            contextA, request, intent, "revise-key", new string('b', 64), CancellationToken.None)).Status);
        Assert.Equal(ReviseOrderDraftStatus.RevisionConflict, (await store.ReviseAsync(
            contextA, request, intent, "stale-key", fingerprint, CancellationToken.None)).Status);
        Assert.Equal(CreateOrderDraftStatus.Replayed, (await store.CreateAsync(
            contextA, CreateIntent("Original"), "create-key", CancellationToken.None)).Status);
        Assert.Equivalent(original, (await store.CreateAsync(
            contextA, CreateIntent("Original"), "create-key", CancellationToken.None)).Order);
        Assert.Equal(2, await CountReceiptsAsync(tenantA));
        Assert.Equal(0, await CountReceiptsAsync(tenantB));
        Assert.Equal(1, await CountRowsAsync(
            "SELECT count(*) FROM orders.order_draft_lines WHERE tenant_id = @tenant_id", tenantA));

        var abandoned = await store.AbandonAsync(
            contextA, new AbandonOrderDraftRequest(original.OrderId, 2),
            "abandon-key", fingerprint, CancellationToken.None);
        Assert.Equal(AbandonOrderDraftStatus.Abandoned, abandoned.Status);
        Assert.Equal(3, abandoned.Order?.Revision);
        Assert.Equal(ReviseOrderDraftStatus.AlreadyAbandoned, (await store.ReviseAsync(
            contextA, new ReviseOrderDraftRequest(original.OrderId, 3, request.Summary,
                request.CurrencyCode, request.Lines), intent, "later-key", fingerprint,
            CancellationToken.None)).Status);
        await using var runtimeConnection = new NpgsqlConnection(runtimeConnectionString);
        await runtimeConnection.OpenAsync(CancellationToken.None);
        await SetTenantContextAsync(runtimeConnection, tenantA);
        await using var forbiddenDelete = runtimeConnection.CreateCommand();
        forbiddenDelete.CommandText = """
            DELETE FROM orders.order_draft_lines
            WHERE tenant_id = @tenant_id AND order_id = @order_id
            """;
        forbiddenDelete.Parameters.AddWithValue("tenant_id", tenantA);
        forbiddenDelete.Parameters.AddWithValue("order_id", original.OrderId);
        Assert.Equal(0, await forbiddenDelete.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(1, await CountRowsAsync(
            "SELECT count(*) FROM orders.order_draft_lines WHERE tenant_id = @tenant_id", tenantA));
    }

    [Fact]
    public async Task RejectedRevisionDoesNotPartiallyReplaceOrderOrReceipt()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var firstOrganization = Guid.CreateVersion7();
        var secondOrganization = Guid.CreateVersion7();
        var programId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await SeedCustomerContextAsync(tenantId, accountId, firstOrganization, programId);
        await SeedCustomerContextAsync(tenantId, accountId, secondOrganization, null);
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var original = (await store.CreateAsync(
            context, CreateIntent("Original"), "create-key", CancellationToken.None)).Order!;
        var badContext = new CustomerOrderContext(secondOrganization, programId);
        var request = new ReviseOrderDraftRequest(
            original.OrderId, 1, "Bad revision", "USD",
            [new OrderDraftLineInput("Bad line", 1m, "EA", 7m)], badContext);
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            request.Summary, request.CurrencyCode, request.Lines, request.CustomerContext));
        var error = await Assert.ThrowsAsync<PostgresException>(() => store.ReviseAsync(
            context, request, intent, "bad-key", new string('a', 64), CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("fk_order_drafts_customer_program", error.ConstraintName);
        Assert.Equivalent(original, await store.FindAsync(context, original.OrderId, CancellationToken.None));
        Assert.Equal(1, await CountReceiptsAsync(tenantId));
        Assert.Equal(1, await CountRowsAsync(
            "SELECT count(*) FROM orders.order_draft_lines WHERE tenant_id = @tenant_id", tenantId));
    }

    [Fact]
    public async Task ConcurrentRevisionWithOneKeyCommitsOneReplacement()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var created = await store.CreateAsync(context, CreateIntent("Original"), "create-key", CancellationToken.None);
        var request = new ReviseOrderDraftRequest(created.Order!.OrderId, 1, "Revision", "USD",
            [new OrderDraftLineInput("New line", 2m, "EA", 9m)]);
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            request.Summary, request.CurrencyCode, request.Lines));
        var fingerprint = new string('a', 64);
        var results = await Task.WhenAll(
            store.ReviseAsync(context, request, intent, "same-key", fingerprint, CancellationToken.None),
            store.ReviseAsync(context, request, intent, "same-key", fingerprint, CancellationToken.None));
        Assert.Single(results, result => result.Status == ReviseOrderDraftStatus.Revised);
        Assert.Single(results, result => result.Status == ReviseOrderDraftStatus.Replayed);
        Assert.Equivalent(results[0].Order, results[1].Order);
        Assert.Equal(2, await CountReceiptsAsync(tenantId));
        Assert.Equal(1, await CountRowsAsync(
            "SELECT count(*) FROM orders.order_draft_lines WHERE tenant_id = @tenant_id", tenantId));
    }

    [Fact]
    public async Task DatabaseRejectsForeignTenantAndWrongOrganizationProgramAttribution()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var firstOrganization = Guid.CreateVersion7();
        var secondOrganization = Guid.CreateVersion7();
        var programId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantA, tenantB);
        await SeedCustomerContextAsync(tenantA, accountId, firstOrganization, programId);
        await SeedCustomerContextAsync(tenantA, accountId, secondOrganization, null);

        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var otherTenant = await ResolveContextAsync(tenantB, accountId);
        var sameTenant = await ResolveContextAsync(tenantA, accountId);

        var foreignTenant = await Assert.ThrowsAsync<PostgresException>(() => store.CreateAsync(
            otherTenant,
            CreateIntent("Foreign tenant", new CustomerOrderContext(firstOrganization, programId)),
            "foreign-tenant-context",
            CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, foreignTenant.SqlState);
        Assert.Equal("fk_order_drafts_customer_organization", foreignTenant.ConstraintName);

        var wrongParent = await Assert.ThrowsAsync<PostgresException>(() => store.CreateAsync(
            sameTenant,
            CreateIntent("Wrong program parent", new CustomerOrderContext(secondOrganization, programId)),
            "wrong-parent-context",
            CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, wrongParent.SqlState);
        Assert.Equal("fk_order_drafts_customer_program", wrongParent.ConstraintName);
        Assert.Equal(0, await CountOrdersAsync(tenantA));
        Assert.Equal(0, await CountOrdersAsync(tenantB));
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

        var committedTenant = Guid.CreateVersion7();
        await using (var session = await OrderTenantDbSession.OpenAsync(
            dataSource, committedTenant, CancellationToken.None))
        {
            await using var command = session.CreateCommand("SELECT current_setting('app.current_tenant', true)");
            Assert.Equal(committedTenant.ToString("D"), await command.ExecuteScalarAsync(CancellationToken.None));
            await session.CommitAsync(CancellationToken.None);
            Assert.Throws<InvalidOperationException>(() => session.CreateCommand("SELECT 1"));
        }

        await using (var checkoutAfterCommit = await dataSource.OpenConnectionAsync(CancellationToken.None))
        {
            Assert.Null(await ReadTenantContextAsync(checkoutAfterCommit));
        }

        var rolledBackTenant = Guid.CreateVersion7();
        await using (var session = await OrderTenantDbSession.OpenAsync(
            dataSource, rolledBackTenant, CancellationToken.None))
        {
            await using var command = session.CreateCommand("SELECT current_setting('app.current_tenant', true)");
            Assert.Equal(rolledBackTenant.ToString("D"), await command.ExecuteScalarAsync(CancellationToken.None));
            await session.RollbackAsync(CancellationToken.None);
            Assert.Throws<InvalidOperationException>(() => session.CreateCommand("SELECT 1"));
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
    public async Task PersistedLegacyReceiptsReplayAlongsideVersionedCreateAndAbandonReceipts()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var intent = CreateIntent("Legacy receipt order");
        var created = Assert.IsType<OrderDraftSnapshot>((await store.CreateAsync(
            context, intent, "new-create-key", CancellationToken.None)).Order);
        var abandonFingerprint = new string('a', 64);
        var abandoned = Assert.IsType<OrderDraftSnapshot>((await store.AbandonAsync(
            context, new AbandonOrderDraftRequest(created.OrderId, 1), "new-abandon-key",
            abandonFingerprint, CancellationToken.None)).Order);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        foreach (var (operation, key, expectedState) in new[]
                 {
                     ("create-order-draft", "new-create-key", 0),
                     ("abandon-order-draft", "new-abandon-key", 1),
                 })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT response_json::text
                FROM orders.command_receipts
                WHERE tenant_id = @tenant_id AND account_id = @account_id
                  AND operation = @operation AND idempotency_key = @key
                """;
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("account_id", accountId);
            command.Parameters.AddWithValue("operation", operation);
            command.Parameters.AddWithValue("key", key);
            var json = Assert.IsType<string>(await command.ExecuteScalarAsync(CancellationToken.None));
            using var document = JsonDocument.Parse(json);
            var envelope = document.RootElement;
            Assert.Equal(1, envelope.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(operation, envelope.GetProperty("operation").GetString());
            Assert.Equal(
                operation == "create-order-draft" ? "order-draft-created" : "order-draft-abandoned",
                envelope.GetProperty("resultType").GetString());
            Assert.Equal(created.OrderId, envelope.GetProperty("payload").GetProperty("orderId").GetGuid());
            Assert.Equal(expectedState, envelope.GetProperty("payload").GetProperty("state").GetInt32());
        }

        // These literal snapshots model rows persisted before envelopes existed. The create
        // fixture also predates lifecycle fields; neither comes from the current serializer.
        var legacyCreate = $$"""
            {
              "orderId": "{{created.OrderId:D}}",
              "tenantId": "{{tenantId:D}}",
              "createdByAccountId": "{{accountId:D}}",
              "summary": "Legacy receipt order",
              "currencyCode": "USD",
              "total": 25,
              "revision": 1,
              "createdAt": "2026-09-22T12:00:00+00:00",
              "lines": [{"position": 1, "description": "Printed panel", "quantity": 2,
                         "unitCode": "EA", "unitPrice": 12.5, "lineTotal": 25}]
            }
            """;
        var legacyAbandon = $$"""
            {
              "orderId": "{{created.OrderId:D}}",
              "tenantId": "{{tenantId:D}}",
              "createdByAccountId": "{{accountId:D}}",
              "summary": "Legacy receipt order",
              "currencyCode": "USD",
              "total": 25,
              "revision": 2,
              "createdAt": "2026-09-22T12:00:00+00:00",
              "lines": [{"position": 1, "description": "Printed panel", "quantity": 2,
                         "unitCode": "EA", "unitPrice": 12.5, "lineTotal": 25}],
              "state": 1,
              "abandonedAt": "2026-09-22T12:00:00+00:00",
              "abandonedByAccountId": "{{accountId:D}}"
            }
            """;
        await InsertPersistedReceiptFixtureAsync(
            connection, tenantId, accountId, created.OrderId, "create-order-draft",
            "legacy-create-key", intent.Fingerprint, legacyCreate);
        await InsertPersistedReceiptFixtureAsync(
            connection, tenantId, accountId, created.OrderId, "abandon-order-draft",
            "legacy-abandon-key", abandonFingerprint, legacyAbandon);

        var createReplay = await store.CreateAsync(context, intent, "legacy-create-key", CancellationToken.None);
        var abandonReplay = await store.AbandonAsync(
            context, new AbandonOrderDraftRequest(created.OrderId, 1),
            "legacy-abandon-key", abandonFingerprint, CancellationToken.None);
        Assert.Equal(CreateOrderDraftStatus.Replayed, createReplay.Status);
        Assert.Equivalent(created, createReplay.Order);
        Assert.Equal(AbandonOrderDraftStatus.Replayed, abandonReplay.Status);
        Assert.Equivalent(abandoned, abandonReplay.Order);
    }

    [Fact]
    public async Task UnsupportedOrMismatchedReceiptEnvelopesFailClosed()
    {
        await ApplyOrderSchemaAsync();

        var accountId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenantId, accountId);
        var intent = CreateIntent("Receipt envelope order");
        await store.CreateAsync(context, intent, "envelope-key", CancellationToken.None);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var read = connection.CreateCommand();
        read.CommandText = """
            SELECT response_json::text
            FROM orders.command_receipts
            WHERE tenant_id = @tenant_id AND account_id = @account_id
              AND operation = 'create-order-draft' AND idempotency_key = 'envelope-key'
            """;
        read.Parameters.AddWithValue("tenant_id", tenantId);
        read.Parameters.AddWithValue("account_id", accountId);
        var originalJson = Assert.IsType<string>(await read.ExecuteScalarAsync(CancellationToken.None));

        foreach (var change in new[]
                 {
                     "wrong-operation", "wrong-result-type", "future-version", "missing-version",
                     "missing-result-type", "missing-payload", "wrong-tenant", "wrong-order",
                 })
        {
            var envelope = JsonNode.Parse(originalJson)?.AsObject()
                ?? throw new InvalidOperationException("The inserted receipt was not an object.");
            switch (change)
            {
                case "wrong-operation":
                    envelope["operation"] = "abandon-order-draft";
                    break;
                case "future-version":
                    envelope["schemaVersion"] = 2;
                    break;
                case "wrong-result-type":
                    envelope["resultType"] = "order-draft-abandoned";
                    break;
                case "missing-version":
                    envelope.Remove("schemaVersion");
                    break;
                case "missing-result-type":
                    envelope.Remove("resultType");
                    break;
                case "missing-payload":
                    envelope.Remove("payload");
                    break;
                case "wrong-tenant":
                    envelope["payload"]!["tenantId"] = Guid.CreateVersion7().ToString("D");
                    break;
                case "wrong-order":
                    envelope["payload"]!["orderId"] = Guid.CreateVersion7().ToString("D");
                    break;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE orders.command_receipts
                SET response_json = @response_json::jsonb
                WHERE tenant_id = @tenant_id AND account_id = @account_id
                  AND operation = 'create-order-draft' AND idempotency_key = 'envelope-key'
                """;
            command.Parameters.AddWithValue("response_json", envelope.ToJsonString());
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("account_id", accountId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(CancellationToken.None));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.CreateAsync(context, intent, "envelope-key", CancellationToken.None));
        }
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
    public async Task RuntimeRoleCannotAbandonWithoutTenantContextOrChangeCreationIdentityAndDatabaseRejectsIncompleteState()
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
        await using (var creationUpdate = connection.CreateCommand())
        {
            creationUpdate.CommandText = "UPDATE orders.order_drafts SET created_at = now() WHERE tenant_id = @tenant_id";
            creationUpdate.Parameters.AddWithValue("tenant_id", tenantId);
            var denied = await Assert.ThrowsAsync<PostgresException>(() =>
                creationUpdate.ExecuteNonQueryAsync(CancellationToken.None));
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
        forbiddenUpdate.CommandText = "UPDATE orders.order_drafts SET created_at = now() WHERE tenant_id = @tenant_id";
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

    private async Task SeedCustomerContextAsync(
        Guid tenantId,
        Guid accountId,
        Guid organizationId,
        Guid? programId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var organization = connection.CreateCommand();
        organization.CommandText = """
            INSERT INTO customers.organizations
                (tenant_id, id, created_by_account_id, display_name, created_at)
            VALUES (@tenant_id, @organization_id, @account_id, 'Customer organization', '2026-09-24T12:00:00Z')
            """;
        organization.Parameters.AddWithValue("tenant_id", tenantId);
        organization.Parameters.AddWithValue("organization_id", organizationId);
        organization.Parameters.AddWithValue("account_id", accountId);
        await organization.ExecuteNonQueryAsync(CancellationToken.None);

        if (programId is { } id)
        {
            await using var program = connection.CreateCommand();
            program.CommandText = """
                INSERT INTO customers.programs
                    (tenant_id, id, organization_id, created_by_account_id, display_name, created_at)
                VALUES (@tenant_id, @program_id, @organization_id, @account_id,
                        'Customer program', '2026-09-24T12:00:00Z')
                """;
            program.Parameters.AddWithValue("tenant_id", tenantId);
            program.Parameters.AddWithValue("program_id", id);
            program.Parameters.AddWithValue("organization_id", organizationId);
            program.Parameters.AddWithValue("account_id", accountId);
            await program.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static OrderDraftIntent CreateIntent(
        string summary,
        CustomerOrderContext? customerContext = null) =>
        OrderDraftIntent.Create(new CreateOrderDraftRequest(
            summary,
            "USD",
            [new OrderDraftLineInput("Printed panel", 2m, "EA", 12.5m)],
            customerContext));

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
            WHERE schemaname = 'orders' AND policyname LIKE '%_tenant_isolation'
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

        await using var deletePolicyCommand = connection.CreateCommand();
        deletePolicyCommand.CommandText = """
            SELECT permissive, cmd, qual FROM pg_policies
            WHERE schemaname = 'orders' AND tablename = 'order_draft_lines'
              AND policyname = 'order_draft_lines_draft_delete'
            """;
        await using var deletePolicy = await deletePolicyCommand.ExecuteReaderAsync(CancellationToken.None);
        Assert.True(await deletePolicy.ReadAsync(CancellationToken.None));
        Assert.Equal("RESTRICTIVE", deletePolicy.GetString(0));
        Assert.Equal("DELETE", deletePolicy.GetString(1));
        Assert.Contains("FOR UPDATE", deletePolicy.GetString(2), StringComparison.OrdinalIgnoreCase);
        await deletePolicy.DisposeAsync();

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

    private static async Task InsertPersistedReceiptFixtureAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid accountId,
        Guid orderId,
        string operation,
        string idempotencyKey,
        string fingerprint,
        string responseJson)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO orders.command_receipts
                (tenant_id, account_id, operation, idempotency_key, fingerprint, order_id, response_json, created_at)
            VALUES
                (@tenant_id, @account_id, @operation, @idempotency_key, @fingerprint,
                 @order_id, @response_json::jsonb, '2026-09-22T12:00:00Z')
            """;
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("order_id", orderId);
        command.Parameters.AddWithValue("response_json", responseJson);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
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
            GRANT UPDATE (summary, currency_code, total, customer_organization_id,
                          customer_program_id, state, revision, abandoned_at, abandoned_by_account_id)
                ON orders.order_drafts TO application_orders_runtime;
            GRANT SELECT, INSERT, DELETE ON orders.order_draft_lines TO application_orders_runtime;
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
