using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed class CoreApiRuntimeRoleProvisioningTests : PostgresTestDatabase
{
    private const string RuntimeRole = "application_runtime_test";
    private const string RuntimePassword = "local-runtime-test-only";

    [Fact]
    public async Task ProvisioningAnExistingRoleIsRepeatableAndEnforcesTheCurrentPrivilegeBoundary()
    {
        await ApplyOrderSchemaAsync();
        await CreateRoleAsync();

        await ApplyProvisioningAsync();
        await ApplyProvisioningAsync();

        var runtimeConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = RuntimeRole,
            Password = RuntimePassword,
        }.ConnectionString;
        await using var runtime = new NpgsqlConnection(runtimeConnectionString);
        await runtime.OpenAsync(CancellationToken.None);

        foreach (var table in new[]
                 {
                     "identity_access.accounts", "identity_access.external_identity_bindings",
                     "tenancy.tenants", "tenancy.memberships",
                     "customers.organizations", "customers.programs",
                     "customers.organization_receipts", "customers.program_receipts",
                     "orders.order_drafts",
                     "orders.order_draft_lines", "orders.command_receipts",
                 })
        {
            await using var command = runtime.CreateCommand();
            command.CommandText = $"SELECT count(*) FROM {table}";
            Assert.Equal(0L, await command.ExecuteScalarAsync(CancellationToken.None));
        }

        await using var forbiddenDelete = runtime.CreateCommand();
        forbiddenDelete.CommandText = "DELETE FROM orders.order_drafts";
        var deleteFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenDelete.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, deleteFailure.SqlState);

        await using var forbiddenCustomerDelete = runtime.CreateCommand();
        forbiddenCustomerDelete.CommandText = "DELETE FROM customers.organizations";
        var customerDeleteFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenCustomerDelete.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, customerDeleteFailure.SqlState);

        await using var forbiddenCreationUpdate = runtime.CreateCommand();
        forbiddenCreationUpdate.CommandText = "UPDATE orders.order_drafts SET created_at = now()";
        var updateFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenCreationUpdate.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, updateFailure.SqlState);

        await using var allowedDraftUpdate = runtime.CreateCommand();
        allowedDraftUpdate.CommandText = """
            UPDATE orders.order_drafts
            SET summary = summary, currency_code = currency_code, total = total,
                customer_organization_id = customer_organization_id,
                customer_program_id = customer_program_id, state = state, revision = revision
            WHERE false
            """;
        Assert.Equal(0, await allowedDraftUpdate.ExecuteNonQueryAsync(CancellationToken.None));

        await using var allowedLineDelete = runtime.CreateCommand();
        allowedLineDelete.CommandText = "DELETE FROM orders.order_draft_lines WHERE false";
        Assert.Equal(0, await allowedLineDelete.ExecuteNonQueryAsync(CancellationToken.None));

        await using var forbiddenDdl = runtime.CreateCommand();
        forbiddenDdl.CommandText = "CREATE TABLE orders.forbidden_runtime_ddl (id integer)";
        var ddlFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenDdl.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ddlFailure.SqlState);

        await using var forbiddenRoleCreate = runtime.CreateCommand();
        forbiddenRoleCreate.CommandText = "CREATE ROLE application_forbidden_runtime_role";
        var roleFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenRoleCreate.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, roleFailure.SqlState);
    }

    [Fact]
    public async Task ProvisioningRejectsAnExistingRoleWithDeletePrivilegeWithoutApplyingPartialGrants()
    {
        await ApplyOrderSchemaAsync();
        await CreateRoleAsync();
        await using (var admin = new NpgsqlConnection(ConnectionString))
        {
            await admin.OpenAsync(CancellationToken.None);
            await using var grant = admin.CreateCommand();
            grant.CommandText = "GRANT DELETE ON orders.order_drafts TO application_runtime_test";
            await grant.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var failure = await Assert.ThrowsAsync<PostgresException>(ApplyProvisioningAsync);
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT has_table_privilege(@role, 'identity_access.accounts', 'SELECT')";
        check.Parameters.AddWithValue("role", RuntimeRole);
        Assert.Equal(false, await check.ExecuteScalarAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ProvisioningRejectsMembershipInABroadPredefinedRole()
    {
        await ApplyOrderSchemaAsync();
        await CreateRoleAsync();
        await using (var admin = new NpgsqlConnection(ConnectionString))
        {
            await admin.OpenAsync(CancellationToken.None);
            await using var grant = admin.CreateCommand();
            grant.CommandText = "GRANT pg_read_all_data TO application_runtime_test";
            await grant.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var failure = await Assert.ThrowsAsync<PostgresException>(ApplyProvisioningAsync);
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        Assert.Contains("inherit or assume", failure.MessageText, StringComparison.Ordinal);
    }

    private async Task CreateRoleAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE ROLE application_runtime_test LOGIN PASSWORD 'local-runtime-test-only'";
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private async Task ApplyProvisioningAsync()
    {
        var script = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "DatabaseProvisioning", "grant-core-api-runtime.sql"));

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None);
        await using (var selectRole = connection.CreateCommand())
        {
            selectRole.Transaction = transaction;
            selectRole.CommandText = "SELECT set_config('app.provision_runtime_role', @role, true)";
            selectRole.Parameters.AddWithValue("role", RuntimeRole);
            await selectRole.ExecuteNonQueryAsync(CancellationToken.None);
        }

        await using (var apply = connection.CreateCommand())
        {
            apply.Transaction = transaction;
            apply.CommandText = script;
            await apply.ExecuteNonQueryAsync(CancellationToken.None);
        }

        await transaction.CommitAsync(CancellationToken.None);
    }
}
