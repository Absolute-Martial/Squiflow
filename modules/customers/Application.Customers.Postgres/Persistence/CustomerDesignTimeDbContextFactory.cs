using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Application.Customers.Postgres;

public sealed class CustomerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CustomerDbContext>
{
    public CustomerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CustomerDbContext>();
        PostgresCustomerOptions.Configure(options, "Host=localhost;Database=design_only;Username=design_only");
        return new CustomerDbContext(options.Options);
    }
}
