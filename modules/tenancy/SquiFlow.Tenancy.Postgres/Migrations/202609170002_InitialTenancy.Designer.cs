using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquiFlow.Tenancy.Postgres.Migrations;

[DbContext(typeof(TenancyDbContext))]
[Migration("202609170002_InitialTenancy")]
partial class InitialTenancy
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        TenancyModel.Build(modelBuilder);
    }
}
