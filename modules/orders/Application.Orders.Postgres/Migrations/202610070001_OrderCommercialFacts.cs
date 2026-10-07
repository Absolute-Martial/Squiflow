using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

public partial class OrderCommercialFacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("commercial_facts", "order_draft_lines", "jsonb", schema: "orders", nullable: true);
        migrationBuilder.AddCheckConstraint("ck_order_draft_lines_commercial_facts", "order_draft_lines",
            "commercial_facts IS NULL OR jsonb_typeof(commercial_facts) = 'object'", "orders");
        migrationBuilder.AlterColumn<string>("unit_code", "order_draft_lines", "character varying(64)", schema: "orders",
            maxLength: 64, nullable: false, oldClrType: typeof(string), oldType: "character varying(16)", oldMaxLength: 16);
        migrationBuilder.DropCheckConstraint("ck_order_draft_lines_unit_code", "order_draft_lines", "orders");
        migrationBuilder.AddCheckConstraint("ck_order_draft_lines_unit_code", "order_draft_lines", "unit_code ~ '^[A-Z0-9_-]{1,64}$'", "orders");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE orders.order_draft_lines, orders.command_receipts IN ACCESS EXCLUSIVE MODE;
            DO $body$
            DECLARE current_tenant uuid;
            BEGIN
                FOR current_tenant IN SELECT id FROM tenancy.tenants LOOP
                    PERFORM set_config('app.current_tenant', current_tenant::text, true);
                    IF EXISTS (SELECT 1 FROM orders.order_draft_lines WHERE tenant_id = current_tenant AND
                            (commercial_facts IS NOT NULL OR unit_code !~ '^[A-Z0-9]{1,16}$'))
                        OR EXISTS (SELECT 1 FROM orders.command_receipts WHERE tenant_id = current_tenant AND response_json->>'schemaVersion' = '4') THEN
                        RAISE EXCEPTION 'Cannot discard retained order commercial facts.';
                    END IF;
                END LOOP;
            END $body$;
            """);
        migrationBuilder.DropCheckConstraint("ck_order_draft_lines_commercial_facts", "order_draft_lines", "orders");
        migrationBuilder.DropColumn("commercial_facts", "order_draft_lines", "orders");
        migrationBuilder.DropCheckConstraint("ck_order_draft_lines_unit_code", "order_draft_lines", "orders");
        migrationBuilder.AlterColumn<string>("unit_code", "order_draft_lines", "character varying(16)", schema: "orders",
            maxLength: 16, nullable: false, oldClrType: typeof(string), oldType: "character varying(64)", oldMaxLength: 64);
        migrationBuilder.AddCheckConstraint("ck_order_draft_lines_unit_code", "order_draft_lines", "unit_code ~ '^[A-Z0-9]{1,16}$'", "orders");
    }
}
