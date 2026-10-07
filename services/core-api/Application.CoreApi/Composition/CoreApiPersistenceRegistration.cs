using Application.IdentityAccess.Postgres;
using Application.Catalog.Postgres;
using Application.Customers.Postgres;
using Application.Orders.Postgres;
using Application.Pricing;
using Application.Pricing.Postgres;
using Application.Tenancy.Postgres;
using Npgsql;

namespace Application.CoreApi.Composition;

internal static class CoreApiPersistenceRegistration
{
    internal static IServiceCollection AddCoreApiPersistence(
        this IServiceCollection services,
        RuntimeDatabaseConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddSingleton<NpgsqlDataSource>(_ => configuration.CreateDataSource());

        services.AddIdentityAccessPostgres();
        services.AddTenancyPostgres();
        services.AddCustomersPostgres();
        services.AddCatalogPostgres();
        services.AddPricingPostgres();
        services.AddScoped<IPricingReferenceReader, PricingReferenceReader>();
        services.AddSingleton(TimeProvider.System);
        services.AddOrdersPostgres();
        services.AddCatalogOrderIntegration();

        return services;
    }
}
