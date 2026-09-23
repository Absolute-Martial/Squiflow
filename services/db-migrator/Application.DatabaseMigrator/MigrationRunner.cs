using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Application.IdentityAccess.Postgres;
using Application.Orders.Postgres;
using Application.Tenancy.Postgres;

namespace Application.DatabaseMigrator;

internal sealed class MigrationRunner
{
    internal static readonly TimeSpan MaximumLockTimeout = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan LockPollInterval = TimeSpan.FromMilliseconds(250);
    private readonly string _connectionString;
    private readonly long _advisoryLockKey;

    public MigrationRunner(string connectionString, long advisoryLockKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (advisoryLockKey == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(advisoryLockKey),
                "The deployment-specific migration advisory-lock key cannot be zero.");
        }

        _connectionString = connectionString;
        _advisoryLockKey = advisoryLockKey;
    }

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
        await using var orderDatabase = CreateOrderDatabase();
        var orderMigrations = await orderDatabase.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);

        return identityMigrations.Select(migration => $"identity-access/{migration}")
            .Concat(tenancyMigrations.Select(migration => $"tenancy/{migration}"))
            .Concat(orderMigrations.Select(migration => $"orders/{migration}"))
            .ToArray();
    }

    public async Task ApplyAsync(TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        if (lockTimeout <= TimeSpan.Zero || lockTimeout > MaximumLockTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lockTimeout),
                "Migration lock timeout must be greater than zero and no more than five minutes.");
        }

        await using var identityDatabase = CreateIdentityDatabase();
        await identityDatabase.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var connection = identityDatabase.Database.GetDbConnection();

        await AcquireLockAsync(
                connection,
                _advisoryLockKey,
                lockTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await identityDatabase.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await using var tenancyDatabase = CreateTenancyDatabase();
            await tenancyDatabase.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await using var orderDatabase = CreateOrderDatabase();
            await orderDatabase.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await ReleaseLockBestEffortAsync(connection, _advisoryLockKey).ConfigureAwait(false);
        }
    }

    private IdentityAccessDbContext CreateIdentityDatabase()
    {
        var builder = new DbContextOptionsBuilder<IdentityAccessDbContext>();
        PostgresIdentityAccessOptions.Configure(builder, _connectionString);
        return new IdentityAccessDbContext(builder.Options);
    }

    private TenancyDbContext CreateTenancyDatabase()
    {
        var builder = new DbContextOptionsBuilder<TenancyDbContext>();
        PostgresTenancyOptions.Configure(builder, _connectionString);
        return new TenancyDbContext(builder.Options);
    }

    private OrderDbContext CreateOrderDatabase()
    {
        var builder = new DbContextOptionsBuilder<OrderDbContext>();
        PostgresOrderOptions.Configure(builder, _connectionString);
        return new OrderDbContext(builder.Options);
    }

    private static async Task AcquireLockAsync(
        DbConnection connection,
        long advisoryLockKey,
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
            key.Value = advisoryLockKey;
            command.Parameters.Add(key);

            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true)
            {
                return;
            }

            var remaining = timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var delay = remaining < LockPollInterval
                ? remaining
                : LockPollInterval;
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new MigrationLockUnavailableException(timeout);
    }

    private static async Task ReleaseLockBestEffortAsync(
        DbConnection connection,
        long advisoryLockKey)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 5;
            command.CommandText = "SELECT pg_advisory_unlock(@key)";
            var key = command.CreateParameter();
            key.ParameterName = "key";
            key.Value = advisoryLockKey;
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
