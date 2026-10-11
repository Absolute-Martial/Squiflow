using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Profiles.Postgres;

// The retained typed JSON contracts and mutation invariants are defined in the owned SQL migration.
public sealed class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : DbContext(options);

public static class ProfilesPostgresRegistration
{
    public static ProfileDbContext CreateContext(string connectionString) => new(new DbContextOptionsBuilder<ProfileDbContext>()
        .UseNpgsql(connectionString, provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "profiles")).Options);
    public static IServiceCollection AddProfilesPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PostgresProfileStore>();
        services.AddScoped<IProfileStore>(provider => provider.GetRequiredService<PostgresProfileStore>());
        return services;
    }
}
