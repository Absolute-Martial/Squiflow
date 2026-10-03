using Microsoft.EntityFrameworkCore;

#nullable disable

namespace Application.Tenancy.Postgres.Migrations;

internal static class MembershipLifecycleReceiptModelV202610030004
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("Application.Tenancy.Postgres.MembershipLifecycleReceiptRow", entity =>
        {
            entity.Property<Guid>("ChangedByPrincipalId")
                .HasColumnType("uuid").HasColumnName("changed_by_principal_id");
            entity.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(200)
                .HasColumnType("character varying(200)").HasColumnName("idempotency_key");
            entity.Property<string>("RequestFingerprint").IsRequired().HasMaxLength(64)
                .HasColumnType("character varying(64)").HasColumnName("request_fingerprint");
            entity.Property<short>("Operation")
                .HasColumnType("smallint").HasColumnName("operation");
            entity.Property<Guid>("TenantId")
                .HasColumnType("uuid").HasColumnName("tenant_id");
            entity.Property<Guid>("AccountId")
                .HasColumnType("uuid").HasColumnName("account_id");
            entity.Property<short>("ResultStatus")
                .HasColumnType("smallint").HasColumnName("result_status");
            entity.Property<short?>("ResultAvailability")
                .HasColumnType("smallint").HasColumnName("result_availability");
            entity.Property<int?>("ResultRevision")
                .HasColumnType("integer").HasColumnName("result_revision");
            entity.Property<DateTimeOffset?>("InvitedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("invited_at");
            entity.Property<DateTimeOffset?>("ActivatedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("activated_at");
            entity.Property<DateTimeOffset?>("SuspendedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("suspended_at");
            entity.Property<DateTimeOffset?>("RemovedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("removed_at");
            entity.Property<Guid>("ChangedByDeviceId")
                .HasColumnType("uuid").HasColumnName("changed_by_device_id");
            entity.Property<DateTimeOffset>("OccurredAt")
                .HasColumnType("timestamp with time zone").HasColumnName("occurred_at");
            entity.HasKey("ChangedByPrincipalId", "IdempotencyKey").HasName("pk_membership_lifecycle_receipts");
            entity.HasIndex("TenantId", "AccountId", "OccurredAt").HasDatabaseName("ix_membership_lifecycle_receipts_membership");
            entity.ToTable("membership_lifecycle_receipts", "tenancy", table =>
            {
                table.HasCheckConstraint("ck_membership_lifecycle_receipts_key_not_blank", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_membership_lifecycle_receipts_fingerprint", "request_fingerprint ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("ck_membership_lifecycle_receipts_operation", "operation IN (1,2,3,4)");
                table.HasCheckConstraint("ck_membership_lifecycle_receipts_result", "result_status = operation AND result_availability IS NOT NULL AND result_revision IS NOT NULL AND result_revision >= 1 AND invited_at IS NOT NULL AND ((operation = 1 AND result_availability = 3) OR (operation = 2 AND result_availability = 1) OR (operation = 3 AND result_availability = 2) OR (operation = 4 AND result_availability = 4))");
            });
        });
        modelBuilder.Entity("Application.Tenancy.Postgres.MembershipLifecycleReceiptRow", entity =>
            entity.HasOne("Application.Tenancy.Postgres.TenantRow", null).WithMany().HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired()
                .HasConstraintName("fk_membership_lifecycle_receipts_tenant_id"));
    }
}
