using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Customers.Postgres.Migrations;

public partial class CustomerIndividuals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE customers.individuals (
                tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
                id uuid NOT NULL,
                display_name varchar(200) NOT NULL,
                email varchar(254),
                phone varchar(32),
                availability integer NOT NULL,
                revision bigint NOT NULL,
                created_by_account_id uuid NOT NULL REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
                created_at timestamptz NOT NULL,
                availability_changed_by_account_id uuid REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
                availability_changed_at timestamptz,
                CONSTRAINT pk_customer_individuals PRIMARY KEY (tenant_id, id),
                CONSTRAINT ck_customer_individuals_name CHECK (btrim(display_name) <> ''),
                CONSTRAINT ck_customer_individuals_email CHECK (email IS NULL OR btrim(email) <> ''),
                CONSTRAINT ck_customer_individuals_phone CHECK (phone IS NULL OR btrim(phone) <> ''),
                CONSTRAINT ck_customer_individuals_availability CHECK (availability IN (1, 2)),
                CONSTRAINT ck_customer_individuals_revision CHECK (revision > 0),
                CONSTRAINT ck_customer_individuals_change_pair CHECK
                    ((availability_changed_by_account_id IS NULL) = (availability_changed_at IS NULL))
            );
            CREATE UNIQUE INDEX ux_customer_individuals_id ON customers.individuals(id);

            CREATE TABLE customers.individual_command_receipts (
                tenant_id uuid NOT NULL,
                account_id uuid NOT NULL REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
                operation varchar(16) NOT NULL,
                idempotency_key varchar(128) NOT NULL,
                fingerprint char(64) NOT NULL,
                individual_id uuid NOT NULL,
                display_name varchar(200) NOT NULL,
                email varchar(254),
                phone varchar(32),
                availability integer NOT NULL,
                revision bigint NOT NULL,
                created_by_account_id uuid NOT NULL,
                created_at timestamptz NOT NULL,
                availability_changed_by_account_id uuid,
                availability_changed_at timestamptz,
                CONSTRAINT pk_customer_individual_command_receipts
                    PRIMARY KEY (tenant_id, account_id, operation, idempotency_key),
                CONSTRAINT fk_customer_individual_receipts_individual FOREIGN KEY (tenant_id, individual_id)
                    REFERENCES customers.individuals(tenant_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_customer_individual_receipts_operation CHECK (operation IN ('create', 'availability')),
                CONSTRAINT ck_customer_individual_receipts_key CHECK (btrim(idempotency_key) <> ''),
                CONSTRAINT ck_customer_individual_receipts_fingerprint CHECK (fingerprint ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_customer_individual_receipts_name CHECK (btrim(display_name) <> ''),
                CONSTRAINT ck_customer_individual_receipts_availability CHECK (availability IN (1, 2)),
                CONSTRAINT ck_customer_individual_receipts_revision CHECK (revision > 0),
                CONSTRAINT ck_customer_individual_receipts_change_pair CHECK
                    ((availability_changed_by_account_id IS NULL) = (availability_changed_at IS NULL))
            );
            CREATE INDEX ix_customer_individual_receipts_individual
                ON customers.individual_command_receipts(tenant_id, individual_id);

            ALTER TABLE customers.individuals ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.individuals FORCE ROW LEVEL SECURITY;
            CREATE POLICY individuals_tenant_isolation ON customers.individuals
                USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);
            ALTER TABLE customers.individual_command_receipts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.individual_command_receipts FORCE ROW LEVEL SECURITY;
            CREATE POLICY individual_command_receipts_tenant_isolation ON customers.individual_command_receipts
                USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE customers.individuals, customers.individual_command_receipts IN ACCESS EXCLUSIVE MODE;
            DO $customer_individual_rollback$
            DECLARE current_tenant uuid;
            BEGIN
                FOR current_tenant IN SELECT id FROM tenancy.tenants LOOP
                    PERFORM set_config('app.current_tenant', current_tenant::text, true);
                    IF EXISTS (SELECT 1 FROM customers.individuals WHERE tenant_id = current_tenant)
                       OR EXISTS (SELECT 1 FROM customers.individual_command_receipts
                                  WHERE tenant_id = current_tenant) THEN
                        RAISE EXCEPTION 'Cannot roll back Customers individuals while billing records exist';
                    END IF;
                END LOOP;
            END
            $customer_individual_rollback$;
            DROP TABLE customers.individual_command_receipts;
            DROP TABLE customers.individuals;
            """);
    }
}
