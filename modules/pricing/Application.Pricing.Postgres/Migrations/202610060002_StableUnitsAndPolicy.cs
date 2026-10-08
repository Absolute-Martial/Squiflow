using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Pricing.Postgres.Migrations;

[Migration("202610060002_StableUnitsAndPolicy")]
[DbContext(typeof(PricingDbContext))]
public sealed class StableUnitsAndPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Legacy units are retained, never guessed into Catalog identities.
        migrationBuilder.Sql("DROP TRIGGER protect_price_revision_rows ON pricing.price_revisions");
        migrationBuilder.Sql("ALTER TABLE pricing.price_revisions ALTER COLUMN unit_code TYPE character varying(64)");
        migrationBuilder.Sql("ALTER TABLE pricing.price_revisions DROP CONSTRAINT ck_pricing_price_revisions_unit_code");
        migrationBuilder.Sql("ALTER TABLE pricing.price_revisions ADD CONSTRAINT ck_pricing_price_revisions_unit_code CHECK (unit_code ~ '^[A-Z0-9_-]{1,64}$')");
        migrationBuilder.AddColumn<Guid>(name: "unit_id", table: "price_revisions", schema: "pricing", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "price_id", table: "price_revisions", schema: "pricing", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<long>(name: "unit_conversion_revision", table: "price_revisions", schema: "pricing", type: "bigint", nullable: true);
        migrationBuilder.Sql("UPDATE pricing.price_revisions SET price_id = revision_id");
        migrationBuilder.Sql("ALTER TABLE pricing.price_revisions ALTER COLUMN price_id SET NOT NULL");
        migrationBuilder.Sql("ALTER TABLE pricing.price_revisions DROP CONSTRAINT ex_pricing_published_validity");
        migrationBuilder.Sql("""
            ALTER TABLE pricing.price_revisions ADD CONSTRAINT ex_pricing_published_validity
            EXCLUDE USING gist (tenant_id WITH =, item_id WITH =, unit_id WITH =, currency_code WITH =,
                scope_kind WITH =, scope_id WITH =,
                tstzrange(valid_from, COALESCE(valid_to, 'infinity'::timestamptz), '[)') WITH &&)
            WHERE (state = 'published');
            CREATE INDEX ix_pricing_stable_lookup ON pricing.price_revisions
                (tenant_id, item_id, unit_id, currency_code, scope_kind, scope_id, valid_from, revision_number);
            CREATE INDEX ix_pricing_family ON pricing.price_revisions (tenant_id, price_id, revision_number);
            CREATE OR REPLACE FUNCTION pricing.protect_price_revision_rows() RETURNS trigger
            LANGUAGE plpgsql AS $body$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Price revisions are retained.' USING ERRCODE = '23514';
                END IF;
                IF TG_OP = 'INSERT' THEN
                    IF NEW.unit_id IS NULL OR NEW.unit_id = '00000000-0000-0000-0000-000000000000' OR
                       NEW.price_id = '00000000-0000-0000-0000-000000000000' OR
                       NEW.unit_conversion_revision < 1 OR
                       NEW.scope_kind IN ('committed_agreement', 'committed_quotation') OR
                       (NEW.scope_kind = 'wholesale' AND NEW.scope_id <> '00000000-0000-0000-0000-000000000001') OR
                       NEW.state <> 'draft' THEN
                        RAISE EXCEPTION 'Unsupported new pricing fact.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END IF;
                IF OLD.state IN ('retired', 'superseded') OR
                   ROW(NEW.tenant_id, NEW.revision_id, NEW.revision_number, NEW.price_id, NEW.item_id, NEW.unit_id, NEW.unit_conversion_revision,
                       NEW.unit_code, NEW.currency_code, NEW.scope_kind, NEW.scope_id, NEW.base_unit_price,
                       NEW.valid_from, NEW.valid_to, NEW.created_by_account_id, NEW.created_at)
                   IS DISTINCT FROM
                   ROW(OLD.tenant_id, OLD.revision_id, OLD.revision_number, OLD.price_id, OLD.item_id, OLD.unit_id, OLD.unit_conversion_revision,
                       OLD.unit_code, OLD.currency_code, OLD.scope_kind, OLD.scope_id, OLD.base_unit_price,
                       OLD.valid_from, OLD.valid_to, OLD.created_by_account_id, OLD.created_at) THEN
                    RAISE EXCEPTION 'Economic pricing history is immutable.' USING ERRCODE = '23514';
                END IF;
                IF OLD.state = 'draft' AND (NEW.state <> 'published' OR NEW.unit_id IS NULL OR
                   NEW.scope_kind IN ('committed_agreement', 'committed_quotation')) THEN
                    RAISE EXCEPTION 'Unsupported publication transition.' USING ERRCODE = '23514';
                END IF;
                IF OLD.state = 'published' AND (NEW.state NOT IN ('retired', 'superseded') OR
                   NEW.published_at IS DISTINCT FROM OLD.published_at) THEN
                    RAISE EXCEPTION 'Unsupported published lifecycle transition.' USING ERRCODE = '23514';
                END IF;
                IF NEW.state <> 'retired' AND NEW.retired_at IS DISTINCT FROM OLD.retired_at THEN
                    RAISE EXCEPTION 'Retirement evidence is immutable.' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $body$;
            CREATE TRIGGER protect_price_revision_rows BEFORE INSERT OR UPDATE OR DELETE ON pricing.price_revisions
                FOR EACH ROW EXECUTE FUNCTION pricing.protect_price_revision_rows();
            CREATE TABLE pricing.override_policies (
                tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id),
                revision bigint NOT NULL CHECK (revision > 0),
                minimum_unit_price numeric(19,4) NOT NULL CHECK (minimum_unit_price >= 0),
                maximum_unit_price numeric(19,4) NOT NULL CHECK (maximum_unit_price >= minimum_unit_price),
                maximum_decrease_percent numeric(9,4) CHECK (maximum_decrease_percent BETWEEN 0 AND 100),
                maximum_increase_percent numeric(9,4) CHECK (maximum_increase_percent BETWEEN 0 AND 10000),
                created_by_account_id uuid NOT NULL REFERENCES identity_access.accounts(id),
                created_at timestamptz NOT NULL,
                PRIMARY KEY (tenant_id, revision));
            ALTER TABLE pricing.override_policies ENABLE ROW LEVEL SECURITY;
            ALTER TABLE pricing.override_policies FORCE ROW LEVEL SECURITY;
            CREATE POLICY override_policies_tenant_isolation ON pricing.override_policies
                USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);
            CREATE FUNCTION pricing.protect_retained_rows() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Retained pricing evidence is immutable.' USING ERRCODE = '23514'; END $body$;
            CREATE TRIGGER protect_override_policies BEFORE UPDATE OR DELETE ON pricing.override_policies
                FOR EACH ROW EXECUTE FUNCTION pricing.protect_retained_rows();
            CREATE TRIGGER protect_command_receipts BEFORE UPDATE OR DELETE ON pricing.command_receipts
                FOR EACH ROW EXECUTE FUNCTION pricing.protect_retained_rows();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Retained stable-unit/policy evidence cannot be destructively downgraded.");

    protected override void BuildTargetModel(ModelBuilder modelBuilder) => PricingModelV202610060002.Build(modelBuilder);
}
