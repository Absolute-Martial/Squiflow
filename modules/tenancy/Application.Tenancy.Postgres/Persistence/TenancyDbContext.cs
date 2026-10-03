using Microsoft.EntityFrameworkCore;

namespace Application.Tenancy.Postgres;

public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options)
    : DbContext(options)
{
    internal DbSet<TenantRow> Tenants => Set<TenantRow>();
    internal DbSet<TenantMembershipRow> Memberships => Set<TenantMembershipRow>();
    internal DbSet<TenantProvisioningReceiptRow> ProvisioningReceipts => Set<TenantProvisioningReceiptRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("tenancy");
        modelBuilder.Entity<MembershipLifecycleReceiptRow>();
        Migrations.MembershipLifecycleReceiptModelV202610030004.Build(modelBuilder);

        modelBuilder.Entity<TenantRow>(entity =>
        {
            entity.ToTable("tenants", table =>
            {
                table.HasCheckConstraint("ck_tenants_availability", "availability IN (1, 2)");
                table.HasCheckConstraint("ck_tenants_display_name_not_blank", "btrim(display_name) <> ''");
            });
            entity.HasKey(tenant => tenant.Id).HasName("pk_tenants");
            entity.Property(tenant => tenant.Id).HasColumnName("id");
            entity.Property(tenant => tenant.DisplayName).HasMaxLength(200).HasColumnName("display_name");
            entity.Property(tenant => tenant.Availability).HasConversion<short>().HasColumnName("availability");
            entity.Property(tenant => tenant.CreatedAt).HasColumnName("created_at");
            entity.Property(tenant => tenant.SuspendedAt).HasColumnName("suspended_at");
        });

        modelBuilder.Entity<TenantMembershipRow>(entity =>
        {
            entity.ToTable("memberships", table =>
            {
                table.HasCheckConstraint("ck_memberships_availability", "availability IN (1, 2, 3, 4)");
                table.HasCheckConstraint("ck_memberships_revision", "revision >= 1");
                table.HasCheckConstraint(
                    "ck_memberships_lifecycle",
                    "(availability = 1 AND activated_at IS NOT NULL AND suspended_at IS NULL AND removed_at IS NULL) OR " +
                    "(availability = 2 AND activated_at IS NOT NULL AND suspended_at IS NOT NULL AND removed_at IS NULL) OR " +
                    "(availability = 3 AND activated_at IS NULL AND suspended_at IS NULL AND removed_at IS NULL) OR " +
                    "(availability = 4 AND removed_at IS NOT NULL)");
            });
            entity.HasKey(membership => new { membership.TenantId, membership.AccountId })
                .HasName("pk_memberships");
            entity.Property(membership => membership.TenantId).HasColumnName("tenant_id");
            entity.Property(membership => membership.AccountId).HasColumnName("account_id");
            entity.Property(membership => membership.Availability).HasConversion<short>().HasColumnName("availability");
            entity.Property(membership => membership.CreatedAt).HasColumnName("created_at");
            entity.Property(membership => membership.Revision).HasColumnName("revision");
            entity.Property(membership => membership.ActivatedAt).HasColumnName("activated_at");
            entity.Property(membership => membership.SuspendedAt).HasColumnName("suspended_at");
            entity.Property(membership => membership.RemovedAt).HasColumnName("removed_at");
            entity.HasOne<TenantRow>().WithMany().HasForeignKey(membership => membership.TenantId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_memberships_tenants_tenant_id");
            entity.HasIndex(membership => new { membership.AccountId, membership.Availability })
                .HasDatabaseName("ix_memberships_account_id_availability");
        });

        modelBuilder.Entity<TenantProvisioningReceiptRow>(entity =>
        {
            entity.ToTable("tenant_provisioning_receipts", table =>
            {
                table.HasCheckConstraint("ck_tenant_provisioning_receipts_idempotency_key_not_blank", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_tenant_provisioning_receipts_request_fingerprint", "request_fingerprint ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("ck_tenant_provisioning_receipts_display_name_not_blank", "btrim(display_name) <> ''");
            });
            entity.HasKey(receipt => new { receipt.ProvisionedByPrincipalId, receipt.IdempotencyKey })
                .HasName("pk_tenant_provisioning_receipts");
            entity.Property(receipt => receipt.ProvisionedByPrincipalId).HasColumnName("provisioned_by_principal_id");
            entity.Property(receipt => receipt.IdempotencyKey).HasMaxLength(TenantProvisioningIntent.IdempotencyKeyLimit)
                .HasColumnName("idempotency_key");
            entity.Property(receipt => receipt.RequestFingerprint).HasMaxLength(64).HasColumnName("request_fingerprint");
            entity.Property(receipt => receipt.TenantId).HasColumnName("tenant_id");
            entity.Property(receipt => receipt.DisplayName).HasMaxLength(TenantProvisioningIntent.DisplayNameLimit)
                .HasColumnName("display_name");
            entity.Property(receipt => receipt.ProvisionedByDeviceId).HasColumnName("provisioned_by_device_id");
            entity.Property(receipt => receipt.ActivatedAt).HasColumnName("activated_at");
            entity.HasIndex(receipt => receipt.TenantId).IsUnique()
                .HasDatabaseName("ux_tenant_provisioning_receipts_tenant_id");
            entity.HasOne<TenantRow>().WithMany().HasForeignKey(receipt => receipt.TenantId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_tenant_provisioning_receipts_tenants_tenant_id");
        });
    }
}

internal sealed class TenantRow
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public TenantAvailability Availability { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
}

internal sealed class TenantMembershipRow
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public MembershipAvailability Availability { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}

internal sealed class TenantProvisioningReceiptRow
{
    public Guid ProvisionedByPrincipalId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public Guid ProvisionedByDeviceId { get; set; }
    public DateTimeOffset ActivatedAt { get; set; }
}

internal sealed class MembershipLifecycleReceiptRow
{
    public Guid ChangedByPrincipalId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public short Operation { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public short ResultStatus { get; set; }
    public short? ResultAvailability { get; set; }
    public int? ResultRevision { get; set; }
    public DateTimeOffset? InvitedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public Guid ChangedByDeviceId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
