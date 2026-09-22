using Application.DatabaseMigrator;
using Application.Orders.Postgres;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public abstract class PostgresTestDatabase : IAsyncLifetime
{
    private const long MigrationAdvisoryLockKey = 0x1A2B3C4D5E6F708;
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("application_tests")
        .WithUsername("postgres")
        .WithPassword("local-integration-test-only")
        .Build();

    protected string ConnectionString => _database.GetConnectionString();

    protected async Task ApplyOrderSchemaAsync()
    {
        var runner = new MigrationRunner(ConnectionString, MigrationAdvisoryLockKey);
        await runner.ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }

    protected OrderDbContext CreateContext(string? connectionString = null)
    {
        var builder = new DbContextOptionsBuilder<OrderDbContext>();
        PostgresOrderOptions.Configure(builder, connectionString ?? ConnectionString);
        return new OrderDbContext(builder.Options);
    }

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();
}
