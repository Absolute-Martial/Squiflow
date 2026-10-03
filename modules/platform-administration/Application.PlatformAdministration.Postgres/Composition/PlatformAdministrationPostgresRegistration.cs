using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Application.PlatformAdministration.Postgres;

public static class PlatformAdministrationPostgresRegistration
{
    public static IServiceCollection AddPlatformAdministrationPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<PlatformAdministrationDbContext>((serviceProvider, options) =>
            PostgresPlatformAdministrationOptions.Configure(
                options,
                serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<IPlatformAdminAccessDirectory, PostgresPlatformAdminAccessDirectory>();
        services.AddScoped<IPlatformAdminAccessAuditStore, PostgresPlatformAdminAccessAuditStore>();
        return services;
    }
}
