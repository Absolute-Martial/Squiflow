using Microsoft.EntityFrameworkCore;

namespace Application.Customers.Postgres;

internal static class CustomerIndividualModel
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerIndividualRow>(entity =>
        {
            entity.ToTable("individuals", "customers", table =>
            {
                table.HasCheckConstraint("ck_customer_individuals_name", "btrim(display_name) <> ''");
                table.HasCheckConstraint("ck_customer_individuals_email", "email IS NULL OR btrim(email) <> ''");
                table.HasCheckConstraint("ck_customer_individuals_phone", "phone IS NULL OR btrim(phone) <> ''");
                table.HasCheckConstraint("ck_customer_individuals_availability", "availability IN (1, 2)");
                table.HasCheckConstraint("ck_customer_individuals_revision", "revision > 0");
                table.HasCheckConstraint("ck_customer_individuals_change_pair",
                    "(availability_changed_by_account_id IS NULL) = (availability_changed_at IS NULL)");
                table.HasCheckConstraint("ck_customer_individuals_contact_change_pair",
                    "(contact_changed_by_account_id IS NULL) = (contact_changed_at IS NULL)");
                table.HasCheckConstraint("ck_customer_individuals_customer_type", "btrim(customer_type) <> ''");
                table.HasCheckConstraint("ck_customer_individuals_redirect", "redirect_target_individual_id IS NULL OR redirect_target_individual_id <> id");
            });
            entity.HasKey(row => new { row.TenantId, row.Id }).HasName("pk_customer_individuals");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.DisplayName).HasMaxLength(200).HasColumnName("display_name");
            entity.Property(row => row.Email).HasMaxLength(254).HasColumnName("email");
            entity.Property(row => row.Phone).HasMaxLength(32).HasColumnName("phone");
            entity.Property(row => row.Availability).HasColumnName("availability");
            entity.Property(row => row.Revision).HasColumnName("revision");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.AvailabilityChangedByAccountId).HasColumnName("availability_changed_by_account_id");
            entity.Property(row => row.AvailabilityChangedAt).HasColumnName("availability_changed_at");
            entity.Property(row => row.ContactChangedByAccountId).HasColumnName("contact_changed_by_account_id");
            entity.Property(row => row.ContactChangedAt).HasColumnName("contact_changed_at");
            entity.Property(row => row.CustomerType).HasMaxLength(32).HasColumnName("customer_type");
            entity.Property(row => row.ExternalRegistrationId).HasMaxLength(200).HasColumnName("external_registration_id");
            entity.Property(row => row.NormalizedName).HasMaxLength(200).HasColumnName("normalized_name");
            entity.Property(row => row.NormalizedEmail).HasMaxLength(254).HasColumnName("normalized_email");
            entity.Property(row => row.NormalizedPhone).HasMaxLength(32).HasColumnName("normalized_phone");
            entity.Property(row => row.NormalizedExternalRegistrationId).HasMaxLength(200).HasColumnName("normalized_external_registration_id");
            entity.Property(row => row.AddressLine1).HasMaxLength(200).HasColumnName("address_line1");
            entity.Property(row => row.AddressLine2).HasMaxLength(200).HasColumnName("address_line2");
            entity.Property(row => row.City).HasMaxLength(120).HasColumnName("city");
            entity.Property(row => row.Notes).HasMaxLength(4000).HasColumnName("notes");
            entity.Property(row => row.RedirectTargetIndividualId).HasColumnName("redirect_target_individual_id");
            entity.HasIndex(row => row.Id).IsUnique().HasDatabaseName("ux_customer_individuals_id");
            entity.HasIndex(row => new { row.TenantId, row.NormalizedEmail })
                .HasDatabaseName("ix_customer_individuals_normalized_email");
            entity.HasIndex(row => new { row.TenantId, row.NormalizedPhone })
                .HasDatabaseName("ix_customer_individuals_normalized_phone");
            entity.HasIndex(row => new { row.TenantId, row.NormalizedExternalRegistrationId })
                .HasDatabaseName("ix_customer_individuals_normalized_external_id");
            entity.HasIndex(row => new { row.TenantId, row.NormalizedName })
                .HasDatabaseName("ix_customer_individuals_normalized_name");
            entity.HasOne<CustomerTenantReferenceRow>().WithMany().HasForeignKey(row => row.TenantId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("individuals_tenant_id_fkey");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.CreatedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("individuals_created_by_account_id_fkey");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany()
                .HasForeignKey(row => row.AvailabilityChangedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("individuals_availability_changed_by_account_id_fkey");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany()
                .HasForeignKey(row => row.ContactChangedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("individuals_contact_changed_by_account_id_fkey");
            entity.HasOne<CustomerIndividualRow>().WithMany()
                .HasForeignKey(row => new { row.TenantId, row.RedirectTargetIndividualId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_individuals_redirect");
        });
        modelBuilder.Entity<CustomerIndividualCommandReceiptRow>(entity =>
        {
            entity.ToTable("individual_command_receipts", "customers", table =>
            {
                table.HasCheckConstraint("ck_customer_individual_receipts_operation", "operation IN ('create', 'availability', 'contact')");
                table.HasCheckConstraint("ck_customer_individual_receipts_key", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_customer_individual_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_customer_individual_receipts_name", "btrim(display_name) <> ''");
                table.HasCheckConstraint("ck_customer_individual_receipts_availability", "availability IN (1, 2)");
                table.HasCheckConstraint("ck_customer_individual_receipts_revision", "revision > 0");
                table.HasCheckConstraint("ck_customer_individual_receipts_change_pair",
                    "(availability_changed_by_account_id IS NULL) = (availability_changed_at IS NULL)");
                table.HasCheckConstraint("ck_customer_individual_receipts_contact_change_pair",
                    "(contact_changed_by_account_id IS NULL) = (contact_changed_at IS NULL)");
            });
            entity.HasKey(row => new { row.TenantId, row.AccountId, row.Operation, row.IdempotencyKey })
                .HasName("pk_customer_individual_command_receipts");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.AccountId).HasColumnName("account_id");
            entity.Property(row => row.Operation).HasMaxLength(16).HasColumnName("operation");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).HasColumnName("idempotency_key");
            entity.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength().HasColumnName("fingerprint");
            entity.Property(row => row.IndividualId).HasColumnName("individual_id");
            entity.Property(row => row.DisplayName).HasMaxLength(200).HasColumnName("display_name");
            entity.Property(row => row.Email).HasMaxLength(254).HasColumnName("email");
            entity.Property(row => row.Phone).HasMaxLength(32).HasColumnName("phone");
            entity.Property(row => row.Availability).HasColumnName("availability");
            entity.Property(row => row.Revision).HasColumnName("revision");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.AvailabilityChangedByAccountId).HasColumnName("availability_changed_by_account_id");
            entity.Property(row => row.AvailabilityChangedAt).HasColumnName("availability_changed_at");
            entity.Property(row => row.ContactChangedByAccountId).HasColumnName("contact_changed_by_account_id");
            entity.Property(row => row.ContactChangedAt).HasColumnName("contact_changed_at");
            entity.HasIndex(row => new { row.TenantId, row.IndividualId })
                .HasDatabaseName("ix_customer_individual_receipts_individual");
            entity.HasOne<CustomerIndividualRow>().WithMany()
                .HasForeignKey(row => new { row.TenantId, row.IndividualId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_individual_receipts_individual");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("individual_receipts_account_id_fkey");
        });
    }
}
