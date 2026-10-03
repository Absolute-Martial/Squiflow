using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.IdentityAccess.Postgres.Migrations;

public partial class AccountOnboarding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE identity_access.account_onboarding_receipts (
                provisioned_by_principal_id uuid NOT NULL,
                idempotency_key varchar(200) NOT NULL,
                request_fingerprint varchar(64) NOT NULL,
                account_id uuid NOT NULL,
                issuer varchar(255) NOT NULL,
                subject varchar(255) NOT NULL,
                provisioned_by_device_id uuid NOT NULL,
                created_at timestamptz NOT NULL,
                CONSTRAINT pk_account_onboarding_receipts PRIMARY KEY (provisioned_by_principal_id, idempotency_key),
                CONSTRAINT fk_account_onboarding_receipts_account_id FOREIGN KEY (account_id)
                    REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
                CONSTRAINT ck_account_onboarding_receipts_key_not_blank CHECK (btrim(idempotency_key) <> ''),
                CONSTRAINT ck_account_onboarding_receipts_fingerprint CHECK (request_fingerprint ~ '^[0-9A-F]{64}$'),
                CONSTRAINT ck_account_onboarding_receipts_issuer_not_blank CHECK (btrim(issuer) <> ''),
                CONSTRAINT ck_account_onboarding_receipts_subject_not_blank CHECK (btrim(subject) <> '')
            );
            CREATE UNIQUE INDEX ux_account_onboarding_receipts_account_id
                ON identity_access.account_onboarding_receipts(account_id);

            CREATE TABLE identity_access.identity_link_receipts (
                linked_by_principal_id uuid NOT NULL,
                idempotency_key varchar(200) NOT NULL,
                request_fingerprint varchar(64) NOT NULL,
                account_id uuid NOT NULL,
                issuer varchar(255) NOT NULL,
                subject varchar(255) NOT NULL,
                linked_by_device_id uuid NOT NULL,
                linked_at timestamptz NOT NULL,
                CONSTRAINT pk_identity_link_receipts PRIMARY KEY (linked_by_principal_id, idempotency_key),
                CONSTRAINT fk_identity_link_receipts_account_id FOREIGN KEY (account_id)
                    REFERENCES identity_access.accounts(id) ON DELETE RESTRICT,
                CONSTRAINT ck_identity_link_receipts_key_not_blank CHECK (btrim(idempotency_key) <> ''),
                CONSTRAINT ck_identity_link_receipts_fingerprint CHECK (request_fingerprint ~ '^[0-9A-F]{64}$'),
                CONSTRAINT ck_identity_link_receipts_issuer_not_blank CHECK (btrim(issuer) <> ''),
                CONSTRAINT ck_identity_link_receipts_subject_not_blank CHECK (btrim(subject) <> '')
            );
            CREATE INDEX ix_identity_link_receipts_account_id
                ON identity_access.identity_link_receipts(account_id);

            CREATE FUNCTION identity_access.lock_account_for_identity_link(p_account_id uuid)
            RETURNS smallint
            LANGUAGE sql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $lock_account$
                SELECT availability
                FROM identity_access.accounts
                WHERE id = p_account_id
                FOR UPDATE
            $lock_account$;
            REVOKE ALL ON FUNCTION identity_access.lock_account_for_identity_link(uuid) FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE identity_access.accounts,
                       identity_access.account_onboarding_receipts,
                       identity_access.identity_link_receipts IN ACCESS EXCLUSIVE MODE;
            DO $identity_onboarding_rollback$
            BEGIN
                IF EXISTS (SELECT 1 FROM identity_access.account_onboarding_receipts)
                   OR EXISTS (SELECT 1 FROM identity_access.identity_link_receipts) THEN
                    RAISE EXCEPTION 'Cannot roll back identity onboarding while receipts exist';
                END IF;
            END
            $identity_onboarding_rollback$;
            DROP FUNCTION identity_access.lock_account_for_identity_link(uuid);
            DROP TABLE identity_access.identity_link_receipts;
            DROP TABLE identity_access.account_onboarding_receipts;
            """);
    }
}
