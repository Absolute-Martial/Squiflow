using Microsoft.EntityFrameworkCore;

namespace Application.Orders.Postgres;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options)
    : DbContext(options)
{
    internal DbSet<OrderDraftRow> OrderDrafts => Set<OrderDraftRow>();

    internal DbSet<OrderDraftLineRow> OrderDraftLines => Set<OrderDraftLineRow>();

    internal DbSet<OrderCommandReceiptRow> CommandReceipts => Set<OrderCommandReceiptRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("orders");

        modelBuilder.Entity<TenantReferenceRow>(entity =>
        {
            entity.ToTable("tenants", "tenancy", table => table.ExcludeFromMigrations());
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
        });

        modelBuilder.Entity<AccountReferenceRow>(entity =>
        {
            entity.ToTable("accounts", "identity_access", table => table.ExcludeFromMigrations());
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
        });

        modelBuilder.Entity<OrderDraftRow>(entity =>
        {
            entity.ToTable("order_drafts", table =>
            {
                table.HasCheckConstraint("ck_order_drafts_summary_not_blank", "btrim(summary) <> ''");
                table.HasCheckConstraint("ck_order_drafts_currency_code", "currency_code ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("ck_order_drafts_total", "total >= 0");
                table.HasCheckConstraint("ck_order_drafts_revision", "revision > 0");
                table.HasCheckConstraint(
                    "ck_order_drafts_lifecycle",
                    "(state = 'draft' AND revision = 1 AND abandoned_at IS NULL AND abandoned_by_account_id IS NULL) OR " +
                    "(state = 'abandoned' AND revision = 2 AND abandoned_at IS NOT NULL AND abandoned_by_account_id IS NOT NULL)");
            });
            entity.HasKey(row => new { row.TenantId, row.Id }).HasName("pk_order_drafts");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.CreatedByAccountId).HasColumnName("created_by_account_id");
            entity.Property(row => row.Summary).HasMaxLength(200).HasColumnName("summary");
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsFixedLength().HasColumnName("currency_code");
            entity.Property(row => row.Total).HasPrecision(19, 4).HasColumnName("total");
            entity.Property(row => row.Revision).HasColumnName("revision");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.State).HasMaxLength(16).HasDefaultValue("draft").HasColumnName("state");
            entity.Property(row => row.AbandonedAt).HasColumnName("abandoned_at");
            entity.Property(row => row.AbandonedByAccountId).HasColumnName("abandoned_by_account_id");
            entity.HasIndex(row => new { row.TenantId, row.CreatedAt, row.Id })
                .IsDescending(false, true, true)
                .HasDatabaseName("ix_order_drafts_tenant_created_at_id");
            entity.HasIndex(row => row.Id).IsUnique().HasDatabaseName("ux_order_drafts_id");
            entity.HasOne<TenantReferenceRow>()
                .WithMany()
                .HasForeignKey(row => row.TenantId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_order_drafts_tenants_tenant_id");
            entity.HasOne<AccountReferenceRow>()
                .WithMany()
                .HasForeignKey(row => row.CreatedByAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_order_drafts_accounts_created_by_account_id");
            entity.HasOne<AccountReferenceRow>()
                .WithMany()
                .HasForeignKey(row => row.AbandonedByAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_order_drafts_accounts_abandoned_by_account_id");
        });

        modelBuilder.Entity<OrderDraftLineRow>(entity =>
        {
            entity.ToTable("order_draft_lines", table =>
            {
                table.HasCheckConstraint("ck_order_draft_lines_position", "position BETWEEN 1 AND 100");
                table.HasCheckConstraint("ck_order_draft_lines_description_not_blank", "btrim(description) <> ''");
                table.HasCheckConstraint("ck_order_draft_lines_quantity", "quantity > 0");
                table.HasCheckConstraint("ck_order_draft_lines_unit_code", "unit_code ~ '^[A-Z0-9]{1,16}$'");
                table.HasCheckConstraint("ck_order_draft_lines_unit_price", "unit_price >= 0");
                table.HasCheckConstraint("ck_order_draft_lines_line_total", "line_total >= 0");
            });
            entity.HasKey(row => new { row.TenantId, row.OrderId, row.Position })
                .HasName("pk_order_draft_lines");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.OrderId).HasColumnName("order_id");
            entity.Property(row => row.Position).HasColumnName("position");
            entity.Property(row => row.Description).HasMaxLength(300).HasColumnName("description");
            entity.Property(row => row.Quantity).HasPrecision(19, 4).HasColumnName("quantity");
            entity.Property(row => row.UnitCode).HasMaxLength(16).HasColumnName("unit_code");
            entity.Property(row => row.UnitPrice).HasPrecision(19, 4).HasColumnName("unit_price");
            entity.Property(row => row.LineTotal).HasPrecision(19, 4).HasColumnName("line_total");
            entity.HasOne<OrderDraftRow>()
                .WithMany()
                .HasForeignKey(row => new { row.TenantId, row.OrderId })
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_order_draft_lines_order_drafts");
        });

        modelBuilder.Entity<OrderCommandReceiptRow>(entity =>
        {
            entity.ToTable("command_receipts", table =>
            {
                table.HasCheckConstraint("ck_command_receipts_operation_not_blank", "btrim(operation) <> ''");
                table.HasCheckConstraint("ck_command_receipts_idempotency_key_not_blank", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_command_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_command_receipts_response_object", "jsonb_typeof(response_json) = 'object'");
            });
            entity.HasKey(row => new { row.TenantId, row.AccountId, row.Operation, row.IdempotencyKey })
                .HasName("pk_command_receipts");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.AccountId).HasColumnName("account_id");
            entity.Property(row => row.Operation).HasMaxLength(80).HasColumnName("operation");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).HasColumnName("idempotency_key");
            entity.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength().HasColumnName("fingerprint");
            entity.Property(row => row.OrderId).HasColumnName("order_id");
            entity.Property(row => row.ResponseJson).HasColumnType("jsonb").HasColumnName("response_json");
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.HasOne<AccountReferenceRow>()
                .WithMany()
                .HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_command_receipts_accounts_account_id");
            entity.HasOne<OrderDraftRow>()
                .WithMany()
                .HasForeignKey(row => new { row.TenantId, row.OrderId })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_command_receipts_order_drafts");
        });
    }
}

internal sealed class OrderDraftRow
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string State { get; set; } = "draft";
    public DateTimeOffset? AbandonedAt { get; set; }
    public Guid? AbandonedByAccountId { get; set; }
}

internal sealed class OrderDraftLineRow
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public int Position { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

internal sealed class OrderCommandReceiptRow
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string ResponseJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

// These are relationship-only mappings. The owning modules keep the referenced rows private.
internal sealed class TenantReferenceRow
{
    public Guid Id { get; set; }
}

internal sealed class AccountReferenceRow
{
    public Guid Id { get; set; }
}
