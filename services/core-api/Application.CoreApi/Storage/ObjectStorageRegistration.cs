using Application.ObjectStorage;
using Application.Customers;
using Application.Customers.Postgres;
using Application.Tenancy;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Application.CoreApi.Storage;

internal static class ObjectStorageRegistration
{
    internal static IServiceCollection AddObjectStorage(
        this IServiceCollection services,
        HuggingFaceObjectStoreConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton(configuration);
        services.AddHttpClient("Application.CoreApi.HuggingFaceObjectStore", client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.TryAddSingleton<IObjectStore>(provider => configuration.Enabled
            ? new HuggingFaceObjectStore(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("Application.CoreApi.HuggingFaceObjectStore"),
                configuration)
            : new UnavailableObjectStore());
        services.TryAddScoped<DisabledCustomerImportSourceStore>();
        services.TryAddScoped<ICustomerImportSourceStore>(provider =>
            provider.GetRequiredService<DisabledCustomerImportSourceStore>());
        services.TryAddScoped<ICustomerImportSourceMaintenanceStore>(provider =>
            provider.GetRequiredService<DisabledCustomerImportSourceStore>());
        services.TryAddScoped<ReconcileCustomerImportSource>();
        services.AddScoped<ReadCustomerImportSource>();
        if (configuration.Enabled)
        {
            services.AddSingleton(CustomerImportSourcePolicy.Create(
                configuration.ProviderScope, configuration.MaximumRetainedBytes));
            services.AddScoped<ICustomerImportSourceStore, PostgresCustomerStore>();
            services.AddScoped<ICustomerImportSourceMaintenanceStore, PostgresCustomerStore>();
        }
        return services;
    }

    private sealed class UnavailableObjectStore : IObjectStore
    {
        public Task<ObjectStorePutResult> PutAsync(ObjectStorePutRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ObjectStorePutResult(ObjectStorePutOutcome.Unavailable));

        public Task<ObjectStoreReadResult> OpenReadAsync(ObjectStoreReadRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ObjectStoreReadResult(ObjectStoreReadOutcome.Unavailable));

        public Task<ObjectStoreMetadataResult> GetMetadataAsync(ObjectStoreKey key, CancellationToken cancellationToken) =>
            Task.FromResult(new ObjectStoreMetadataResult(ObjectStoreMetadataOutcome.Unavailable));

        public Task<ObjectStoreDeleteResult> DeleteAsync(ObjectStoreKey key, CancellationToken cancellationToken) =>
            Task.FromResult(new ObjectStoreDeleteResult(ObjectStoreDeleteOutcome.Unavailable));
    }

    private sealed class DisabledCustomerImportSourceStore : ICustomerImportSourceStore, ICustomerImportSourceMaintenanceStore
    {
        public Task<CustomerImportSourceLease> BeginSourceAsync(TenantContext context, CustomerImportPlan plan,
            string idempotencyKey, CustomerImportRetention retention, CustomerImportSourcePolicy policy,
            CancellationToken cancellationToken) => throw Unavailable();

        public Task<CreateCustomerImportResult> CompleteSourceAsync(TenantContext context, CustomerImportPlan plan,
            string idempotencyKey, CustomerImportRetention retention, CustomerImportSourcePolicy policy,
            CustomerImportSourceLease lease, CancellationToken cancellationToken) => throw Unavailable();

        public Task MarkSourceFailureAsync(TenantContext context, CustomerImportSourceLease lease,
            CustomerImportSourceFailureKind failureKind, string failureCode, CancellationToken cancellationToken) =>
            throw Unavailable();

        public Task<CustomerImportSourceSnapshot?> ReadSourceAsync(TenantContext context, Guid importId,
            CancellationToken cancellationToken) => Task.FromResult<CustomerImportSourceSnapshot?>(null);

        public Task<CustomerImportSourceRetirementLease?> ClaimExpiredSourceRetirementAsync(TenantContext context,
            DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<CustomerImportSourceRetirementLease?>(null);

        public Task<CustomerImportSourceRetirementLease?> ClaimExpiredSourceRetirementAsync(Guid tenantId,
            DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<CustomerImportSourceRetirementLease?>(null);

        public Task CompleteSourceRetirementAsync(TenantContext context, CustomerImportSourceRetirementLease lease,
            bool deleted, string? failureCode, CancellationToken cancellationToken) =>
            throw Unavailable();

        public Task CompleteSourceRetirementAsync(Guid tenantId, CustomerImportSourceRetirementLease lease,
            bool deleted, string? failureCode, CancellationToken cancellationToken) => throw Unavailable();

        private static CustomerValidationException Unavailable() =>
            new("import_source_unavailable", "Import source retention is disabled.");
    }
}
