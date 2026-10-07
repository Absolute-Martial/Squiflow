using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable
// Frozen one-shot migration metadata uses literal column arrays, not hot-path allocation.
#pragma warning disable CA1861

namespace Application.Catalog.Postgres.Migrations;

[Migration("202610060001_CatalogFoundation")]
[DbContext(typeof(CatalogDbContext))]
public partial class CatalogFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.EnsureSchema(name: "catalog");

        migrationBuilder.CreateTable(
            name: "units",
            schema: "catalog",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                precision = table.Column<int>(type: "smallint", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                revision = table.Column<long>(type: "bigint", nullable: false),
                created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                retired_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_catalog_units", unit => new { unit.tenant_id, unit.id });
                table.CheckConstraint("ck_catalog_units_code", "code ~ '^[A-Z0-9_-]{1,64}$'");
                table.CheckConstraint("ck_catalog_units_name_not_blank", "btrim(name) <> ''");
                table.CheckConstraint("ck_catalog_units_precision", "precision BETWEEN 0 AND 9");
                table.CheckConstraint("ck_catalog_units_status", "status IN ('active', 'retired')");
                table.CheckConstraint(
                    "ck_catalog_units_retirement",
                    "(status = 'active' AND retired_at IS NULL AND retired_by_account_id IS NULL) OR " +
                    "(status = 'retired' AND retired_at IS NOT NULL AND retired_by_account_id IS NOT NULL)");
                table.ForeignKey("fk_catalog_units_tenants_tenant_id", unit => unit.tenant_id,
                    principalSchema: "tenancy", principalTable: "tenants", principalColumn: "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_catalog_units_accounts_created_by_account_id", unit => unit.created_by_account_id,
                    principalSchema: "identity_access", principalTable: "accounts", principalColumn: "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_catalog_units_accounts_retired_by_account_id", unit => unit.retired_by_account_id,
                    principalSchema: "identity_access", principalTable: "accounts", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "items",
            schema: "catalog",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                base_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                stock_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                revision = table.Column<long>(type: "bigint", nullable: false),
                created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                retired_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_catalog_items", item => new { item.tenant_id, item.id });
                table.CheckConstraint("ck_catalog_items_code", "code IS NULL OR code ~ '^[A-Z0-9_-]{1,64}$'");
                table.CheckConstraint("ck_catalog_items_name_not_blank", "btrim(name) <> ''");
                table.CheckConstraint("ck_catalog_items_kind", "kind IN ('product', 'service')");
                table.CheckConstraint("ck_catalog_items_status", "status IN ('active', 'retired')");
                table.CheckConstraint("ck_catalog_items_stock_mode", "stock_mode IN ('precise_stock', 'availability_only', 'non_stock')");
                table.CheckConstraint(
                    "ck_catalog_items_retirement",
                    "(status = 'active' AND retired_at IS NULL AND retired_by_account_id IS NULL) OR " +
                    "(status = 'retired' AND retired_at IS NOT NULL AND retired_by_account_id IS NOT NULL)");
                table.ForeignKey("fk_catalog_items_tenants_tenant_id", item => item.tenant_id,
                    principalSchema: "tenancy", principalTable: "tenants", principalColumn: "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_catalog_items_accounts_created_by_account_id", item => item.created_by_account_id,
                    principalSchema: "identity_access", principalTable: "accounts", principalColumn: "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_catalog_items_accounts_retired_by_account_id", item => item.retired_by_account_id,
                    principalSchema: "identity_access", principalTable: "accounts", principalColumn: "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_catalog_items_units_base_unit", item => new { item.tenant_id, item.base_unit_id },
                    principalSchema: "catalog", principalTable: "units", principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "command_receipts",
            schema: "catalog",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_catalog_command_receipts", receipt => new
                { receipt.tenant_id, receipt.account_id, receipt.operation, receipt.idempotency_key });
                table.CheckConstraint("ck_catalog_receipts_operation", "btrim(operation) <> ''");
                table.CheckConstraint("ck_catalog_receipts_idempotency_key", "btrim(idempotency_key) <> ''");
                table.CheckConstraint("ck_catalog_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.CheckConstraint("ck_catalog_receipts_response_object", "jsonb_typeof(response_json) = 'object'");
                table.ForeignKey("fk_catalog_receipts_accounts_account_id", receipt => receipt.account_id,
                    principalSchema: "identity_access", principalTable: "accounts", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_catalog_units_tenant_code", schema: "catalog", table: "units",
            columns: new[] { "tenant_id", "code" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_catalog_units_tenant_created_at_id", schema: "catalog", table: "units",
            columns: new[] { "tenant_id", "created_at", "id" }, descending: new[] { false, true, true });
        migrationBuilder.CreateIndex("IX_units_created_by_account_id", "units", "created_by_account_id", schema: "catalog");
        migrationBuilder.CreateIndex("IX_units_retired_by_account_id", "units", "retired_by_account_id", schema: "catalog");
        migrationBuilder.CreateIndex(
            name: "ux_catalog_items_tenant_code", schema: "catalog", table: "items",
            columns: new[] { "tenant_id", "code" }, unique: true, filter: "code IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "ix_catalog_items_tenant_created_at_id", schema: "catalog", table: "items",
            columns: new[] { "tenant_id", "created_at", "id" }, descending: new[] { false, true, true });
        migrationBuilder.CreateIndex("IX_items_created_by_account_id", "items", "created_by_account_id", schema: "catalog");
        migrationBuilder.CreateIndex("IX_items_retired_by_account_id", "items", "retired_by_account_id", schema: "catalog");
        migrationBuilder.CreateIndex("IX_items_tenant_id_base_unit_id", "items", new[] { "tenant_id", "base_unit_id" }, schema: "catalog");
        migrationBuilder.CreateIndex("IX_command_receipts_account_id", "command_receipts", "account_id", schema: "catalog");

        migrationBuilder.Sql("REVOKE ALL ON SCHEMA catalog FROM PUBLIC");
        foreach (var table in new[] { "units", "items", "command_receipts" })
        {
            migrationBuilder.Sql($"ALTER TABLE catalog.{table} ENABLE ROW LEVEL SECURITY");
            migrationBuilder.Sql($"ALTER TABLE catalog.{table} FORCE ROW LEVEL SECURITY");
            migrationBuilder.Sql($"""
                CREATE POLICY {table}_tenant_isolation ON catalog.{table}
                USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                """);
        }

        migrationBuilder.Sql("""
            CREATE FUNCTION catalog.protect_unit_rows() RETURNS trigger
            LANGUAGE plpgsql AS $body$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Catalog master rows are retained and cannot be deleted.' USING ERRCODE = '23514';
                END IF;
                IF OLD.status = 'retired' THEN
                    RAISE EXCEPTION 'Retired catalog rows are immutable.' USING ERRCODE = '23514';
                END IF;
                IF NEW.code IS DISTINCT FROM OLD.code OR NEW.precision IS DISTINCT FROM OLD.precision OR
                   NEW.revision <> OLD.revision + 1 THEN
                    RAISE EXCEPTION 'Unit mathematical meaning and revision cannot be silently changed.' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END
            $body$;
            CREATE FUNCTION catalog.protect_item_rows() RETURNS trigger
            LANGUAGE plpgsql AS $body$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Catalog master rows are retained and cannot be deleted.' USING ERRCODE = '23514';
                END IF;
                IF OLD.status = 'retired' THEN
                    RAISE EXCEPTION 'Retired catalog rows are immutable.' USING ERRCODE = '23514';
                END IF;
                IF NEW.code IS DISTINCT FROM OLD.code OR NEW.kind IS DISTINCT FROM OLD.kind OR
                   NEW.base_unit_id IS DISTINCT FROM OLD.base_unit_id OR NEW.stock_mode IS DISTINCT FROM OLD.stock_mode OR
                   NEW.revision <> OLD.revision + 1 THEN
                    RAISE EXCEPTION 'Catalog item identity, kind, unit and stock mode are immutable.' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END
            $body$;
            CREATE TRIGGER protect_catalog_units
                BEFORE DELETE OR UPDATE ON catalog.units
                FOR EACH ROW EXECUTE FUNCTION catalog.protect_unit_rows();
            CREATE TRIGGER protect_catalog_items
                BEFORE DELETE OR UPDATE ON catalog.items
                FOR EACH ROW EXECUTE FUNCTION catalog.protect_item_rows();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS protect_catalog_items ON catalog.items");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS protect_catalog_units ON catalog.units");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS catalog.protect_item_rows()");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS catalog.protect_unit_rows()");
        migrationBuilder.Sql("DROP TABLE IF EXISTS catalog.command_receipts");
        migrationBuilder.Sql("DROP TABLE IF EXISTS catalog.items");
        migrationBuilder.Sql("DROP TABLE IF EXISTS catalog.units");
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS catalog");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        CatalogModelV202610060001.Build(modelBuilder);
    }
}
