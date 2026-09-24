using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

[DbContext(typeof(OrderDbContext))]
[Migration("202609240002_OrderDraftRevision")]
partial class OrderDraftRevision
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        OrderModelV202609240001.Build(modelBuilder);
        modelBuilder.Entity("Application.Orders.Postgres.OrderDraftRow", entity =>
            entity.ToTable("order_drafts", "orders", table =>
                table.HasCheckConstraint(
                    "ck_order_drafts_lifecycle",
                    "(state = 'draft' AND revision >= 1 AND abandoned_at IS NULL AND abandoned_by_account_id IS NULL) OR " +
                    "(state = 'abandoned' AND revision >= 2 AND abandoned_at IS NOT NULL AND abandoned_by_account_id IS NOT NULL)")));
    }
}
