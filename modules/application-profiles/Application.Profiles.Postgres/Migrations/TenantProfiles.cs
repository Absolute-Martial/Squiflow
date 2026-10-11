using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Profiles.Postgres.Migrations;

[DbContext(typeof(ProfileDbContext))]
[Migration("20261008010000_TenantProfiles")]
public sealed class TenantProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        using var stream = typeof(TenantProfiles).Assembly.GetManifestResourceStream("Application.Profiles.Postgres.Sql.Schema.sql")
            ?? throw new InvalidOperationException("The tenant profile schema resource is missing.");
        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Tenant profile policy, publications and activation evidence are retained; destructive rollback requires a supported contraction plan.");
}
