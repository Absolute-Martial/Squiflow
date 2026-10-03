using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Tenancy.Postgres.Migrations;

[DbContext(typeof(TenancyDbContext))]
[Migration("202610030004_MembershipLifecycle")]
public partial class MembershipLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenancy.memberships DROP CONSTRAINT ck_memberships_availability;
            ALTER TABLE tenancy.memberships
                ADD COLUMN revision integer NOT NULL DEFAULT 1,
                ADD COLUMN activated_at timestamptz NULL,
                ADD COLUMN removed_at timestamptz NULL;
            UPDATE tenancy.memberships SET activated_at = created_at WHERE availability IN (1, 2);
            ALTER TABLE tenancy.memberships ALTER COLUMN revision DROP DEFAULT;
            ALTER TABLE tenancy.memberships
                ADD CONSTRAINT ck_memberships_availability CHECK (availability IN (1, 2, 3, 4)),
                ADD CONSTRAINT ck_memberships_revision CHECK (revision >= 1),
                ADD CONSTRAINT ck_memberships_lifecycle CHECK (
                    (availability = 1 AND activated_at IS NOT NULL AND suspended_at IS NULL AND removed_at IS NULL) OR
                    (availability = 2 AND activated_at IS NOT NULL AND suspended_at IS NOT NULL AND removed_at IS NULL) OR
                    (availability = 3 AND activated_at IS NULL AND suspended_at IS NULL AND removed_at IS NULL) OR
                    (availability = 4 AND removed_at IS NOT NULL));

            CREATE TABLE tenancy.membership_lifecycle_receipts (
                changed_by_principal_id uuid NOT NULL,
                idempotency_key varchar(200) NOT NULL,
                request_fingerprint varchar(64) NOT NULL,
                operation smallint NOT NULL,
                tenant_id uuid NOT NULL,
                account_id uuid NOT NULL,
                result_status smallint NOT NULL,
                result_availability smallint NULL,
                result_revision integer NULL,
                invited_at timestamptz NULL,
                activated_at timestamptz NULL,
                suspended_at timestamptz NULL,
                removed_at timestamptz NULL,
                changed_by_device_id uuid NOT NULL,
                occurred_at timestamptz NOT NULL,
                CONSTRAINT pk_membership_lifecycle_receipts PRIMARY KEY (changed_by_principal_id, idempotency_key),
                CONSTRAINT ck_membership_lifecycle_receipts_key_not_blank CHECK (btrim(idempotency_key) <> ''),
                CONSTRAINT ck_membership_lifecycle_receipts_fingerprint CHECK (request_fingerprint ~ '^[0-9A-F]{64}$'),
                CONSTRAINT ck_membership_lifecycle_receipts_operation CHECK (operation IN (1,2,3,4)),
                CONSTRAINT ck_membership_lifecycle_receipts_result CHECK (
                    result_status = operation AND result_availability IS NOT NULL AND result_revision IS NOT NULL AND result_revision >= 1 AND invited_at IS NOT NULL AND ((operation = 1 AND result_availability = 3) OR (operation = 2 AND result_availability = 1) OR (operation = 3 AND result_availability = 2) OR (operation = 4 AND result_availability = 4))),
                CONSTRAINT fk_membership_lifecycle_receipts_tenant_id FOREIGN KEY (tenant_id)
                    REFERENCES tenancy.tenants(id) ON DELETE RESTRICT
            );
            CREATE INDEX ix_membership_lifecycle_receipts_membership
                ON tenancy.membership_lifecycle_receipts(tenant_id, account_id, occurred_at);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE tenancy.memberships, tenancy.membership_lifecycle_receipts IN ACCESS EXCLUSIVE MODE;
            DO $membership_lifecycle_rollback$
            BEGIN
                IF EXISTS (SELECT 1 FROM tenancy.membership_lifecycle_receipts)
                   OR EXISTS (SELECT 1 FROM tenancy.memberships WHERE availability IN (3, 4)) THEN
                    RAISE EXCEPTION 'Cannot roll back membership lifecycle while lifecycle evidence exists';
                END IF;
            END
            $membership_lifecycle_rollback$;
            DROP TABLE tenancy.membership_lifecycle_receipts;
            ALTER TABLE tenancy.memberships DROP CONSTRAINT ck_memberships_lifecycle;
            ALTER TABLE tenancy.memberships DROP CONSTRAINT ck_memberships_revision;
            ALTER TABLE tenancy.memberships DROP CONSTRAINT ck_memberships_availability;
            ALTER TABLE tenancy.memberships DROP COLUMN removed_at;
            ALTER TABLE tenancy.memberships DROP COLUMN activated_at;
            ALTER TABLE tenancy.memberships DROP COLUMN revision;
            ALTER TABLE tenancy.memberships ADD CONSTRAINT ck_memberships_availability CHECK (availability IN (1, 2));
            """);
    }
}
