using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace SquiFlow.IdentityAccess.Postgres.Tests;

public abstract class PostgresTestDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("squiflow_tests")
        .WithUsername("postgres")
        .WithPassword("local-integration-test-only")
        .Build();

    protected string ConnectionString => _postgres.GetConnectionString();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    protected IdentityAccessDbContext CreateContext(string? connectionString = null)
    {
        var builder = new DbContextOptionsBuilder<IdentityAccessDbContext>();
        PostgresIdentityAccessOptions.Configure(builder, connectionString ?? ConnectionString);
        return new IdentityAccessDbContext(builder.Options);
    }
}
