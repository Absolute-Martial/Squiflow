using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Application.IdentityAccess.Postgres;

public static class IdentityAccessPostgresRegistration
{
    public static IServiceCollection AddIdentityAccessPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<IdentityAccessDbContext>((serviceProvider, options) =>
            PostgresIdentityAccessOptions.Configure(
                options,
                serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<IAccountBindingDirectory, PostgresAccountBindingDirectory>();
        services.AddScoped<IAccountDirectory, PostgresAccountDirectory>();
        services.AddScoped<IAccountOnboardingStore, PostgresAccountOnboardingStore>();
        services.AddScoped<ResolveAccountBinding>();
        services.AddScoped<AccountOnboarding>();

        return services;
    }
}
