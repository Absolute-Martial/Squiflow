using Microsoft.EntityFrameworkCore;
using Npgsql;
using Application.DatabaseMigrator;
using System.Diagnostics;
using Xunit;

namespace Application.IdentityAccess.Postgres.Tests;

public sealed class MigrationLifecycleTests : PostgresTestDatabase
{
    [Fact]
    public async Task MigratorListsAppliesAndRepeatsOwnedMigrations()
    {
        var runner = CreateMigrationRunner();

        var before = await runner.ListPendingAsync(CancellationToken.None);
        Assert.Contains("identity-access/202609170001_InitialAccountBindings", before);
        Assert.Contains("tenancy/202609170002_InitialTenancy", before);

        await runner.ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await runner.ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        var after = await runner.ListPendingAsync(CancellationToken.None);
        Assert.Empty(after);

        await using var context = CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(0, await context.Accounts.CountAsync(CancellationToken.None));
        Assert.Equal(0, await context.ExternalIdentityBindings.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task MigratorFailsWithinItsBudgetWhenAnotherRunOwnsTheLock()
    {
        await using var lockConnection = new NpgsqlConnection(ConnectionString);
        await lockConnection.OpenAsync(CancellationToken.None);
        await using (var acquire = lockConnection.CreateCommand())
        {
            acquire.CommandText = "SELECT pg_advisory_lock(@key)";
            acquire.Parameters.AddWithValue("key", MigrationAdvisoryLockKey);
            await acquire.ExecuteScalarAsync(CancellationToken.None);
        }

        var runner = CreateMigrationRunner();
        var elapsed = Stopwatch.StartNew();
        await Assert.ThrowsAsync<MigrationLockUnavailableException>(() =>
            runner.ApplyAsync(TimeSpan.FromMilliseconds(300), CancellationToken.None));
        Assert.InRange(elapsed.Elapsed, TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(2));
    }
}
