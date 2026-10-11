using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Quotations.Postgres;

// The capability's append-only JSON fact contracts are enforced by its owned SQL migration.
public sealed class QuotationDbContext(DbContextOptions<QuotationDbContext> options) : DbContext(options);
public static class QuotationsPostgresRegistration
{
    public static QuotationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<QuotationDbContext>().UseNpgsql(connectionString,
            provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "quotations"));
        options.ReplaceService<IMigrationsIdGenerator, QuotationMigrationIdentifierGenerator>();
        return new(options.Options);
    }
    public static IServiceCollection AddQuotationsPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PostgresQuotationStore>();
        services.AddScoped<IQuotationStore>(provider => provider.GetRequiredService<PostgresQuotationStore>());
        services.AddScoped<IQuotationResponseStore>(provider => provider.GetRequiredService<PostgresQuotationStore>());
        services.AddScoped<Application.Orders.IOrderAcceptedQuotationReader>(provider => provider.GetRequiredService<PostgresQuotationStore>());
        services.AddScoped<QuotationPricing>();
        services.AddScoped<QuotationApplication>();
        return services;
    }
}
internal static class QuotationSql
{
    internal static string Load(string name)
    {
        using var stream = typeof(QuotationSql).Assembly.GetManifestResourceStream($"Application.Quotations.Postgres.Sql.{name}.sql")
            ?? throw new InvalidOperationException("A quotation SQL resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
