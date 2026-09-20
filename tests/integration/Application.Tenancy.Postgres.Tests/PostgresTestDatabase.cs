using Microsoft.EntityFrameworkCore;
using Application.DatabaseMigrator;
using Application.Tenancy.Postgres;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.Tenancy.Postgres.Tests;

public abstract class PostgresTestDatabase : IAsyncLifetime
{
    private protected const long MigrationAdvisoryLockKey = 0x1A2B3C4D5E6F708;

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("application_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected string ConnectionString => _database.GetConnectionString();

    private protected MigrationRunner CreateMigrationRunner() =>
        new(ConnectionString, MigrationAdvisoryLockKey);

    protected TenancyDbContext CreateContext(string? connectionString = null)
    {
        var builder = new DbContextOptionsBuilder<TenancyDbContext>();
        PostgresTenancyOptions.Configure(builder, connectionString ?? ConnectionString);
        return new TenancyDbContext(builder.Options);
    }

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();
}
