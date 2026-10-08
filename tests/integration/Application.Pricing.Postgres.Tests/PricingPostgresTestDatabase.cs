using Application.Pricing.Postgres;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.Pricing.Postgres.Tests;

public abstract class PricingPostgresTestDatabase : IAsyncLifetime
{
    private const string RuntimeRole = "pricing_runtime_test";
    private const string RuntimePassword = "pricing-runtime-test-only";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
        .WithDatabase("pricing_tests")
        .WithUsername("postgres")
        .WithPassword("local-integration-test-only")
        .Build();

    protected string ConnectionString => database.GetConnectionString();

    protected async Task ApplyPricingSchemaAsync()
    {
        await using var authority = new NpgsqlConnection(ConnectionString);
        await authority.OpenAsync(CancellationToken.None);
        await using (var createAuthority = authority.CreateCommand())
        {
            createAuthority.CommandText = """
                CREATE SCHEMA IF NOT EXISTS tenancy;
                CREATE TABLE IF NOT EXISTS tenancy.tenants (id uuid PRIMARY KEY);
                CREATE SCHEMA IF NOT EXISTS identity_access;
                CREATE TABLE IF NOT EXISTS identity_access.accounts (id uuid PRIMARY KEY);
                """;
            await createAuthority.ExecuteNonQueryAsync(CancellationToken.None);
        }

        await using var context = PricingPostgresMigrations.CreateContext(ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
        await using (var grant = authority.CreateCommand())
        {
            grant.CommandText = $"""
                CREATE ROLE {RuntimeRole} LOGIN PASSWORD '{RuntimePassword}';
                GRANT USAGE ON SCHEMA pricing, tenancy, identity_access TO {RuntimeRole};
                 GRANT SELECT, INSERT, UPDATE ON pricing.price_revisions, pricing.command_receipts TO {RuntimeRole};
                 GRANT SELECT, INSERT ON pricing.override_policies TO {RuntimeRole};
                GRANT USAGE, SELECT ON SEQUENCE pricing.price_revision_number_seq TO {RuntimeRole};
                GRANT SELECT, REFERENCES ON tenancy.tenants, identity_access.accounts TO {RuntimeRole};
                """;
            await grant.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    protected async Task SeedAuthorityAsync(params Guid[] ids)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        foreach (var id in ids)
        {
            await using var tenant = new NpgsqlCommand(
                "INSERT INTO tenancy.tenants (id) VALUES (@id) ON CONFLICT DO NOTHING", connection);
            tenant.Parameters.AddWithValue("id", id);
            await tenant.ExecuteNonQueryAsync(CancellationToken.None);
            await using var account = new NpgsqlCommand(
                "INSERT INTO identity_access.accounts (id) VALUES (@id) ON CONFLICT DO NOTHING", connection);
            account.Parameters.AddWithValue("id", id);
            await account.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    protected async Task<NpgsqlDataSource> CreateRuntimeDataSourceAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = RuntimeRole,
            Password = RuntimePassword,
            Pooling = false,
        };
        return new NpgsqlDataSourceBuilder(builder.ConnectionString).Build();
    }

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();
}
