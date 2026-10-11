using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Profiles.Postgres.Migrations;

[DbContext(typeof(ProfileDbContext))]
[Migration("20261009010000_ProfilePolicyV2")]
public sealed class ProfilePolicyV2 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        using var stream = typeof(ProfilePolicyV2).Assembly.GetManifestResourceStream("Application.Profiles.Postgres.Sql.ProfilePolicyV2.sql")
            ?? throw new InvalidOperationException("The profile policy v2 migration resource is missing.");
        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Published policy facts, profile snapshots and command receipts are retained; destructive rollback requires a supported contraction plan.");
}
