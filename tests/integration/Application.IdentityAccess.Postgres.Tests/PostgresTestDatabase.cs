using Microsoft.EntityFrameworkCore;
using Application.DatabaseMigrator;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.IdentityAccess.Postgres.Tests;

public abstract class PostgresTestDatabase : IAsyncLifetime
{
    private protected const long MigrationAdvisoryLockKey = 0x1A2B3C4D5E6F708;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("application_tests")
        .WithUsername("postgres")
        .WithPassword("local-integration-test-only")
        .Build();

    protected string ConnectionString => _postgres.GetConnectionString();

    private protected MigrationRunner CreateMigrationRunner() =>
        new(ConnectionString, MigrationAdvisoryLockKey);

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    protected IdentityAccessDbContext CreateContext(string? connectionString = null)
    {
        var builder = new DbContextOptionsBuilder<IdentityAccessDbContext>();
        PostgresIdentityAccessOptions.Configure(builder, connectionString ?? ConnectionString);
        return new IdentityAccessDbContext(builder.Options);
    }
}
