using Microsoft.EntityFrameworkCore;
using SquiFlow.Tenancy.Postgres;
using Testcontainers.PostgreSql;
using Xunit;

namespace SquiFlow.Tenancy.Postgres.Tests;

public abstract class PostgresTestDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("squiflow_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected string ConnectionString => _database.GetConnectionString();

    protected TenancyDbContext CreateContext(string? connectionString = null)
    {
        var builder = new DbContextOptionsBuilder<TenancyDbContext>();
        PostgresTenancyOptions.Configure(builder, connectionString ?? ConnectionString);
        return new TenancyDbContext(builder.Options);
    }

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();
}
