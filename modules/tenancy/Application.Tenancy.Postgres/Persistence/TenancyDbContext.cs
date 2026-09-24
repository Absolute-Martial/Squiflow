using Microsoft.EntityFrameworkCore;

namespace Application.Tenancy.Postgres;

public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options)
    : DbContext(options)
{
    internal DbSet<TenantRow> Tenants => Set<TenantRow>();

    internal DbSet<TenantMembershipRow> Memberships => Set<TenantMembershipRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("tenancy");

        modelBuilder.Entity<TenantRow>(entity =>
        {
            entity.ToTable("tenants", table =>
            {
                table.HasCheckConstraint("ck_tenants_availability", "availability IN (1, 2)");
                table.HasCheckConstraint("ck_tenants_display_name_not_blank", "btrim(display_name) <> ''");
            });
            entity.HasKey(tenant => tenant.Id).HasName("pk_tenants");
            entity.Property(tenant => tenant.Id).HasColumnName("id");
            entity.Property(tenant => tenant.DisplayName)
                .HasMaxLength(200)
                .HasColumnName("display_name");
            entity.Property(tenant => tenant.Availability)
                .HasConversion<short>()
                .HasColumnName("availability");
            entity.Property(tenant => tenant.CreatedAt).HasColumnName("created_at");
            entity.Property(tenant => tenant.SuspendedAt).HasColumnName("suspended_at");
        });

        modelBuilder.Entity<TenantMembershipRow>(entity =>
        {
            entity.ToTable("memberships", table =>
                table.HasCheckConstraint("ck_memberships_availability", "availability IN (1, 2)"));
            entity.HasKey(membership => new { membership.TenantId, membership.AccountId })
                .HasName("pk_memberships");
            entity.Property(membership => membership.TenantId).HasColumnName("tenant_id");
            entity.Property(membership => membership.AccountId).HasColumnName("account_id");
            entity.Property(membership => membership.Availability)
                .HasConversion<short>()
                .HasColumnName("availability");
            entity.Property(membership => membership.CreatedAt).HasColumnName("created_at");
            entity.Property(membership => membership.SuspendedAt).HasColumnName("suspended_at");
            entity.HasOne<TenantRow>()
                .WithMany()
                .HasForeignKey(membership => membership.TenantId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_memberships_tenants_tenant_id");
            entity.HasIndex(membership => new { membership.AccountId, membership.Availability })
                .HasDatabaseName("ix_memberships_account_id_availability");
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

    public DateTimeOffset? SuspendedAt { get; set; }
}
