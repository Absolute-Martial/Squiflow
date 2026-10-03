using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Tenancy.Postgres.Migrations;

[DbContext(typeof(TenancyDbContext))]
[Migration("202610030005_TenantAndInitialOwnerLifecycle")]
public partial class TenantAndInitialOwnerLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenancy.tenants
                ADD COLUMN revision integer NOT NULL DEFAULT 1,
                ADD CONSTRAINT ck_tenants_revision CHECK (revision >= 1);

            ALTER TABLE tenancy.memberships
                ADD COLUMN is_initial_owner boolean NOT NULL DEFAULT false;
            CREATE UNIQUE INDEX ux_memberships_initial_owner
                ON tenancy.memberships(tenant_id) WHERE is_initial_owner;

            ALTER TABLE tenancy.membership_lifecycle_receipts
                ADD COLUMN is_initial_owner boolean NOT NULL DEFAULT false;
            ALTER TABLE tenancy.membership_lifecycle_receipts
                DROP CONSTRAINT ck_membership_lifecycle_receipts_operation,
                DROP CONSTRAINT ck_membership_lifecycle_receipts_result,
                ADD CONSTRAINT ck_membership_lifecycle_receipts_operation
                    CHECK (operation IN (1,2,3,4,5)),
                ADD CONSTRAINT ck_membership_lifecycle_receipts_result CHECK (
                    result_availability IS NOT NULL
                    AND result_revision IS NOT NULL
                    AND result_revision >= 1
                    AND invited_at IS NOT NULL
                    AND (
                        (operation = 1 AND result_status = 1 AND result_availability = 3 AND NOT is_initial_owner) OR
                        (operation = 2 AND result_status = 2 AND result_availability = 1) OR
                        (operation = 3 AND result_status = 3 AND result_availability = 2) OR
                        (operation = 4 AND result_status = 4 AND result_availability = 4) OR
                        (operation = 5 AND result_status = 14 AND result_availability = 1 AND is_initial_owner)
                    ));

            CREATE TABLE tenancy.tenant_lifecycle_receipts (
                changed_by_principal_id uuid NOT NULL,
                idempotency_key varchar(200) NOT NULL,
                request_fingerprint varchar(64) NOT NULL,
                operation smallint NOT NULL,
                tenant_id uuid NOT NULL,
                result_availability smallint NOT NULL,
                result_revision integer NOT NULL,
                suspended_at timestamptz NULL,
                changed_by_device_id uuid NOT NULL,
                occurred_at timestamptz NOT NULL,
                CONSTRAINT pk_tenant_lifecycle_receipts
                    PRIMARY KEY (changed_by_principal_id, idempotency_key),
                CONSTRAINT fk_tenant_lifecycle_receipts_tenant_id FOREIGN KEY (tenant_id)
                    REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
                CONSTRAINT ck_tenant_lifecycle_receipts_key_not_blank CHECK (btrim(idempotency_key) <> ''),
                CONSTRAINT ck_tenant_lifecycle_receipts_fingerprint CHECK (request_fingerprint ~ '^[0-9A-F]{64}$'),
                CONSTRAINT ck_tenant_lifecycle_receipts_operation CHECK (operation IN (1,2)),
                CONSTRAINT ck_tenant_lifecycle_receipts_result CHECK (
                    result_revision >= 1 AND
                    ((operation = 1 AND result_availability = 2 AND suspended_at IS NOT NULL) OR
                     (operation = 2 AND result_availability = 1 AND suspended_at IS NULL)))
            );
            CREATE INDEX ix_tenant_lifecycle_receipts_tenant
                ON tenancy.tenant_lifecycle_receipts(tenant_id, occurred_at);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE tenancy.tenants, tenancy.memberships,
                tenancy.membership_lifecycle_receipts,
                tenancy.tenant_lifecycle_receipts IN ACCESS EXCLUSIVE MODE;
            DO $tenant_lifecycle_rollback$
            BEGIN
                IF EXISTS (SELECT 1 FROM tenancy.tenant_lifecycle_receipts)
                   OR EXISTS (SELECT 1 FROM tenancy.memberships WHERE is_initial_owner) THEN
                    RAISE EXCEPTION 'Cannot roll back tenant lifecycle while lifecycle evidence exists';
                END IF;
            END
            $tenant_lifecycle_rollback$;
            DROP TABLE tenancy.tenant_lifecycle_receipts;
            DROP INDEX tenancy.ux_memberships_initial_owner;
            ALTER TABLE tenancy.membership_lifecycle_receipts
                DROP CONSTRAINT ck_membership_lifecycle_receipts_operation,
                DROP CONSTRAINT ck_membership_lifecycle_receipts_result,
                DROP COLUMN is_initial_owner,
                ADD CONSTRAINT ck_membership_lifecycle_receipts_operation
                    CHECK (operation IN (1,2,3,4)),
                ADD CONSTRAINT ck_membership_lifecycle_receipts_result CHECK (
                    result_status = operation AND result_availability IS NOT NULL
                    AND result_revision IS NOT NULL AND result_revision >= 1
                    AND invited_at IS NOT NULL AND
                    ((operation = 1 AND result_availability = 3) OR
                     (operation = 2 AND result_availability = 1) OR
                     (operation = 3 AND result_availability = 2) OR
                     (operation = 4 AND result_availability = 4)));
            ALTER TABLE tenancy.memberships DROP COLUMN is_initial_owner;
            ALTER TABLE tenancy.tenants
                DROP CONSTRAINT ck_tenants_revision,
                DROP COLUMN revision;
            """);
    }
}
