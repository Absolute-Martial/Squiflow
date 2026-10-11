using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Customers.Postgres.Migrations;

[Migration("20261011090000_CustomerImportUploadRecovery")]
[DbContext(typeof(CustomerDbContext))]
public sealed class CustomerImportUploadRecovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE customers.object_storage_reservations
              ADD COLUMN account_id uuid NULL REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
              ADD COLUMN import_id uuid NULL,
              ADD COLUMN upload_generation bigint NOT NULL DEFAULT 0,
              ADD COLUMN upload_lease_id uuid NULL,
              ADD COLUMN upload_lease_expires_at timestamptz NULL;
            ALTER TABLE customers.import_source_objects
              ADD COLUMN staging_expires_at timestamptz NULL,
              ADD COLUMN retirement_fenced boolean NOT NULL DEFAULT false;

            -- FORCE RLS applies to the owning migrator too. Only exact retained receipt
            -- evidence attributes old reservations; ambiguous staging remains ownerless.
            DO $backfill$
            DECLARE tenant uuid;
            BEGIN
              FOR tenant IN SELECT id FROM tenancy.tenants LOOP
                PERFORM set_config('app.current_tenant', tenant::text, true);
                UPDATE customers.imports i SET source_object_key = s.object_key
                FROM customers.import_source_objects s
                WHERE i.tenant_id = tenant AND s.tenant_id = tenant AND i.source_object_key IS NULL
                  AND s.state = 3 AND s.sha256 = i.manifest_hash AND s.byte_length = i.byte_length
                  AND s.object_key = 'imports/' || tenant::text || '/' || replace(i.import_id::text, '-', '') || '/' || i.manifest_hash || '.csv';
                WITH attributable AS (
                  SELECT r.reservation_id, i.account_id, i.import_id,
                    count(*) OVER (PARTITION BY r.reservation_id) AS matches
                  FROM customers.object_storage_reservations r JOIN customers.imports i
                    ON i.tenant_id = r.tenant_id AND i.source_object_key = r.object_key
                    AND i.idempotency_key = r.idempotency_key AND i.fingerprint = r.fingerprint
                  WHERE r.tenant_id = tenant
                )
                UPDATE customers.object_storage_reservations r
                  SET account_id = a.account_id, import_id = a.import_id
                FROM attributable a
                WHERE r.tenant_id = tenant AND r.reservation_id = a.reservation_id AND a.matches = 1;
                UPDATE customers.import_source_objects s
                  SET staging_expires_at = s.created_at + interval '7 days'
                WHERE s.tenant_id = tenant AND s.state <> 6
                  AND NOT EXISTS (SELECT 1 FROM customers.imports i
                    WHERE i.tenant_id = tenant AND i.source_object_key = s.object_key);
              END LOOP;
              PERFORM set_config('app.current_tenant', '', true);
            END $backfill$;

            ALTER TABLE customers.object_storage_reservations DROP CONSTRAINT ux_customer_object_storage_reservation_key;
            CREATE UNIQUE INDEX ux_customer_object_storage_reservation_caller_key
              ON customers.object_storage_reservations(tenant_id, provider_scope, account_id, idempotency_key)
              WHERE account_id IS NOT NULL;
            CREATE UNIQUE INDEX ux_customer_object_storage_reservation_legacy_key
              ON customers.object_storage_reservations(tenant_id, provider_scope, idempotency_key)
              WHERE account_id IS NULL;
            ALTER TABLE customers.object_storage_reservations
              ADD CONSTRAINT ck_customer_source_reservation_provenance CHECK ((account_id IS NULL) = (import_id IS NULL)),
              ADD CONSTRAINT ck_customer_source_upload_generation CHECK (upload_generation >= 0),
              ADD CONSTRAINT ck_customer_source_upload_lease CHECK (
                (upload_lease_id IS NULL AND upload_lease_expires_at IS NULL)
                OR (upload_generation > 0 AND upload_lease_id IS NOT NULL AND upload_lease_expires_at IS NOT NULL AND state IN (1,4)));
            ALTER TABLE customers.import_source_objects
              ADD CONSTRAINT ck_customer_source_staging_expiry CHECK (staging_expires_at IS NULL OR staging_expires_at <= created_at + interval '7 days'),
              ADD CONSTRAINT ck_customer_source_retirement_fence CHECK (NOT retirement_fenced OR state = 6);
            CREATE INDEX ix_customer_import_source_staging_expiry
              ON customers.import_source_objects(tenant_id, staging_expires_at, retirement_lease_expires_at)
              WHERE staging_expires_at IS NOT NULL AND state <> 6;
            """);
        migrationBuilder.Sql(DiscoverySql);
    }

    // Preserve bounded, private discovery. Its existing SELECT-only owner policies
    // also cover staging; no runtime role gains cross-tenant table access.
    internal const string DiscoverySql = """
        DO $policies$ BEGIN
          EXECUTE format('ALTER POLICY import_discovery_owner ON customers.import_work TO %I', current_user);
          EXECUTE format('ALTER POLICY import_discovery_owner ON customers.import_rows TO %I', current_user);
          EXECUTE format('ALTER POLICY import_source_discovery_owner ON customers.import_source_objects TO %I', current_user);
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
          RETURN QUERY SELECT candidate.tenant_id FROM (
            SELECT DISTINCT w.tenant_id FROM customers.import_work w
            WHERE w.status IN (1,2,4)
              AND (w.last_error IS NULL OR w.last_error NOT IN ('authority_changed','legacy_work_requires_replan'))
              AND (w.next_attempt_at IS NULL OR w.next_attempt_at <= clock_timestamp())
              AND (w.lease_expires_at IS NULL OR w.lease_expires_at <= clock_timestamp())
              AND (EXISTS (SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id
                AND r.import_id=w.import_id AND r.status IN(1,5) AND r.attempts<3)
                OR NOT EXISTS (SELECT 1 FROM customers.import_rows r WHERE r.tenant_id=w.tenant_id
                AND r.import_id=w.import_id AND r.status IN(1,5)))
            UNION
            SELECT DISTINCT s.tenant_id FROM customers.import_source_objects s
            WHERE s.state <> 6
              AND ((s.staging_expires_at IS NOT NULL AND s.staging_expires_at <= clock_timestamp())
                OR (s.retention = 1 AND s.expires_at IS NOT NULL AND s.expires_at <= clock_timestamp()))
              AND (s.state <> 7 OR s.retirement_lease_expires_at <= clock_timestamp())
          ) candidate
          WHERE after_tenant_id IS NULL OR candidate.tenant_id > after_tenant_id
          ORDER BY candidate.tenant_id LIMIT page_size;
        END $discover$;
        REVOKE ALL ON FUNCTION customers.discover_runnable_import_tenants(uuid,integer) FROM PUBLIC;
        """;

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Contraction must see all tenants and cannot discard a marker or caller provenance.
            ALTER TABLE customers.object_storage_reservations DISABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.import_source_objects DISABLE ROW LEVEL SECURITY;
            DO $guard$ BEGIN
              IF EXISTS (SELECT 1 FROM customers.object_storage_reservations WHERE account_id IS NOT NULL OR upload_generation > 0)
                OR EXISTS (SELECT 1 FROM customers.import_source_objects WHERE retirement_fenced OR staging_expires_at IS NOT NULL) THEN
                RAISE EXCEPTION 'Import upload recovery downgrade refuses to discard provenance, pending staging or retirement fences' USING ERRCODE='55000';
              END IF;
            END $guard$;
            DROP INDEX customers.ux_customer_object_storage_reservation_caller_key;
            DROP INDEX customers.ux_customer_object_storage_reservation_legacy_key;
            ALTER TABLE customers.object_storage_reservations
              ADD CONSTRAINT ux_customer_object_storage_reservation_key UNIQUE(tenant_id, provider_scope, idempotency_key),
              DROP COLUMN account_id, DROP COLUMN import_id, DROP COLUMN upload_generation,
              DROP COLUMN upload_lease_id, DROP COLUMN upload_lease_expires_at;
            DROP INDEX customers.ix_customer_import_source_staging_expiry;
            ALTER TABLE customers.import_source_objects DROP COLUMN staging_expires_at, DROP COLUMN retirement_fenced;
            ALTER TABLE customers.object_storage_reservations ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.import_source_objects ENABLE ROW LEVEL SECURITY;
            """);
        // The preceding migration owns the exact former discovery contract.
        var previous = new CustomerImportRawSourceLifecycle();
        foreach (var operation in previous.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>())
        {
            var start = operation.Sql.IndexOf("CREATE OR REPLACE FUNCTION customers.discover_runnable_import_tenants", StringComparison.Ordinal);
            if (start >= 0) migrationBuilder.Sql(operation.Sql[start..]);
        }
    }
}
