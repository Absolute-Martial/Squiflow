using Application.Customers;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Application.Customers.Postgres;

public static class CustomersPostgresRegistration
{
    public static IServiceCollection AddCustomersPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ICustomerStore, PostgresCustomerStore>();
        services.AddScoped<CreateCustomerOrganization>();
        services.AddScoped<CreateCustomerProgram>();
        services.AddScoped<GetCustomerOrganization>();
        services.AddScoped<GetCustomerProgram>();
        services.AddScoped<ListCustomerOrganizations>();
        services.AddScoped<ListCustomerPrograms>();
        services.AddScoped<ResolveCustomerOrderContext>();
        services.AddScoped<ICustomerIndividualStore, PostgresCustomerStore>();
        services.AddScoped<ICustomerIndividualContactStore, PostgresCustomerStore>();
        services.AddScoped<CreateCustomerIndividual>();
        services.AddScoped<GetCustomerIndividual>();
        services.AddScoped<EditCustomerIndividualContact>();
        services.AddScoped<ChangeCustomerIndividualAvailability>();
        services.AddScoped<ICustomerRepresentativeStore, PostgresCustomerStore>();
        services.AddScoped<LinkCustomerRepresentative>();
        services.AddScoped<GetCustomerRepresentative>();
        services.AddScoped<UnlinkCustomerRepresentative>();
        services.AddScoped<ICustomerDuplicateDiscoveryStore, PostgresCustomerStore>();
        services.AddScoped<ICustomerDuplicateResolutionStore, PostgresCustomerStore>();
        services.AddScoped<ICustomerDuplicateConsolidationStore, PostgresCustomerStore>();
        services.AddScoped<ICustomerCanonicalDirectory, PostgresCustomerStore>();
        services.AddScoped<FindCustomerDuplicates>();
        services.AddScoped<ResolveCustomerDuplicate>();
        services.AddScoped<ConsolidateCustomerDuplicate>();
        services.AddScoped<ICustomerImportStore, PostgresCustomerStore>();
        services.AddScoped<ICustomerImportWorkStore, PostgresCustomerStore>();
        services.AddScoped<ICustomerImportWorkDiscovery, PostgresCustomerStore>();
        services.AddScoped<CreateCustomerImport>();
        services.AddScoped<ExecuteCustomerImport>();
        services.AddScoped<RunCustomerImportBatch>();
        return services;
    }
}
