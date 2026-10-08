using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Customers.Postgres.Migrations;

[Migration("20261007080000_CustomerImportAutonomousDiscovery")]
// Inherit the single DbContext attribute and previous immutable target model;
// only function/policy/index objects change.
// the retained table/column model and EF snapshot are deliberately unchanged.
public sealed class CustomerImportAutonomousDiscovery : CustomerImportFencedExecution
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Only the trusted migration/function owner receives the narrow SELECT policies.
            -- No role, BYPASSRLS attribute, tenant-table grant or runtime policy is created.
            DO $policies$ BEGIN
              EXECUTE format('CREATE POLICY import_discovery_owner ON customers.import_work FOR SELECT TO %I USING (true)', current_user);
              EXECUTE format('CREATE POLICY import_discovery_owner ON customers.import_rows FOR SELECT TO %I USING (true)', current_user);
            END $policies$;

            CREATE INDEX ix_customer_import_work_discovery ON customers.import_work(tenant_id, import_id)
              WHERE status IN (1,2,4) AND (last_error IS NULL OR last_error NOT IN ('authority_changed','legacy_work_requires_replan'));

            CREATE FUNCTION customers.discover_runnable_import_tenants(after_tenant_id uuid, page_size integer)
            RETURNS TABLE(tenant_id uuid)
            LANGUAGE plpgsql SECURITY DEFINER
            SET search_path = pg_catalog
            SET row_security = on
            AS $discover$
            BEGIN
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
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP FUNCTION customers.discover_runnable_import_tenants(uuid,integer);
            DROP INDEX customers.ix_customer_import_work_discovery;
            DROP POLICY import_discovery_owner ON customers.import_work;
            DROP POLICY import_discovery_owner ON customers.import_rows;
            """);
    }
}
