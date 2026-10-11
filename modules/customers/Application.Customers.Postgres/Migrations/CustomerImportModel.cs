using Microsoft.EntityFrameworkCore;

namespace Application.Customers.Postgres;

internal static class CustomerImportModel
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerImportRow>(entity =>
        {
            entity.ToTable("imports", "customers", table =>
            {
                table.HasCheckConstraint("ck_customer_imports_contract", "contract_version = 'customer-import/v1'");
                table.HasCheckConstraint("ck_customer_imports_hash", "manifest_hash ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_customer_imports_bytes", "byte_length BETWEEN 0 AND 10485760");
                table.HasCheckConstraint("ck_customer_imports_rows", "row_count BETWEEN 0 AND 10000");
            });
            entity.HasKey(row => new { row.TenantId, row.ImportId }).HasName("pk_customer_imports");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.ImportId).HasColumnName("import_id");
            entity.Property(row => row.AccountId).HasColumnName("account_id");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).HasColumnName("idempotency_key");
            entity.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength().HasColumnName("fingerprint");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.ContractVersion).HasMaxLength(32).HasColumnName("contract_version");
            entity.Property(row => row.ManifestHash).HasMaxLength(64).IsFixedLength().HasColumnName("manifest_hash");
            entity.Property(row => row.ByteLength).HasColumnName("byte_length");
            entity.Property(row => row.RowCount).HasColumnName("row_count");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(row => new { row.TenantId, row.AccountId, row.IdempotencyKey })
                .IsUnique().HasDatabaseName("ux_customer_imports_operation");
            entity.HasIndex(row => new { row.TenantId, row.ImportId, row.Fingerprint })
                .HasDatabaseName("ix_customer_imports_fingerprint");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("imports_account_id_fkey");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.CreatedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("imports_created_by_account_id_fkey");
        });

        modelBuilder.Entity<CustomerImportItemRow>(entity =>
        {
            entity.ToTable("import_rows", "customers", table =>
            {
                table.HasCheckConstraint("ck_customer_import_rows_status", "status IN (1, 2, 3, 4, 5)");
                table.HasCheckConstraint("ck_customer_import_rows_hash", "source_row_hash ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_customer_import_rows_row", "row_number >= 2");
            });
            entity.HasKey(row => new { row.TenantId, row.ImportId, row.RowNumber }).HasName("pk_customer_import_rows");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.ImportId).HasColumnName("import_id");
            entity.Property(row => row.RowNumber).HasColumnName("row_number");
            entity.Property(row => row.SourceRowHash).HasMaxLength(64).IsFixedLength().HasColumnName("source_row_hash");
            entity.Property(row => row.Name).HasMaxLength(200).HasColumnName("name");
            entity.Property(row => row.CustomerType).HasMaxLength(32).HasColumnName("customer_type");
            entity.Property(row => row.ExternalRegistrationId).HasMaxLength(200).HasColumnName("external_id");
            entity.Property(row => row.Email).HasMaxLength(254).HasColumnName("email");
            entity.Property(row => row.Phone).HasMaxLength(32).HasColumnName("phone");
            entity.Property(row => row.AddressLine1).HasMaxLength(200).HasColumnName("address_line1");
            entity.Property(row => row.AddressLine2).HasMaxLength(200).HasColumnName("address_line2");
            entity.Property(row => row.City).HasMaxLength(120).HasColumnName("city");
            entity.Property(row => row.Notes).HasMaxLength(4000).HasColumnName("notes");
            entity.Property(row => row.Status).HasColumnName("status");
            entity.Property(row => row.ErrorCode).HasMaxLength(64).HasColumnName("error_code");
            entity.Property(row => row.ErrorMessage).HasMaxLength(1000).HasColumnName("error_message");
            entity.Property(row => row.CustomerId).HasColumnName("customer_id");
            entity.Property(row => row.ProcessedAt).HasColumnName("processed_at");
            entity.Property(row => row.RowId).HasColumnName("row_id");
            entity.Property(row => row.RequiresDecision).HasColumnName("requires_decision");
            entity.Property(row => row.DuplicateEvidence).HasMaxLength(4000).HasColumnName("duplicate_evidence");
            entity.Property(row => row.Decision).HasColumnName("decision");
            entity.Property(row => row.MappingCustomerId).HasColumnName("mapping_customer_id");
            entity.Property(row => row.Attempts).HasColumnName("attempts");
            entity.Property(row => row.NameSignal).HasMaxLength(200).HasColumnName("name_signal");
            entity.Property(row => row.EmailSignal).HasMaxLength(254).HasColumnName("email_signal");
            entity.Property(row => row.PhoneSignal).HasMaxLength(32).HasColumnName("phone_signal");
            entity.Property(row => row.ExternalIdSignal).HasMaxLength(200).HasColumnName("external_id_signal");
            entity.HasIndex(row => new { row.TenantId, row.RowId }).IsUnique().HasDatabaseName("ux_customer_import_rows_identity");
            entity.HasOne<CustomerIndividualRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.MappingCustomerId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_import_rows_mapping");
            entity.HasIndex(row => new { row.TenantId, row.ImportId, row.Status })
                .HasDatabaseName("ix_customer_import_rows_status");
            entity.HasOne<CustomerImportRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.ImportId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_import_rows_import");
            entity.HasOne<CustomerIndividualRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.CustomerId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_import_rows_customer");
        });

        modelBuilder.Entity<CustomerImportWorkRow>(entity =>
        {
            entity.ToTable("import_work", "customers", table =>
            {
                table.HasCheckConstraint("ck_customer_import_work_status", "status IN (1, 2, 3, 4)");
                table.HasCheckConstraint("ck_customer_import_work_key", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_customer_import_work_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
            });
            entity.HasKey(row => new { row.TenantId, row.WorkId }).HasName("pk_customer_import_work");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.WorkId).HasColumnName("work_id");
            entity.Property(row => row.ImportId).HasColumnName("import_id");
            entity.Property(row => row.AccountId).HasColumnName("account_id");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).HasColumnName("idempotency_key");
            entity.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength().HasColumnName("fingerprint");
            entity.Property(row => row.Status).HasColumnName("status");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.CompletedAt).HasColumnName("completed_at");
            entity.Property(row => row.LastError).HasMaxLength(1000).HasColumnName("last_error");
            entity.Property(row => row.AuthorizationRevision).HasColumnName("authorization_revision");
            entity.Property(row => row.Generation).HasColumnName("generation");
            entity.Property(row => row.WorkerId).HasColumnName("worker_id");
            entity.Property(row => row.LeaseExpiresAt).HasColumnName("lease_expires_at");
            entity.Property(row => row.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.HasIndex(row => new { row.TenantId, row.ImportId }).IsUnique().HasDatabaseName("ux_customer_import_work_import");
            entity.HasIndex(row => new { row.TenantId, row.AccountId, row.ImportId, row.IdempotencyKey })
                .IsUnique().HasDatabaseName("ux_customer_import_work_operation");
            entity.HasOne<CustomerImportRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.ImportId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_import_work_import");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("import_work_account_id_fkey");
        });
    }
}
