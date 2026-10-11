using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Application.Purchasing.Postgres;

public interface IPurchasingSourceReader
{
    Task<PurchasingSourceFacts?> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, PurchasingMutation mutation, CancellationToken cancellationToken);
}

public interface IPurchasingTransactionalAuthorityReader
{
    Task<PurchasingAuthority> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, CancellationToken cancellationToken);
}

// Owned SQL migrations protect immutable evidence and mutable current projections.
public sealed class PurchasingDbContext(DbContextOptions<PurchasingDbContext> options) : DbContext(options);

public static class PurchasingPostgresRegistration
{
    public static PurchasingDbContext CreateContext(string connectionString) => new(new DbContextOptionsBuilder<PurchasingDbContext>()
        .UseNpgsql(connectionString, provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "purchasing")).Options);

    public static IServiceCollection AddPurchasingPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PostgresPurchasingStore>();
        services.AddScoped<IPurchasingStore>(provider => provider.GetRequiredService<PostgresPurchasingStore>());
        return services;
    }
}
