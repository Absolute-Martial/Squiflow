using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

public partial class OrderDraftRevision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_order_drafts_lifecycle", schema: "orders", table: "order_drafts");
        migrationBuilder.AddCheckConstraint(
            name: "ck_order_drafts_lifecycle", schema: "orders", table: "order_drafts",
            sql: "(state = 'draft' AND revision >= 1 AND abandoned_at IS NULL AND abandoned_by_account_id IS NULL) OR " +
                 "(state = 'abandoned' AND revision >= 2 AND abandoned_at IS NOT NULL AND abandoned_by_account_id IS NOT NULL)");
        migrationBuilder.Sql("""
            CREATE POLICY order_draft_lines_draft_delete ON orders.order_draft_lines
            AS RESTRICTIVE FOR DELETE
            USING (EXISTS (
                SELECT 1 FROM orders.order_drafts AS draft
                WHERE draft.tenant_id = order_draft_lines.tenant_id
                  AND draft.id = order_draft_lines.order_id
                  AND draft.state = 'draft'
                FOR UPDATE
            ))
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP POLICY order_draft_lines_draft_delete ON orders.order_draft_lines");
        migrationBuilder.DropCheckConstraint(
            name: "ck_order_drafts_lifecycle", schema: "orders", table: "order_drafts");
        migrationBuilder.AddCheckConstraint(
            name: "ck_order_drafts_lifecycle", schema: "orders", table: "order_drafts",
            sql: "(state = 'draft' AND revision = 1 AND abandoned_at IS NULL AND abandoned_by_account_id IS NULL) OR " +
                 "(state = 'abandoned' AND revision = 2 AND abandoned_at IS NOT NULL AND abandoned_by_account_id IS NOT NULL)");
    }
}
