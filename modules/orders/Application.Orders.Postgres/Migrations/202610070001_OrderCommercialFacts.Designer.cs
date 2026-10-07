using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

[DbContext(typeof(OrderDbContext))]
[Migration("202610070001_OrderCommercialFacts")]
partial class OrderCommercialFacts
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => OrderModelV202610070001.Build(modelBuilder);
}

internal static class OrderModelV202610070001
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        OrderModelV202610030001.Build(modelBuilder);
        modelBuilder.Entity("Application.Orders.Postgres.OrderDraftLineRow", entity =>
        {
            entity.Property<string>("CommercialFacts").HasColumnType("jsonb").HasColumnName("commercial_facts");
            entity.Property<string>("UnitCode").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)").HasColumnName("unit_code");
            entity.ToTable("order_draft_lines", "orders", table => table.HasCheckConstraint(
                "ck_order_draft_lines_unit_code", "unit_code ~ '^[A-Z0-9_-]{1,64}$'"));
            entity.ToTable("order_draft_lines", "orders", table => table.HasCheckConstraint(
                "ck_order_draft_lines_commercial_facts", "commercial_facts IS NULL OR jsonb_typeof(commercial_facts) = 'object'"));
        });
    }
}
