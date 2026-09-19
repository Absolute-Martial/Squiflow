using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SquiFlow.IdentityAccess.Postgres;
using SquiFlow.Tenancy.Postgres;

namespace SquiFlow.DbMigrator;

public sealed class MigrationRunner(string connectionString)
{
    private const long AdvisoryLockKey = 0x53515549464C4F57;

    public async Task<IReadOnlyList<string>> ListPendingAsync(CancellationToken cancellationToken)
    {
        await using var identityDatabase = CreateIdentityDatabase();
        var identityMigrations = await identityDatabase.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var tenancyDatabase = CreateTenancyDatabase();
        var tenancyMigrations = await tenancyDatabase.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);

        return identityMigrations.Select(migration => $"identity-access/{migration}")
            .Concat(tenancyMigrations.Select(migration => $"tenancy/{migration}"))
            .ToArray();
    }

    public async Task ApplyAsync(TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        if (lockTimeout <= TimeSpan.Zero || lockTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lockTimeout),
                "Migration lock timeout must be greater than zero and no more than five minutes.");
        }

        await using var identityDatabase = CreateIdentityDatabase();
        await identityDatabase.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var connection = identityDatabase.Database.GetDbConnection();

        await AcquireLockAsync(connection, lockTimeout, cancellationToken).ConfigureAwait(false);
        try
        {
            await identityDatabase.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await using var tenancyDatabase = CreateTenancyDatabase();
            await tenancyDatabase.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await ReleaseLockBestEffortAsync(connection).ConfigureAwait(false);
        }
    }

    private IdentityAccessDbContext CreateIdentityDatabase()
    {
        var builder = new DbContextOptionsBuilder<IdentityAccessDbContext>();
        PostgresIdentityAccessOptions.Configure(builder, connectionString);
        return new IdentityAccessDbContext(builder.Options);
    }

    private TenancyDbContext CreateTenancyDatabase()
    {
        var builder = new DbContextOptionsBuilder<TenancyDbContext>();
        PostgresTenancyOptions.Configure(builder, connectionString);
        return new TenancyDbContext(builder.Options);
    }

    private static async Task AcquireLockAsync(
        DbConnection connection,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < timeout)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(@key)";
            var key = command.CreateParameter();
            key.ParameterName = "key";
            key.Value = AdvisoryLockKey;
            command.Parameters.Add(key);

            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true)
            {
                return;
            }

            var remaining = timeout - elapsed.Elapsed;
            var delay = remaining < TimeSpan.FromMilliseconds(250)
                ? remaining
                : TimeSpan.FromMilliseconds(250);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        throw new MigrationLockUnavailableException(timeout);
    }

    private static async Task ReleaseLockBestEffortAsync(DbConnection connection)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 5;
            command.CommandText = "SELECT pg_advisory_unlock(@key)";
            var key = command.CreateParameter();
            key.ParameterName = "key";
            key.Value = AdvisoryLockKey;
            command.Parameters.Add(key);
            await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // Disposing the owning connection below releases its session-scoped lock.
        }
        catch (InvalidOperationException)
        {
            // A broken/closed connection has already surrendered its session-scoped lock.
        }
    }
}
