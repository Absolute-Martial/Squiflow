using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.PlatformAdministration.Postgres.Migrations;

[DbContext(typeof(PlatformAdministrationDbContext))]
[Migration("202610030001_AdminApiAccessAudit")]
public partial class AdminApiAccessAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE platform_administration.access_audit_events
            (
                id uuid NOT NULL CONSTRAINT pk_platform_admin_access_audit_events PRIMARY KEY,
                principal_id uuid NULL,
                device_id uuid NULL,
                issuer character varying(255) NOT NULL,
                subject character varying(255) NOT NULL,
                certificate_fingerprint character varying(64) NULL,
                operation character varying(100) NOT NULL,
                outcome smallint NOT NULL,
                reason character varying(100) NOT NULL,
                occurred_at timestamp with time zone NOT NULL,
                CONSTRAINT ck_platform_admin_access_audit_outcome CHECK (outcome IN (1, 2)),
                CONSTRAINT ck_platform_admin_access_audit_issuer_not_blank CHECK (btrim(issuer) <> ''),
                CONSTRAINT ck_platform_admin_access_audit_subject_not_blank CHECK (btrim(subject) <> ''),
                CONSTRAINT ck_platform_admin_access_audit_operation_not_blank CHECK (btrim(operation) <> ''),
                CONSTRAINT ck_platform_admin_access_audit_reason_not_blank CHECK (btrim(reason) <> ''),
                CONSTRAINT ck_platform_admin_access_audit_certificate_fingerprint CHECK (
                    certificate_fingerprint IS NULL OR certificate_fingerprint ~ '^[0-9A-F]{64}$'),
                CONSTRAINT ck_platform_admin_access_audit_identity_pair CHECK (
                    (principal_id IS NULL AND device_id IS NULL) OR
                    (principal_id IS NOT NULL AND device_id IS NOT NULL)),
                CONSTRAINT fk_platform_admin_access_audit_principal_id FOREIGN KEY (principal_id)
                    REFERENCES platform_administration.principals (id) ON DELETE RESTRICT,
                CONSTRAINT fk_platform_admin_access_audit_device_id FOREIGN KEY (device_id)
                    REFERENCES platform_administration.admin_devices (id) ON DELETE RESTRICT
            );

            CREATE INDEX ix_platform_admin_access_audit_occurred_at
                ON platform_administration.access_audit_events (occurred_at);
            CREATE INDEX ix_platform_admin_access_audit_principal_occurred_at
                ON platform_administration.access_audit_events (principal_id, occurred_at);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE platform_administration.access_audit_events;");
    }
}
