using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Customers.Postgres.Migrations;

public partial class InitialCustomers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA customers;
            REVOKE ALL ON SCHEMA customers FROM PUBLIC;

            CREATE TABLE customers.organizations (
                tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
                id uuid NOT NULL,
                created_by_account_id uuid NOT NULL REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
                display_name varchar(200) NOT NULL,
                created_at timestamptz NOT NULL,
                CONSTRAINT pk_customer_organizations PRIMARY KEY (tenant_id, id),
                CONSTRAINT ck_customer_organizations_name CHECK (btrim(display_name) <> '')
            );
            CREATE UNIQUE INDEX ux_customer_organizations_id ON customers.organizations(id);
            CREATE INDEX ix_customer_organizations_browse
                ON customers.organizations(tenant_id, created_at DESC, id DESC);

            CREATE TABLE customers.programs (
                tenant_id uuid NOT NULL,
                id uuid NOT NULL,
                organization_id uuid NOT NULL,
                created_by_account_id uuid NOT NULL REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
                display_name varchar(200) NOT NULL,
                created_at timestamptz NOT NULL,
                CONSTRAINT pk_customer_programs PRIMARY KEY (tenant_id, id),
                CONSTRAINT fk_customer_programs_organization FOREIGN KEY (tenant_id, organization_id)
                    REFERENCES customers.organizations(tenant_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_customer_programs_name CHECK (btrim(display_name) <> '')
            );
            CREATE UNIQUE INDEX ux_customer_programs_id ON customers.programs(id);
            CREATE UNIQUE INDEX ux_customer_programs_organization_identity
                ON customers.programs(tenant_id, organization_id, id);
            CREATE INDEX ix_customer_programs_browse
                ON customers.programs(tenant_id, organization_id, created_at DESC, id DESC);

            CREATE TABLE customers.organization_receipts (
                tenant_id uuid NOT NULL,
                account_id uuid NOT NULL REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
                idempotency_key varchar(128) NOT NULL,
                fingerprint char(64) NOT NULL,
                organization_id uuid NOT NULL,
                created_at timestamptz NOT NULL,
                CONSTRAINT pk_customer_organization_receipts PRIMARY KEY (tenant_id, account_id, idempotency_key),
                CONSTRAINT fk_customer_organization_receipts_organization FOREIGN KEY (tenant_id, organization_id)
                    REFERENCES customers.organizations(tenant_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_customer_organization_receipts_key CHECK (btrim(idempotency_key) <> ''),
                CONSTRAINT ck_customer_organization_receipts_fingerprint CHECK (fingerprint ~ '^[0-9a-f]{64}$')
            );
            CREATE INDEX ix_customer_organization_receipts_organization
                ON customers.organization_receipts(tenant_id, organization_id);

            CREATE TABLE customers.program_receipts (
                tenant_id uuid NOT NULL,
                account_id uuid NOT NULL REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
                idempotency_key varchar(128) NOT NULL,
                fingerprint char(64) NOT NULL,
                program_id uuid NOT NULL,
                created_at timestamptz NOT NULL,
                CONSTRAINT pk_customer_program_receipts PRIMARY KEY (tenant_id, account_id, idempotency_key),
                CONSTRAINT fk_customer_program_receipts_program FOREIGN KEY (tenant_id, program_id)
                    REFERENCES customers.programs(tenant_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_customer_program_receipts_key CHECK (btrim(idempotency_key) <> ''),
                CONSTRAINT ck_customer_program_receipts_fingerprint CHECK (fingerprint ~ '^[0-9a-f]{64}$')
            );
            CREATE INDEX ix_customer_program_receipts_program
                ON customers.program_receipts(tenant_id, program_id);

            ALTER TABLE customers.organizations ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.organizations FORCE ROW LEVEL SECURITY;
            ALTER TABLE customers.programs ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.programs FORCE ROW LEVEL SECURITY;
            ALTER TABLE customers.organization_receipts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.organization_receipts FORCE ROW LEVEL SECURITY;
            ALTER TABLE customers.program_receipts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.program_receipts FORCE ROW LEVEL SECURITY;
            """);

        foreach (var table in new[] { "organizations", "programs", "organization_receipts", "program_receipts" })
        {
            migrationBuilder.Sql($"""
                CREATE POLICY {table}_tenant_isolation ON customers.{table}
                USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE customers.program_receipts;
            DROP TABLE customers.organization_receipts;
            DROP TABLE customers.programs;
            DROP TABLE customers.organizations;
            DROP SCHEMA customers;
            """);
    }
}
