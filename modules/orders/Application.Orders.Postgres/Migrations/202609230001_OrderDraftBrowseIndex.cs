using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

public partial class OrderDraftBrowseIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_order_drafts_tenant_created_at_id",
            schema: "orders",
            table: "order_drafts",
            columns: new[] { "tenant_id", "created_at", "id" },
            descending: new[] { false, true, true });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_order_drafts_tenant_created_at_id",
            schema: "orders",
            table: "order_drafts");
    }
}
