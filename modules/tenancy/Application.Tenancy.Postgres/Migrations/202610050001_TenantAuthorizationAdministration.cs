using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Tenancy.Postgres.Migrations;

[DbContext(typeof(TenancyDbContext))]
[Migration("202610050001_TenantAuthorizationAdministration")]
public partial class TenantAuthorizationAdministration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE tenancy.tenant_authorization_state (
                tenant_id uuid PRIMARY KEY REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
                revision integer NOT NULL DEFAULT 1 CHECK (revision >= 1),
                updated_at timestamptz NOT NULL
            );

            CREATE TABLE tenancy.tenant_authorization_proposals (
                proposal_id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
                requested_by_account_id uuid NOT NULL,
                idempotency_key varchar(200) NOT NULL,
                request_fingerprint varchar(64) NOT NULL CHECK (request_fingerprint ~ '^[0-9A-F]{64}$'),
                kind smallint NOT NULL CHECK (kind BETWEEN 1 AND 7),
                status smallint NOT NULL CHECK (status BETWEEN 1 AND 4),
                expected_authorization_revision integer NOT NULL CHECK (expected_authorization_revision >= 1),
                applied_authorization_revision integer NULL CHECK (applied_authorization_revision IS NULL OR applied_authorization_revision >= 2),
                target_account_id uuid NULL,
                permission_id varchar(120) NULL,
                role_id uuid NULL,
                expected_role_revision integer NULL CHECK (expected_role_revision IS NULL OR expected_role_revision >= 1),
                role_name varchar(100) NULL,
                requested_permissions text[] NOT NULL DEFAULT ARRAY[]::text[],
                applied_permissions text[] NOT NULL DEFAULT ARRAY[]::text[],
                attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0 AND attempt_count <= 20),
                failure_code varchar(120) NULL,
                requested_at timestamptz NOT NULL,
                updated_at timestamptz NOT NULL,
                UNIQUE (tenant_id, requested_by_account_id, idempotency_key)
            );
            CREATE INDEX ix_tenant_authorization_proposals_tenant_status
                ON tenancy.tenant_authorization_proposals(tenant_id, status, requested_at, proposal_id);
            CREATE UNIQUE INDEX ux_tenant_authorization_one_active_reconciliation
                ON tenancy.tenant_authorization_proposals(tenant_id)
                WHERE status IN (1,4);

            CREATE TABLE tenancy.tenant_permission_grants (
                tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
                account_id uuid NOT NULL,
                permission_id varchar(120) NOT NULL,
                is_active boolean NOT NULL,
                revision integer NOT NULL CHECK (revision >= 1),
                granted_at timestamptz NOT NULL,
                revoked_at timestamptz NULL,
                PRIMARY KEY (tenant_id, account_id, permission_id),
                CHECK ((is_active AND revoked_at IS NULL) OR (NOT is_active AND revoked_at IS NOT NULL))
            );

            CREATE TABLE tenancy.custom_roles (
                tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
                role_id uuid NOT NULL,
                name varchar(100) NOT NULL CHECK (btrim(name) <> ''),
                availability smallint NOT NULL CHECK (availability IN (2,5)),
                revision integer NOT NULL CHECK (revision >= 1),
                permission_ids text[] NOT NULL,
                created_at timestamptz NOT NULL,
                updated_at timestamptz NOT NULL,
                retired_at timestamptz NULL,
                PRIMARY KEY (tenant_id, role_id),
                CHECK ((availability = 2 AND retired_at IS NULL) OR (availability = 5 AND retired_at IS NOT NULL)),
                CHECK (cardinality(permission_ids) BETWEEN 1 AND 32)
            );
            CREATE UNIQUE INDEX ux_custom_roles_active_name
                ON tenancy.custom_roles(tenant_id, lower(name)) WHERE availability = 2;

            CREATE TABLE tenancy.custom_role_assignments (
                tenant_id uuid NOT NULL,
                role_id uuid NOT NULL,
                account_id uuid NOT NULL,
                availability smallint NOT NULL CHECK (availability IN (1,2)),
                revision integer NOT NULL CHECK (revision >= 1),
                assigned_at timestamptz NOT NULL,
                removed_at timestamptz NULL,
                PRIMARY KEY (tenant_id, role_id, account_id),
                FOREIGN KEY (tenant_id, role_id) REFERENCES tenancy.custom_roles(tenant_id, role_id) ON DELETE RESTRICT,
                CHECK ((availability = 1 AND removed_at IS NULL) OR (availability = 2 AND removed_at IS NOT NULL))
            );

            CREATE TABLE tenancy.tenant_authorization_events (
                event_id uuid PRIMARY KEY,
                proposal_id uuid NOT NULL REFERENCES tenancy.tenant_authorization_proposals(proposal_id) ON DELETE RESTRICT,
                tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
                actor_account_id uuid NOT NULL,
                status smallint NOT NULL CHECK (status BETWEEN 1 AND 4),
                reason varchar(120) NOT NULL CHECK (btrim(reason) <> ''),
                authorization_revision integer NOT NULL CHECK (authorization_revision >= 1),
                occurred_at timestamptz NOT NULL
            );
            CREATE INDEX ix_tenant_authorization_events_tenant
                ON tenancy.tenant_authorization_events(tenant_id, occurred_at, event_id);

            CREATE TABLE tenancy.owner_transfer_receipts (
                tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id) ON DELETE RESTRICT,
                requested_by_account_id uuid NOT NULL,
                idempotency_key varchar(200) NOT NULL,
                request_fingerprint varchar(64) NOT NULL CHECK (request_fingerprint ~ '^[0-9A-F]{64}$'),
                previous_owner_account_id uuid NOT NULL,
                current_owner_account_id uuid NOT NULL,
                result_tenant_revision integer NOT NULL CHECK (result_tenant_revision >= 2),
                result_authorization_revision integer NOT NULL CHECK (result_authorization_revision >= 2),
                occurred_at timestamptz NOT NULL,
                PRIMARY KEY (tenant_id, requested_by_account_id, idempotency_key)
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE tenancy.tenant_authorization_state,
                       tenancy.tenant_authorization_proposals,
                       tenancy.tenant_permission_grants,
                       tenancy.custom_roles,
                       tenancy.custom_role_assignments,
                       tenancy.tenant_authorization_events,
                       tenancy.owner_transfer_receipts IN ACCESS EXCLUSIVE MODE;
            DO $rollback$
            BEGIN
                IF EXISTS (SELECT 1 FROM tenancy.tenant_authorization_proposals)
                   OR EXISTS (SELECT 1 FROM tenancy.tenant_permission_grants)
                   OR EXISTS (SELECT 1 FROM tenancy.custom_roles)
                   OR EXISTS (SELECT 1 FROM tenancy.custom_role_assignments)
                   OR EXISTS (SELECT 1 FROM tenancy.tenant_authorization_events)
                   OR EXISTS (SELECT 1 FROM tenancy.owner_transfer_receipts) THEN
                    RAISE EXCEPTION 'Cannot roll back tenant authorization administration while durable evidence exists';
                END IF;
            END
            $rollback$;
            DROP TABLE tenancy.owner_transfer_receipts;
            DROP TABLE tenancy.tenant_authorization_events;
            DROP TABLE tenancy.custom_role_assignments;
            DROP TABLE tenancy.custom_roles;
            DROP TABLE tenancy.tenant_permission_grants;
            DROP TABLE tenancy.tenant_authorization_proposals;
            DROP TABLE tenancy.tenant_authorization_state;
            """);
    }
}
