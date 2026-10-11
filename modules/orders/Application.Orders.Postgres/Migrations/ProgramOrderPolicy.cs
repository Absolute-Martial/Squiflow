using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Orders.Postgres.Migrations;

[DbContext(typeof(OrderDbContext))]
[Migration("202610080010_ProgramOrderPolicy")]
public sealed class ProgramOrderPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(OrderSql.ProgramOrderPolicySchema);
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => OrderModelV202610070001.Build(modelBuilder);
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Order policy and reference evidence must be retained.");
}
