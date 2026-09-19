using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquiFlow.IdentityAccess.Postgres.Migrations;

[DbContext(typeof(IdentityAccessDbContext))]
[Migration("202609170001_InitialAccountBindings")]
partial class InitialAccountBindings
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        IdentityAccessModel.Build(modelBuilder);
    }
}
