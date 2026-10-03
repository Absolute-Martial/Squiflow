using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Application.PlatformAdministration.Postgres;

public static class PostgresPlatformAdministrationOptions
{
    public const string MigrationHistoryTable = "__platform_administration_migrations";

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder builder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseNpgsql(connectionString, postgres =>
        {
            postgres.MigrationsHistoryTable(MigrationHistoryTable);
            postgres.MigrationsAssembly(typeof(PlatformAdministrationDbContext).Assembly.FullName);
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
            postgres.MigrationsAssembly(typeof(PlatformAdministrationDbContext).Assembly.FullName);
        });
    }
}
