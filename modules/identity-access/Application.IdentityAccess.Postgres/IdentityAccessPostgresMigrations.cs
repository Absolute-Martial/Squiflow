using Microsoft.EntityFrameworkCore;

namespace Application.IdentityAccess.Postgres;

public static class IdentityAccessPostgresMigrations
{
    public static IdentityAccessDbContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<IdentityAccessDbContext>();
        PostgresIdentityAccessOptions.Configure(builder, connectionString);
        return new IdentityAccessDbContext(builder.Options);
    }
}
