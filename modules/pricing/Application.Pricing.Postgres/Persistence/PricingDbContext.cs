using Microsoft.EntityFrameworkCore;

namespace Application.Pricing.Postgres;

public sealed class PricingDbContext(DbContextOptions<PricingDbContext> options) : DbContext(options)
{
    internal DbSet<PriceRevisionRow> PriceRevisions => Set<PriceRevisionRow>();

    internal DbSet<PricingCommandReceiptRow> CommandReceipts => Set<PricingCommandReceiptRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        PricingModelV202610060002.Build(modelBuilder);
    }
}

internal sealed class PriceRevisionRow
{
    public Guid TenantId { get; set; }
    public Guid RevisionId { get; set; }
    public long RevisionNumber { get; set; }
    public Guid ItemId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid PriceId { get; set; }
    public long? UnitConversionRevision { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string ScopeKind { get; set; } = string.Empty;
    public Guid ScopeId { get; set; }
    public decimal BaseUnitPrice { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string State { get; set; } = string.Empty;
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
}

internal sealed class PricingCommandReceiptRow
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class PricingTenantReferenceRow
{
    public Guid Id { get; set; }
}

internal sealed class PricingAccountReferenceRow
{
    public Guid Id { get; set; }
}

internal static class PricingModelV202610060001
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("pricing");

        modelBuilder.Entity<PricingTenantReferenceRow>(entity =>
        {
            entity.ToTable("tenants", "tenancy", table => table.ExcludeFromMigrations());
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
        });

        modelBuilder.Entity<PricingAccountReferenceRow>(entity =>
        {
            entity.ToTable("accounts", "identity_access", table => table.ExcludeFromMigrations());
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
        });

        modelBuilder.Entity<PriceRevisionRow>(entity =>
        {
            entity.ToTable("price_revisions", table =>
            {
                table.HasCheckConstraint("ck_pricing_price_revisions_unit_code", "unit_code ~ '^[A-Z0-9_]{1,32}$'");
                table.HasCheckConstraint("ck_pricing_price_revisions_currency_code", "currency_code ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("ck_pricing_price_revisions_scope_kind", "scope_kind IN ('committed_quotation', 'committed_agreement', 'customer', 'program', 'organization', 'wholesale', 'default')");
                table.HasCheckConstraint("ck_pricing_price_revisions_base_price", "base_unit_price >= 0 AND base_unit_price <= 999999999999999.9999");
                table.HasCheckConstraint("ck_pricing_price_revisions_validity", "valid_to IS NULL OR valid_to > valid_from");
                table.HasCheckConstraint("ck_pricing_price_revisions_state", "state IN ('draft', 'published', 'superseded', 'retired')");
                table.HasCheckConstraint("ck_pricing_price_revisions_scope_id", "(scope_kind = 'default' AND scope_id = '00000000-0000-0000-0000-000000000000') OR (scope_kind <> 'default' AND scope_id <> '00000000-0000-0000-0000-000000000000')");
                table.HasCheckConstraint(
                    "ck_pricing_price_revisions_lifecycle",
                    "(state = 'draft' AND published_at IS NULL AND retired_at IS NULL) OR " +
                    "(state = 'published' AND published_at IS NOT NULL AND retired_at IS NULL) OR " +
                    "(state = 'superseded' AND published_at IS NOT NULL AND retired_at IS NULL) OR " +
                    "(state = 'retired' AND published_at IS NOT NULL AND retired_at IS NOT NULL)");
            });
            entity.HasKey(row => new { row.TenantId, row.RevisionId }).HasName("pk_pricing_price_revisions");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.RevisionId).HasColumnName("revision_id");
            entity.Property(row => row.RevisionNumber).HasColumnName("revision_number")
                .HasDefaultValueSql("nextval('pricing.price_revision_number_seq')");
            entity.Property(row => row.ItemId).HasColumnName("item_id");
            entity.Property(row => row.UnitCode).HasMaxLength(32).IsRequired().HasColumnName("unit_code");
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired().HasColumnName("currency_code");
            entity.Property(row => row.ScopeKind).HasMaxLength(32).IsRequired().HasColumnName("scope_kind");
            entity.Property(row => row.ScopeId).HasColumnName("scope_id");
            entity.Property(row => row.BaseUnitPrice).HasPrecision(19, 4).HasColumnName("base_unit_price");
            entity.Property(row => row.ValidFrom).HasColumnName("valid_from");
            entity.Property(row => row.ValidTo).HasColumnName("valid_to");
            entity.Property(row => row.State).HasMaxLength(16).IsRequired().HasColumnName("state");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.PublishedAt).HasColumnName("published_at");
            entity.Property(row => row.RetiredAt).HasColumnName("retired_at");
            entity.HasIndex(row => new { row.TenantId, row.ItemId, row.UnitCode, row.CurrencyCode, row.ScopeKind, row.ScopeId })
                .HasDatabaseName("ix_pricing_price_revisions_lookup");
            entity.HasIndex(row => new { row.TenantId, row.RevisionNumber }).IsUnique()
                .HasDatabaseName("ux_pricing_price_revisions_tenant_revision");
            entity.HasIndex(row => row.CreatedByAccountId).HasDatabaseName("IX_price_revisions_created_by_account_id");
            entity.HasOne<PricingTenantReferenceRow>().WithMany().HasForeignKey(row => row.TenantId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pricing_price_revisions_tenants");
            entity.HasOne<PricingAccountReferenceRow>().WithMany().HasForeignKey(row => row.CreatedByAccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pricing_price_revisions_accounts_created_by");
        });

        modelBuilder.Entity<PricingCommandReceiptRow>(entity =>
        {
            entity.ToTable("command_receipts", table =>
            {
                table.HasCheckConstraint("ck_pricing_receipts_operation", "btrim(operation) <> ''");
                table.HasCheckConstraint("ck_pricing_receipts_idempotency_key", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_pricing_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_pricing_receipts_response_object", "jsonb_typeof(response_json) = 'object'");
            });
            entity.HasKey(row => new { row.TenantId, row.AccountId, row.Operation, row.IdempotencyKey })
                .HasName("pk_pricing_command_receipts");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.AccountId).HasColumnName("account_id");
            entity.Property(row => row.Operation).HasMaxLength(80).IsRequired().HasColumnName("operation");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).IsRequired().HasColumnName("idempotency_key");
            entity.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength().IsRequired().HasColumnName("fingerprint");
            entity.Property(row => row.ResponseJson).HasColumnType("jsonb").IsRequired().HasColumnName("response_json");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(row => row.AccountId).HasDatabaseName("IX_command_receipts_account_id");
            entity.HasOne<PricingAccountReferenceRow>().WithMany().HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pricing_receipts_accounts");
        });
    }
}
