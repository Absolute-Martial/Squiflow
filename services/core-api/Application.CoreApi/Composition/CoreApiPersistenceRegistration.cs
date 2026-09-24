using Application.IdentityAccess;
using Application.IdentityAccess.Postgres;
using Application.Orders;
using Application.Orders.Postgres;
using Application.Tenancy;
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

        services.AddDbContext<IdentityAccessDbContext>((serviceProvider, options) =>
            PostgresIdentityAccessOptions.Configure(
                options,
                serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<IAccountBindingDirectory, PostgresAccountBindingDirectory>();
        services.AddScoped<ResolveAccountBinding>();

        services.AddDbContext<TenancyDbContext>((serviceProvider, options) =>
            PostgresTenancyOptions.Configure(
                options,
                serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<ITenantMembershipDirectory, PostgresTenantMembershipDirectory>();
        services.AddScoped<ResolveTenantContext>();

        services.AddDbContext<OrderDbContext>((serviceProvider, options) =>
            PostgresOrderOptions.Configure(
                options,
                serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<IOrderDraftStore, PostgresOrderDraftStore>();
        services.AddScoped<CreateOrderDraft>();
        services.AddScoped<GetOrderDraft>();
        services.AddScoped<ListOrderDrafts>();
        services.AddScoped<AbandonOrderDraft>();

        return services;
    }
}
