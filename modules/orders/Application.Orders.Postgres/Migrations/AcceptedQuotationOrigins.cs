using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Orders.Postgres.Migrations;

[DbContext(typeof(OrderDbContext))]
[Migration("202610070030_AcceptedQuotationOrigins")]
public sealed class AcceptedQuotationOrigins : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(OrderSql.QuotationOriginsSchema);
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => OrderModelV202610070001.Build(modelBuilder);
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Accepted quotation origins must be retained.");
}
