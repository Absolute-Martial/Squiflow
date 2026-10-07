using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Application.Catalog.Postgres;

public static class CatalogPostgresOptions
{
    public static void Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(dataSource);
        options.UseNpgsql(dataSource, npgsql => npgsql.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName));
    }

    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName));
    }
}
