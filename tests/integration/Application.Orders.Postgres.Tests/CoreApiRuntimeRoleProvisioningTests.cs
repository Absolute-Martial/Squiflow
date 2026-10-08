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
                     "tenancy.tenant_authorization_state",
                     "tenancy.tenant_authorization_proposals",
                     "tenancy.tenant_permission_grants",
                     "tenancy.custom_roles",
                     "tenancy.custom_role_assignments",
                     "tenancy.owner_transfer_receipts",
                     "customers.organizations", "customers.programs",
                      "customers.organization_receipts", "customers.program_receipts",
                      "customers.duplicate_cases", "customers.duplicate_command_receipts", "customers.customer_redirects",
                      "customers.imports", "customers.import_rows", "customers.import_work",
                      "catalog.units", "catalog.items", "catalog.unit_conversions", "catalog.command_receipts",
                     "pricing.price_revisions", "pricing.command_receipts", "pricing.override_policies",
                     "profiles.policy_heads", "profiles.policy_revisions", "profiles.publications",
                     "profiles.authority", "profiles.command_receipts",
                     "orders.order_drafts",
                     "orders.order_draft_lines", "orders.command_receipts", "orders.program_order_metadata",
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

        await using var allowedTenantRevisionUpdate = runtime.CreateCommand();
        allowedTenantRevisionUpdate.CommandText = "UPDATE tenancy.tenants SET revision = revision WHERE false";
        Assert.Equal(0, await allowedTenantRevisionUpdate.ExecuteNonQueryAsync(CancellationToken.None));

        await using var allowedMembershipOwnerUpdate = runtime.CreateCommand();
        allowedMembershipOwnerUpdate.CommandText =
            "UPDATE tenancy.memberships SET revision = revision, is_initial_owner = is_initial_owner WHERE false";
        Assert.Equal(0, await allowedMembershipOwnerUpdate.ExecuteNonQueryAsync(CancellationToken.None));

        await using var forbiddenMembershipAvailabilityUpdate = runtime.CreateCommand();
        forbiddenMembershipAvailabilityUpdate.CommandText =
            "UPDATE tenancy.memberships SET availability = availability WHERE false";
        var membershipUpdateFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenMembershipAvailabilityUpdate.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, membershipUpdateFailure.SqlState);

        await using var allowedAuthorizationStateUpdate = runtime.CreateCommand();
        allowedAuthorizationStateUpdate.CommandText =
            "UPDATE tenancy.tenant_authorization_state SET revision = revision, updated_at = updated_at WHERE false";
        Assert.Equal(0, await allowedAuthorizationStateUpdate.ExecuteNonQueryAsync(CancellationToken.None));

        await using var allowedProposalUpdate = runtime.CreateCommand();
        allowedProposalUpdate.CommandText =
            "UPDATE tenancy.tenant_authorization_proposals " +
            "SET status = status, applied_authorization_revision = applied_authorization_revision, " +
            "attempt_count = attempt_count, failure_code = failure_code, updated_at = updated_at WHERE false";
        Assert.Equal(0, await allowedProposalUpdate.ExecuteNonQueryAsync(CancellationToken.None));

        await using var forbiddenProposalIntentRewrite = runtime.CreateCommand();
        forbiddenProposalIntentRewrite.CommandText =
            "UPDATE tenancy.tenant_authorization_proposals SET request_fingerprint = request_fingerprint WHERE false";
        var proposalRewriteFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenProposalIntentRewrite.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, proposalRewriteFailure.SqlState);

        await using var forbiddenEventRead = runtime.CreateCommand();
        forbiddenEventRead.CommandText = "SELECT count(*) FROM tenancy.tenant_authorization_events";
        var eventReadFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenEventRead.ExecuteScalarAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, eventReadFailure.SqlState);

        await using var forbiddenAuthorizationDelete = runtime.CreateCommand();
        forbiddenAuthorizationDelete.CommandText = "DELETE FROM tenancy.custom_roles";
        var authorizationDeleteFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenAuthorizationDelete.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, authorizationDeleteFailure.SqlState);

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

        foreach (var sql in new[]
        {
            "DELETE FROM pricing.price_revisions",
            "DELETE FROM customers.import_rows",
            "UPDATE pricing.price_revisions SET base_unit_price=base_unit_price WHERE false",
            "UPDATE catalog.units SET precision=precision WHERE false",
            "UPDATE catalog.items SET base_unit_id=base_unit_id WHERE false",
            "UPDATE customers.import_rows SET source_row_hash=source_row_hash WHERE false",
        })
        {
            await using var denied = new NpgsqlCommand(sql, runtime);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
                (await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync())).SqlState);
        }
        await using var discover = new NpgsqlCommand("SELECT count(*) FROM customers.discover_runnable_import_tenants(NULL, 10)", runtime);
        Assert.Equal(0L, await discover.ExecuteScalarAsync());

        await using var allowedPolicyHeadUpdate = new NpgsqlCommand(
            "UPDATE profiles.policy_heads SET revision = revision WHERE false", runtime);
        Assert.Equal(0, await allowedPolicyHeadUpdate.ExecuteNonQueryAsync());

        foreach (var sql in new[]
                 {
                     "UPDATE profiles.authority SET active_profile_id = active_profile_id WHERE false",
                     "INSERT INTO orders.program_order_metadata(baseline_principal_id) VALUES (NULL)",
                     "INSERT INTO profiles.publications(tenant_id, profile_id, policy_id, legacy_baseline, version, facts) " +
                     "VALUES ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', " +
                     "'00000000-0000-0000-0000-000000000003', false, 1, '{}'::jsonb)",
                 })
        {
            await using var forbidden = new NpgsqlCommand(sql, runtime);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => forbidden.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
        }
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

    [Fact]
    public async Task ProvisioningRejectsInheritedCommercialHistoryMutationPrivilegesWithoutApplyingPartialGrants()
    {
        await ApplyOrderSchemaAsync();
        await CreateRoleAsync();

        foreach (var table in new[] { "heads", "issued", "numbers", "receipts" })
        {
            await AssertProvisioningRejectedAsync($"TRUNCATE ON TABLE quotations.{table}");
            await AssertProvisioningRejectedAsync($"DELETE ON TABLE quotations.{table}");
        }

        foreach (var table in new[] { "orders.quotation_origins", "quotations.responses", "quotations.conversions" })
        {
            await AssertProvisioningRejectedAsync($"TRUNCATE ON TABLE {table}");
            await AssertProvisioningRejectedAsync($"DELETE ON TABLE {table}");
        }
        await AssertProvisioningRejectedAsync("UPDATE ON TABLE orders.quotation_origins");

        foreach (var (table, column) in new[]
                 {
                     ("quotations.heads", "created_by"), ("quotations.issued", "facts"),
                     ("quotations.numbers", "tenant_id"), ("quotations.receipts", "response"),
                     ("orders.quotation_origins", "quotation_id"), ("quotations.responses", "facts"),
                     ("quotations.conversions", "facts"),
                 })
        {
            await AssertProvisioningRejectedAsync($"UPDATE ({column}) ON TABLE {table}");
        }

        foreach (var table in new[] { "heads", "issued", "numbers", "receipts", "responses", "conversions" })
        {
            await AssertProvisioningRejectedAsync($"UPDATE ON TABLE quotations.{table}");
        }
    }

    [Fact]
    public async Task ProvisioningRejectsPublicCoreBaselineMetadataInsertColumnPrivilege()
    {
        await ApplyOrderSchemaAsync();
        await CreateRoleAsync();

        await AssertProvisioningRejectedAsync(
            "INSERT (baseline_principal_id) ON TABLE orders.program_order_metadata");
    }

    [Fact]
    public async Task ProvisioningRejectsPublicProfileAuthorityUpdateColumnPrivilege()
    {
        await ApplyOrderSchemaAsync();
        await CreateRoleAsync();

        await AssertProvisioningRejectedAsync("UPDATE (active_profile_id) ON TABLE profiles.authority");
    }

    [Theory]
    [InlineData("profiles.policy_revisions")]
    [InlineData("profiles.publications")]
    [InlineData("orders.program_order_metadata")]
    public async Task ProvisioningRejectsPublicProfileAndOrderColumnReferences(string table)
    {
        await ApplyOrderSchemaAsync(); await CreateRoleAsync();
        await AssertProvisioningRejectedAsync($"REFERENCES (tenant_id) ON TABLE {table}");
    }

    private async Task AssertProvisioningRejectedAsync(string privilege)
    {
        await using (var admin = new NpgsqlConnection(ConnectionString))
        {
            await admin.OpenAsync(CancellationToken.None);
            await using var grant = admin.CreateCommand();
            grant.CommandText = $"GRANT {privilege} TO PUBLIC";
            await grant.ExecuteNonQueryAsync(CancellationToken.None);
        }

        try
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(ApplyProvisioningAsync);
            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);

            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync(CancellationToken.None);
            await using var check = connection.CreateCommand();
            check.CommandText = """
                SELECT has_table_privilege(@role, 'identity_access.accounts', 'SELECT'),
                       has_schema_privilege(@role, 'orders', 'USAGE'),
                       has_schema_privilege(@role, 'quotations', 'USAGE')
                """;
            check.Parameters.AddWithValue("role", RuntimeRole);
            await using var reader = await check.ExecuteReaderAsync(CancellationToken.None);
            Assert.True(await reader.ReadAsync(CancellationToken.None));
            Assert.False(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
            Assert.False(reader.GetBoolean(2));
        }
        finally
        {
            await using var admin = new NpgsqlConnection(ConnectionString);
            await admin.OpenAsync(CancellationToken.None);
            await using var revoke = admin.CreateCommand();
            revoke.CommandText = $"REVOKE {privilege} FROM PUBLIC";
            await revoke.ExecuteNonQueryAsync(CancellationToken.None);
        }
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
