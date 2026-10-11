using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Orders.Postgres.Migrations;

[DbContext(typeof(OrderDbContext))]
[Migration("202610090010_OrderApprovals")]
public sealed class OrderApprovals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(OrderSql.OrderApprovalsSchema);
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => OrderModelV202610070001.Build(modelBuilder);
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Order approval subjects, decisions and receipts must be retained.");
}
