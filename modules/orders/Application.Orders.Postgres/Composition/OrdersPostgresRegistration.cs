using Application.Orders;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Application.Orders.Postgres;

public static class OrdersPostgresRegistration
{
    public static IServiceCollection AddOrdersPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<OrderDbContext>((serviceProvider, options) =>
            PostgresOrderOptions.Configure(
                options,
                serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<PostgresOrderDraftStore>();
        services.AddScoped<IOrderDraftStore>(provider => provider.GetRequiredService<PostgresOrderDraftStore>());
        services.AddScoped<IOrderDraftHistoryStore>(provider => provider.GetRequiredService<PostgresOrderDraftStore>());
        services.AddScoped<GetOrderDraftHistory>();
        services.AddScoped<CreateOrderDraft>();
        services.AddScoped<GetOrderDraft>();
        services.AddScoped<ListOrderDrafts>();
        services.AddScoped<AbandonOrderDraft>();
        services.AddScoped<CommitOrderDraft>();
        services.AddScoped<ReviseOrderDraft>();

        return services;
    }
}
