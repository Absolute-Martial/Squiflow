using Application.Catalog.Postgres;
using Application.IdentityAccess.Postgres;
using Application.Tenancy.Postgres;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.Catalog.Postgres.Tests;

public abstract class CatalogPostgresTestDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder(
        new DockerImage(repository: "postgres", tag: "17-alpine"))
        .WithDatabase("application_catalog_tests")
        .WithUsername("postgres")
        .WithPassword("local-integration-test-only")
        .Build();

    private string? runtimeConnectionString;

    protected string ConnectionString => database.GetConnectionString();

    protected async Task ApplyCatalogSchemaAsync()
    {
        await using var identity = IdentityAccessPostgresMigrations.CreateContext(ConnectionString);
        await identity.Database.MigrateAsync(CancellationToken.None);
        await using var tenancy = TenancyPostgresMigrations.CreateContext(ConnectionString);
        await tenancy.Database.MigrateAsync(CancellationToken.None);
        await using var context = CatalogPostgresMigrations.CreateContext(ConnectionString);
        await context.Database.MigrateAsync(CancellationToken.None);
    }

    protected async Task SeedAuthorityAsync(Guid accountId, params Guid[] tenantIds)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using (var account = connection.CreateCommand())
        {
            account.CommandText = "INSERT INTO identity_access.accounts (id, availability, created_at) VALUES (@id, 1, '2026-10-06T12:00:00Z') ON CONFLICT DO NOTHING";
            account.Parameters.AddWithValue("id", accountId);
            await account.ExecuteNonQueryAsync(CancellationToken.None);
        }

        foreach (var tenantId in tenantIds)
        {
            await using var tenant = connection.CreateCommand();
            tenant.CommandText = "INSERT INTO tenancy.tenants (id, display_name, availability, created_at) VALUES (@id, 'Catalog tenant', 1, '2026-10-06T12:00:00Z') ON CONFLICT DO NOTHING";
            tenant.Parameters.AddWithValue("id", tenantId);
            await tenant.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    protected async Task<string> CreateRuntimeRoleAsync()
    {
        if (runtimeConnectionString is not null)
            return runtimeConnectionString;

        var roleName = $"catalog_runtime_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync(CancellationToken.None);
        await using var command = admin.CreateCommand();
        command.CommandText = $"""
            CREATE ROLE {roleName} LOGIN PASSWORD 'local-runtime-test-only';
            GRANT CONNECT ON DATABASE application_catalog_tests TO {roleName};
            GRANT USAGE ON SCHEMA catalog TO {roleName};
            GRANT SELECT, INSERT, UPDATE (name, revision, status, retired_at, retired_by_account_id)
                ON catalog.units TO {roleName};
            GRANT SELECT, INSERT, UPDATE (name, description, revision, status, retired_at, retired_by_account_id,
                availability, availability_changed_at, availability_changed_by_account_id)
                ON catalog.items TO {roleName};
            GRANT SELECT, INSERT ON catalog.command_receipts TO {roleName};
            GRANT SELECT, INSERT ON catalog.unit_conversions TO {roleName};
            """;
        await command.ExecuteNonQueryAsync(CancellationToken.None);

        var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = roleName,
            Password = "local-runtime-test-only",
        };
        runtimeConnectionString = builder.ConnectionString;
        return runtimeConnectionString;
    }

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();
}
