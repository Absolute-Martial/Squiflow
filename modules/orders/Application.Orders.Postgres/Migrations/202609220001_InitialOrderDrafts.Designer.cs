using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Orders.Postgres.Migrations;

[DbContext(typeof(OrderDbContext))]
[Migration("202609220001_InitialOrderDrafts")]
partial class InitialOrderDrafts
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        OrderModel.Build(modelBuilder);
    }
}
