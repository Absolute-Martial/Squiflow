using Microsoft.EntityFrameworkCore;

namespace Application.Pricing.Postgres;

internal sealed class PricingPolicyRow
{
    public Guid TenantId { get; set; }
    public long Revision { get; set; }
    public decimal MinimumUnitPrice { get; set; }
    public decimal MaximumUnitPrice { get; set; }
    public decimal? MaximumDecreasePercent { get; set; }
    public decimal? MaximumIncreasePercent { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal static class PricingModelV202610060002
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        PricingModelV202610060001.Build(modelBuilder);
        modelBuilder.Entity<PriceRevisionRow>(entity =>
        {
            entity.Property(row => row.UnitCode).HasMaxLength(64);
            entity.ToTable("price_revisions", table => table.HasCheckConstraint("ck_pricing_price_revisions_unit_code", "unit_code ~ '^[A-Z0-9_-]{1,64}$'"));
            entity.Property(row => row.UnitId).HasColumnName("unit_id");
            entity.Property(row => row.PriceId).HasColumnName("price_id");
            entity.Property(row => row.UnitConversionRevision).HasColumnName("unit_conversion_revision");
            entity.HasIndex(row => new { row.TenantId, row.PriceId, row.RevisionNumber }).HasDatabaseName("ix_pricing_family");
            entity.HasIndex(row => new { row.TenantId, row.ItemId, row.UnitId, row.CurrencyCode, row.ScopeKind, row.ScopeId, row.ValidFrom, row.RevisionNumber })
                .HasDatabaseName("ix_pricing_stable_lookup");
        });
        modelBuilder.Entity<PricingPolicyRow>(entity =>
        {
            entity.ToTable("override_policies");
            entity.HasKey(row => new { row.TenantId, row.Revision });
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Revision).HasColumnName("revision");
            entity.Property(row => row.MinimumUnitPrice).HasColumnName("minimum_unit_price").HasPrecision(19, 4);
            entity.Property(row => row.MaximumUnitPrice).HasColumnName("maximum_unit_price").HasPrecision(19, 4);
            entity.Property(row => row.MaximumDecreasePercent).HasColumnName("maximum_decrease_percent").HasPrecision(9, 4);
            entity.Property(row => row.MaximumIncreasePercent).HasColumnName("maximum_increase_percent").HasPrecision(9, 4);
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
        });
    }
}
