using Microsoft.EntityFrameworkCore;

namespace Application.Customers.Postgres;

// This frozen model is the target of the first Customers migration and its snapshot.
internal static class CustomerModelV202609240001
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("customers");
        modelBuilder.Entity<CustomerTenantReferenceRow>(entity =>
        {
            entity.ToTable("tenants", "tenancy", table => table.ExcludeFromMigrations());
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
        });
        modelBuilder.Entity<CustomerAccountReferenceRow>(entity =>
        {
            entity.ToTable("accounts", "identity_access", table => table.ExcludeFromMigrations());
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
        });
        modelBuilder.Entity<CustomerOrganizationRow>(entity =>
        {
            entity.ToTable("organizations", table =>
                table.HasCheckConstraint("ck_customer_organizations_name", "btrim(display_name) <> ''"));
            entity.HasKey(row => new { row.TenantId, row.Id }).HasName("pk_customer_organizations");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.DisplayName).HasMaxLength(200).HasColumnName("display_name");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(row => row.Id).IsUnique().HasDatabaseName("ux_customer_organizations_id");
            entity.HasIndex(row => new { row.TenantId, row.CreatedAt, row.Id })
                .IsDescending(false, true, true).HasDatabaseName("ix_customer_organizations_browse");
            entity.HasOne<CustomerTenantReferenceRow>().WithMany().HasForeignKey(row => row.TenantId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("organizations_tenant_id_fkey");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.CreatedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("organizations_created_by_account_id_fkey");
        });
        modelBuilder.Entity<CustomerProgramRow>(entity =>
        {
            entity.ToTable("programs", table =>
                table.HasCheckConstraint("ck_customer_programs_name", "btrim(display_name) <> ''"));
            entity.HasKey(row => new { row.TenantId, row.Id }).HasName("pk_customer_programs");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.OrganizationId).HasColumnName("organization_id");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.DisplayName).HasMaxLength(200).HasColumnName("display_name");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(row => row.Id).IsUnique().HasDatabaseName("ux_customer_programs_id");
            entity.HasIndex(row => new { row.TenantId, row.OrganizationId, row.Id })
                .IsUnique().HasDatabaseName("ux_customer_programs_organization_identity");
            entity.HasIndex(row => new { row.TenantId, row.OrganizationId, row.CreatedAt, row.Id })
                .IsDescending(false, false, true, true).HasDatabaseName("ix_customer_programs_browse");
            entity.HasOne<CustomerOrganizationRow>().WithMany()
                .HasForeignKey(row => new { row.TenantId, row.OrganizationId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_programs_organization");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.CreatedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("programs_created_by_account_id_fkey");
        });
        modelBuilder.Entity<OrganizationReceiptRow>(entity =>
        {
            entity.ToTable("organization_receipts", table =>
            {
                table.HasCheckConstraint("ck_customer_organization_receipts_key", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_customer_organization_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
            });
            entity.HasKey(row => new { row.TenantId, row.AccountId, row.IdempotencyKey })
                .HasName("pk_customer_organization_receipts");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.AccountId).HasColumnName("account_id");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).HasColumnName("idempotency_key");
            entity.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength().HasColumnName("fingerprint");
            entity.Property(row => row.OrganizationId).HasColumnName("organization_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(row => new { row.TenantId, row.OrganizationId })
                .HasDatabaseName("ix_customer_organization_receipts_organization");
            entity.HasOne<CustomerOrganizationRow>().WithMany()
                .HasForeignKey(row => new { row.TenantId, row.OrganizationId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_organization_receipts_organization");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("organization_receipts_account_id_fkey");
        });
        modelBuilder.Entity<ProgramReceiptRow>(entity =>
        {
            entity.ToTable("program_receipts", table =>
            {
                table.HasCheckConstraint("ck_customer_program_receipts_key", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_customer_program_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
            });
            entity.HasKey(row => new { row.TenantId, row.AccountId, row.IdempotencyKey })
                .HasName("pk_customer_program_receipts");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.AccountId).HasColumnName("account_id");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).HasColumnName("idempotency_key");
            entity.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength().HasColumnName("fingerprint");
            entity.Property(row => row.ProgramId).HasColumnName("program_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(row => new { row.TenantId, row.ProgramId })
                .HasDatabaseName("ix_customer_program_receipts_program");
            entity.HasOne<CustomerProgramRow>().WithMany()
                .HasForeignKey(row => new { row.TenantId, row.ProgramId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_customer_program_receipts_program");
            entity.HasOne<CustomerAccountReferenceRow>().WithMany().HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("program_receipts_account_id_fkey");
        });
    }
}
