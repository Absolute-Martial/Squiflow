using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Tenancy.Postgres.Migrations;

[DbContext(typeof(TenancyDbContext))]
[Migration("202610030002_TenantProvisioning")]
public partial class TenantProvisioning
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        TenancyModel.Build(modelBuilder);
        TenantProvisioningReceiptModelV202610030002.Build(modelBuilder);
    }
}
