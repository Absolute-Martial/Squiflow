using Microsoft.EntityFrameworkCore;

namespace Application.Catalog.Postgres;

internal sealed class CatalogConversionRow
{
    public Guid TenantId { get; set; }
    public Guid SourceUnitId { get; set; }
    public Guid TargetUnitId { get; set; }
    public long Revision { get; set; }
    public decimal Numerator { get; set; }
    public decimal Denominator { get; set; }
    public Guid PublishedByAccountId { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}

internal static class CatalogModelV202610060002
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        CatalogModelV202610060001.Build(modelBuilder);
        modelBuilder.Entity<CatalogUnitRow>().ToTable("units", "catalog", table =>
            table.HasCheckConstraint("ck_catalog_units_revision", "revision > 0"));
        modelBuilder.Entity<CatalogItemRow>(entity =>
        {
            entity.Property<string>("Availability").HasMaxLength(16).HasColumnName("availability");
            entity.Property<DateTimeOffset?>("AvailabilityChangedAt").HasColumnName("availability_changed_at");
            entity.Property<Guid?>("AvailabilityChangedByAccountId").HasColumnName("availability_changed_by_account_id");
            entity.HasIndex("AvailabilityChangedByAccountId").HasDatabaseName("IX_items_availability_changed_by_account_id");
            entity.HasOne<CatalogAccountReferenceRow>().WithMany().HasForeignKey("AvailabilityChangedByAccountId")
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_items_accounts_availability_changed_by");
            entity.ToTable("items", "catalog", table =>
            {
                table.HasCheckConstraint("ck_catalog_items_revision", "revision > 0");
                table.HasCheckConstraint("ck_catalog_items_availability",
                    "(stock_mode = 'availability_only' AND availability IS NOT NULL AND availability IN ('available', 'unavailable')) OR " +
                    "(stock_mode <> 'availability_only' AND availability IS NULL)");
                table.HasCheckConstraint("ck_catalog_items_availability_audit",
                    "(availability_changed_at IS NULL) = (availability_changed_by_account_id IS NULL)");
            });
        });
        modelBuilder.Entity<CatalogConversionRow>(entity =>
        {
            entity.ToTable("unit_conversions", "catalog", table =>
            {
                table.HasCheckConstraint("ck_catalog_conversions_pair", "source_unit_id <> target_unit_id");
                table.HasCheckConstraint("ck_catalog_conversions_revision", "revision > 0");
                table.HasCheckConstraint("ck_catalog_conversions_ratio", "numerator > 0 AND denominator > 0");
            });
            entity.HasKey(row => new { row.TenantId, row.SourceUnitId, row.TargetUnitId, row.Revision })
                .HasName("pk_catalog_unit_conversions");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.SourceUnitId).HasColumnName("source_unit_id");
            entity.Property(row => row.TargetUnitId).HasColumnName("target_unit_id");
            entity.Property(row => row.Revision).HasColumnName("revision");
            entity.Property(row => row.Numerator).HasPrecision(19, 9).HasColumnName("numerator");
            entity.Property(row => row.Denominator).HasPrecision(19, 9).HasColumnName("denominator");
            entity.Property(row => row.PublishedByAccountId).HasColumnName("published_by_account_id");
            entity.Property(row => row.PublishedAt).HasColumnName("published_at");
            entity.HasIndex(row => row.PublishedByAccountId).HasDatabaseName("IX_unit_conversions_published_by_account_id");
            entity.HasIndex(row => new { row.TenantId, row.TargetUnitId })
                .HasDatabaseName("IX_unit_conversions_tenant_id_target_unit_id");
            entity.HasOne<CatalogUnitRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.SourceUnitId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_conversions_units_source");
            entity.HasOne<CatalogUnitRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.TargetUnitId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_conversions_units_target");
            entity.HasOne<CatalogAccountReferenceRow>().WithMany().HasForeignKey(row => row.PublishedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_catalog_conversions_accounts_published_by");
        });
    }
}
