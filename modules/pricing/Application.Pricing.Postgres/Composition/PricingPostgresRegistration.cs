using Application.Pricing;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Pricing.Postgres;

public static class PricingPostgresRegistration
{
    public static IServiceCollection AddPricingPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PostgresPricingStore>();
        services.AddScoped<IPricingPublicationStore>(provider => provider.GetRequiredService<PostgresPricingStore>());
        services.AddScoped<IPricingCandidateReader>(provider => provider.GetRequiredService<PostgresPricingStore>());
        services.AddScoped<IPricingPolicyStore>(provider => provider.GetRequiredService<PostgresPricingStore>());
        services.AddScoped<PriceResolver>();
        services.AddScoped<PricingApplication>();
        return services;
    }
}
