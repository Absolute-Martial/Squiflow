using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Tenancy.Postgres.Migrations;

public partial class TenantProvisioning : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE tenancy.tenant_provisioning_receipts (
                provisioned_by_principal_id uuid NOT NULL,
                idempotency_key varchar(200) NOT NULL,
                request_fingerprint varchar(64) NOT NULL,
                tenant_id uuid NOT NULL,
                display_name varchar(200) NOT NULL,
                provisioned_by_device_id uuid NOT NULL,
                activated_at timestamptz NOT NULL,
                CONSTRAINT pk_tenant_provisioning_receipts
                    PRIMARY KEY (provisioned_by_principal_id, idempotency_key),
                CONSTRAINT fk_tenant_provisioning_receipts_tenants_tenant_id FOREIGN KEY (tenant_id)
                    REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
                CONSTRAINT ck_tenant_provisioning_receipts_idempotency_key_not_blank
                    CHECK (btrim(idempotency_key) <> ''),
                CONSTRAINT ck_tenant_provisioning_receipts_request_fingerprint
                    CHECK (request_fingerprint ~ '^[0-9A-F]{64}$'),
                CONSTRAINT ck_tenant_provisioning_receipts_display_name_not_blank
                    CHECK (btrim(display_name) <> '')
            );
            CREATE UNIQUE INDEX ux_tenant_provisioning_receipts_tenant_id
                ON tenancy.tenant_provisioning_receipts(tenant_id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE tenancy.tenants, tenancy.tenant_provisioning_receipts IN ACCESS EXCLUSIVE MODE;
            DO $tenant_provisioning_rollback$
            BEGIN
                IF EXISTS (SELECT 1 FROM tenancy.tenant_provisioning_receipts) THEN
                    RAISE EXCEPTION 'Cannot roll back tenant provisioning while receipts exist';
                END IF;
            END
            $tenant_provisioning_rollback$;
            DROP TABLE tenancy.tenant_provisioning_receipts;
            """);
    }
}
