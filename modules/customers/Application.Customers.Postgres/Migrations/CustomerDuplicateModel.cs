using Microsoft.EntityFrameworkCore;

namespace Application.Customers.Postgres;

internal static class CustomerDuplicateModel
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerDuplicateCaseRow>(entity =>
        {
            entity.ToTable("duplicate_cases", "customers", table =>
            {
                table.HasCheckConstraint("ck_customer_duplicate_cases_pair", "customer_id <> other_customer_id");
                table.HasCheckConstraint("ck_customer_duplicate_cases_outcome", "outcome IS NULL OR outcome IN (1, 2, 3, 4)");
            });
            entity.HasKey(row => new { row.TenantId, row.CaseId }).HasName("pk_customer_duplicate_cases");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.CaseId).HasColumnName("case_id");
            entity.Property(row => row.CustomerId).HasColumnName("customer_id");
            entity.Property(row => row.OtherCustomerId).HasColumnName("other_customer_id");
            entity.Property(row => row.Evidence).HasMaxLength(512).HasColumnName("evidence");
            entity.Property(row => row.Outcome).HasColumnName("outcome");
            entity.Property(row => row.ResolvedByAccountId).HasColumnName("resolved_by_account_id");
            entity.Property(row => row.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(row => row.Reason).HasMaxLength(1000).HasColumnName("reason");
            entity.HasIndex(row => new { row.TenantId, row.CustomerId, row.OtherCustomerId })
                .HasDatabaseName("ix_customer_duplicate_cases_pair");
            entity.HasOne<CustomerIndividualRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.CustomerId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_duplicate_cases_customer");
            entity.HasOne<CustomerIndividualRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.OtherCustomerId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_duplicate_cases_other_customer");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.ResolvedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("duplicate_cases_resolved_by_account_id_fkey");
        });

        modelBuilder.Entity<CustomerDuplicateReceiptRow>(entity =>
        {
            entity.ToTable("duplicate_command_receipts", "customers", table =>
            {
                table.HasCheckConstraint("ck_customer_duplicate_receipts_operation", "operation IN ('resolve', 'consolidate')");
                table.HasCheckConstraint("ck_customer_duplicate_receipts_key", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_customer_duplicate_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_customer_duplicate_receipts_outcome", "outcome IN (1, 2, 3, 4)");
            });
            entity.HasKey(row => new { row.TenantId, row.AccountId, row.Operation, row.IdempotencyKey })
                .HasName("pk_customer_duplicate_command_receipts");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.AccountId).HasColumnName("account_id");
            entity.Property(row => row.Operation).HasMaxLength(16).HasColumnName("operation");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).HasColumnName("idempotency_key");
            entity.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength().HasColumnName("fingerprint");
            entity.Property(row => row.ResolutionId).HasColumnName("resolution_id");
            entity.Property(row => row.Evidence).HasMaxLength(512).HasColumnName("evidence");
            entity.Property(row => row.CustomerId).HasColumnName("customer_id");
            entity.Property(row => row.OtherCustomerId).HasColumnName("other_customer_id");
            entity.Property(row => row.Outcome).HasColumnName("outcome");
            entity.Property(row => row.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(row => row.Reason).HasMaxLength(1000).HasColumnName("reason");
            entity.HasIndex(row => new { row.TenantId, row.ResolutionId })
                .HasDatabaseName("ix_customer_duplicate_receipts_resolution");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("duplicate_receipts_account_id_fkey");
        });

        modelBuilder.Entity<CustomerRedirectRow>(entity =>
        {
            entity.ToTable("customer_redirects", "customers", table =>
                table.HasCheckConstraint("ck_customer_redirects_pair", "source_customer_id <> canonical_customer_id"));
            entity.HasKey(row => new { row.TenantId, row.SourceCustomerId }).HasName("pk_customer_redirects");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.SourceCustomerId).HasColumnName("source_customer_id");
            entity.Property(row => row.CanonicalCustomerId).HasColumnName("canonical_customer_id");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(row => new { row.TenantId, row.CanonicalCustomerId })
                .HasDatabaseName("ix_customer_redirects_canonical");
            entity.HasOne<CustomerIndividualRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.SourceCustomerId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_redirects_source");
            entity.HasOne<CustomerIndividualRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.CanonicalCustomerId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_redirects_canonical");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.CreatedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("redirects_created_by_account_id_fkey");
        });
    }
}
