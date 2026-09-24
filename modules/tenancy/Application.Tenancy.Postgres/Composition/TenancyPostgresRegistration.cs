using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Application.Tenancy.Postgres;

public static class TenancyPostgresRegistration
{
    public static IServiceCollection AddTenancyPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<TenancyDbContext>((serviceProvider, options) =>
            PostgresTenancyOptions.Configure(
                options,
                serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<ITenantMembershipDirectory, PostgresTenantMembershipDirectory>();
        services.AddScoped<ResolveTenantContext>();

        return services;
    }
}
