using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.IdentityAccess.Postgres.Tests;

public sealed class IdentityAccessModelSnapshotTests
{
    [Fact]
    public void RuntimeModelMatchesTheLatestMigrationSnapshotWithoutDatabaseAccess()
    {
        using var context = IdentityAccessPostgresMigrations.CreateContext(
            "Host=127.0.0.1;Port=1;Database=model_snapshot_guard;Username=guard;Password=guard");

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
