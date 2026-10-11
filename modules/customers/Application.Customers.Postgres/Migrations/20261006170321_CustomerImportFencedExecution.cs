using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Customers.Postgres.Migrations;

/// <inheritdoc />
public partial class CustomerImportFencedExecution : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "evidence", schema: "customers", table: "duplicate_command_receipts",
            type: "character varying(512)", maxLength: 512, nullable: false, defaultValue: "");
        migrationBuilder.DropIndex(
            name: "IX_import_work_tenant_id_import_id",
            schema: "customers",
            table: "import_work");

        migrationBuilder.AddColumn<long>(
            name: "authorization_revision",
            schema: "customers",
            table: "import_work",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "generation",
            schema: "customers",
            table: "import_work",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "lease_expires_at",
            schema: "customers",
            table: "import_work",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "next_attempt_at",
            schema: "customers",
            table: "import_work",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "worker_id",
            schema: "customers",
            table: "import_work",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "attempts",
            schema: "customers",
            table: "import_rows",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "decision",
            schema: "customers",
            table: "import_rows",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "duplicate_evidence",
            schema: "customers",
            table: "import_rows",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "email_signal",
            schema: "customers",
            table: "import_rows",
            type: "character varying(254)",
            maxLength: 254,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "external_id_signal",
            schema: "customers",
            table: "import_rows",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "mapping_customer_id",
            schema: "customers",
            table: "import_rows",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "name_signal",
            schema: "customers",
            table: "import_rows",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "phone_signal",
            schema: "customers",
            table: "import_rows",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "requires_decision",
            schema: "customers",
            table: "import_rows",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "row_id",
            schema: "customers",
            table: "import_rows",
            type: "uuid",
            nullable: false,
            defaultValueSql: "gen_random_uuid()");

        migrationBuilder.CreateIndex(
            name: "ux_customer_import_work_import",
            schema: "customers",
            table: "import_work",
            columns: new[] { "tenant_id", "import_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_import_rows_tenant_id_mapping_customer_id",
            schema: "customers",
            table: "import_rows",
            columns: new[] { "tenant_id", "mapping_customer_id" });

        migrationBuilder.CreateIndex(
            name: "ux_customer_import_rows_identity",
            schema: "customers",
            table: "import_rows",
            columns: new[] { "tenant_id", "row_id" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "fk_customer_import_rows_mapping",
            schema: "customers",
            table: "import_rows",
            columns: new[] { "tenant_id", "mapping_customer_id" },
            principalSchema: "customers",
            principalTable: "individuals",
            principalColumns: new[] { "tenant_id", "id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.Sql("""
                DO $legacy_import$ DECLARE tenant uuid; BEGIN
                  FOR tenant IN SELECT id FROM tenancy.tenants LOOP
                    PERFORM set_config('app.current_tenant', tenant::text, true);
                    UPDATE customers.import_work SET status=4,last_error='legacy_work_requires_replan'
                      WHERE tenant_id=tenant AND authorization_revision=0 AND status<>3;
                    UPDATE customers.import_rows SET requires_decision=true,duplicate_evidence='legacy_plan_requires_replan'
                      WHERE tenant_id=tenant AND status IN(1,5);
                    IF EXISTS(SELECT 1 FROM customers.customer_redirects r JOIN customers.customer_redirects next
                      ON next.tenant_id=r.tenant_id AND next.source_customer_id=r.canonical_customer_id WHERE r.tenant_id=tenant) THEN
                      RAISE EXCEPTION 'Existing customer redirect chains require explicit reconciliation';
                    END IF;
                  END LOOP;
                END $legacy_import$;
                ALTER TABLE customers.import_rows
                  ADD CONSTRAINT ck_import_row_decision CHECK (decision IS NULL OR decision IN (1,2,3)),
                  ADD CONSTRAINT ck_import_row_attempts CHECK (attempts BETWEEN 0 AND 3),
                  ADD CONSTRAINT ck_import_row_mapping CHECK ((decision=2) = (mapping_customer_id IS NOT NULL) OR decision IS NULL);
                ALTER TABLE customers.import_work
                  ADD CONSTRAINT ck_import_work_lease CHECK ((worker_id IS NULL) = (lease_expires_at IS NULL)),
                  ADD CONSTRAINT ck_import_work_generation CHECK (generation>=0);

                CREATE FUNCTION customers.protect_import_plan() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF (OLD.tenant_id,OLD.import_id,OLD.row_number,OLD.source_row_hash,OLD.row_id,OLD.name,
                      OLD.email,OLD.phone,OLD.external_id,OLD.customer_type,OLD.address_line1,OLD.address_line2,
                      OLD.city,OLD.notes,OLD.name_signal,OLD.email_signal,OLD.phone_signal,OLD.external_id_signal)
                     IS DISTINCT FROM
                     (NEW.tenant_id,NEW.import_id,NEW.row_number,NEW.source_row_hash,NEW.row_id,NEW.name,
                      NEW.email,NEW.phone,NEW.external_id,NEW.customer_type,NEW.address_line1,NEW.address_line2,
                      NEW.city,NEW.notes,NEW.name_signal,NEW.email_signal,NEW.phone_signal,NEW.external_id_signal)
                    OR (OLD.decision IS NOT NULL AND (OLD.decision,OLD.mapping_customer_id) IS DISTINCT FROM (NEW.decision,NEW.mapping_customer_id))
                    OR (EXISTS(SELECT 1 FROM customers.import_work WHERE tenant_id=OLD.tenant_id AND import_id=OLD.import_id)
                        AND (OLD.requires_decision,OLD.duplicate_evidence) IS DISTINCT FROM (NEW.requires_decision,NEW.duplicate_evidence))
                    OR (OLD.status IN (2,3,4) AND (OLD.status,OLD.customer_id) IS DISTINCT FROM (NEW.status,NEW.customer_id)) THEN
                    RAISE EXCEPTION 'Retained import intent or terminal result is immutable' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER protect_import_plan BEFORE UPDATE ON customers.import_rows
                  FOR EACH ROW EXECUTE FUNCTION customers.protect_import_plan();

                CREATE FUNCTION customers.protect_redirect_shape() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  PERFORM id FROM customers.individuals WHERE tenant_id=NEW.tenant_id
                    AND id IN (NEW.source_customer_id,NEW.canonical_customer_id) ORDER BY id FOR UPDATE;
                  IF EXISTS (SELECT 1 FROM customers.customer_redirects WHERE tenant_id=NEW.tenant_id
                      AND canonical_customer_id=NEW.source_customer_id)
                     OR EXISTS(SELECT 1 FROM customers.individuals WHERE tenant_id=NEW.tenant_id
                       AND id IN (NEW.source_customer_id,NEW.canonical_customer_id) AND redirect_target_individual_id IS NOT NULL) THEN
                    RAISE EXCEPTION 'Customer redirect chains are not supported' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER protect_redirect_shape BEFORE INSERT ON customers.customer_redirects
                  FOR EACH ROW EXECUTE FUNCTION customers.protect_redirect_shape();

                CREATE FUNCTION customers.protect_individual_redirect() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF OLD.redirect_target_individual_id IS NOT NULL AND NEW IS DISTINCT FROM OLD THEN
                    RAISE EXCEPTION 'Redirected customer master facts are retained' USING ERRCODE='23514';
                  END IF;
                  IF NEW.redirect_target_individual_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM customers.customer_redirects
                    WHERE tenant_id=NEW.tenant_id AND source_customer_id=NEW.id AND canonical_customer_id=NEW.redirect_target_individual_id) THEN
                    RAISE EXCEPTION 'Customer redirect requires its atomic ledger' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER protect_individual_redirect BEFORE UPDATE ON customers.individuals
                  FOR EACH ROW EXECUTE FUNCTION customers.protect_individual_redirect();

                CREATE FUNCTION customers.protect_current_representative() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW.availability=1 THEN
                    PERFORM id FROM customers.individuals WHERE tenant_id=NEW.tenant_id AND id=NEW.individual_id FOR SHARE;
                    IF EXISTS(SELECT 1 FROM customers.individuals WHERE tenant_id=NEW.tenant_id AND id=NEW.individual_id
                      AND redirect_target_individual_id IS NOT NULL) THEN
                      RAISE EXCEPTION 'Representative requires a current canonical customer' USING ERRCODE='23514';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER protect_current_representative BEFORE INSERT OR UPDATE ON customers.representatives
                  FOR EACH ROW EXECUTE FUNCTION customers.protect_current_representative();
                REVOKE ALL ON FUNCTION customers.protect_import_plan(), customers.protect_redirect_shape(),
                  customers.protect_current_representative(),customers.protect_individual_redirect() FROM PUBLIC;
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                LOCK TABLE customers.imports,customers.import_rows,customers.import_work IN ACCESS EXCLUSIVE MODE;
                DO $guard$ DECLARE tenant uuid; BEGIN
                  FOR tenant IN SELECT id FROM tenancy.tenants LOOP
                    PERFORM set_config('app.current_tenant',tenant::text,true);
                    IF EXISTS(SELECT 1 FROM customers.imports WHERE tenant_id=tenant)
                      OR EXISTS(SELECT 1 FROM customers.customer_redirects WHERE tenant_id=tenant)
                      OR EXISTS(SELECT 1 FROM customers.duplicate_command_receipts WHERE tenant_id=tenant) THEN
                      RAISE EXCEPTION 'Cannot roll back while retained Customers duplicate/import evidence exists';
                    END IF;
                  END LOOP;
                END $guard$;
                DROP TRIGGER protect_import_plan ON customers.import_rows;
                DROP TRIGGER protect_redirect_shape ON customers.customer_redirects;
                DROP TRIGGER protect_current_representative ON customers.representatives;
                DROP TRIGGER protect_individual_redirect ON customers.individuals;
                DROP FUNCTION customers.protect_import_plan(),customers.protect_redirect_shape(),customers.protect_current_representative(),customers.protect_individual_redirect();
                ALTER TABLE customers.import_rows DROP CONSTRAINT ck_import_row_decision,
                    DROP CONSTRAINT ck_import_row_attempts,DROP CONSTRAINT ck_import_row_mapping;
                ALTER TABLE customers.import_work DROP CONSTRAINT ck_import_work_lease,DROP CONSTRAINT ck_import_work_generation;
                """);
        migrationBuilder.DropColumn(name: "evidence", schema: "customers", table: "duplicate_command_receipts");
        migrationBuilder.DropForeignKey(
            name: "fk_customer_import_rows_mapping",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropIndex(
            name: "ux_customer_import_work_import",
            schema: "customers",
            table: "import_work");

        migrationBuilder.DropIndex(
            name: "IX_import_rows_tenant_id_mapping_customer_id",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropIndex(
            name: "ux_customer_import_rows_identity",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropColumn(
            name: "authorization_revision",
            schema: "customers",
            table: "import_work");

        migrationBuilder.DropColumn(
            name: "generation",
            schema: "customers",
            table: "import_work");

        migrationBuilder.DropColumn(
            name: "lease_expires_at",
            schema: "customers",
            table: "import_work");

        migrationBuilder.DropColumn(
            name: "next_attempt_at",
            schema: "customers",
            table: "import_work");

        migrationBuilder.DropColumn(
            name: "worker_id",
            schema: "customers",
            table: "import_work");

        migrationBuilder.DropColumn(
            name: "attempts",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropColumn(
            name: "decision",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropColumn(
            name: "duplicate_evidence",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropColumn(
            name: "email_signal",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropColumn(
            name: "external_id_signal",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropColumn(
            name: "mapping_customer_id",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropColumn(
            name: "name_signal",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropColumn(
            name: "phone_signal",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropColumn(
            name: "requires_decision",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.DropColumn(
            name: "row_id",
            schema: "customers",
            table: "import_rows");

        migrationBuilder.CreateIndex(
            name: "IX_import_work_tenant_id_import_id",
            schema: "customers",
            table: "import_work",
            columns: new[] { "tenant_id", "import_id" });
    }
}
