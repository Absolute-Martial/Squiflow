using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

public partial class OrderDraftAbandonment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "state",
            schema: "orders",
            table: "order_drafts",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "draft");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "abandoned_at",
            schema: "orders",
            table: "order_drafts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "abandoned_by_account_id",
            schema: "orders",
            table: "order_drafts",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_order_drafts_lifecycle",
            schema: "orders",
            table: "order_drafts",
            sql: "(state = 'draft' AND revision = 1 AND abandoned_at IS NULL AND abandoned_by_account_id IS NULL) OR " +
                 "(state = 'abandoned' AND revision = 2 AND abandoned_at IS NOT NULL AND abandoned_by_account_id IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_order_drafts_abandoned_by_account_id",
            schema: "orders",
            table: "order_drafts",
            column: "abandoned_by_account_id");

        migrationBuilder.AddForeignKey(
            name: "fk_order_drafts_accounts_abandoned_by_account_id",
            schema: "orders",
            table: "order_drafts",
            column: "abandoned_by_account_id",
            principalSchema: "identity_access",
            principalTable: "accounts",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_order_drafts_accounts_abandoned_by_account_id",
            schema: "orders",
            table: "order_drafts");
        migrationBuilder.DropCheckConstraint(
            name: "ck_order_drafts_lifecycle",
            schema: "orders",
            table: "order_drafts");
        migrationBuilder.DropIndex(
            name: "IX_order_drafts_abandoned_by_account_id",
            schema: "orders",
            table: "order_drafts");
        migrationBuilder.DropColumn(name: "abandoned_at", schema: "orders", table: "order_drafts");
        migrationBuilder.DropColumn(name: "abandoned_by_account_id", schema: "orders", table: "order_drafts");
        migrationBuilder.DropColumn(name: "state", schema: "orders", table: "order_drafts");
    }
}
