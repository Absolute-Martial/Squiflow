using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Application.CoreApi.Composition;

internal static class ProfileRuntimeServiceCollectionExtensions
{
    internal static IServiceCollection AddProfileRuntimeComposition(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ProfileRuntimeOptions>()
            .Bind(configuration.GetSection(ProfileRuntimeOptions.SectionName))
            .Validate(
                static options => options.MaximumRetainedRuntimes > 0,
                "ProfileRuntime:MaximumRetainedRuntimes must be greater than zero.")
            .Validate(
                static options => options.MaximumConcurrentBuilds > 0,
                "ProfileRuntime:MaximumConcurrentBuilds must be greater than zero.")
            .Validate(
                static options => options.IdleRetention > TimeSpan.Zero,
                "ProfileRuntime:IdleRetention must be greater than zero.")
            .Validate(
                static options => options.MaintenanceInterval > TimeSpan.Zero,
                "ProfileRuntime:MaintenanceInterval must be greater than zero.")
            .Validate(
                static options => options.ShutdownDrainTimeout > TimeSpan.Zero,
                "ProfileRuntime:ShutdownDrainTimeout must be greater than zero.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ProfileRuntimeRegistry>();
        services.AddHostedService<ProfileRuntimeMaintenanceService>();

        return services;
    }
}
