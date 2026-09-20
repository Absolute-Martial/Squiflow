using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Application.IdentityAccess.Postgres;

public static class PostgresIdentityAccessOptions
{
    public const string MigrationHistoryTable = "__identity_access_migrations";

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder builder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseNpgsql(connectionString, postgres =>
        {
            postgres.MigrationsHistoryTable(MigrationHistoryTable);
            postgres.MigrationsAssembly(typeof(IdentityAccessDbContext).Assembly.FullName);
        });
    }

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder builder,
        DbDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dataSource);

        return builder.UseNpgsql(dataSource, postgres =>
        {
            postgres.MigrationsHistoryTable(MigrationHistoryTable);
            postgres.MigrationsAssembly(typeof(IdentityAccessDbContext).Assembly.FullName);
        });
    }
}
