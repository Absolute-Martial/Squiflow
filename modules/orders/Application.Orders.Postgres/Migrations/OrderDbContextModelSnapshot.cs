using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

[DbContext(typeof(OrderDbContext))]
partial class OrderDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        OrderModel.Build(modelBuilder);
    }
}

internal static class OrderModel
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.8");
        modelBuilder.HasDefaultSchema("orders");

        modelBuilder.Entity("Application.Orders.Postgres.AccountReferenceRow", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            entity.HasKey("Id");
            entity.ToTable("accounts", "identity_access", table => table.ExcludeFromMigrations());
        });

        modelBuilder.Entity("Application.Orders.Postgres.TenantReferenceRow", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            entity.HasKey("Id");
            entity.ToTable("tenants", "tenancy", table => table.ExcludeFromMigrations());
        });

        modelBuilder.Entity("Application.Orders.Postgres.OrderDraftRow", entity =>
        {
            entity.Property<Guid>("TenantId").HasColumnType("uuid").HasColumnName("tenant_id");
            entity.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            entity.Property<Guid>("CreatedByAccountId")
                .HasColumnType("uuid")
                .HasColumnName("created_by_account_id");
            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property<string>("CurrencyCode")
                .IsRequired()
                .IsFixedLength()
                .HasMaxLength(3)
                .HasColumnType("character(3)")
                .HasColumnName("currency_code");
            entity.Property<long>("Revision").HasColumnType("bigint").HasColumnName("revision");
            entity.Property<string>("Summary")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)")
                .HasColumnName("summary");
            entity.Property<decimal>("Total")
                .HasPrecision(19, 4)
                .HasColumnType("numeric(19,4)")
                .HasColumnName("total");
            entity.HasKey("TenantId", "Id").HasName("pk_order_drafts");
            entity.HasIndex("CreatedByAccountId");
            entity.HasIndex("Id").IsUnique().HasDatabaseName("ux_order_drafts_id");
            entity.ToTable("order_drafts", "orders", table =>
            {
                table.HasCheckConstraint("ck_order_drafts_summary_not_blank", "btrim(summary) <> ''");
                table.HasCheckConstraint("ck_order_drafts_currency_code", "currency_code ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("ck_order_drafts_total", "total >= 0");
                table.HasCheckConstraint("ck_order_drafts_revision", "revision > 0");
            });
        });

        modelBuilder.Entity("Application.Orders.Postgres.OrderDraftLineRow", entity =>
        {
            entity.Property<Guid>("TenantId").HasColumnType("uuid").HasColumnName("tenant_id");
            entity.Property<Guid>("OrderId").HasColumnType("uuid").HasColumnName("order_id");
            entity.Property<int>("Position").HasColumnType("integer").HasColumnName("position");
            entity.Property<string>("Description")
                .IsRequired()
                .HasMaxLength(300)
                .HasColumnType("character varying(300)")
                .HasColumnName("description");
            entity.Property<decimal>("LineTotal")
                .HasPrecision(19, 4)
                .HasColumnType("numeric(19,4)")
                .HasColumnName("line_total");
            entity.Property<decimal>("Quantity")
                .HasPrecision(19, 4)
                .HasColumnType("numeric(19,4)")
                .HasColumnName("quantity");
            entity.Property<decimal>("UnitPrice")
                .HasPrecision(19, 4)
                .HasColumnType("numeric(19,4)")
                .HasColumnName("unit_price");
            entity.Property<string>("UnitCode")
                .IsRequired()
                .HasMaxLength(16)
                .HasColumnType("character varying(16)")
                .HasColumnName("unit_code");
            entity.HasKey("TenantId", "OrderId", "Position").HasName("pk_order_draft_lines");
            entity.ToTable("order_draft_lines", "orders", table =>
            {
                table.HasCheckConstraint("ck_order_draft_lines_position", "position BETWEEN 1 AND 100");
                table.HasCheckConstraint("ck_order_draft_lines_description_not_blank", "btrim(description) <> ''");
                table.HasCheckConstraint("ck_order_draft_lines_quantity", "quantity > 0");
                table.HasCheckConstraint("ck_order_draft_lines_unit_code", "unit_code ~ '^[A-Z0-9]{1,16}$'");
                table.HasCheckConstraint("ck_order_draft_lines_unit_price", "unit_price >= 0");
                table.HasCheckConstraint("ck_order_draft_lines_line_total", "line_total >= 0");
            });
        });

        modelBuilder.Entity("Application.Orders.Postgres.OrderCommandReceiptRow", entity =>
        {
            entity.Property<Guid>("TenantId").HasColumnType("uuid").HasColumnName("tenant_id");
            entity.Property<Guid>("AccountId").HasColumnType("uuid").HasColumnName("account_id");
            entity.Property<string>("Operation")
                .HasMaxLength(80)
                .HasColumnType("character varying(80)")
                .HasColumnName("operation");
            entity.Property<string>("IdempotencyKey")
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("idempotency_key");
            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property<string>("Fingerprint")
                .IsRequired()
                .IsFixedLength()
                .HasMaxLength(64)
                .HasColumnType("character(64)")
                .HasColumnName("fingerprint");
            entity.Property<Guid>("OrderId").HasColumnType("uuid").HasColumnName("order_id");
            entity.Property<string>("ResponseJson")
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("response_json");
            entity.HasKey("TenantId", "AccountId", "Operation", "IdempotencyKey").HasName("pk_command_receipts");
            entity.HasIndex("AccountId");
            entity.HasIndex("TenantId", "OrderId");
            entity.ToTable("command_receipts", "orders", table =>
            {
                table.HasCheckConstraint("ck_command_receipts_operation_not_blank", "btrim(operation) <> ''");
                table.HasCheckConstraint("ck_command_receipts_idempotency_key_not_blank", "btrim(idempotency_key) <> ''");
                table.HasCheckConstraint("ck_command_receipts_fingerprint", "fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_command_receipts_response_object", "jsonb_typeof(response_json) = 'object'");
            });
        });

        modelBuilder.Entity("Application.Orders.Postgres.OrderDraftRow", entity =>
        {
            entity.HasOne("Application.Orders.Postgres.AccountReferenceRow", null)
                .WithMany()
                .HasForeignKey("CreatedByAccountId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_order_drafts_accounts_created_by_account_id");
            entity.HasOne("Application.Orders.Postgres.TenantReferenceRow", null)
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_order_drafts_tenants_tenant_id");
        });

        modelBuilder.Entity("Application.Orders.Postgres.OrderDraftLineRow", entity =>
        {
            entity.HasOne("Application.Orders.Postgres.OrderDraftRow", null)
                .WithMany()
                .HasForeignKey("TenantId", "OrderId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired()
                .HasConstraintName("fk_order_draft_lines_order_drafts");
        });

        modelBuilder.Entity("Application.Orders.Postgres.OrderCommandReceiptRow", entity =>
        {
            entity.HasOne("Application.Orders.Postgres.AccountReferenceRow", null)
                .WithMany()
                .HasForeignKey("AccountId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_command_receipts_accounts_account_id");
            entity.HasOne("Application.Orders.Postgres.OrderDraftRow", null)
                .WithMany()
                .HasForeignKey("TenantId", "OrderId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_command_receipts_order_drafts");
        });
    }
}
