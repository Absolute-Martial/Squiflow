using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Application.PlatformAdministration.Postgres.Migrations;

[DbContext(typeof(PlatformAdministrationDbContext))]
partial class PlatformAdministrationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        PlatformAdministrationModel.Build(modelBuilder);
    }
}

internal static class PlatformAdministrationModel
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.8");
        modelBuilder.HasDefaultSchema("platform_administration");

        modelBuilder.Entity("Application.PlatformAdministration.Postgres.PlatformPrincipalRow", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            entity.Property<short>("Availability").HasColumnType("smallint").HasColumnName("availability");
            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("created_at");
            entity.Property<DateTimeOffset?>("DisabledAt")
                .HasColumnType("timestamp with time zone").HasColumnName("disabled_at");
            entity.Property<string>("Issuer").IsRequired().HasMaxLength(255)
                .HasColumnType("character varying(255)").HasColumnName("issuer");
            entity.Property<string>("Subject").IsRequired().HasMaxLength(255)
                .HasColumnType("character varying(255)").HasColumnName("subject");
            entity.HasKey("Id").HasName("pk_platform_principals");
            entity.HasIndex("Issuer", "Subject").IsUnique()
                .HasDatabaseName("ux_platform_principals_external_identity");
            entity.ToTable("principals", "platform_administration", table =>
            {
                table.HasCheckConstraint("ck_platform_principals_availability", "availability IN (1, 2)");
                table.HasCheckConstraint("ck_platform_principals_issuer_not_blank", "btrim(issuer) <> ''");
                table.HasCheckConstraint("ck_platform_principals_subject_not_blank", "btrim(subject) <> ''");
            });
        });

        modelBuilder.Entity("Application.PlatformAdministration.Postgres.AdminDeviceRow", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            entity.Property<short>("Availability").HasColumnType("smallint").HasColumnName("availability");
            entity.Property<string>("CertificateFingerprint").IsRequired().HasMaxLength(64)
                .HasColumnType("character varying(64)").HasColumnName("certificate_fingerprint");
            entity.Property<string>("DisplayName").IsRequired().HasMaxLength(200)
                .HasColumnType("character varying(200)").HasColumnName("display_name");
            entity.Property<Guid>("PrincipalId").HasColumnType("uuid").HasColumnName("principal_id");
            entity.Property<DateTimeOffset>("RegisteredAt")
                .HasColumnType("timestamp with time zone").HasColumnName("registered_at");
            entity.Property<DateTimeOffset?>("RevokedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("revoked_at");
            entity.HasKey("Id").HasName("pk_admin_devices");
            entity.HasIndex("CertificateFingerprint").IsUnique()
                .HasDatabaseName("ux_admin_devices_certificate_fingerprint");
            entity.HasIndex("PrincipalId");
            entity.ToTable("admin_devices", "platform_administration", table =>
            {
                table.HasCheckConstraint("ck_admin_devices_availability", "availability IN (1, 2)");
                table.HasCheckConstraint(
                    "ck_admin_devices_certificate_fingerprint",
                    "certificate_fingerprint ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("ck_admin_devices_display_name_not_blank", "btrim(display_name) <> ''");
            });
        });

        modelBuilder.Entity("Application.PlatformAdministration.Postgres.PlatformAdminBootstrapRow", entity =>
        {
            entity.Property<Guid>("BootstrapId").HasColumnType("uuid").HasColumnName("bootstrap_id");
            entity.Property<DateTimeOffset?>("CompletedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("completed_at");
            entity.Property<Guid>("DeviceId").HasColumnType("uuid").HasColumnName("device_id");
            entity.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(200)
                .HasColumnType("character varying(200)").HasColumnName("idempotency_key");
            entity.Property<string>("IntentFingerprint").IsRequired().HasMaxLength(64)
                .HasColumnType("character varying(64)").HasColumnName("intent_fingerprint");
            entity.Property<DateTimeOffset>("PreparedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("prepared_at");
            entity.Property<Guid>("PrincipalId").HasColumnType("uuid").HasColumnName("principal_id");
            entity.Property<short>("SingletonKey").HasColumnType("smallint").HasColumnName("singleton_key");
            entity.Property<short>("Status").HasColumnType("smallint").HasColumnName("status");
            entity.HasKey("BootstrapId").HasName("pk_platform_admin_bootstrap");
            entity.HasIndex("DeviceId");
            entity.HasIndex("PrincipalId");
            entity.HasIndex("SingletonKey").IsUnique()
                .HasDatabaseName("ux_platform_admin_bootstrap_singleton");
            entity.ToTable("bootstrap_state", "platform_administration", table =>
            {
                table.HasCheckConstraint("ck_platform_admin_bootstrap_singleton", "singleton_key = 1");
                table.HasCheckConstraint("ck_platform_admin_bootstrap_status", "status IN (1, 2)");
                table.HasCheckConstraint(
                    "ck_platform_admin_bootstrap_idempotency_key",
                    "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint(
                    "ck_platform_admin_bootstrap_intent_fingerprint",
                    "intent_fingerprint ~ '^[0-9A-F]{64}$'");
            });
        });

        modelBuilder.Entity("Application.PlatformAdministration.Postgres.PlatformAdminAuditEventRow", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            entity.Property<Guid>("BootstrapId").HasColumnType("uuid").HasColumnName("bootstrap_id");
            entity.Property<Guid>("DeviceId").HasColumnType("uuid").HasColumnName("device_id");
            entity.Property<short>("EventKind").HasColumnType("smallint").HasColumnName("event_kind");
            entity.Property<DateTimeOffset>("OccurredAt")
                .HasColumnType("timestamp with time zone").HasColumnName("occurred_at");
            entity.Property<short>("Outcome").HasColumnType("smallint").HasColumnName("outcome");
            entity.Property<Guid>("PrincipalId").HasColumnType("uuid").HasColumnName("principal_id");
            entity.HasKey("Id").HasName("pk_platform_admin_audit_events");
            entity.HasIndex("BootstrapId", "OccurredAt")
                .HasDatabaseName("ix_platform_admin_audit_bootstrap_occurred_at");
            entity.ToTable("audit_events", "platform_administration", table =>
            {
                table.HasCheckConstraint("ck_platform_admin_audit_event_kind", "event_kind IN (1, 2)");
                table.HasCheckConstraint("ck_platform_admin_audit_outcome", "outcome IN (1, 2)");
            });
        });

        modelBuilder.Entity("Application.PlatformAdministration.Postgres.AdminDeviceRow", entity =>
        {
            entity.HasOne("Application.PlatformAdministration.Postgres.PlatformPrincipalRow", null)
                .WithMany()
                .HasForeignKey("PrincipalId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_admin_devices_platform_principals_principal_id");
        });

        modelBuilder.Entity("Application.PlatformAdministration.Postgres.PlatformAdminBootstrapRow", entity =>
        {
            entity.HasOne("Application.PlatformAdministration.Postgres.AdminDeviceRow", null)
                .WithMany()
                .HasForeignKey("DeviceId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_platform_admin_bootstrap_device_id");
            entity.HasOne("Application.PlatformAdministration.Postgres.PlatformPrincipalRow", null)
                .WithMany()
                .HasForeignKey("PrincipalId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_platform_admin_bootstrap_principal_id");
        });

        modelBuilder.Entity("Application.PlatformAdministration.Postgres.PlatformAdminAuditEventRow", entity =>
        {
            entity.HasOne("Application.PlatformAdministration.Postgres.PlatformAdminBootstrapRow", null)
                .WithMany()
                .HasForeignKey("BootstrapId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_platform_admin_audit_bootstrap_id");
        });
    }
}
