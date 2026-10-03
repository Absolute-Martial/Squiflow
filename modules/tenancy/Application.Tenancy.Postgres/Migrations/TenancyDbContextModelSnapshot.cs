using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Application.Tenancy.Postgres.Migrations;

[DbContext(typeof(TenancyDbContext))]
partial class TenancyDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        TenancyModel.Build(modelBuilder);
        TenantProvisioningReceiptModelV202610030002.Build(modelBuilder);
    }
}

internal static class TenantProvisioningReceiptModelV202610030002
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("Application.Tenancy.Postgres.TenantProvisioningReceiptRow", entity =>
        {
            entity.Property<Guid>("ProvisionedByPrincipalId")
                .HasColumnType("uuid").HasColumnName("provisioned_by_principal_id");
            entity.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(200)
                .HasColumnType("character varying(200)").HasColumnName("idempotency_key");
            entity.Property<string>("RequestFingerprint").IsRequired().HasMaxLength(64)
                .HasColumnType("character varying(64)").HasColumnName("request_fingerprint");
            entity.Property<Guid>("TenantId").HasColumnType("uuid").HasColumnName("tenant_id");
            entity.Property<string>("DisplayName").IsRequired().HasMaxLength(200)
                .HasColumnType("character varying(200)").HasColumnName("display_name");
            entity.Property<Guid>("ProvisionedByDeviceId")
                .HasColumnType("uuid").HasColumnName("provisioned_by_device_id");
            entity.Property<DateTimeOffset>("ActivatedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("activated_at");
            entity.HasKey("ProvisionedByPrincipalId", "IdempotencyKey")
                .HasName("pk_tenant_provisioning_receipts");
            entity.HasIndex("TenantId").IsUnique()
                .HasDatabaseName("ux_tenant_provisioning_receipts_tenant_id");
            entity.ToTable("tenant_provisioning_receipts", "tenancy", table =>
            {
                table.HasCheckConstraint("ck_tenant_provisioning_receipts_idempotency_key_not_blank",
                    "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_tenant_provisioning_receipts_request_fingerprint",
                    "request_fingerprint ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("ck_tenant_provisioning_receipts_display_name_not_blank",
                    "btrim(display_name) <> ''");
            });
        });

        modelBuilder.Entity("Application.Tenancy.Postgres.TenantProvisioningReceiptRow", entity =>
        {
            entity.HasOne("Application.Tenancy.Postgres.TenantRow", null)
                .WithMany().HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired()
                .HasConstraintName("fk_tenant_provisioning_receipts_tenants_tenant_id");
        });
    }
}

internal static class TenancyModel
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.8");
        modelBuilder.HasDefaultSchema("tenancy");

        modelBuilder.Entity("Application.Tenancy.Postgres.TenantRow", entity =>
        {
            entity.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("id");
            entity.Property<short>("Availability")
                .HasColumnType("smallint")
                .HasColumnName("availability");
            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property<string>("DisplayName")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)")
                .HasColumnName("display_name");
            entity.Property<DateTimeOffset?>("SuspendedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("suspended_at");
            entity.HasKey("Id").HasName("pk_tenants");
            entity.ToTable("tenants", "tenancy", table =>
            {
                table.HasCheckConstraint("ck_tenants_availability", "availability IN (1, 2)");
                table.HasCheckConstraint("ck_tenants_display_name_not_blank", "btrim(display_name) <> ''");
            });
        });

        modelBuilder.Entity("Application.Tenancy.Postgres.TenantMembershipRow", entity =>
        {
            entity.Property<Guid>("TenantId")
                .HasColumnType("uuid")
                .HasColumnName("tenant_id");
            entity.Property<Guid>("AccountId")
                .HasColumnType("uuid")
                .HasColumnName("account_id");
            entity.Property<short>("Availability")
                .HasColumnType("smallint")
                .HasColumnName("availability");
            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property<DateTimeOffset?>("SuspendedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("suspended_at");
            entity.HasKey("TenantId", "AccountId").HasName("pk_memberships");
            entity.HasIndex("AccountId", "Availability")
                .HasDatabaseName("ix_memberships_account_id_availability");
            entity.ToTable("memberships", "tenancy", table =>
                table.HasCheckConstraint("ck_memberships_availability", "availability IN (1, 2)"));
        });

        modelBuilder.Entity("Application.Tenancy.Postgres.TenantMembershipRow", entity =>
        {
            entity.HasOne("Application.Tenancy.Postgres.TenantRow", null)
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_memberships_tenants_tenant_id");
        });
    }
}
