using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Application.Catalog.Postgres;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        CatalogModelV202610060002.Build(modelBuilder);
    }
}

internal sealed class CatalogUnitRow
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Precision { get; set; }
    public string Status { get; set; } = "active";
    public long Revision { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public Guid? RetiredByAccountId { get; set; }
}

internal sealed class CatalogItemRow
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Kind { get; set; } = "product";
    public string Status { get; set; } = "active";
    public Guid BaseUnitId { get; set; }
    public string StockMode { get; set; } = "non_stock";
    public long Revision { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public Guid? RetiredByAccountId { get; set; }
}

internal sealed class CatalogCommandReceiptRow
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class CatalogTenantReferenceRow
{
    public Guid Id { get; set; }
}

internal sealed class CatalogAccountReferenceRow
{
    public Guid Id { get; set; }
}

internal static class CatalogModelV202610060001
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasDefaultSchema("catalog")
            .HasAnnotation("ProductVersion", "10.0.8")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity<CatalogTenantReferenceRow>(entity =>
        {
            entity.ToTable("tenants", "tenancy", table => table.ExcludeFromMigrations());
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
        });

        modelBuilder.Entity<CatalogAccountReferenceRow>(entity =>
        {
            entity.ToTable("accounts", "identity_access", table => table.ExcludeFromMigrations());
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
        });

        modelBuilder.Entity<CatalogUnitRow>(entity =>
        {
            entity.ToTable("units", "catalog", table =>
            {
                table.HasCheckConstraint("ck_catalog_units_code", "code ~ '^[A-Z0-9_-]{1,64}$'");
                table.HasCheckConstraint("ck_catalog_units_name_not_blank", "btrim(name) <> ''");
                table.HasCheckConstraint("ck_catalog_units_precision", "precision BETWEEN 0 AND 9");
                table.HasCheckConstraint("ck_catalog_units_status", "status IN ('active', 'retired')");
                table.HasCheckConstraint(
                    "ck_catalog_units_retirement",
                    "(status = 'active' AND retired_at IS NULL AND retired_by_account_id IS NULL) OR " +
                    "(status = 'retired' AND retired_at IS NOT NULL AND retired_by_account_id IS NOT NULL)");
            });
            entity.HasKey(row => new { row.TenantId, row.Id }).HasName("pk_catalog_units");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired().HasColumnName("code");
            entity.Property(row => row.Name).HasMaxLength(200).IsRequired().HasColumnName("name");
            entity.Property(row => row.Precision).HasColumnName("precision").HasColumnType("smallint");
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired().HasColumnName("status");
            entity.Property(row => row.Revision).HasColumnName("revision");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.RetiredAt).HasColumnName("retired_at");
            entity.Property(row => row.RetiredByAccountId).HasColumnName("retired_by_account_id");
            entity.HasIndex(row => new { row.TenantId, row.Code }).IsUnique().HasDatabaseName("ux_catalog_units_tenant_code");
            entity.HasIndex(row => new { row.TenantId, row.CreatedAt, row.Id })
                .IsDescending(false, true, true).HasDatabaseName("ix_catalog_units_tenant_created_at_id");
            entity.HasIndex(row => row.CreatedByAccountId).HasDatabaseName("IX_units_created_by_account_id");
            entity.HasIndex(row => row.RetiredByAccountId).HasDatabaseName("IX_units_retired_by_account_id");
            entity.HasOne<CatalogTenantReferenceRow>().WithMany().HasForeignKey(row => row.TenantId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_units_tenants_tenant_id");
            entity.HasOne<CatalogAccountReferenceRow>().WithMany().HasForeignKey(row => row.CreatedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_units_accounts_created_by_account_id");
            entity.HasOne<CatalogAccountReferenceRow>().WithMany().HasForeignKey(row => row.RetiredByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_units_accounts_retired_by_account_id");
        });

        modelBuilder.Entity<CatalogItemRow>(entity =>
        {
            entity.ToTable("items", "catalog", table =>
            {
                table.HasCheckConstraint("ck_catalog_items_code", "code IS NULL OR code ~ '^[A-Z0-9_-]{1,64}$'");
                table.HasCheckConstraint("ck_catalog_items_name_not_blank", "btrim(name) <> ''");
                table.HasCheckConstraint("ck_catalog_items_kind", "kind IN ('product', 'service')");
                table.HasCheckConstraint("ck_catalog_items_status", "status IN ('active', 'retired')");
                table.HasCheckConstraint("ck_catalog_items_stock_mode", "stock_mode IN ('precise_stock', 'availability_only', 'non_stock')");
                table.HasCheckConstraint(
                    "ck_catalog_items_retirement",
                    "(status = 'active' AND retired_at IS NULL AND retired_by_account_id IS NULL) OR " +
                    "(status = 'retired' AND retired_at IS NOT NULL AND retired_by_account_id IS NOT NULL)");
            });
            entity.HasKey(row => new { row.TenantId, row.Id }).HasName("pk_catalog_items");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Code).HasMaxLength(64).HasColumnName("code");
            entity.Property(row => row.Name).HasMaxLength(200).IsRequired().HasColumnName("name");
            entity.Property(row => row.Description).HasMaxLength(1000).HasColumnName("description");
            entity.Property(row => row.Kind).HasMaxLength(16).IsRequired().HasColumnName("kind");
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired().HasColumnName("status");
            entity.Property(row => row.BaseUnitId).HasColumnName("base_unit_id");
            entity.Property(row => row.StockMode).HasMaxLength(32).IsRequired().HasColumnName("stock_mode");
            entity.Property(row => row.Revision).HasColumnName("revision");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.RetiredAt).HasColumnName("retired_at");
            entity.Property(row => row.RetiredByAccountId).HasColumnName("retired_by_account_id");
            entity.HasIndex(row => new { row.TenantId, row.Code }).IsUnique()
                .HasDatabaseName("ux_catalog_items_tenant_code").HasFilter("code IS NOT NULL");
            entity.HasIndex(row => new { row.TenantId, row.CreatedAt, row.Id })
                .IsDescending(false, true, true).HasDatabaseName("ix_catalog_items_tenant_created_at_id");
            entity.HasIndex(row => row.CreatedByAccountId).HasDatabaseName("IX_items_created_by_account_id");
            entity.HasIndex(row => row.RetiredByAccountId).HasDatabaseName("IX_items_retired_by_account_id");
            entity.HasIndex(row => new { row.TenantId, row.BaseUnitId }).HasDatabaseName("IX_items_tenant_id_base_unit_id");
            entity.HasOne<CatalogTenantReferenceRow>().WithMany().HasForeignKey(row => row.TenantId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_items_tenants_tenant_id");
            entity.HasOne<CatalogAccountReferenceRow>().WithMany().HasForeignKey(row => row.CreatedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_items_accounts_created_by_account_id");
            entity.HasOne<CatalogAccountReferenceRow>().WithMany().HasForeignKey(row => row.RetiredByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_items_accounts_retired_by_account_id");
            entity.HasOne<CatalogUnitRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.BaseUnitId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_items_units_base_unit");
        });

        modelBuilder.Entity<CatalogCommandReceiptRow>(entity =>
        {
            entity.ToTable("command_receipts", "catalog", table =>
            {
                table.HasCheckConstraint("ck_catalog_receipts_operation", "btrim(operation) <> ''");
                table.HasCheckConstraint("ck_catalog_receipts_idempotency_key", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_catalog_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_catalog_receipts_response_object", "jsonb_typeof(response_json) = 'object'");
            });
            entity.HasKey(row => new { row.TenantId, row.AccountId, row.Operation, row.IdempotencyKey })
                .HasName("pk_catalog_command_receipts");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.AccountId).HasColumnName("account_id");
            entity.Property(row => row.Operation).HasMaxLength(80).IsRequired().HasColumnName("operation");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).IsRequired().HasColumnName("idempotency_key");
            entity.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength().IsRequired().HasColumnName("fingerprint");
            entity.Property(row => row.ResponseJson).HasColumnType("jsonb").IsRequired().HasColumnName("response_json");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(row => row.AccountId).HasDatabaseName("IX_command_receipts_account_id");
            entity.HasOne<CatalogAccountReferenceRow>().WithMany().HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_receipts_accounts_account_id");
        });
    }
}
