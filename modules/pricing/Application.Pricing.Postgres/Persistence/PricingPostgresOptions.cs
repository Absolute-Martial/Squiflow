using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Pricing.Postgres;

public static class PricingPostgresOptions
{
    public const string MigrationHistoryTable = "__pricing_migrations";

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return ConfigureCore(builder, options => options.UseNpgsql(connectionString));
    }

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, DbDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dataSource);
        return ConfigureCore(builder, options => options.UseNpgsql(dataSource));
    }

    private static DbContextOptionsBuilder ConfigureCore(
        DbContextOptionsBuilder builder,
        Func<DbContextOptionsBuilder, DbContextOptionsBuilder> configure)
    {
        builder.ReplaceService<IMigrationsIdGenerator, PricingMigrationIdentifierGenerator>();
        configure(builder).UseNpgsql(options =>
        {
            options.MigrationsHistoryTable(MigrationHistoryTable);
            options.MigrationsAssembly(typeof(PricingDbContext).Assembly.FullName);
        });
        return builder;
    }
}
