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
        services.AddScoped<ITenantDirectory, PostgresTenantDirectory>();
        services.AddScoped<IPlatformTenantRegistry, PostgresPlatformTenantRegistry>();
        services.AddScoped<IPlatformMembershipRegistry, PostgresPlatformMembershipRegistry>();
        services.AddScoped<ITenantProvisioningStore, PostgresTenantProvisioningStore>();
        services.AddScoped<ITenantMembershipLifecycleStore, PostgresTenantMembershipLifecycleStore>();
        services.AddScoped<ITenantLifecycleStore, PostgresTenantLifecycleStore>();
        services.AddScoped<ITenantAuthorizationAdministrationStore, PostgresTenantAuthorizationAdministrationStore>();
        services.AddScoped<ResolveTenantContext>();
        services.AddScoped<ProvisionTenant>();
        services.AddScoped<ManageTenantMembership>();
        services.AddScoped<ManageTenantLifecycle>();
        services.AddScoped<TenantAuthorizationAdministration>();

        return services;
    }
}
