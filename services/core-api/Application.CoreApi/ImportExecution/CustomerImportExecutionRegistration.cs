using Application.CoreApi.Storage;
using Application.Customers;
using Microsoft.Extensions.Options;

namespace Application.CoreApi.ImportExecution;

internal static class CustomerImportExecutionRegistration
{
    internal static IServiceCollection AddCustomerImportExecution(
        this IServiceCollection services, IConfiguration configuration, RuntimeDatabaseConfiguration database,
        HuggingFaceObjectStoreConfiguration objectStorage)
    {
        var execution = CustomerImportExecutionConfiguration.From(configuration);
        if (execution.Enabled && database.MaximumPoolSize < 2)
            throw new InvalidOperationException("Customer import execution requires a shared database pool of at least two connections.");
        // Retained raw customer PII has exactly one deleter: the hosted executor's source retirement.
        // Enabling real object storage without it uploads expiring bytes that nothing ever
        // removes, so the unrecoverable retention exposure fails startup instead of waiting.
        if (objectStorage.Enabled && !execution.Enabled)
            throw new InvalidOperationException(
                "Object storage requires CustomerImports:Execution:Enabled=true; raw import sources are only ever deleted by the customer import executor.");
        services.AddSingleton(execution);
        services.AddSingleton<CustomerImportExecutionState>();
        services.AddScoped<ICustomerImportAuthority, CurrentCustomerImportAuthority>();
        services.AddHostedService<CustomerImportHostedExecutor>();
        // Reuse the standard host shutdown setting; no independent detached shutdown mechanism.
        services.AddOptions<HostOptions>().Bind(configuration.GetSection("HostOptions")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<HostOptions>>(new ImportShutdownValidation(execution));
        return services;
    }

    private sealed class ImportShutdownValidation(CustomerImportExecutionConfiguration execution) : IValidateOptions<HostOptions>
    {
        public ValidateOptionsResult Validate(string? name, HostOptions options)
        {
            if (!execution.Enabled) return ValidateOptionsResult.Success;
            if (options.BackgroundServiceExceptionBehavior != BackgroundServiceExceptionBehavior.StopHost)
                return ValidateOptionsResult.Fail("Enabled customer import execution requires HostOptions:BackgroundServiceExceptionBehavior=StopHost.");
            return options.ShutdownTimeout < execution.OperationTimeout + TimeSpan.FromSeconds(5)
                ? ValidateOptionsResult.Fail("HostOptions:ShutdownTimeout must cover the customer import operation deadline plus its five-second cleanup budget.")
                : ValidateOptionsResult.Success;
        }
    }
}
