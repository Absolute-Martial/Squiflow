using Microsoft.EntityFrameworkCore;

namespace SquiFlow.IdentityAccess.Postgres;

public sealed class IdentityAccessDbContext(DbContextOptions<IdentityAccessDbContext> options)
    : DbContext(options)
{
    internal DbSet<AccountRow> Accounts => Set<AccountRow>();

    internal DbSet<ExternalIdentityBindingRow> ExternalIdentityBindings => Set<ExternalIdentityBindingRow>();

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
