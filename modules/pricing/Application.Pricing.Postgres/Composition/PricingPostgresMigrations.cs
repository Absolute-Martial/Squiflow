using Microsoft.EntityFrameworkCore;

namespace Application.Pricing.Postgres;

public static class PricingPostgresMigrations
{
    public static PricingDbContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<PricingDbContext>();
        PricingPostgresOptions.Configure(builder, connectionString);
        return new PricingDbContext(builder.Options);
    }
}
