using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Application.IdentityAccess.Postgres.Migrations;

[DbContext(typeof(IdentityAccessDbContext))]
partial class IdentityAccessDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        IdentityAccessModel.BuildOnboarding(modelBuilder);
    }
}

internal static class IdentityAccessModel
{
    internal static void BuildOnboarding(ModelBuilder modelBuilder)
    {
        Build(modelBuilder);

        modelBuilder.Entity("Application.IdentityAccess.Postgres.AccountOnboardingReceiptRow", entity =>
        {
            entity.Property<Guid>("ProvisionedByPrincipalId")
                .HasColumnType("uuid").HasColumnName("provisioned_by_principal_id");
            entity.Property<string>("IdempotencyKey")
                .HasMaxLength(200).HasColumnType("character varying(200)").HasColumnName("idempotency_key");
            entity.Property<string>("RequestFingerprint")
                .IsRequired().HasMaxLength(64).HasColumnType("character varying(64)").HasColumnName("request_fingerprint");
            entity.Property<Guid>("AccountId")
                .HasColumnType("uuid").HasColumnName("account_id");
            entity.Property<string>("Issuer")
                .IsRequired().HasMaxLength(255).HasColumnType("character varying(255)").HasColumnName("issuer");
            entity.Property<string>("Subject")
                .IsRequired().HasMaxLength(255).HasColumnType("character varying(255)").HasColumnName("subject");
            entity.Property<Guid>("ProvisionedByDeviceId")
                .HasColumnType("uuid").HasColumnName("provisioned_by_device_id");
            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("created_at");
            entity.HasKey("ProvisionedByPrincipalId", "IdempotencyKey")
                .HasName("pk_account_onboarding_receipts");
            entity.HasIndex("AccountId").IsUnique()
                .HasDatabaseName("ux_account_onboarding_receipts_account_id");
            entity.ToTable("account_onboarding_receipts", "identity_access", table =>
            {
                table.HasCheckConstraint("ck_account_onboarding_receipts_key_not_blank", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_account_onboarding_receipts_fingerprint", "request_fingerprint ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("ck_account_onboarding_receipts_issuer_not_blank", "btrim(issuer) <> ''");
                table.HasCheckConstraint("ck_account_onboarding_receipts_subject_not_blank", "btrim(subject) <> ''");
            });
        });

        modelBuilder.Entity("Application.IdentityAccess.Postgres.IdentityLinkReceiptRow", entity =>
        {
            entity.Property<Guid>("LinkedByPrincipalId")
                .HasColumnType("uuid").HasColumnName("linked_by_principal_id");
            entity.Property<string>("IdempotencyKey")
                .HasMaxLength(200).HasColumnType("character varying(200)").HasColumnName("idempotency_key");
            entity.Property<string>("RequestFingerprint")
                .IsRequired().HasMaxLength(64).HasColumnType("character varying(64)").HasColumnName("request_fingerprint");
            entity.Property<Guid>("AccountId")
                .HasColumnType("uuid").HasColumnName("account_id");
            entity.Property<string>("Issuer")
                .IsRequired().HasMaxLength(255).HasColumnType("character varying(255)").HasColumnName("issuer");
            entity.Property<string>("Subject")
                .IsRequired().HasMaxLength(255).HasColumnType("character varying(255)").HasColumnName("subject");
            entity.Property<Guid>("LinkedByDeviceId")
                .HasColumnType("uuid").HasColumnName("linked_by_device_id");
            entity.Property<DateTimeOffset>("LinkedAt")
                .HasColumnType("timestamp with time zone").HasColumnName("linked_at");
            entity.HasKey("LinkedByPrincipalId", "IdempotencyKey")
                .HasName("pk_identity_link_receipts");
            entity.HasIndex("AccountId").HasDatabaseName("ix_identity_link_receipts_account_id");
            entity.ToTable("identity_link_receipts", "identity_access", table =>
            {
                table.HasCheckConstraint("ck_identity_link_receipts_key_not_blank", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_identity_link_receipts_fingerprint", "request_fingerprint ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("ck_identity_link_receipts_issuer_not_blank", "btrim(issuer) <> ''");
                table.HasCheckConstraint("ck_identity_link_receipts_subject_not_blank", "btrim(subject) <> ''");
            });
        });

        modelBuilder.Entity("Application.IdentityAccess.Postgres.AccountOnboardingReceiptRow", entity =>
        {
            entity.HasOne("Application.IdentityAccess.Postgres.AccountRow", null)
                .WithMany().HasForeignKey("AccountId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired()
                .HasConstraintName("fk_account_onboarding_receipts_account_id");
        });

        modelBuilder.Entity("Application.IdentityAccess.Postgres.IdentityLinkReceiptRow", entity =>
        {
            entity.HasOne("Application.IdentityAccess.Postgres.AccountRow", null)
                .WithMany().HasForeignKey("AccountId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired()
                .HasConstraintName("fk_identity_link_receipts_account_id");
        });
    }

    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.8");
        modelBuilder.HasDefaultSchema("identity_access");

        modelBuilder.Entity("Application.IdentityAccess.Postgres.AccountRow", entity =>
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
            entity.Property<DateTimeOffset?>("DisabledAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("disabled_at");
            entity.HasKey("Id").HasName("pk_accounts");
            entity.ToTable("accounts", "identity_access", table =>
                table.HasCheckConstraint("ck_accounts_availability", "availability IN (1, 2)"));
        });

        modelBuilder.Entity("Application.IdentityAccess.Postgres.ExternalIdentityBindingRow", entity =>
        {
            entity.Property<string>("Issuer")
                .HasMaxLength(255)
                .HasColumnType("character varying(255)")
                .HasColumnName("issuer");
            entity.Property<string>("Subject")
                .HasMaxLength(255)
                .HasColumnType("character varying(255)")
                .HasColumnName("subject");
            entity.Property<Guid>("AccountId")
                .HasColumnType("uuid")
                .HasColumnName("account_id");
            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.HasKey("Issuer", "Subject").HasName("pk_external_identity_bindings");
            entity.HasIndex("AccountId").HasDatabaseName("ix_external_identity_bindings_account_id");
            entity.ToTable("external_identity_bindings", "identity_access", table =>
            {
                table.HasCheckConstraint(
                    "ck_external_identity_bindings_issuer_not_blank",
                    "btrim(issuer) <> ''");
                table.HasCheckConstraint(
                    "ck_external_identity_bindings_subject_not_blank",
                    "btrim(subject) <> ''");
            });
        });

        modelBuilder.Entity("Application.IdentityAccess.Postgres.ExternalIdentityBindingRow", entity =>
        {
            entity.HasOne("Application.IdentityAccess.Postgres.AccountRow", null)
                .WithMany()
                .HasForeignKey("AccountId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_external_identity_bindings_accounts_account_id");
        });
    }
}
