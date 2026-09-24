using Microsoft.EntityFrameworkCore;

namespace Application.Customers.Postgres;

public static class CustomersPostgresMigrations
{
    public static CustomerDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CustomerDbContext>();
        PostgresCustomerOptions.Configure(options, connectionString);
        return new CustomerDbContext(options.Options);
    }
}
