using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

public partial class OrderDraftCustomerAttribution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "customer_organization_id",
            schema: "orders",
            table: "order_drafts",
            type: "uuid",
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "customer_program_id",
            schema: "orders",
            table: "order_drafts",
            type: "uuid",
            nullable: true);
        migrationBuilder.AddCheckConstraint(
            name: "ck_order_drafts_customer_context",
            schema: "orders",
            table: "order_drafts",
            sql: "customer_program_id IS NULL OR customer_organization_id IS NOT NULL");
        migrationBuilder.AddForeignKey(
            name: "fk_order_drafts_customer_organization",
            schema: "orders",
            table: "order_drafts",
            columns: ["tenant_id", "customer_organization_id"],
            principalSchema: "customers",
            principalTable: "organizations",
            principalColumns: ["tenant_id", "id"],
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "fk_order_drafts_customer_program",
            schema: "orders",
            table: "order_drafts",
            columns: ["tenant_id", "customer_organization_id", "customer_program_id"],
            principalSchema: "customers",
            principalTable: "programs",
            principalColumns: ["tenant_id", "organization_id", "id"],
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_order_drafts_customer_program",
            schema: "orders",
            table: "order_drafts");
        migrationBuilder.DropForeignKey(
            name: "fk_order_drafts_customer_organization",
            schema: "orders",
            table: "order_drafts");
        migrationBuilder.DropCheckConstraint(
            name: "ck_order_drafts_customer_context",
            schema: "orders",
            table: "order_drafts");
        migrationBuilder.DropColumn(
            name: "customer_program_id",
            schema: "orders",
            table: "order_drafts");
        migrationBuilder.DropColumn(
            name: "customer_organization_id",
            schema: "orders",
            table: "order_drafts");
    }
}
