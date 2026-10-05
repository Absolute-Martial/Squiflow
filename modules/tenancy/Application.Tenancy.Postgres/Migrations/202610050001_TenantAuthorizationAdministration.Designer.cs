using Microsoft.EntityFrameworkCore;

#nullable disable

namespace Application.Tenancy.Postgres.Migrations;

public partial class TenantAuthorizationAdministration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        TenancyModelV202610030004.Build(modelBuilder);
        TenantProvisioningReceiptModelV202610030002.Build(modelBuilder);
        MembershipLifecycleReceiptModelV202610030004.Build(modelBuilder);
    }
}
