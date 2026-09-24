using Application.IdentityAccess.Postgres;
using Application.Orders.Postgres;
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
        services.AddSingleton<NpgsqlDataSource>(serviceProvider =>
            configuration.CreateDataSource(serviceProvider.GetRequiredService<ILoggerFactory>()));

        services.AddIdentityAccessPostgres();
        services.AddTenancyPostgres();
        services.AddOrdersPostgres();

        return services;
    }
}
