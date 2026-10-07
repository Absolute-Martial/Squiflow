using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Customers.Postgres.Migrations;

/// <inheritdoc />
public partial class CustomerContactsAndRepresentatives : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_customer_individual_receipts_operation",
            schema: "customers",
            table: "individual_command_receipts");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "contact_changed_at",
            schema: "customers",
            table: "individuals",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "contact_changed_by_account_id",
            schema: "customers",
            table: "individuals",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "contact_changed_at",
            schema: "customers",
            table: "individual_command_receipts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "contact_changed_by_account_id",
            schema: "customers",
            table: "individual_command_receipts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "representatives",
            schema: "customers",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                program_id = table.Column<Guid>(type: "uuid", nullable: true),
                individual_id = table.Column<Guid>(type: "uuid", nullable: false),
                availability = table.Column<int>(type: "integer", nullable: false),
                revision = table.Column<long>(type: "bigint", nullable: false),
                created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                changed_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_customer_representatives", x => new { x.tenant_id, x.id });
                table.CheckConstraint("ck_customer_representatives_availability", "availability IN (1, 2)");
                table.CheckConstraint("ck_customer_representatives_change_pair", "(changed_by_account_id IS NULL) = (changed_at IS NULL)");
                table.CheckConstraint("ck_customer_representatives_revision", "revision > 0");
                table.ForeignKey(
                    name: "fk_customer_representatives_individual",
                    columns: x => new { x.tenant_id, x.individual_id },
                    principalSchema: "customers",
                    principalTable: "individuals",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_customer_representatives_organization",
                    columns: x => new { x.tenant_id, x.organization_id },
                    principalSchema: "customers",
                    principalTable: "organizations",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_customer_representatives_program",
                    columns: x => new { x.tenant_id, x.program_id },
                    principalSchema: "customers",
                    principalTable: "programs",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "representatives_changed_by_account_id_fkey",
                    column: x => x.changed_by_account_id,
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "representatives_created_by_account_id_fkey",
                    column: x => x.created_by_account_id,
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "representative_command_receipts",
            schema: "customers",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                representative_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                program_id = table.Column<Guid>(type: "uuid", nullable: true),
                individual_id = table.Column<Guid>(type: "uuid", nullable: false),
                availability = table.Column<int>(type: "integer", nullable: false),
                revision = table.Column<long>(type: "bigint", nullable: false),
                created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                changed_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_customer_representative_command_receipts", x => new { x.tenant_id, x.account_id, x.operation, x.idempotency_key });
                table.CheckConstraint("ck_customer_representative_receipts_availability", "availability IN (1, 2)");
                table.CheckConstraint("ck_customer_representative_receipts_change_pair", "(changed_by_account_id IS NULL) = (changed_at IS NULL)");
                table.CheckConstraint("ck_customer_representative_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.CheckConstraint("ck_customer_representative_receipts_key", "btrim(idempotency_key) <> ''");
                table.CheckConstraint("ck_customer_representative_receipts_operation", "operation IN ('link', 'unlink')");
                table.CheckConstraint("ck_customer_representative_receipts_revision", "revision > 0");
                table.ForeignKey(
                    name: "fk_customer_representative_receipts_representative",
                    columns: x => new { x.tenant_id, x.representative_id },
                    principalSchema: "customers",
                    principalTable: "representatives",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "representative_receipts_account_id_fkey",
                    column: x => x.account_id,
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_individuals_contact_changed_by_account_id",
            schema: "customers",
            table: "individuals",
            column: "contact_changed_by_account_id");

        migrationBuilder.AddCheckConstraint(
            name: "ck_customer_individuals_contact_change_pair",
            schema: "customers",
            table: "individuals",
            sql: "(contact_changed_by_account_id IS NULL) = (contact_changed_at IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_customer_individual_receipts_contact_change_pair",
            schema: "customers",
            table: "individual_command_receipts",
            sql: "(contact_changed_by_account_id IS NULL) = (contact_changed_at IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_customer_individual_receipts_operation",
            schema: "customers",
            table: "individual_command_receipts",
            sql: "operation IN ('create', 'availability', 'contact')");

        migrationBuilder.CreateIndex(
            name: "ix_customer_representative_receipts_representative",
            schema: "customers",
            table: "representative_command_receipts",
            columns: new[] { "tenant_id", "representative_id" });

        migrationBuilder.CreateIndex(
            name: "IX_representative_command_receipts_account_id",
            schema: "customers",
            table: "representative_command_receipts",
            column: "account_id");

        migrationBuilder.CreateIndex(
            name: "IX_representatives_changed_by_account_id",
            schema: "customers",
            table: "representatives",
            column: "changed_by_account_id");

        migrationBuilder.CreateIndex(
            name: "IX_representatives_created_by_account_id",
            schema: "customers",
            table: "representatives",
            column: "created_by_account_id");

        migrationBuilder.CreateIndex(
            name: "IX_representatives_tenant_id_individual_id",
            schema: "customers",
            table: "representatives",
            columns: new[] { "tenant_id", "individual_id" });

        migrationBuilder.CreateIndex(
            name: "IX_representatives_tenant_id_program_id",
            schema: "customers",
            table: "representatives",
            columns: new[] { "tenant_id", "program_id" });

        migrationBuilder.CreateIndex(
            name: "ux_customer_representatives_active_organization",
            schema: "customers",
            table: "representatives",
            columns: new[] { "tenant_id", "organization_id", "individual_id" },
            unique: true,
            filter: "availability = 1 AND program_id IS NULL");

        migrationBuilder.CreateIndex(
            name: "ux_customer_representatives_active_program",
            schema: "customers",
            table: "representatives",
            columns: new[] { "tenant_id", "organization_id", "program_id", "individual_id" },
            unique: true,
            filter: "availability = 1 AND program_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_customer_representatives_id",
            schema: "customers",
            table: "representatives",
            column: "id",
            unique: true);

        migrationBuilder.Sql("""
            ALTER TABLE customers.representatives
                ADD CONSTRAINT fk_customer_representatives_program_parent
                FOREIGN KEY (tenant_id, organization_id, program_id)
                REFERENCES customers.programs(tenant_id, organization_id, id)
                ON DELETE RESTRICT;

            ALTER TABLE customers.representatives ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.representatives FORCE ROW LEVEL SECURITY;
            CREATE POLICY representatives_tenant_isolation ON customers.representatives
                USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

            ALTER TABLE customers.representative_command_receipts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers.representative_command_receipts FORCE ROW LEVEL SECURITY;
            CREATE POLICY representative_command_receipts_tenant_isolation
                ON customers.representative_command_receipts
                USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);
            """);

        migrationBuilder.AddForeignKey(
            name: "individuals_contact_changed_by_account_id_fkey",
            schema: "customers",
            table: "individuals",
            column: "contact_changed_by_account_id",
            principalSchema: "identity_access",
            principalTable: "accounts",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE customers.individuals, customers.individual_command_receipts,
                customers.representatives, customers.representative_command_receipts
                IN ACCESS EXCLUSIVE MODE;
            DO $customer_contacts_representatives_rollback$
            DECLARE current_tenant uuid;
            BEGIN
                FOR current_tenant IN SELECT id FROM tenancy.tenants LOOP
                    PERFORM set_config('app.current_tenant', current_tenant::text, true);
                    IF EXISTS (
                            SELECT 1 FROM customers.individuals
                            WHERE tenant_id = current_tenant
                              AND contact_changed_at IS NOT NULL)
                       OR EXISTS (
                            SELECT 1 FROM customers.individual_command_receipts
                            WHERE tenant_id = current_tenant
                              AND operation = 'contact')
                       OR EXISTS (
                            SELECT 1 FROM customers.representatives
                            WHERE tenant_id = current_tenant)
                       OR EXISTS (
                            SELECT 1 FROM customers.representative_command_receipts
                            WHERE tenant_id = current_tenant) THEN
                        RAISE EXCEPTION
                            'Cannot roll back Customers contacts/representatives while retained COM-002 history exists';
                    END IF;
                END LOOP;
            END
            $customer_contacts_representatives_rollback$;
            """);

        migrationBuilder.DropForeignKey(
            name: "individuals_contact_changed_by_account_id_fkey",
            schema: "customers",
            table: "individuals");

        migrationBuilder.DropTable(
            name: "representative_command_receipts",
            schema: "customers");

        migrationBuilder.DropTable(
            name: "representatives",
            schema: "customers");

        migrationBuilder.DropIndex(
            name: "IX_individuals_contact_changed_by_account_id",
            schema: "customers",
            table: "individuals");

        migrationBuilder.DropCheckConstraint(
            name: "ck_customer_individuals_contact_change_pair",
            schema: "customers",
            table: "individuals");

        migrationBuilder.DropCheckConstraint(
            name: "ck_customer_individual_receipts_contact_change_pair",
            schema: "customers",
            table: "individual_command_receipts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_customer_individual_receipts_operation",
            schema: "customers",
            table: "individual_command_receipts");

        migrationBuilder.DropColumn(
            name: "contact_changed_at",
            schema: "customers",
            table: "individuals");

        migrationBuilder.DropColumn(
            name: "contact_changed_by_account_id",
            schema: "customers",
            table: "individuals");

        migrationBuilder.DropColumn(
            name: "contact_changed_at",
            schema: "customers",
            table: "individual_command_receipts");

        migrationBuilder.DropColumn(
            name: "contact_changed_by_account_id",
            schema: "customers",
            table: "individual_command_receipts");

        migrationBuilder.AddCheckConstraint(
            name: "ck_customer_individual_receipts_operation",
            schema: "customers",
            table: "individual_command_receipts",
            sql: "operation IN ('create', 'availability')");
    }
}
