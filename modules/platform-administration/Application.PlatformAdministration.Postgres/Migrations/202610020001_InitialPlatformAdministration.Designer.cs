using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.PlatformAdministration.Postgres.Migrations;

[DbContext(typeof(PlatformAdministrationDbContext))]
[Migration("202610020001_InitialPlatformAdministration")]
partial class InitialPlatformAdministration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        PlatformAdministrationModel.Build(modelBuilder);
    }
}
