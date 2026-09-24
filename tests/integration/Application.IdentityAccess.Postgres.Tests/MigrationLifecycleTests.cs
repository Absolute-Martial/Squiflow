using Microsoft.EntityFrameworkCore;
using Npgsql;
using Application.DatabaseMigrator;
using System.Diagnostics;
using Xunit;

namespace Application.IdentityAccess.Postgres.Tests;

public sealed class MigrationLifecycleTests : PostgresTestDatabase
{
    [Fact]
    public async Task MigratorListsAppliesAndRepeatsOwnedMigrationsWithOnePooledConnection()
    {
        var oneConnectionPool = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            MaxPoolSize = 1,
        }.ConnectionString;
        var runner = new MigrationRunner(oneConnectionPool, MigrationAdvisoryLockKey);

        var before = await runner.ListPendingAsync(CancellationToken.None);
        Assert.Contains("identity-access/202609170001_InitialAccountBindings", before);
        Assert.Contains("tenancy/202609170002_InitialTenancy", before);
        Assert.Contains("orders/202609220001_InitialOrderDrafts", before);
        Assert.Contains("orders/202609230001_OrderDraftBrowseIndex", before);
        Assert.Contains("orders/202609230002_OrderDraftAbandonment", before);

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

    [Fact]
    public async Task MigratorProcessReturnsDocumentedExitCodeWhenLockIsUnavailable()
    {
        await using var lockConnection = new NpgsqlConnection(ConnectionString);
        await lockConnection.OpenAsync(CancellationToken.None);
        await using (var acquire = lockConnection.CreateCommand())
        {
            acquire.CommandText = "SELECT pg_advisory_lock(@key)";
            acquire.Parameters.AddWithValue("key", MigrationAdvisoryLockKey);
            await acquire.ExecuteScalarAsync(CancellationToken.None);
        }

        var executable = Path.Combine(AppContext.BaseDirectory, "Application.DatabaseMigrator.dll");
        Assert.True(File.Exists(executable), $"Migrator executable was not copied to {executable}.");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(executable);
        start.ArgumentList.Add("apply");
        start.Environment["ConnectionStrings__PrimaryDatabase"] = ConnectionString;
        start.Environment["Migration__AdvisoryLockKey"] = MigrationAdvisoryLockKey.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["Migration__LockTimeoutSeconds"] = "1";

        using var process = Process.Start(start);
        Assert.NotNull(process);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(deadline.Token);
        var error = await process.StandardError.ReadToEndAsync(deadline.Token);
        Assert.Equal(3, process.ExitCode);
        Assert.Contains("[migrator] Lock timeout:", error, StringComparison.Ordinal);
    }
}
