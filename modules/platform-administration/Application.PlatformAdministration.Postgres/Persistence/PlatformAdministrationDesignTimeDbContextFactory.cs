using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Application.PlatformAdministration.Postgres;

public sealed class PlatformAdministrationDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<PlatformAdministrationDbContext>
{
    public PlatformAdministrationDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<PlatformAdministrationDbContext>();
        PostgresPlatformAdministrationOptions.Configure(
            builder,
            "Host=localhost;Database=design_only;Username=design_only");
        return new PlatformAdministrationDbContext(builder.Options);
    }
}
