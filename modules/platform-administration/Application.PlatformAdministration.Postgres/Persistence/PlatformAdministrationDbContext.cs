using Microsoft.EntityFrameworkCore;

namespace Application.PlatformAdministration.Postgres;

public sealed class PlatformAdministrationDbContext(
    DbContextOptions<PlatformAdministrationDbContext> options) : DbContext(options)
{
    internal DbSet<PlatformPrincipalRow> Principals => Set<PlatformPrincipalRow>();

    internal DbSet<AdminDeviceRow> AdminDevices => Set<AdminDeviceRow>();

    internal DbSet<PlatformAdminBootstrapRow> BootstrapStates => Set<PlatformAdminBootstrapRow>();

    internal DbSet<PlatformAdminAuditEventRow> AuditEvents => Set<PlatformAdminAuditEventRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("platform_administration");

        modelBuilder.Entity<PlatformPrincipalRow>(entity =>
        {
            entity.ToTable("principals", table =>
            {
                table.HasCheckConstraint("ck_platform_principals_availability", "availability IN (1, 2)");
                table.HasCheckConstraint("ck_platform_principals_issuer_not_blank", "btrim(issuer) <> ''");
                table.HasCheckConstraint("ck_platform_principals_subject_not_blank", "btrim(subject) <> ''");
            });
            entity.HasKey(row => row.Id).HasName("pk_platform_principals");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Issuer).HasMaxLength(255).HasColumnName("issuer");
            entity.Property(row => row.Subject).HasMaxLength(255).HasColumnName("subject");
            entity.Property(row => row.Availability).HasConversion<short>().HasColumnName("availability");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.DisabledAt).HasColumnName("disabled_at");
            entity.HasIndex(row => new { row.Issuer, row.Subject })
                .IsUnique()
                .HasDatabaseName("ux_platform_principals_external_identity");
        });

        modelBuilder.Entity<AdminDeviceRow>(entity =>
        {
            entity.ToTable("admin_devices", table =>
            {
                table.HasCheckConstraint("ck_admin_devices_availability", "availability IN (1, 2)");
                table.HasCheckConstraint(
                    "ck_admin_devices_certificate_fingerprint",
                    "certificate_fingerprint ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("ck_admin_devices_display_name_not_blank", "btrim(display_name) <> ''");
            });
            entity.HasKey(row => row.Id).HasName("pk_admin_devices");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.PrincipalId).HasColumnName("principal_id");
            entity.Property(row => row.CertificateFingerprint)
                .HasMaxLength(AdminDeviceCertificateFingerprint.HexLength)
                .HasColumnName("certificate_fingerprint");
            entity.Property(row => row.DisplayName)
                .HasMaxLength(PlatformAdminBootstrapIntent.DeviceNameLimit)
                .HasColumnName("display_name");
            entity.Property(row => row.Availability).HasConversion<short>().HasColumnName("availability");
            entity.Property(row => row.RegisteredAt).HasColumnName("registered_at");
            entity.Property(row => row.RevokedAt).HasColumnName("revoked_at");
            entity.HasIndex(row => row.CertificateFingerprint)
                .IsUnique()
                .HasDatabaseName("ux_admin_devices_certificate_fingerprint");
            entity.HasOne<PlatformPrincipalRow>()
                .WithMany()
                .HasForeignKey(row => row.PrincipalId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_admin_devices_platform_principals_principal_id");
        });

        modelBuilder.Entity<PlatformAdminBootstrapRow>(entity =>
        {
            entity.ToTable("bootstrap_state", table =>
            {
                table.HasCheckConstraint("ck_platform_admin_bootstrap_singleton", "singleton_key = 1");
                table.HasCheckConstraint("ck_platform_admin_bootstrap_status", "status IN (1, 2)");
                table.HasCheckConstraint("ck_platform_admin_bootstrap_idempotency_key", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint(
                    "ck_platform_admin_bootstrap_intent_fingerprint",
                    "intent_fingerprint ~ '^[0-9A-F]{64}$'");
            });
            entity.HasKey(row => row.BootstrapId).HasName("pk_platform_admin_bootstrap");
            entity.Property(row => row.BootstrapId).HasColumnName("bootstrap_id");
            entity.Property(row => row.SingletonKey).HasColumnName("singleton_key");
            entity.Property(row => row.PrincipalId).HasColumnName("principal_id");
            entity.Property(row => row.DeviceId).HasColumnName("device_id");
            entity.Property(row => row.IdempotencyKey)
                .HasMaxLength(PlatformAdminBootstrapIntent.IdempotencyKeyLimit)
                .HasColumnName("idempotency_key");
            entity.Property(row => row.IntentFingerprint).HasMaxLength(64).HasColumnName("intent_fingerprint");
            entity.Property(row => row.Status).HasConversion<short>().HasColumnName("status");
            entity.Property(row => row.PreparedAt).HasColumnName("prepared_at");
            entity.Property(row => row.CompletedAt).HasColumnName("completed_at");
            entity.HasIndex(row => row.SingletonKey)
                .IsUnique()
                .HasDatabaseName("ux_platform_admin_bootstrap_singleton");
            entity.HasOne<PlatformPrincipalRow>()
                .WithMany()
                .HasForeignKey(row => row.PrincipalId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_platform_admin_bootstrap_principal_id");
            entity.HasOne<AdminDeviceRow>()
                .WithMany()
                .HasForeignKey(row => row.DeviceId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_platform_admin_bootstrap_device_id");
        });

        modelBuilder.Entity<PlatformAdminAuditEventRow>(entity =>
        {
            entity.ToTable("audit_events", table =>
            {
                table.HasCheckConstraint("ck_platform_admin_audit_event_kind", "event_kind IN (1, 2)");
                table.HasCheckConstraint("ck_platform_admin_audit_outcome", "outcome IN (1, 2)");
            });
            entity.HasKey(row => row.Id).HasName("pk_platform_admin_audit_events");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.BootstrapId).HasColumnName("bootstrap_id");
            entity.Property(row => row.PrincipalId).HasColumnName("principal_id");
            entity.Property(row => row.DeviceId).HasColumnName("device_id");
            entity.Property(row => row.EventKind).HasConversion<short>().HasColumnName("event_kind");
            entity.Property(row => row.Outcome).HasConversion<short>().HasColumnName("outcome");
            entity.Property(row => row.OccurredAt).HasColumnName("occurred_at");
            entity.HasIndex(row => new { row.BootstrapId, row.OccurredAt })
                .HasDatabaseName("ix_platform_admin_audit_bootstrap_occurred_at");
            entity.HasOne<PlatformAdminBootstrapRow>()
                .WithMany()
                .HasForeignKey(row => row.BootstrapId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_platform_admin_audit_bootstrap_id");
        });
    }
}

internal sealed class PlatformPrincipalRow
{
    public Guid Id { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public PlatformPrincipalAvailability Availability { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
}

internal sealed class AdminDeviceRow
{
    public Guid Id { get; set; }
    public Guid PrincipalId { get; set; }
    public string CertificateFingerprint { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public AdminDeviceAvailability Availability { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

internal sealed class PlatformAdminBootstrapRow
{
    public Guid BootstrapId { get; set; }
    public short SingletonKey { get; set; } = 1;
    public Guid PrincipalId { get; set; }
    public Guid DeviceId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string IntentFingerprint { get; set; } = string.Empty;
    public PlatformAdminBootstrapStatus Status { get; set; }
    public DateTimeOffset PreparedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

internal sealed class PlatformAdminAuditEventRow
{
    public Guid Id { get; set; }
    public Guid BootstrapId { get; set; }
    public Guid PrincipalId { get; set; }
    public Guid DeviceId { get; set; }
    public PlatformAdminAuditEventKind EventKind { get; set; }
    public PlatformAdminAuditOutcome Outcome { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
