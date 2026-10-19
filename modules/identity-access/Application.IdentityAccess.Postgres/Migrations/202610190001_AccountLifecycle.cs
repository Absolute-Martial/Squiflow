using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.IdentityAccess.Postgres.Migrations;

[DbContext(typeof(IdentityAccessDbContext))]
[Migration("202610190001_AccountLifecycle")]
public sealed class AccountLifecycle : Migration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => IdentityAccessModel.BuildLifecycle(modelBuilder);

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE identity_access.accounts ADD COLUMN revision integer NOT NULL DEFAULT 1;
            ALTER TABLE identity_access.accounts ADD CONSTRAINT ck_accounts_revision CHECK (revision >= 1);
            CREATE TABLE identity_access.account_lifecycle_receipts (
                changed_by_principal_id uuid NOT NULL CHECK (changed_by_principal_id <> '00000000-0000-0000-0000-000000000000'),
                idempotency_key varchar(200) NOT NULL CHECK (btrim(idempotency_key) <> ''),
                request_fingerprint varchar(64) NOT NULL CHECK (request_fingerprint ~ '^[0-9A-F]{64}$'),
                account_id uuid NOT NULL REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
                availability smallint NOT NULL CHECK (availability IN (1, 2)),
                revision integer NOT NULL CHECK (revision >= 2),
                disabled_at timestamptz,
                changed_by_device_id uuid NOT NULL CHECK (changed_by_device_id <> '00000000-0000-0000-0000-000000000000'),
                reason varchar(500) NOT NULL CHECK (btrim(reason) <> ''),
                changed_at timestamptz NOT NULL,
                CONSTRAINT pk_account_lifecycle_receipts PRIMARY KEY (changed_by_principal_id, idempotency_key),
                CONSTRAINT ck_account_lifecycle_disabled_at CHECK ((availability = 2) = (disabled_at IS NOT NULL)),
                CONSTRAINT ux_account_lifecycle_revision UNIQUE (account_id, revision)
            );
            REVOKE ALL ON TABLE identity_access.account_lifecycle_receipts FROM PUBLIC;
            CREATE FUNCTION identity_access.transition_account_availability(
                p_account_id uuid, p_expected_revision integer, p_availability smallint, p_changed_at timestamptz)
            RETURNS integer LANGUAGE sql SECURITY DEFINER SET search_path = pg_catalog
            AS $transition$
                UPDATE identity_access.accounts
                SET availability = p_availability,
                    revision = revision + 1,
                    disabled_at = CASE WHEN p_availability = 2 THEN p_changed_at ELSE NULL END
                WHERE id = p_account_id AND revision = p_expected_revision
                  AND p_expected_revision BETWEEN 1 AND 2147483646
                  AND p_availability IN (1, 2) AND availability <> p_availability
                  AND p_changed_at IS NOT NULL
                RETURNING revision
            $transition$;
            REVOKE ALL ON FUNCTION identity_access.transition_account_availability(uuid, integer, smallint, timestamptz) FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE identity_access.accounts, identity_access.account_lifecycle_receipts IN ACCESS EXCLUSIVE MODE;
            DO $rollback$
            BEGIN
                IF EXISTS (SELECT 1 FROM identity_access.account_lifecycle_receipts)
                   OR EXISTS (SELECT 1 FROM identity_access.accounts WHERE revision <> 1) THEN
                    RAISE EXCEPTION 'Cannot roll back account lifecycle while retained transitions exist';
                END IF;
            END
            $rollback$;
            DROP FUNCTION identity_access.transition_account_availability(uuid, integer, smallint, timestamptz);
            DROP TABLE identity_access.account_lifecycle_receipts;
            ALTER TABLE identity_access.accounts DROP CONSTRAINT ck_accounts_revision;
            ALTER TABLE identity_access.accounts DROP COLUMN revision;
            """);
    }
}
