using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Fulfillment.Postgres.Migrations;

[DbContext(typeof(FulfillmentDbContext))]
[Migration("20261009150000_FirstServiceFulfillment")]
public sealed class FirstServiceFulfillment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        using var resource = typeof(FirstServiceFulfillment).Assembly.GetManifestResourceStream("Application.Fulfillment.Postgres.Sql.FirstServiceFulfillment.sql")
            ?? throw new InvalidOperationException("The fulfillment migration resource is missing.");
        using var reader = new StreamReader(resource);
        migrationBuilder.Sql(reader.ReadToEnd());
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Fulfillment, handoff and correction evidence are retained; destructive contraction requires a supported plan.");
}
