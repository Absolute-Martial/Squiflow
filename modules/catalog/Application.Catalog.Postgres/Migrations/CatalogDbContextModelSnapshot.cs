using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Application.Catalog.Postgres.Migrations;

[DbContext(typeof(CatalogDbContext))]
sealed partial class CatalogDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        CatalogModelV202610060002.Build(modelBuilder);
#pragma warning restore 612, 618
    }
}
