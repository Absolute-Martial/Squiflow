extern alias DatabaseMigrator;

using DatabaseMigrator::Application.DatabaseMigrator;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class InvoiceRuntimeGrantTests
{
    private const string RuntimeRole = "invoice_grant_runtime";
    private const string RuntimePassword = "local-invoice-grant-only";
    private static readonly string[] InvoiceTables =
    [
        "invoices.organization_ordinals",
        "invoices.issued_invoices",
        "invoices.issued_lines",
        "invoices.issue_receipts",
    ];

    [Fact]
    public async Task ProvisioningRepeatedlyGrantsOnlyInvoiceReadInsertAndOrdinalUpdateRights()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var connectionString = database.GetConnectionString();
        await CreateRuntimeRoleAsync(connectionString);
        await ApplyProvisioningAsync(connectionString);
        await ApplyProvisioningAsync(connectionString);

        var runtimeConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Username = RuntimeRole,
            Password = RuntimePassword,
        }.ConnectionString;
        await using var runtime = new NpgsqlConnection(runtimeConnectionString);
        await runtime.OpenAsync();

        foreach (var table in InvoiceTables)
        {
            await using var privileges = runtime.CreateCommand();
            privileges.CommandText = """
                SELECT has_table_privilege(current_user, @table, 'SELECT'),
                       has_table_privilege(current_user, @table, 'INSERT'),
                       has_table_privilege(current_user, @table, 'UPDATE'),
                       has_table_privilege(current_user, @table, 'DELETE'),
                       has_table_privilege(current_user, @table, 'TRUNCATE'),
                       has_table_privilege(current_user, @table, 'TRIGGER'),
                       has_table_privilege(current_user, @table, 'REFERENCES')
                """;
            privileges.Parameters.AddWithValue("table", table);
            await using var reader = await privileges.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetBoolean(0));
            Assert.True(reader.GetBoolean(1));
            for (var column = 2; column < 7; column++)
            {
                Assert.False(reader.GetBoolean(column));
            }
        }

        await using var columns = runtime.CreateCommand();
        columns.CommandText = """
            SELECT c.relname, a.attname,
                   has_column_privilege(current_user, format('invoices.%I', c.relname), a.attname, 'SELECT'),
                   has_column_privilege(current_user, format('invoices.%I', c.relname), a.attname, 'INSERT'),
                   has_column_privilege(current_user, format('invoices.%I', c.relname), a.attname, 'UPDATE'),
                   has_column_privilege(current_user, format('invoices.%I', c.relname), a.attname, 'REFERENCES')
            FROM pg_class AS c
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            JOIN pg_attribute AS a ON a.attrelid = c.oid
            WHERE n.nspname = 'invoices'
              AND c.relname IN ('organization_ordinals', 'issued_invoices', 'issued_lines', 'issue_receipts')
              AND a.attnum > 0 AND NOT a.attisdropped
            """;
        await using (var reader = await columns.ExecuteReaderAsync())
        {
            var columnCount = 0;
            while (await reader.ReadAsync())
            {
                columnCount++;
                var table = reader.GetString(0);
                var column = reader.GetString(1);
                Assert.True(reader.GetBoolean(2));
                Assert.True(reader.GetBoolean(3));
                Assert.Equal(table == "organization_ordinals" && column == "last_ordinal", reader.GetBoolean(4));
                Assert.False(reader.GetBoolean(5));
            }

            Assert.True(columnCount > 0);
        }

        await using var schemaRights = runtime.CreateCommand();
        schemaRights.CommandText = """
            SELECT has_schema_privilege(current_user, 'invoices', 'USAGE'),
                   has_schema_privilege(current_user, 'invoices', 'CREATE'),
                   has_function_privilege(current_user, 'invoices.valid_debtor(jsonb)', 'EXECUTE'),
                   has_function_privilege(current_user, 'invoices.valid_receipt(jsonb)', 'EXECUTE')
            """;
        await using var schemaReader = await schemaRights.ExecuteReaderAsync();
        Assert.True(await schemaReader.ReadAsync());
        Assert.True(schemaReader.GetBoolean(0));
        Assert.False(schemaReader.GetBoolean(1));
        Assert.True(schemaReader.GetBoolean(2));
        Assert.True(schemaReader.GetBoolean(3));
    }

    [Theory]
    [InlineData("runtime_table")]
    [InlineData("runtime_column")]
    [InlineData("runtime_schema")]
    [InlineData("public_table")]
    [InlineData("inherited_table")]
    public async Task ProvisioningRejectsUnsafeEffectiveInvoiceRightsWithoutLeavingBootstrapGrants(string exposure)
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var connectionString = database.GetConnectionString();
        await CreateRuntimeRoleAsync(connectionString);
        await AssertNoRuntimeBootstrapPrivilegesAsync(connectionString);
        await AddUnsafeExposureAsync(connectionString, exposure);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => ApplyProvisioningAsync(connectionString));
        var expectedFailure = exposure switch
        {
            "inherited_table" => "inherit or assume another role",
            "runtime_schema" => "invoice schema privileges are unsafe",
            _ => "unsafe on",
        };
        Assert.Contains(expectedFailure, failure.Message, StringComparison.OrdinalIgnoreCase);

        await AssertNoRuntimeBootstrapPrivilegesAsync(connectionString);
    }

    private static async Task<PostgreSqlContainer> CreateMigratedDatabaseAsync()
    {
        var database = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("invoice_grant_tests")
            .WithUsername("postgres")
            .WithPassword("local-invoice-grant-admin-only")
            .Build();
        await database.StartAsync();
        await new MigrationRunner(database.GetConnectionString(), 0x73121110)
            .ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await using (var connection = new NpgsqlConnection(database.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var revokePublicConnect = connection.CreateCommand();
            revokePublicConnect.CommandText = "REVOKE CONNECT ON DATABASE invoice_grant_tests FROM PUBLIC";
            await revokePublicConnect.ExecuteNonQueryAsync();
        }

        return database;
    }

    private static async Task CreateRuntimeRoleAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = $"CREATE ROLE {RuntimeRole} LOGIN PASSWORD '{RuntimePassword}'";
        await create.ExecuteNonQueryAsync();
    }

    private static async Task AssertNoRuntimeBootstrapPrivilegesAsync(string connectionString)
    {
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using var effective = admin.CreateCommand();
        effective.CommandText = """
            SELECT has_database_privilege(@role, current_database(), 'CONNECT'),
                   has_schema_privilege(@role, 'identity_access', 'USAGE'),
                   has_schema_privilege(@role, 'tenancy', 'USAGE'),
                   has_schema_privilege(@role, 'customers', 'USAGE'),
                   has_schema_privilege(@role, 'catalog', 'USAGE'),
                   has_schema_privilege(@role, 'pricing', 'USAGE'),
                   has_schema_privilege(@role, 'orders', 'USAGE'),
                   has_schema_privilege(@role, 'profiles', 'USAGE'),
                   has_schema_privilege(@role, 'fulfillment', 'USAGE'),
                   has_schema_privilege(@role, 'purchasing', 'USAGE'),
                   has_schema_privilege(@role, 'invoices', 'USAGE'),
                   has_table_privilege(@role, 'invoices.organization_ordinals', 'SELECT'),
                   has_table_privilege(@role, 'invoices.organization_ordinals', 'INSERT'),
                   has_table_privilege(@role, 'invoices.issued_invoices', 'SELECT'),
                   has_table_privilege(@role, 'invoices.issued_invoices', 'INSERT'),
                   has_table_privilege(@role, 'invoices.issued_lines', 'SELECT'),
                   has_table_privilege(@role, 'invoices.issued_lines', 'INSERT'),
                   has_table_privilege(@role, 'invoices.issue_receipts', 'SELECT'),
                   has_table_privilege(@role, 'invoices.issue_receipts', 'INSERT'),
                   has_function_privilege(@role, 'invoices.valid_debtor(jsonb)', 'EXECUTE'),
                   has_function_privilege(@role, 'invoices.valid_receipt(jsonb)', 'EXECUTE')
            """;
        effective.Parameters.AddWithValue("role", RuntimeRole);
        await using var reader = await effective.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        for (var column = 0; column < 21; column++)
        {
            Assert.False(reader.GetBoolean(column));
        }
    }

    private static async Task AddUnsafeExposureAsync(string connectionString, string exposure)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = exposure switch
        {
            "runtime_table" => $"GRANT UPDATE ON invoices.issued_invoices TO {RuntimeRole}",
            "runtime_column" => $"GRANT UPDATE (invoice_id) ON invoices.issued_invoices TO {RuntimeRole}",
            "runtime_schema" => $"GRANT CREATE ON SCHEMA invoices TO {RuntimeRole}",
            "public_table" => "GRANT UPDATE ON invoices.issue_receipts TO PUBLIC",
            "inherited_table" => $"CREATE ROLE invoice_grant_parent NOLOGIN; GRANT UPDATE ON invoices.issued_lines TO invoice_grant_parent; GRANT invoice_grant_parent TO {RuntimeRole}",
            _ => throw new ArgumentOutOfRangeException(nameof(exposure)),
        };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ApplyProvisioningAsync(string connectionString)
    {
        var script = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "deploy/database/grant-core-api-runtime.sql"));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var role = connection.CreateCommand())
        {
            role.Transaction = transaction;
            role.CommandText = "SELECT set_config('app.provision_runtime_role', @role, true)";
            role.Parameters.AddWithValue("role", RuntimeRole);
            await role.ExecuteNonQueryAsync();
        }

        await using (var provision = connection.CreateCommand())
        {
            provision.Transaction = transaction;
            provision.CommandText = script;
            await provision.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static string RepositoryRoot()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
        {
            if (File.Exists(Path.Combine(path.FullName, "Application.slnx")))
            {
                return path.FullName;
            }
        }

        throw new InvalidOperationException("Repository source root was not found.");
    }
}
