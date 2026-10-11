using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Application.Fulfillment.Postgres;

public interface IFulfillmentStartSource
{
    Task<FulfillmentSourceFacts?> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, Guid orderId, CancellationToken cancellationToken);
}

public interface IFulfillmentTransactionalAuthorityReader
{
    Task<FulfillmentAuthority> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, CancellationToken cancellationToken);
}

// The owned SQL migration defines retained facts and guarded mutable projections.
public sealed class FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options) : DbContext(options);

public static class FulfillmentPostgresRegistration
{
    public static FulfillmentDbContext CreateContext(string connectionString) => new(new DbContextOptionsBuilder<FulfillmentDbContext>()
        .UseNpgsql(connectionString, provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "fulfillment")).Options);

    public static IServiceCollection AddFulfillmentPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PostgresFulfillmentStore>();
        services.AddScoped<IFulfillmentStore>(provider => provider.GetRequiredService<PostgresFulfillmentStore>());
        return services;
    }
}
