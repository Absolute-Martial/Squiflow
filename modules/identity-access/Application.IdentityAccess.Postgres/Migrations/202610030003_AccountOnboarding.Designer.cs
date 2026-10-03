using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.IdentityAccess.Postgres.Migrations;

[DbContext(typeof(IdentityAccessDbContext))]
[Migration("202610030003_AccountOnboarding")]
partial class AccountOnboarding
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        IdentityAccessModel.BuildOnboarding(modelBuilder);
    }
}
