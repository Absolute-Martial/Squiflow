using Application.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Application.Catalog.Postgres;

public static class CatalogPostgresRegistration
{
    public static IServiceCollection AddCatalogPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDbContext<CatalogDbContext>((provider, options) =>
            CatalogPostgresOptions.Configure(options, provider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<PostgresCatalogStore>();
        services.AddScoped<ICatalogStore>(provider => provider.GetRequiredService<PostgresCatalogStore>());
        services.AddScoped<ICatalogConversionStore>(provider => provider.GetRequiredService<PostgresCatalogStore>());
        services.AddScoped<ICatalogAvailabilityStore>(provider => provider.GetRequiredService<PostgresCatalogStore>());
        services.AddScoped<PublishCatalogConversion>();
        services.AddScoped<GetCatalogConversion>();
        services.AddScoped<ChangeCatalogAvailability>();
        services.AddScoped<CreateCatalogUnit>();
        services.AddScoped<CreateCatalogItem>();
        services.AddScoped<GetCatalogUnit>();
        services.AddScoped<GetCatalogItem>();
        services.AddScoped<ListCatalogUnits>();
        services.AddScoped<ListCatalogItems>();
        services.AddScoped<RenameCatalogUnit>();
        services.AddScoped<RenameCatalogItem>();
        services.AddScoped<RetireCatalogUnit>();
        services.AddScoped<RetireCatalogItem>();
        services.AddScoped<SelectCatalogLineFacts>();
        return services;
    }
}

public static class CatalogPostgresMigrations
{
    public static CatalogDbContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<CatalogDbContext>();
        CatalogPostgresOptions.Configure(builder, connectionString);
        return new CatalogDbContext(builder.Options);
    }
}
