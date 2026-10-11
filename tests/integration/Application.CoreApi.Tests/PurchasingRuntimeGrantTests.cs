extern alias DatabaseMigrator;

using DatabaseMigrator::Application.DatabaseMigrator;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class PurchasingRuntimeGrantTests
{
    private const string RuntimeRole = "purchasing_grant_runtime";
    private const string RuntimePassword = "local-purchasing-grant-only";

    [Fact]
    public async Task ProvisioningGrantsOnlyTheEffectivePurchasingTableAndColumnRights()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        await CreateRuntimeRoleAsync(database.GetConnectionString());
        await ApplyProvisioningAsync(database.GetConnectionString());

        var runtimeConnectionString = new NpgsqlConnectionStringBuilder(database.GetConnectionString())
        {
            Username = RuntimeRole,
            Password = RuntimePassword,
        }.ConnectionString;
        await using var runtime = new NpgsqlConnection(runtimeConnectionString);
        await runtime.OpenAsync();

        foreach (var table in new[]
                 {
                     "purchasing.suppliers", "purchasing.supplier_events", "purchasing.supplier_command_receipts",
                     "purchasing.purchases", "purchasing.purchase_events", "purchasing.purchase_command_receipts",
                 })
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
                Assert.False(reader.GetBoolean(column));
        }

        await using var columns = runtime.CreateCommand();
        columns.CommandText = """
            SELECT c.relname, a.attname,
                   has_column_privilege(current_user, format('purchasing.%I', c.relname), a.attname, 'SELECT'),
                   has_column_privilege(current_user, format('purchasing.%I', c.relname), a.attname, 'INSERT'),
                   has_column_privilege(current_user, format('purchasing.%I', c.relname), a.attname, 'UPDATE'),
                   has_column_privilege(current_user, format('purchasing.%I', c.relname), a.attname, 'REFERENCES')
            FROM pg_class AS c
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            JOIN pg_attribute AS a ON a.attrelid = c.oid
            WHERE n.nspname = 'purchasing'
              AND c.relname IN ('suppliers', 'supplier_events', 'supplier_command_receipts', 'purchases', 'purchase_events', 'purchase_command_receipts')
              AND a.attnum > 0 AND NOT a.attisdropped
            """;
        var allowedUpdates = new HashSet<(string Table, string Column)>
        {
            ("suppliers", "revision"), ("suppliers", "snapshot"), ("suppliers", "updated_at"),
            ("purchases", "revision"), ("purchases", "snapshot"), ("purchases", "updated_at"),
        };
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
                Assert.Equal(allowedUpdates.Contains((table, column)), reader.GetBoolean(4));
                Assert.False(reader.GetBoolean(5));
            }
            Assert.True(columnCount > 0);
        }
    }

    [Theory]
    [InlineData("runtime_table")]
    [InlineData("runtime_column")]
    [InlineData("public_table")]
    [InlineData("inherited_table")]
    public async Task ProvisioningRejectsUnsafeEffectivePurchasingRightsBeforeGrantingAnything(string exposure)
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var connectionString = database.GetConnectionString();
        await CreateRuntimeRoleAsync(connectionString);
        await AssertNoRuntimeBootstrapPrivilegesAsync(connectionString);
        await AddUnsafeExposureAsync(connectionString, exposure);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => ApplyProvisioningAsync(connectionString));
        var expectedFailure = exposure == "inherited_table"
            ? "inherit or assume another role"
            : "unsafe effective purchasing";
        Assert.Contains(expectedFailure, failure.Message, StringComparison.OrdinalIgnoreCase);

        await AssertNoRuntimeBootstrapPrivilegesAsync(connectionString);
    }

    private static async Task<PostgreSqlContainer> CreateMigratedDatabaseAsync()
    {
        var database = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("purchasing_grant_tests")
            .WithUsername("postgres")
            .WithPassword("local-purchasing-grant-admin-only")
            .Build();
        await database.StartAsync();
        await new MigrationRunner(database.GetConnectionString(), 0x73121109)
            .ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await using (var connection = new NpgsqlConnection(database.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var revokePublicConnect = connection.CreateCommand();
            revokePublicConnect.CommandText = "REVOKE CONNECT ON DATABASE purchasing_grant_tests FROM PUBLIC";
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
                   has_schema_privilege(@role, 'purchasing', 'USAGE')
            """;
        effective.Parameters.AddWithValue("role", RuntimeRole);
        await using var reader = await effective.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
    }

    private static async Task AddUnsafeExposureAsync(string connectionString, string exposure)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = exposure switch
        {
            "runtime_table" => $"GRANT UPDATE ON purchasing.purchases TO {RuntimeRole}",
            "runtime_column" => $"GRANT UPDATE (event_id) ON purchasing.purchase_events TO {RuntimeRole}",
            "public_table" => "GRANT UPDATE ON purchasing.purchase_command_receipts TO PUBLIC",
            "inherited_table" => $"CREATE ROLE purchasing_grant_parent NOLOGIN; GRANT UPDATE ON purchasing.purchase_events TO purchasing_grant_parent; GRANT purchasing_grant_parent TO {RuntimeRole}",
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
            if (File.Exists(Path.Combine(path.FullName, "Application.slnx")))
                return path.FullName;
        throw new InvalidOperationException("Repository source root was not found.");
    }
}
