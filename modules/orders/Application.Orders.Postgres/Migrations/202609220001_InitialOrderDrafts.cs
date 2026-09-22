using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

public partial class InitialOrderDrafts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "orders");

        migrationBuilder.CreateTable(
            name: "order_drafts",
            schema: "orders",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                total = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                revision = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_drafts", order => new { order.tenant_id, order.id });
                table.CheckConstraint("ck_order_drafts_summary_not_blank", "btrim(summary) <> ''");
                table.CheckConstraint("ck_order_drafts_currency_code", "currency_code ~ '^[A-Z]{3}$'");
                table.CheckConstraint("ck_order_drafts_total", "total >= 0");
                table.CheckConstraint("ck_order_drafts_revision", "revision > 0");
                table.ForeignKey(
                    name: "fk_order_drafts_accounts_created_by_account_id",
                    column: order => order.created_by_account_id,
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_order_drafts_tenants_tenant_id",
                    column: order => order.tenant_id,
                    principalSchema: "tenancy",
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "command_receipts",
            schema: "orders",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_command_receipts",
                    receipt => new { receipt.tenant_id, receipt.account_id, receipt.operation, receipt.idempotency_key });
                table.CheckConstraint("ck_command_receipts_operation_not_blank", "btrim(operation) <> ''");
                table.CheckConstraint("ck_command_receipts_idempotency_key_not_blank", "btrim(idempotency_key) <> ''");
                table.CheckConstraint("ck_command_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.CheckConstraint("ck_command_receipts_response_object", "jsonb_typeof(response_json) = 'object'");
                table.ForeignKey(
                    name: "fk_command_receipts_accounts_account_id",
                    column: receipt => receipt.account_id,
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_command_receipts_order_drafts",
                    columns: receipt => new { receipt.tenant_id, receipt.order_id },
                    principalSchema: "orders",
                    principalTable: "order_drafts",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "order_draft_lines",
            schema: "orders",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                quantity = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                unit_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                unit_price = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                line_total = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_order_draft_lines",
                    line => new { line.tenant_id, line.order_id, line.position });
                table.CheckConstraint("ck_order_draft_lines_position", "position BETWEEN 1 AND 100");
                table.CheckConstraint("ck_order_draft_lines_description_not_blank", "btrim(description) <> ''");
                table.CheckConstraint("ck_order_draft_lines_quantity", "quantity > 0");
                table.CheckConstraint("ck_order_draft_lines_unit_code", "unit_code ~ '^[A-Z0-9]{1,16}$'");
                table.CheckConstraint("ck_order_draft_lines_unit_price", "unit_price >= 0");
                table.CheckConstraint("ck_order_draft_lines_line_total", "line_total >= 0");
                table.ForeignKey(
                    name: "fk_order_draft_lines_order_drafts",
                    columns: line => new { line.tenant_id, line.order_id },
                    principalSchema: "orders",
                    principalTable: "order_drafts",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_order_drafts_created_by_account_id",
            schema: "orders",
            table: "order_drafts",
            column: "created_by_account_id");

        migrationBuilder.CreateIndex(
            name: "IX_command_receipts_account_id",
            schema: "orders",
            table: "command_receipts",
            column: "account_id");

        migrationBuilder.CreateIndex(
            name: "IX_command_receipts_tenant_id_order_id",
            schema: "orders",
            table: "command_receipts",
            columns: new[] { "tenant_id", "order_id" });

        migrationBuilder.CreateIndex(
            name: "ux_order_drafts_id",
            schema: "orders",
            table: "order_drafts",
            column: "id",
            unique: true);

        migrationBuilder.Sql("REVOKE ALL ON SCHEMA orders FROM PUBLIC");
        migrationBuilder.Sql("ALTER TABLE orders.order_drafts ENABLE ROW LEVEL SECURITY");
        migrationBuilder.Sql("ALTER TABLE orders.order_drafts FORCE ROW LEVEL SECURITY");
        migrationBuilder.Sql("ALTER TABLE orders.order_draft_lines ENABLE ROW LEVEL SECURITY");
        migrationBuilder.Sql("ALTER TABLE orders.order_draft_lines FORCE ROW LEVEL SECURITY");
        migrationBuilder.Sql("ALTER TABLE orders.command_receipts ENABLE ROW LEVEL SECURITY");
        migrationBuilder.Sql("ALTER TABLE orders.command_receipts FORCE ROW LEVEL SECURITY");

        AddTenantPolicy(migrationBuilder, "order_drafts");
        AddTenantPolicy(migrationBuilder, "order_draft_lines");
        AddTenantPolicy(migrationBuilder, "command_receipts");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "command_receipts", schema: "orders");
        migrationBuilder.DropTable(name: "order_draft_lines", schema: "orders");
        migrationBuilder.DropTable(name: "order_drafts", schema: "orders");
    }

    private static void AddTenantPolicy(MigrationBuilder migrationBuilder, string table) =>
        migrationBuilder.Sql(
            $"""
            CREATE POLICY {table}_tenant_isolation ON orders.{table}
            USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
            WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
            """);
}
