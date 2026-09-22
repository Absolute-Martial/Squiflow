using Application.Orders;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed class OrderMigrationAndRlsTests : PostgresTestDatabase
{
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
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO orders.order_drafts
                (tenant_id, id, created_by_account_id, summary, currency_code, total, revision, created_at)
            VALUES
                (@tenant_id, @id, @account_id, 'Database fixture order', 'USD', 0, 1, '2026-09-22T12:00:00Z')
            """;
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("id", orderId);
        command.Parameters.AddWithValue("account_id", accountId);
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
