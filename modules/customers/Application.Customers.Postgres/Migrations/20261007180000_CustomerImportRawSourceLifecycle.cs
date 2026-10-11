using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Application.Customers.Postgres.Migrations;

[Migration("20261007180000_CustomerImportRawSourceLifecycle")]
[DbContext(typeof(CustomerDbContext))]
public sealed class CustomerImportRawSourceLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE customers.object_storage_usage (
              tenant_id uuid NOT NULL,
              provider_scope varchar(256) NOT NULL,
              maximum_bytes bigint NOT NULL,
              reserved_bytes bigint NOT NULL DEFAULT 0,
              retained_bytes bigint NOT NULL DEFAULT 0,
              updated_at timestamptz NOT NULL,
              CONSTRAINT pk_customer_object_storage_usage PRIMARY KEY (tenant_id, provider_scope),
              CONSTRAINT ck_customer_object_storage_usage_limit CHECK (maximum_bytes > 0),
              CONSTRAINT ck_customer_object_storage_usage_reserved CHECK (reserved_bytes >= 0),
              CONSTRAINT ck_customer_object_storage_usage_retained CHECK (retained_bytes >= 0)
            );

            CREATE TABLE customers.import_source_objects (
              tenant_id uuid NOT NULL,
              object_key varchar(512) NOT NULL,
              provider_scope varchar(256) NOT NULL,
              byte_length bigint NOT NULL,
              sha256 char(64) NOT NULL,
              content_type varchar(128) NOT NULL,
              state integer NOT NULL,
              retention integer NOT NULL,
              created_at timestamptz NOT NULL,
              expires_at timestamptz NULL,
              failure_code varchar(128) NULL,
              retirement_generation bigint NOT NULL DEFAULT 0,
              retirement_lease_id uuid NULL,
              retirement_lease_expires_at timestamptz NULL,
              CONSTRAINT pk_customer_import_source_objects PRIMARY KEY (tenant_id, object_key),
              CONSTRAINT ck_customer_import_source_object_bytes CHECK (byte_length BETWEEN 0 AND 10485760),
              CONSTRAINT ck_customer_import_source_object_hash CHECK (sha256 ~ '^[0-9a-f]{64}$'),
              CONSTRAINT ck_customer_import_source_object_state CHECK (state BETWEEN 1 AND 7),
              CONSTRAINT ck_customer_import_source_object_retention CHECK (retention IN (1, 2)),
              CONSTRAINT ck_customer_import_source_object_archive CHECK (retention = 2 OR expires_at IS NOT NULL),
              CONSTRAINT ck_customer_import_source_object_retirement_generation CHECK (retirement_generation >= 0),
              CONSTRAINT ck_customer_import_source_object_retirement_lease CHECK (
                (state = 7 AND retirement_generation > 0 AND retirement_lease_id IS NOT NULL
                  AND retirement_lease_expires_at IS NOT NULL)
                OR (state <> 7 AND retirement_lease_id IS NULL AND retirement_lease_expires_at IS NULL)
              )
            );

            CREATE TABLE customers.object_storage_reservations (
              tenant_id uuid NOT NULL,
              provider_scope varchar(256) NOT NULL,
              reservation_id uuid NOT NULL,
              idempotency_key varchar(128) NOT NULL,
              fingerprint char(64) NOT NULL,
              object_key varchar(512) NOT NULL,
              byte_length bigint NOT NULL,
              state integer NOT NULL,
              created_at timestamptz NOT NULL,
              updated_at timestamptz NOT NULL,
              CONSTRAINT pk_customer_object_storage_reservations PRIMARY KEY (tenant_id, reservation_id),
              CONSTRAINT ux_customer_object_storage_reservation_key UNIQUE (tenant_id, provider_scope, idempotency_key),
              CONSTRAINT ck_customer_object_storage_reservation_hash CHECK (fingerprint ~ '^[0-9a-f]{64}$'),
              CONSTRAINT ck_customer_object_storage_reservation_bytes CHECK (byte_length BETWEEN 0 AND 10485760),
              CONSTRAINT ck_customer_object_storage_reservation_state CHECK (state IN (1, 2, 3, 4))
            );

            ALTER TABLE customers.imports ADD COLUMN source_object_key varchar(512) NULL;
            ALTER TABLE customers.imports ADD CONSTRAINT fk_customer_imports_source_object
              FOREIGN KEY (tenant_id, source_object_key)
              REFERENCES customers.import_source_objects (tenant_id, object_key)
              ON DELETE RESTRICT;
            ALTER TABLE customers.object_storage_reservations ADD CONSTRAINT fk_customer_object_storage_reservation_source
              FOREIGN KEY (tenant_id, object_key)
              REFERENCES customers.import_source_objects (tenant_id, object_key)
              ON DELETE RESTRICT;
            ALTER TABLE customers.object_storage_usage ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.object_storage_usage FORCE ROW LEVEL SECURITY;
            ALTER TABLE customers.import_source_objects ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.import_source_objects FORCE ROW LEVEL SECURITY;
            ALTER TABLE customers.object_storage_reservations ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.object_storage_reservations FORCE ROW LEVEL SECURITY;
            CREATE POLICY object_storage_usage_tenant_isolation ON customers.object_storage_usage
              USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
              WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);
            CREATE POLICY import_source_objects_tenant_isolation ON customers.import_source_objects
              USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
              WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);
            CREATE POLICY object_storage_reservations_tenant_isolation ON customers.object_storage_reservations
              USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
              WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);
            CREATE INDEX ix_customer_import_source_objects_expiry
              ON customers.import_source_objects (tenant_id, expires_at, retirement_lease_expires_at)
              WHERE state IN (3, 5, 7) AND retention = 1 AND expires_at IS NOT NULL;
            CREATE INDEX ix_customer_object_storage_reservations_state
              ON customers.object_storage_reservations (tenant_id, provider_scope, state);

            -- The trusted executor must visit tenants that only have expired raw bytes;
            -- otherwise retirement would depend on another import being runnable. The
            -- policy is SELECT-only and is limited to the migration-owned discovery
            -- function below; tenant-scoped runtime sessions retain the normal RLS path.
            DO $policies$ BEGIN
              EXECUTE format('CREATE POLICY import_source_discovery_owner ON customers.import_source_objects FOR SELECT TO %I USING (true)', current_user);
            END $policies$;

            CREATE OR REPLACE FUNCTION customers.discover_runnable_import_tenants(after_tenant_id uuid, page_size integer)
            RETURNS TABLE(tenant_id uuid)
            LANGUAGE plpgsql SECURITY DEFINER
            SET search_path = pg_catalog
            SET row_security = on
            AS $discover$ BEGIN
              IF page_size IS NULL OR page_size < 1 OR page_size > 50
                 OR after_tenant_id = '00000000-0000-0000-0000-000000000000'::uuid THEN
                RAISE EXCEPTION 'Import discovery bounds are invalid' USING ERRCODE='22023';
              END IF;
              RETURN QUERY
                SELECT candidate.tenant_id
                FROM (
                  SELECT DISTINCT w.tenant_id
                  FROM customers.import_work w
                  WHERE w.status IN (1,2,4)
                    AND (w.last_error IS NULL OR w.last_error NOT IN ('authority_changed','legacy_work_requires_replan'))
                    AND (w.next_attempt_at IS NULL OR w.next_attempt_at <= clock_timestamp())
                    AND (w.lease_expires_at IS NULL OR w.lease_expires_at <= clock_timestamp())
                    AND (EXISTS (SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id
                        AND r.import_id=w.import_id AND r.status IN(1,5) AND r.attempts<3)
                      OR NOT EXISTS (SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id
                        AND r.import_id=w.import_id AND r.status IN(1,5)))
                  UNION
                  SELECT DISTINCT source.tenant_id
                  FROM customers.import_source_objects source
                   WHERE source.state IN (3, 5) AND source.retention = 1
                     AND source.expires_at IS NOT NULL AND source.expires_at <= clock_timestamp()
                   UNION
                   SELECT DISTINCT source.tenant_id
                   FROM customers.import_source_objects source
                   WHERE source.state = 7 AND source.retention = 1
                     AND source.expires_at IS NOT NULL AND source.expires_at <= clock_timestamp()
                     AND source.retirement_lease_expires_at IS NOT NULL
                     AND source.retirement_lease_expires_at <= clock_timestamp()
                 ) candidate
                WHERE after_tenant_id IS NULL OR candidate.tenant_id > after_tenant_id
                ORDER BY candidate.tenant_id LIMIT page_size;
            END $discover$;
            REVOKE ALL ON FUNCTION customers.discover_runnable_import_tenants(uuid,integer) FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- The lifecycle tables use FORCE RLS for runtime sessions. Temporarily
            -- disable it inside the migration transaction so the owning migrator
            -- cannot mistake hidden tenant rows for an empty downgrade.
            ALTER TABLE customers.object_storage_usage NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE customers.import_source_objects NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE customers.object_storage_reservations NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE customers.object_storage_usage DISABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.import_source_objects DISABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.object_storage_reservations DISABLE ROW LEVEL SECURITY;

            DO $guard$ BEGIN
              IF EXISTS (SELECT 1 FROM customers.object_storage_usage)
                 OR EXISTS (SELECT 1 FROM customers.import_source_objects)
                 OR EXISTS (SELECT 1 FROM customers.object_storage_reservations)
                 OR EXISTS (SELECT 1 FROM customers.imports WHERE source_object_key IS NOT NULL) THEN
                RAISE EXCEPTION 'Customer raw-source lifecycle downgrade refuses to discard retained metadata, accounting or import references'
                  USING ERRCODE = '55000';
              END IF;
            END $guard$;

            CREATE OR REPLACE FUNCTION customers.discover_runnable_import_tenants(after_tenant_id uuid, page_size integer)
            RETURNS TABLE(tenant_id uuid)
            LANGUAGE plpgsql SECURITY DEFINER
            SET search_path = pg_catalog
            SET row_security = on
            AS $discover$ BEGIN
              IF page_size IS NULL OR page_size < 1 OR page_size > 50
                 OR after_tenant_id = '00000000-0000-0000-0000-000000000000'::uuid THEN
                RAISE EXCEPTION 'Import discovery bounds are invalid' USING ERRCODE='22023';
              END IF;
              RETURN QUERY
                SELECT DISTINCT w.tenant_id FROM customers.import_work w
                WHERE (after_tenant_id IS NULL OR w.tenant_id > after_tenant_id)
                  AND w.status IN (1,2,4)
                  AND (w.last_error IS NULL OR w.last_error NOT IN ('authority_changed','legacy_work_requires_replan'))
                  AND (w.next_attempt_at IS NULL OR w.next_attempt_at <= clock_timestamp())
                  AND (w.lease_expires_at IS NULL OR w.lease_expires_at <= clock_timestamp())
                  AND (EXISTS (SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id
                      AND r.import_id=w.import_id AND r.status IN(1,5) AND r.attempts<3)
                    OR NOT EXISTS (SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id
                      AND r.import_id=w.import_id AND r.status IN(1,5)))
                ORDER BY w.tenant_id LIMIT page_size;
            END $discover$;
            REVOKE ALL ON FUNCTION customers.discover_runnable_import_tenants(uuid,integer) FROM PUBLIC;
            DROP POLICY import_source_discovery_owner ON customers.import_source_objects;
            ALTER TABLE customers.imports DROP CONSTRAINT fk_customer_imports_source_object;
            ALTER TABLE customers.object_storage_reservations DROP CONSTRAINT fk_customer_object_storage_reservation_source;
            ALTER TABLE customers.imports DROP COLUMN source_object_key;
            DROP INDEX customers.ix_customer_import_source_objects_expiry;
            DROP INDEX customers.ix_customer_object_storage_reservations_state;
            DROP POLICY object_storage_usage_tenant_isolation ON customers.object_storage_usage;
            DROP POLICY import_source_objects_tenant_isolation ON customers.import_source_objects;
            DROP POLICY object_storage_reservations_tenant_isolation ON customers.object_storage_reservations;
            DROP TABLE customers.object_storage_reservations;
            DROP TABLE customers.import_source_objects;
            DROP TABLE customers.object_storage_usage;
            """);
    }
}
