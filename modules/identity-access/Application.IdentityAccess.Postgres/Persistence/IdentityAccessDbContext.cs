using Microsoft.EntityFrameworkCore;

namespace Application.IdentityAccess.Postgres;

public sealed class IdentityAccessDbContext(DbContextOptions<IdentityAccessDbContext> options)
    : DbContext(options)
{
    internal DbSet<AccountRow> Accounts => Set<AccountRow>();

    internal DbSet<ExternalIdentityBindingRow> ExternalIdentityBindings => Set<ExternalIdentityBindingRow>();

    internal DbSet<AccountOnboardingReceiptRow> AccountOnboardingReceipts => Set<AccountOnboardingReceiptRow>();

    internal DbSet<IdentityLinkReceiptRow> IdentityLinkReceipts => Set<IdentityLinkReceiptRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("identity_access");

        modelBuilder.Entity<AccountRow>(entity =>
        {
            entity.ToTable("accounts", table =>
                table.HasCheckConstraint("ck_accounts_availability", "availability IN (1, 2)"));
            entity.HasKey(account => account.Id).HasName("pk_accounts");
            entity.Property(account => account.Id).HasColumnName("id");
            entity.Property(account => account.Availability)
                .HasConversion<short>()
                .HasColumnName("availability");
            entity.Property(account => account.CreatedAt).HasColumnName("created_at");
            entity.Property(account => account.DisabledAt).HasColumnName("disabled_at");
        });

        modelBuilder.Entity<ExternalIdentityBindingRow>(entity =>
        {
            entity.ToTable("external_identity_bindings", table =>
            {
                table.HasCheckConstraint(
                    "ck_external_identity_bindings_issuer_not_blank",
                    "btrim(issuer) <> ''");
                table.HasCheckConstraint(
                    "ck_external_identity_bindings_subject_not_blank",
                    "btrim(subject) <> ''");
            });
            entity.HasKey(binding => new { binding.Issuer, binding.Subject })
                .HasName("pk_external_identity_bindings");
            entity.Property(binding => binding.Issuer).HasMaxLength(255).HasColumnName("issuer");
            entity.Property(binding => binding.Subject).HasMaxLength(255).HasColumnName("subject");
            entity.Property(binding => binding.AccountId).HasColumnName("account_id");
            entity.Property(binding => binding.CreatedAt).HasColumnName("created_at");
            entity.HasOne<AccountRow>()
                .WithMany()
                .HasForeignKey(binding => binding.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_external_identity_bindings_accounts_account_id");
            entity.HasIndex(binding => binding.AccountId)
                .HasDatabaseName("ix_external_identity_bindings_account_id");
        });

        modelBuilder.Entity<AccountOnboardingReceiptRow>(entity =>
        {
            entity.ToTable("account_onboarding_receipts", table =>
            {
                table.HasCheckConstraint("ck_account_onboarding_receipts_key_not_blank", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_account_onboarding_receipts_fingerprint", "request_fingerprint ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("ck_account_onboarding_receipts_issuer_not_blank", "btrim(issuer) <> ''");
                table.HasCheckConstraint("ck_account_onboarding_receipts_subject_not_blank", "btrim(subject) <> ''");
            });
            entity.HasKey(receipt => new { receipt.ProvisionedByPrincipalId, receipt.IdempotencyKey })
                .HasName("pk_account_onboarding_receipts");
            entity.Property(receipt => receipt.ProvisionedByPrincipalId).HasColumnName("provisioned_by_principal_id");
            entity.Property(receipt => receipt.IdempotencyKey).HasMaxLength(200).HasColumnName("idempotency_key");
            entity.Property(receipt => receipt.RequestFingerprint).HasMaxLength(64).HasColumnName("request_fingerprint");
            entity.Property(receipt => receipt.AccountId).HasColumnName("account_id");
            entity.Property(receipt => receipt.Issuer).HasMaxLength(255).HasColumnName("issuer");
            entity.Property(receipt => receipt.Subject).HasMaxLength(255).HasColumnName("subject");
            entity.Property(receipt => receipt.ProvisionedByDeviceId).HasColumnName("provisioned_by_device_id");
            entity.Property(receipt => receipt.CreatedAt).HasColumnName("created_at");
            entity.HasOne<AccountRow>().WithMany().HasForeignKey(receipt => receipt.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_account_onboarding_receipts_account_id");
            entity.HasIndex(receipt => receipt.AccountId)
                .IsUnique()
                .HasDatabaseName("ux_account_onboarding_receipts_account_id");
        });

        modelBuilder.Entity<IdentityLinkReceiptRow>(entity =>
        {
            entity.ToTable("identity_link_receipts", table =>
            {
                table.HasCheckConstraint("ck_identity_link_receipts_key_not_blank", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_identity_link_receipts_fingerprint", "request_fingerprint ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("ck_identity_link_receipts_issuer_not_blank", "btrim(issuer) <> ''");
                table.HasCheckConstraint("ck_identity_link_receipts_subject_not_blank", "btrim(subject) <> ''");
            });
            entity.HasKey(receipt => new { receipt.LinkedByPrincipalId, receipt.IdempotencyKey })
                .HasName("pk_identity_link_receipts");
            entity.Property(receipt => receipt.LinkedByPrincipalId).HasColumnName("linked_by_principal_id");
            entity.Property(receipt => receipt.IdempotencyKey).HasMaxLength(200).HasColumnName("idempotency_key");
            entity.Property(receipt => receipt.RequestFingerprint).HasMaxLength(64).HasColumnName("request_fingerprint");
            entity.Property(receipt => receipt.AccountId).HasColumnName("account_id");
            entity.Property(receipt => receipt.Issuer).HasMaxLength(255).HasColumnName("issuer");
            entity.Property(receipt => receipt.Subject).HasMaxLength(255).HasColumnName("subject");
            entity.Property(receipt => receipt.LinkedByDeviceId).HasColumnName("linked_by_device_id");
            entity.Property(receipt => receipt.LinkedAt).HasColumnName("linked_at");
            entity.HasOne<AccountRow>().WithMany().HasForeignKey(receipt => receipt.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_identity_link_receipts_account_id");
            entity.HasIndex(receipt => receipt.AccountId)
                .HasDatabaseName("ix_identity_link_receipts_account_id");
        });

    }
}

internal sealed class AccountRow
{
    public Guid Id { get; set; }

    public AccountAvailability Availability { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DisabledAt { get; set; }
}

internal sealed class ExternalIdentityBindingRow
{
    public Guid AccountId { get; set; }

    public string Issuer { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class AccountOnboardingReceiptRow
{
    public Guid ProvisionedByPrincipalId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public Guid ProvisionedByDeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class IdentityLinkReceiptRow
{
    public Guid LinkedByPrincipalId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public Guid LinkedByDeviceId { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
}
