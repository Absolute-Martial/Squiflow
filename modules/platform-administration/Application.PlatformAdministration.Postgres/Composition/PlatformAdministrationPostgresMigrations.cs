using Microsoft.EntityFrameworkCore;

namespace Application.PlatformAdministration.Postgres;

public static class PlatformAdministrationPostgresMigrations
{
    public static PlatformAdministrationDbContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<PlatformAdministrationDbContext>();
        PostgresPlatformAdministrationOptions.Configure(builder, connectionString);
        return new PlatformAdministrationDbContext(builder.Options);
    }
}
