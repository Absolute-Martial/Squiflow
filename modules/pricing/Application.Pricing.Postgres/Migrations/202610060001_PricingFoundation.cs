using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Pricing.Postgres.Migrations;

[Migration("202610060001_PricingFoundation")]
[DbContext(typeof(PricingDbContext))]
public partial class PricingFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "pricing");
        migrationBuilder.CreateSequence<long>(
            name: "price_revision_number_seq",
            schema: "pricing",
            incrementBy: 1);

        migrationBuilder.CreateTable(
            name: "price_revisions",
            schema: "pricing",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                revision_number = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('pricing.price_revision_number_seq')"),
                item_id = table.Column<Guid>(type: "uuid", nullable: false),
                unit_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                scope_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                base_unit_price = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "draft"),
                created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pricing_price_revisions", row => new { row.tenant_id, row.revision_id });
                table.CheckConstraint("ck_pricing_price_revisions_unit_code", "unit_code ~ '^[A-Z0-9_]{1,32}$'");
                table.CheckConstraint("ck_pricing_price_revisions_currency_code", "currency_code ~ '^[A-Z]{3}$'");
                table.CheckConstraint("ck_pricing_price_revisions_scope_kind", "scope_kind IN ('committed_quotation', 'committed_agreement', 'customer', 'program', 'organization', 'wholesale', 'default')");
                table.CheckConstraint("ck_pricing_price_revisions_base_price", "base_unit_price >= 0 AND base_unit_price <= 999999999999999.9999");
                table.CheckConstraint("ck_pricing_price_revisions_validity", "valid_to IS NULL OR valid_to > valid_from");
                table.CheckConstraint("ck_pricing_price_revisions_state", "state IN ('draft', 'published', 'superseded', 'retired')");
                table.CheckConstraint("ck_pricing_price_revisions_scope_id", "(scope_kind = 'default' AND scope_id = '00000000-0000-0000-0000-000000000000') OR (scope_kind <> 'default' AND scope_id <> '00000000-0000-0000-0000-000000000000')");
                table.CheckConstraint(
                    "ck_pricing_price_revisions_lifecycle",
                    "(state = 'draft' AND published_at IS NULL AND retired_at IS NULL) OR " +
                    "(state = 'published' AND published_at IS NOT NULL AND retired_at IS NULL) OR " +
                    "(state = 'superseded' AND published_at IS NOT NULL AND retired_at IS NULL) OR " +
                    "(state = 'retired' AND published_at IS NOT NULL AND retired_at IS NOT NULL)");
                table.ForeignKey("fk_pricing_price_revisions_tenants", row => row.tenant_id,
                    principalSchema: "tenancy", principalTable: "tenants", principalColumn: "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_pricing_price_revisions_accounts_created_by", row => row.created_by_account_id,
                    principalSchema: "identity_access", principalTable: "accounts", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "command_receipts",
            schema: "pricing",
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
                table.PrimaryKey("pk_pricing_command_receipts", row => new { row.tenant_id, row.account_id, row.operation, row.idempotency_key });
                table.CheckConstraint("ck_pricing_receipts_operation", "btrim(operation) <> ''");
                table.CheckConstraint("ck_pricing_receipts_idempotency_key", "btrim(idempotency_key) <> ''");
                table.CheckConstraint("ck_pricing_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.CheckConstraint("ck_pricing_receipts_response_object", "jsonb_typeof(response_json) = 'object'");
                table.ForeignKey("fk_pricing_receipts_accounts", row => row.account_id,
                    principalSchema: "identity_access", principalTable: "accounts", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_pricing_price_revisions_lookup",
            schema: "pricing",
            table: "price_revisions",
            columns: new[] { "tenant_id", "item_id", "unit_code", "currency_code", "scope_kind", "scope_id" });
        migrationBuilder.CreateIndex(
            name: "ux_pricing_price_revisions_tenant_revision",
            schema: "pricing",
            table: "price_revisions",
            columns: new[] { "tenant_id", "revision_number" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_price_revisions_created_by_account_id",
            schema: "pricing",
            table: "price_revisions",
            column: "created_by_account_id");
        migrationBuilder.CreateIndex(
            name: "IX_command_receipts_account_id",
            schema: "pricing",
            table: "command_receipts",
            column: "account_id");

        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist");
        migrationBuilder.Sql("REVOKE ALL ON SCHEMA pricing FROM PUBLIC");
        migrationBuilder.Sql("ALTER TABLE pricing.price_revisions ENABLE ROW LEVEL SECURITY");
        migrationBuilder.Sql("ALTER TABLE pricing.price_revisions FORCE ROW LEVEL SECURITY");
        migrationBuilder.Sql("ALTER TABLE pricing.command_receipts ENABLE ROW LEVEL SECURITY");
        migrationBuilder.Sql("ALTER TABLE pricing.command_receipts FORCE ROW LEVEL SECURITY");
        migrationBuilder.Sql("""
            CREATE POLICY price_revisions_tenant_isolation ON pricing.price_revisions
            USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
            WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
            """);
        migrationBuilder.Sql("""
            CREATE POLICY command_receipts_tenant_isolation ON pricing.command_receipts
            USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
            WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
            """);
        migrationBuilder.Sql("""
            ALTER TABLE pricing.price_revisions
            ADD CONSTRAINT ex_pricing_published_validity
            EXCLUDE USING gist
            (tenant_id WITH =, item_id WITH =, unit_code WITH =, currency_code WITH =,
             scope_kind WITH =, scope_id WITH =,
             tstzrange(valid_from, COALESCE(valid_to, 'infinity'::timestamptz), '[)') WITH &&)
            WHERE (state = 'published')
            """);
        migrationBuilder.Sql("""
            CREATE FUNCTION pricing.protect_price_revision_rows() RETURNS trigger
            LANGUAGE plpgsql AS $body$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Price revisions are retained and cannot be deleted.' USING ERRCODE = '23514';
                END IF;
                IF OLD.state IN ('superseded', 'retired') THEN
                    RAISE EXCEPTION 'Historical price revisions are immutable.' USING ERRCODE = '23514';
                END IF;
                IF NEW.tenant_id IS DISTINCT FROM OLD.tenant_id OR
                   NEW.revision_id IS DISTINCT FROM OLD.revision_id OR
                   NEW.revision_number IS DISTINCT FROM OLD.revision_number OR
                   NEW.item_id IS DISTINCT FROM OLD.item_id OR
                   NEW.unit_code IS DISTINCT FROM OLD.unit_code OR
                   NEW.currency_code IS DISTINCT FROM OLD.currency_code OR
                   NEW.scope_kind IS DISTINCT FROM OLD.scope_kind OR
                   NEW.scope_id IS DISTINCT FROM OLD.scope_id OR
                   NEW.base_unit_price IS DISTINCT FROM OLD.base_unit_price OR
                   NEW.valid_from IS DISTINCT FROM OLD.valid_from OR
                   NEW.valid_to IS DISTINCT FROM OLD.valid_to OR
                   NEW.created_by_account_id IS DISTINCT FROM OLD.created_by_account_id OR
                   NEW.created_at IS DISTINCT FROM OLD.created_at THEN
                    RAISE EXCEPTION 'Price facts and revision identity are immutable.' USING ERRCODE = '23514';
                END IF;
                IF OLD.state <> 'draft' AND NEW.published_at IS DISTINCT FROM OLD.published_at THEN
                    RAISE EXCEPTION 'Publication timestamps are immutable after publication.' USING ERRCODE = '23514';
                END IF;
                IF NOT (OLD.state = 'published' AND NEW.state = 'retired') AND
                   NEW.retired_at IS DISTINCT FROM OLD.retired_at THEN
                    RAISE EXCEPTION 'Retirement timestamps can only be set by retirement.' USING ERRCODE = '23514';
                END IF;
                IF OLD.state = 'draft' AND NEW.state NOT IN ('draft', 'published') THEN
                    RAISE EXCEPTION 'A draft price can only be published.' USING ERRCODE = '23514';
                END IF;
                IF OLD.state = 'published' AND NEW.state NOT IN ('published', 'superseded', 'retired') THEN
                    RAISE EXCEPTION 'A published price can only be superseded or retired.' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END
            $body$;
            CREATE TRIGGER protect_price_revision_rows
                BEFORE DELETE OR UPDATE ON pricing.price_revisions
                FOR EACH ROW EXECUTE FUNCTION pricing.protect_price_revision_rows()
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS protect_price_revision_rows ON pricing.price_revisions");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS pricing.protect_price_revision_rows()");
        migrationBuilder.Sql("ALTER TABLE IF EXISTS pricing.price_revisions DROP CONSTRAINT IF EXISTS ex_pricing_published_validity");
        migrationBuilder.DropTable(name: "command_receipts", schema: "pricing");
        migrationBuilder.DropTable(name: "price_revisions", schema: "pricing");
        migrationBuilder.DropSequence(name: "price_revision_number_seq", schema: "pricing");
        migrationBuilder.DropSchema(name: "pricing");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder) =>
        PricingModelV202610060001.Build(modelBuilder);
}
