using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Catalog.Postgres.Migrations;

[DbContext(typeof(CatalogDbContext))]
[Migration("202610060002_CatalogConversionsAndAvailability")]
public sealed class CatalogConversionsAndAvailability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql("""
            ALTER TABLE catalog.units ADD CONSTRAINT ck_catalog_units_revision CHECK (revision > 0);
            ALTER TABLE catalog.items ADD COLUMN availability varchar(16);
            ALTER TABLE catalog.items ADD COLUMN availability_changed_at timestamptz;
            ALTER TABLE catalog.items ADD COLUMN availability_changed_by_account_id uuid
                CONSTRAINT fk_catalog_items_accounts_availability_changed_by REFERENCES identity_access.accounts(id) ON DELETE RESTRICT;
            ALTER TABLE catalog.items ADD CONSTRAINT ck_catalog_items_revision CHECK (revision > 0);
            ALTER TABLE catalog.items ADD CONSTRAINT ck_catalog_items_availability_audit CHECK (
                (availability_changed_at IS NULL) = (availability_changed_by_account_id IS NULL));
            CREATE INDEX "IX_items_availability_changed_by_account_id" ON catalog.items(availability_changed_by_account_id);
            CREATE TABLE catalog.unit_conversions (
                tenant_id uuid NOT NULL, source_unit_id uuid NOT NULL, target_unit_id uuid NOT NULL,
                revision bigint NOT NULL, numerator numeric(19,9) NOT NULL, denominator numeric(19,9) NOT NULL,
                published_by_account_id uuid NOT NULL, published_at timestamptz NOT NULL,
                CONSTRAINT pk_catalog_unit_conversions PRIMARY KEY (tenant_id, source_unit_id, target_unit_id, revision),
                CONSTRAINT ck_catalog_conversions_pair CHECK (source_unit_id <> target_unit_id),
                CONSTRAINT ck_catalog_conversions_revision CHECK (revision > 0),
                CONSTRAINT ck_catalog_conversions_ratio CHECK (numerator > 0 AND denominator > 0),
                CONSTRAINT fk_catalog_conversions_units_source FOREIGN KEY (tenant_id, source_unit_id)
                    REFERENCES catalog.units(tenant_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_catalog_conversions_units_target FOREIGN KEY (tenant_id, target_unit_id)
                    REFERENCES catalog.units(tenant_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_catalog_conversions_accounts_published_by FOREIGN KEY (published_by_account_id)
                    REFERENCES identity_access.accounts(id) ON DELETE RESTRICT);
            CREATE INDEX "IX_unit_conversions_published_by_account_id" ON catalog.unit_conversions(published_by_account_id);
            CREATE INDEX "IX_unit_conversions_tenant_id_target_unit_id" ON catalog.unit_conversions(tenant_id, target_unit_id);
            ALTER TABLE catalog.unit_conversions ENABLE ROW LEVEL SECURITY;
            ALTER TABLE catalog.unit_conversions FORCE ROW LEVEL SECURITY;
            CREATE POLICY unit_conversions_tenant_isolation ON catalog.unit_conversions
                USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);
            CREATE FUNCTION catalog.protect_retained_facts() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                RAISE EXCEPTION 'Retained catalog facts are immutable.' USING ERRCODE = '23514';
            END $body$;
            CREATE TRIGGER protect_catalog_conversion_facts BEFORE UPDATE OR DELETE ON catalog.unit_conversions
                FOR EACH ROW EXECUTE FUNCTION catalog.protect_retained_facts();
            CREATE TRIGGER protect_catalog_receipt_facts BEFORE UPDATE OR DELETE ON catalog.command_receipts
                FOR EACH ROW EXECUTE FUNCTION catalog.protect_retained_facts();
            """);
        // The baseline is not yet activated; existing availability-only rows must
        // be migrated explicitly per tenant without bypassing forced tenant RLS.
        migrationBuilder.Sql("""
            ALTER TABLE catalog.items DISABLE TRIGGER protect_catalog_items;
            DO $body$ DECLARE current_tenant uuid; BEGIN
                FOR current_tenant IN SELECT id FROM tenancy.tenants LOOP
                    PERFORM set_config('app.current_tenant', current_tenant::text, true);
                    UPDATE catalog.items SET availability = 'unavailable' WHERE stock_mode = 'availability_only';
                END LOOP;
            END $body$;
            ALTER TABLE catalog.items ENABLE TRIGGER protect_catalog_items;
            ALTER TABLE catalog.items ADD CONSTRAINT ck_catalog_items_availability CHECK (
                (stock_mode = 'availability_only' AND availability IS NOT NULL AND availability IN ('available', 'unavailable')) OR
                (stock_mode <> 'availability_only' AND availability IS NULL));
            """);
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION catalog.protect_unit_rows() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    IF NEW.status <> 'active' OR NEW.revision <> 1 THEN
                        RAISE EXCEPTION 'Catalog units must begin active at revision one.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END IF;
                IF TG_OP = 'DELETE' OR OLD.status = 'retired' THEN
                    RAISE EXCEPTION 'Catalog rows are retained and retired rows immutable.' USING ERRCODE = '23514';
                END IF;
                IF ROW(NEW.tenant_id,NEW.id,NEW.code,NEW.precision,NEW.created_at,NEW.created_by_account_id)
                    IS DISTINCT FROM ROW(OLD.tenant_id,OLD.id,OLD.code,OLD.precision,OLD.created_at,OLD.created_by_account_id)
                    OR NEW.revision <> OLD.revision + 1 THEN
                    RAISE EXCEPTION 'Unit mathematical meaning is immutable.' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $body$;
            CREATE OR REPLACE FUNCTION catalog.protect_item_rows() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE unit_status varchar(16);
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    SELECT status INTO unit_status FROM catalog.units
                        WHERE tenant_id = NEW.tenant_id AND id = NEW.base_unit_id FOR SHARE;
                    IF unit_status IS DISTINCT FROM 'active' OR NEW.status <> 'active' OR NEW.revision <> 1 THEN
                        RAISE EXCEPTION 'New catalog items require an active base unit.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END IF;
                IF TG_OP = 'DELETE' OR OLD.status = 'retired' THEN
                    RAISE EXCEPTION 'Catalog rows are retained and retired rows immutable.' USING ERRCODE = '23514';
                END IF;
                IF ROW(NEW.tenant_id,NEW.id,NEW.code,NEW.kind,NEW.base_unit_id,NEW.stock_mode,NEW.created_at,NEW.created_by_account_id)
                    IS DISTINCT FROM ROW(OLD.tenant_id,OLD.id,OLD.code,OLD.kind,OLD.base_unit_id,OLD.stock_mode,OLD.created_at,OLD.created_by_account_id)
                    OR NEW.revision <> OLD.revision + 1 THEN
                    RAISE EXCEPTION 'Catalog item meaning is immutable.' USING ERRCODE = '23514';
                END IF;
                IF NEW.availability IS DISTINCT FROM OLD.availability AND
                    (NEW.availability_changed_at IS NULL OR NEW.availability_changed_by_account_id IS NULL) THEN
                    RAISE EXCEPTION 'Availability changes require actor and time.' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $body$;
            DROP TRIGGER protect_catalog_units ON catalog.units;
            CREATE TRIGGER protect_catalog_units BEFORE INSERT OR UPDATE OR DELETE ON catalog.units
                FOR EACH ROW EXECUTE FUNCTION catalog.protect_unit_rows();
            DROP TRIGGER protect_catalog_items ON catalog.items;
            CREATE TRIGGER protect_catalog_items BEFORE INSERT OR UPDATE OR DELETE ON catalog.items
                FOR EACH ROW EXECUTE FUNCTION catalog.protect_item_rows();
            CREATE FUNCTION catalog.admit_conversion_revision() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE current_revision bigint; unit_row record; active_count integer := 0;
            BEGIN
                PERFORM pg_advisory_xact_lock(hashtextextended(
                    'catalog-pair:' || replace(NEW.tenant_id::text,'-','') || ':' ||
                    replace(NEW.source_unit_id::text,'-','') || ':' || replace(NEW.target_unit_id::text,'-',''), 0));
                FOR unit_row IN SELECT id,status FROM catalog.units
                    WHERE tenant_id = NEW.tenant_id AND id IN (NEW.source_unit_id,NEW.target_unit_id)
                    ORDER BY id FOR SHARE LOOP
                    IF unit_row.status = 'active' THEN active_count := active_count + 1; END IF;
                END LOOP;
                SELECT COALESCE(MAX(revision),0) INTO current_revision FROM catalog.unit_conversions
                    WHERE tenant_id = NEW.tenant_id AND source_unit_id = NEW.source_unit_id AND target_unit_id = NEW.target_unit_id;
                IF active_count <> 2 OR NEW.revision <> current_revision + 1 THEN
                    RAISE EXCEPTION 'Conversion publication requires active units and the next immutable revision.' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $body$;
            CREATE TRIGGER admit_catalog_conversion_revision BEFORE INSERT ON catalog.unit_conversions
                FOR EACH ROW EXECUTE FUNCTION catalog.admit_conversion_revision();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql("DO $body$ BEGIN RAISE EXCEPTION 'Catalog history requires forward recovery, not destructive downgrade.'; END $body$;");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        CatalogModelV202610060002.Build(modelBuilder);
    }
}
