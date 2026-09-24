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
        return services;
    }
}
