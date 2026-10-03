using Application.Customers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Application.Customers.Postgres.Tests;

public sealed partial class CustomerPostgresTests
{
    [Fact]
    public async Task IndividualCreateReplayAndAvailabilityRetainOriginalFacts()
    {
        await MigrateAsync();
        var (tenant, account) = await SeedAsync();
        var context = await ResolveContextAsync(tenant, account);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var create = new CreateCustomerIndividual(store);
        var change = new ChangeCustomerIndividualAvailability(store);
        var request = new CreateCustomerIndividualRequest("  Cafe\u0301 Person  ", " person@example.test ", " +1 555-0100 ");

        var attempts = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            create.ExecuteAsync(context, request, "same-create", CancellationToken.None)));
        Assert.Single(attempts, result => result.Status == CreateCustomerIndividualStatus.Created);
        Assert.Equal(4, attempts.Count(result => result.Status == CreateCustomerIndividualStatus.Replayed));
        var original = attempts[0].Individual!;
        Assert.All(attempts, result => Assert.Equal(original, result.Individual));
        Assert.Equal("Café Person", original.DisplayName);
        Assert.Equal("person@example.test", original.Email);
        Assert.Equal("+1 555-0100", original.Phone);
        Assert.Equal(CustomerIndividualAvailability.Active, original.Availability);
        Assert.Equal(1, original.Revision);
        Assert.Equal(account, original.CreatedByAccountId);
        Assert.Null(original.AvailabilityChangedAt);

        var changed = await change.ExecuteAsync(context,
            new(original.IndividualId, 1, CustomerIndividualAvailability.Inactive),
            "disable", CancellationToken.None);
        Assert.Equal(ChangeCustomerIndividualAvailabilityStatus.Changed, changed.Status);
        Assert.Equal(2, changed.Individual!.Revision);
        Assert.Equal(CustomerIndividualAvailability.Inactive, changed.Individual.Availability);
        Assert.Equal(account, changed.Individual.AvailabilityChangedByAccountId);
        Assert.NotNull(changed.Individual.AvailabilityChangedAt);
        Assert.Equal(changed.Individual, await new GetCustomerIndividual(store).ExecuteAsync(
            context, original.IndividualId, CancellationToken.None));

        var createReplay = await create.ExecuteAsync(context, request, "same-create", CancellationToken.None);
        Assert.Equal(CreateCustomerIndividualStatus.Replayed, createReplay.Status);
        Assert.Equal(original, createReplay.Individual);
        var changeReplay = await change.ExecuteAsync(context,
            new(original.IndividualId, 1, CustomerIndividualAvailability.Inactive),
            "disable", CancellationToken.None);
        Assert.Equal(ChangeCustomerIndividualAvailabilityStatus.Replayed, changeReplay.Status);
        Assert.Equal(changed.Individual, changeReplay.Individual);
        var conflictingCreate = await create.ExecuteAsync(context, request with { Email = "different@example.test" },
            "same-create", CancellationToken.None);
        Assert.Equal(CreateCustomerIndividualStatus.IdempotencyKeyConflict, conflictingCreate.Status);
        var conflictingChange = await change.ExecuteAsync(context,
            new(original.IndividualId, 1, CustomerIndividualAvailability.Active),
            "disable", CancellationToken.None);
        Assert.Equal(ChangeCustomerIndividualAvailabilityStatus.IdempotencyKeyConflict, conflictingChange.Status);
        Assert.Equal(1L, await CountAsync("customers.individuals"));
        Assert.Equal(2L, await CountAsync("customers.individual_command_receipts"));
    }

    [Fact]
    public async Task IndividualAvailabilityIsRevisionCheckedAndTenantIsolated()
    {
        await MigrateAsync();
        var (tenantA, accountA) = await SeedAsync();
        var (tenantB, accountB) = await SeedAsync();
        var contextA = await ResolveContextAsync(tenantA, accountA);
        var contextB = await ResolveContextAsync(tenantB, accountB);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var created = (await new CreateCustomerIndividual(store).ExecuteAsync(contextA,
            new("Person", null, null), "create", CancellationToken.None)).Individual!;
        var change = new ChangeCustomerIndividualAvailability(store);

        Assert.Null(await store.FindIndividualAsync(contextB, created.IndividualId, CancellationToken.None));
        var wrongTenant = await change.ExecuteAsync(contextB,
            new(created.IndividualId, 1, CustomerIndividualAvailability.Inactive),
            "change", CancellationToken.None);
        Assert.Equal(ChangeCustomerIndividualAvailabilityStatus.NotFound, wrongTenant.Status);
        var initialSameState = await change.ExecuteAsync(contextA,
            new(created.IndividualId, 1, CustomerIndividualAvailability.Active),
            "initial-same-state", CancellationToken.None);
        var initialSameStateAgain = await change.ExecuteAsync(contextA,
            new(created.IndividualId, 1, CustomerIndividualAvailability.Active),
            "initial-same-state", CancellationToken.None);
        Assert.Equal(ChangeCustomerIndividualAvailabilityStatus.AlreadyInState, initialSameState.Status);
        Assert.Equal(ChangeCustomerIndividualAvailabilityStatus.AlreadyInState, initialSameStateAgain.Status);
        Assert.Equal(created, initialSameState.Individual);
        Assert.Equal(created, initialSameStateAgain.Individual);
        Assert.Equal(1L, await CountAsync("customers.individual_command_receipts"));
        var simultaneous = await Task.WhenAll(Enumerable.Range(0, 4).Select(index =>
            change.ExecuteAsync(contextA,
                new(created.IndividualId, 1, CustomerIndividualAvailability.Inactive),
                $"change-{index}", CancellationToken.None)));
        Assert.Single(simultaneous, result => result.Status == ChangeCustomerIndividualAvailabilityStatus.Changed);
        Assert.Equal(3, simultaneous.Count(result =>
            result.Status == ChangeCustomerIndividualAvailabilityStatus.RevisionConflict));
        var current = await store.FindIndividualAsync(contextA, created.IndividualId, CancellationToken.None);
        Assert.Equal(2, current!.Revision);
        Assert.Equal(CustomerIndividualAvailability.Inactive, current.Availability);
        var sameState = await change.ExecuteAsync(contextA,
            new(created.IndividualId, 2, CustomerIndividualAvailability.Inactive),
            "same-state", CancellationToken.None);
        Assert.Equal(ChangeCustomerIndividualAvailabilityStatus.AlreadyInState, sameState.Status);
        Assert.Equal(2, sameState.Individual!.Revision);
        var sameStateReplay = await change.ExecuteAsync(contextA,
            new(created.IndividualId, 2, CustomerIndividualAvailability.Inactive),
            "same-state", CancellationToken.None);
        Assert.Equal(ChangeCustomerIndividualAvailabilityStatus.AlreadyInState, sameStateReplay.Status);
        Assert.Equal(sameState.Individual, sameStateReplay.Individual);
        Assert.Equal(2L, await CountAsync("customers.individual_command_receipts"));
    }

    [Fact]
    public async Task IndividualMigrationRoundTripsWithoutChangingHistoricalTables()
    {
        await MigrateAsync();
        await using var db = CustomersPostgresMigrations.CreateContext(ConnectionString);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        await db.Database.MigrateAsync("0");
        Assert.Equal(0L, await CountAsync("pg_catalog.pg_class WHERE oid = to_regclass('customers.individuals')"));
        await db.Database.MigrateAsync();
        Assert.Equal(1L, await CountAsync("pg_catalog.pg_class WHERE oid = 'customers.individuals'::regclass"));
        Assert.Equal(1L, await CountAsync("pg_catalog.pg_class WHERE oid = 'customers.organizations'::regclass"));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task IndividualRollbackRefusesToDiscardRetainedBillingRecords()
    {
        await MigrateAsync();
        var (tenant, account) = await SeedAsync();
        var context = await ResolveContextAsync(tenant, account);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var individual = (await new CreateCustomerIndividual(new PostgresCustomerStore(source)).ExecuteAsync(
            context, new("Retained Person", null, null), "retained", CancellationToken.None)).Individual!;

        await using var db = CustomersPostgresMigrations.CreateContext(ConnectionString);
        var refused = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync("0"));
        Assert.Contains("billing records exist", refused.MessageText, StringComparison.Ordinal);
        Assert.Equal(1L, await CountAsync("customers.individuals"));
        Assert.Equal(1L, await CountAsync("customers.individual_command_receipts"));
        Assert.Equal(individual, await new GetCustomerIndividual(new PostgresCustomerStore(source)).ExecuteAsync(
            context, individual.IndividualId, CancellationToken.None));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task IndividualRowsAndReceiptsRequireTenantContextAndRuntimeCannotEditIdentity()
    {
        await MigrateAsync();
        var (tenantA, accountA) = await SeedAsync();
        var (tenantB, _) = await SeedAsync();
        var context = await ResolveContextAsync(tenantA, accountA);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime);
        var created = (await new CreateCustomerIndividual(new PostgresCustomerStore(source)).ExecuteAsync(
            context, new("Private Person", "private@example.test", null), "create",
            CancellationToken.None)).Individual!;

        await using var connection = new NpgsqlConnection(runtime);
        await connection.OpenAsync();
        await using (var noTenant = connection.CreateCommand())
        {
            noTenant.CommandText = "SELECT count(*) FROM customers.individuals";
            Assert.Equal(0L, await noTenant.ExecuteScalarAsync());
        }
        await using (var setWrongTenant = connection.CreateCommand())
        {
            setWrongTenant.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
            setWrongTenant.Parameters.AddWithValue("tenant_id", tenantB.ToString("D"));
            await setWrongTenant.ExecuteNonQueryAsync();
        }
        await using (var hidden = connection.CreateCommand())
        {
            hidden.CommandText = "SELECT count(*) FROM customers.individuals WHERE id = @id";
            hidden.Parameters.AddWithValue("id", created.IndividualId);
            Assert.Equal(0L, await hidden.ExecuteScalarAsync());
        }
        await using (var receipts = connection.CreateCommand())
        {
            receipts.CommandText = "SELECT count(*) FROM customers.individual_command_receipts";
            Assert.Equal(0L, await receipts.ExecuteScalarAsync());
        }
        await using (var forbidden = connection.CreateCommand())
        {
            forbidden.CommandText = "UPDATE customers.individuals SET display_name = 'Tampered'";
            var denied = await Assert.ThrowsAsync<PostgresException>(() => forbidden.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
        await using (var forbidden = connection.CreateCommand())
        {
            forbidden.CommandText = "DELETE FROM customers.individual_command_receipts";
            var denied = await Assert.ThrowsAsync<PostgresException>(() => forbidden.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
    }

    [Fact]
    public async Task UncommittedIndividualCreateAndReceiptRollBackTogether()
    {
        await MigrateAsync();
        var (tenant, account) = await SeedAsync();
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime);
        await using (var session = await CustomerTenantDbSession.OpenAsync(source, tenant, CancellationToken.None))
        {
            var id = Guid.NewGuid();
            await using (var insert = session.CreateCommand(CustomerSql.InsertIndividual))
            {
                insert.Parameters.AddWithValue("tenant_id", tenant);
                insert.Parameters.AddWithValue("id", id);
                insert.Parameters.AddWithValue("display_name", "Rollback Person");
                insert.Parameters.Add("email", NpgsqlTypes.NpgsqlDbType.Varchar).Value = DBNull.Value;
                insert.Parameters.Add("phone", NpgsqlTypes.NpgsqlDbType.Varchar).Value = DBNull.Value;
                insert.Parameters.AddWithValue("account_id", account);
                insert.Parameters.AddWithValue("created_at", DateTimeOffset.UnixEpoch);
                Assert.Equal(1, await insert.ExecuteNonQueryAsync());
            }
            await using var receipt = session.CreateCommand(CustomerSql.InsertIndividualReceipt);
            receipt.Parameters.AddWithValue("tenant_id", tenant);
            receipt.Parameters.AddWithValue("account_id", account);
            receipt.Parameters.AddWithValue("operation", "create");
            receipt.Parameters.AddWithValue("key", "rolled-back");
            receipt.Parameters.AddWithValue("fingerprint", new string('a', 64));
            receipt.Parameters.AddWithValue("individual_id", id);
            receipt.Parameters.AddWithValue("display_name", "Rollback Person");
            receipt.Parameters.Add("email", NpgsqlTypes.NpgsqlDbType.Varchar).Value = DBNull.Value;
            receipt.Parameters.Add("phone", NpgsqlTypes.NpgsqlDbType.Varchar).Value = DBNull.Value;
            receipt.Parameters.AddWithValue("availability", 1);
            receipt.Parameters.AddWithValue("revision", 1L);
            receipt.Parameters.AddWithValue("created_by_account_id", account);
            receipt.Parameters.AddWithValue("created_at", DateTimeOffset.UnixEpoch);
            receipt.Parameters.Add("availability_changed_by_account_id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = DBNull.Value;
            receipt.Parameters.Add("availability_changed_at", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = DBNull.Value;
            Assert.Equal(1, await receipt.ExecuteScalarAsync());
        }
        Assert.Equal(0L, await CountAsync("customers.individuals"));
        Assert.Equal(0L, await CountAsync("customers.individual_command_receipts"));
    }
}
