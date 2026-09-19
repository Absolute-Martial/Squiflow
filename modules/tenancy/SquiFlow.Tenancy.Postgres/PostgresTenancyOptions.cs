using Microsoft.EntityFrameworkCore;

namespace SquiFlow.Tenancy.Postgres;

public static class PostgresTenancyOptions
{
    public const string MigrationHistoryTable = "__tenancy_migrations";

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder builder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseNpgsql(connectionString, postgres =>
        {
            postgres.MigrationsHistoryTable(MigrationHistoryTable);
            postgres.MigrationsAssembly(typeof(TenancyDbContext).Assembly.FullName);
            postgres.EnableRetryOnFailure(maxRetryCount: 3);
        });
    }
}
