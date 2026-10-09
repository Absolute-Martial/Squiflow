using Application.DatabaseMigrator;

MigratorVerb verb;
try
{
    verb = MigratorCommand.Parse(args);
}
catch (ArgumentException exception)
{
    await Console.Error.WriteLineAsync(exception.Message);
    await Console.Error.WriteLineAsync(MigratorCommand.HelpText);
    return 2;
}

if (verb == MigratorVerb.Help)
{
    await Console.Out.WriteLineAsync(MigratorCommand.HelpText);
    return 0;
}

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PrimaryDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    await Console.Error.WriteLineAsync(
        "[migrator] Configuration error: ConnectionStrings__PrimaryDatabase is required.");
    return 2;
}

var lockTimeoutText = Environment.GetEnvironmentVariable("Migration__LockTimeoutSeconds");
if (!int.TryParse(lockTimeoutText, out var lockTimeoutSeconds) ||
    lockTimeoutSeconds < 1 ||
    lockTimeoutSeconds > MigrationRunner.MaximumLockTimeout.TotalSeconds)
{
    await Console.Error.WriteLineAsync(
        "[migrator] Configuration error: Migration__LockTimeoutSeconds is required and must be between 1 and 300.");
    return 2;
}

var advisoryLockKeyText = Environment.GetEnvironmentVariable("Migration__AdvisoryLockKey");
if (!long.TryParse(advisoryLockKeyText, out var advisoryLockKey) || advisoryLockKey == 0)
{
    await Console.Error.WriteLineAsync(
        "[migrator] Configuration error: Migration__AdvisoryLockKey is required and must be a nonzero signed 64-bit integer unique to this database/application migration boundary.");
    return 2;
}

using var stopping = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stopping.Cancel();
};
Console.CancelKeyPress += cancelHandler;

var runner = new MigrationRunner(connectionString, advisoryLockKey);
try
{
    if (verb == MigratorVerb.ListPending)
    {
        var pending = await runner.ListPendingAsync(stopping.Token);
        foreach (var migration in pending)
        {
            await Console.Out.WriteLineAsync(migration);
        }

        return 0;
    }

    await runner.ApplyAsync(TimeSpan.FromSeconds(lockTimeoutSeconds), stopping.Token);
    await Console.Out.WriteLineAsync("[migrator] Applied all pending application migrations.");
    return 0;
}
catch (MigrationLockUnavailableException exception)
{
    await Console.Error.WriteLineAsync($"[migrator] Lock timeout: {exception.Message}");
    return 3;
}
catch (OperationCanceledException) when (stopping.IsCancellationRequested)
{
    await Console.Error.WriteLineAsync("[migrator] Cancelled before completion.");
    return 130;
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    await Console.Error.WriteLineAsync(
        $"[migrator] Migration failed ({exception.GetType().Name}). Review database diagnostics using the deployment correlation context.");
    return 1;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
