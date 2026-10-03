using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.PlatformAdministration.Postgres.Migrations;

public partial class InitialPlatformAdministration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "platform_administration");

        migrationBuilder.CreateTable(
            name: "principals",
            schema: "platform_administration",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                issuer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                availability = table.Column<short>(type: "smallint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_platform_principals", row => row.id);
                table.CheckConstraint("ck_platform_principals_availability", "availability IN (1, 2)");
                table.CheckConstraint("ck_platform_principals_issuer_not_blank", "btrim(issuer) <> ''");
                table.CheckConstraint("ck_platform_principals_subject_not_blank", "btrim(subject) <> ''");
            });

        migrationBuilder.CreateTable(
            name: "admin_devices",
            schema: "platform_administration",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                certificate_fingerprint = table.Column<string>(
                    type: "character varying(64)", maxLength: 64, nullable: false),
                display_name = table.Column<string>(
                    type: "character varying(200)", maxLength: 200, nullable: false),
                availability = table.Column<short>(type: "smallint", nullable: false),
                registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_admin_devices", row => row.id);
                table.CheckConstraint("ck_admin_devices_availability", "availability IN (1, 2)");
                table.CheckConstraint(
                    "ck_admin_devices_certificate_fingerprint",
                    "certificate_fingerprint ~ '^[0-9A-F]{64}$'");
                table.CheckConstraint("ck_admin_devices_display_name_not_blank", "btrim(display_name) <> ''");
                table.ForeignKey(
                    name: "fk_admin_devices_platform_principals_principal_id",
                    column: row => row.principal_id,
                    principalSchema: "platform_administration",
                    principalTable: "principals",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "bootstrap_state",
            schema: "platform_administration",
            columns: table => new
            {
                bootstrap_id = table.Column<Guid>(type: "uuid", nullable: false),
                singleton_key = table.Column<short>(type: "smallint", nullable: false),
                principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                idempotency_key = table.Column<string>(
                    type: "character varying(200)", maxLength: 200, nullable: false),
                intent_fingerprint = table.Column<string>(
                    type: "character varying(64)", maxLength: 64, nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                prepared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_platform_admin_bootstrap", row => row.bootstrap_id);
                table.CheckConstraint("ck_platform_admin_bootstrap_singleton", "singleton_key = 1");
                table.CheckConstraint("ck_platform_admin_bootstrap_status", "status IN (1, 2)");
                table.CheckConstraint("ck_platform_admin_bootstrap_idempotency_key", "btrim(idempotency_key) <> ''");
                table.CheckConstraint(
                    "ck_platform_admin_bootstrap_intent_fingerprint",
                    "intent_fingerprint ~ '^[0-9A-F]{64}$'");
                table.ForeignKey(
                    name: "fk_platform_admin_bootstrap_device_id",
                    column: row => row.device_id,
                    principalSchema: "platform_administration",
                    principalTable: "admin_devices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_platform_admin_bootstrap_principal_id",
                    column: row => row.principal_id,
                    principalSchema: "platform_administration",
                    principalTable: "principals",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "audit_events",
            schema: "platform_administration",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                bootstrap_id = table.Column<Guid>(type: "uuid", nullable: false),
                principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_kind = table.Column<short>(type: "smallint", nullable: false),
                outcome = table.Column<short>(type: "smallint", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_platform_admin_audit_events", row => row.id);
                table.CheckConstraint("ck_platform_admin_audit_event_kind", "event_kind IN (1, 2)");
                table.CheckConstraint("ck_platform_admin_audit_outcome", "outcome IN (1, 2)");
                table.ForeignKey(
                    name: "fk_platform_admin_audit_bootstrap_id",
                    column: row => row.bootstrap_id,
                    principalSchema: "platform_administration",
                    principalTable: "bootstrap_state",
                    principalColumn: "bootstrap_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_platform_principals_external_identity",
            schema: "platform_administration",
            table: "principals",
            columns: new[] { "issuer", "subject" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ux_admin_devices_certificate_fingerprint",
            schema: "platform_administration",
            table: "admin_devices",
            column: "certificate_fingerprint",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_admin_devices_principal_id",
            schema: "platform_administration",
            table: "admin_devices",
            column: "principal_id");
        migrationBuilder.CreateIndex(
            name: "ux_platform_admin_bootstrap_singleton",
            schema: "platform_administration",
            table: "bootstrap_state",
            column: "singleton_key",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_platform_admin_bootstrap_device_id",
            schema: "platform_administration",
            table: "bootstrap_state",
            column: "device_id");
        migrationBuilder.CreateIndex(
            name: "ix_platform_admin_bootstrap_principal_id",
            schema: "platform_administration",
            table: "bootstrap_state",
            column: "principal_id");
        migrationBuilder.CreateIndex(
            name: "ix_platform_admin_audit_bootstrap_occurred_at",
            schema: "platform_administration",
            table: "audit_events",
            columns: new[] { "bootstrap_id", "occurred_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "audit_events", schema: "platform_administration");
        migrationBuilder.DropTable(name: "bootstrap_state", schema: "platform_administration");
        migrationBuilder.DropTable(name: "admin_devices", schema: "platform_administration");
        migrationBuilder.DropTable(name: "principals", schema: "platform_administration");
    }
}
