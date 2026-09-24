using Microsoft.EntityFrameworkCore;

namespace Application.Tenancy.Postgres;

public static class TenancyPostgresMigrations
{
    public static TenancyDbContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<TenancyDbContext>();
        PostgresTenancyOptions.Configure(builder, connectionString);
        return new TenancyDbContext(builder.Options);
    }
}
