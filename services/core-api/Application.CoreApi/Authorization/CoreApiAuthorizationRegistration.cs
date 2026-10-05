using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi.Authorization;

internal static class CoreApiAuthorizationRegistration
{
    internal static IServiceCollection AddCoreApiAuthorization(
        this IServiceCollection services,
        OpenFgaAuthorizationConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddSingleton<OpenFga.Sdk.Client.IOpenFgaClient>(_ =>
            new OpenFga.Sdk.Client.OpenFgaClient(configuration.ToClientConfiguration()));
        services.AddSingleton<OpenFgaTenantAuthorization>();
        services.AddSingleton<ITenantAuthorizationAdministrationProvider, OpenFgaTenantAuthorizationAdministrationProvider>();
        services.AddScoped<TenantAuthorizationReconciler>();
        services.AddSingleton<ITenantWorkspaceAuthorization>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenFgaTenantAuthorization>());
        services.AddSingleton<ITenantOrderAuthorization>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenFgaTenantAuthorization>());
        services.AddSingleton<ITenantCustomerAuthorization>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenFgaTenantAuthorization>());
        services.AddScoped<IAuthorizationHandler, ViewTenantWorkspaceAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, CreateOrderAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ViewOrdersAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, AbandonOrderAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, EditOrderAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ApplyManualPriceAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, CommitOrderAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, CreateOrganizationAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ViewOrganizationsAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, CreateProgramAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ViewProgramsAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, CreateIndividualAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ViewIndividualsAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ChangeIndividualAvailabilityAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ManageTenantRolesAuthorizationHandler>();
        services.AddAuthorization();

        return services;
    }
}
