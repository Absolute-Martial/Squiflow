using Microsoft.EntityFrameworkCore;

namespace Application.Customers.Postgres;

internal static class CustomerRepresentativeModel
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerRepresentativeRow>(entity =>
        {
            entity.ToTable("representatives", "customers", table =>
            {
                table.HasCheckConstraint("ck_customer_representatives_availability", "availability IN (1, 2)");
                table.HasCheckConstraint("ck_customer_representatives_revision", "revision > 0");
                table.HasCheckConstraint("ck_customer_representatives_change_pair",
                    "(changed_by_account_id IS NULL) = (changed_at IS NULL)");
            });
            entity.HasKey(row => new { row.TenantId, row.Id }).HasName("pk_customer_representatives");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.OrganizationId).HasColumnName("organization_id");
            entity.Property(row => row.ProgramId).HasColumnName("program_id");
            entity.Property(row => row.IndividualId).HasColumnName("individual_id");
            entity.Property(row => row.Availability).HasColumnName("availability");
            entity.Property(row => row.Revision).HasColumnName("revision");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.ChangedByAccountId).HasColumnName("changed_by_account_id");
            entity.Property(row => row.ChangedAt).HasColumnName("changed_at");
            entity.HasIndex(row => row.Id).IsUnique().HasDatabaseName("ux_customer_representatives_id");
            entity.HasIndex(row => new { row.TenantId, row.OrganizationId, row.IndividualId })
                .IsUnique().HasFilter("availability = 1 AND program_id IS NULL")
                .HasDatabaseName("ux_customer_representatives_active_organization");
            entity.HasIndex(row => new { row.TenantId, row.OrganizationId, row.ProgramId, row.IndividualId })
                .IsUnique().HasFilter("availability = 1 AND program_id IS NOT NULL")
                .HasDatabaseName("ux_customer_representatives_active_program");
            entity.HasOne<CustomerOrganizationRow>().WithMany()
                .HasForeignKey(row => new { row.TenantId, row.OrganizationId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_representatives_organization");
            entity.HasOne<CustomerProgramRow>().WithMany()
                .HasForeignKey(row => new { row.TenantId, row.ProgramId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_representatives_program");
            entity.HasOne<CustomerIndividualRow>().WithMany()
                .HasForeignKey(row => new { row.TenantId, row.IndividualId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_representatives_individual");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.CreatedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("representatives_created_by_account_id_fkey");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.ChangedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("representatives_changed_by_account_id_fkey");
        });

        modelBuilder.Entity<CustomerRepresentativeCommandReceiptRow>(entity =>
        {
            entity.ToTable("representative_command_receipts", "customers", table =>
            {
                table.HasCheckConstraint("ck_customer_representative_receipts_operation", "operation IN ('link', 'unlink')");
                table.HasCheckConstraint("ck_customer_representative_receipts_key", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_customer_representative_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_customer_representative_receipts_availability", "availability IN (1, 2)");
                table.HasCheckConstraint("ck_customer_representative_receipts_revision", "revision > 0");
                table.HasCheckConstraint("ck_customer_representative_receipts_change_pair",
                    "(changed_by_account_id IS NULL) = (changed_at IS NULL)");
            });
            entity.HasKey(row => new { row.TenantId, row.AccountId, row.Operation, row.IdempotencyKey })
                .HasName("pk_customer_representative_command_receipts");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.AccountId).HasColumnName("account_id");
            entity.Property(row => row.Operation).HasMaxLength(16).HasColumnName("operation");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).HasColumnName("idempotency_key");
            entity.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength().HasColumnName("fingerprint");
            entity.Property(row => row.RepresentativeId).HasColumnName("representative_id");
            entity.Property(row => row.OrganizationId).HasColumnName("organization_id");
            entity.Property(row => row.ProgramId).HasColumnName("program_id");
            entity.Property(row => row.IndividualId).HasColumnName("individual_id");
            entity.Property(row => row.Availability).HasColumnName("availability");
            entity.Property(row => row.Revision).HasColumnName("revision");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.ChangedByAccountId).HasColumnName("changed_by_account_id");
            entity.Property(row => row.ChangedAt).HasColumnName("changed_at");
            entity.HasIndex(row => new { row.TenantId, row.RepresentativeId })
                .HasDatabaseName("ix_customer_representative_receipts_representative");
            entity.HasOne<CustomerRepresentativeRow>().WithMany()
                .HasForeignKey(row => new { row.TenantId, row.RepresentativeId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_representative_receipts_representative");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("representative_receipts_account_id_fkey");
        });
    }
}
