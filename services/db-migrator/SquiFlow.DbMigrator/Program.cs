using SquiFlow.DbMigrator;

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
var lockTimeoutSeconds = 30;
if ((!string.IsNullOrWhiteSpace(lockTimeoutText) &&
     !int.TryParse(lockTimeoutText, out lockTimeoutSeconds)) ||
    lockTimeoutSeconds is < 1 or > 300)
{
    await Console.Error.WriteLineAsync(
        "[migrator] Configuration error: Migration__LockTimeoutSeconds must be between 1 and 300.");
    return 2;
}

var runner = new MigrationRunner(connectionString);
try
{
    if (verb == MigratorVerb.ListPending)
    {
        var pending = await runner.ListPendingAsync(CancellationToken.None);
        foreach (var migration in pending)
        {
            await Console.Out.WriteLineAsync(migration);
        }

        return 0;
    }

    await runner.ApplyAsync(TimeSpan.FromSeconds(lockTimeoutSeconds), CancellationToken.None);
    await Console.Out.WriteLineAsync("[migrator] Applied all pending application migrations.");
    return 0;
}
catch (MigrationLockUnavailableException exception)
{
    await Console.Error.WriteLineAsync($"[migrator] Lock timeout: {exception.Message}");
    return 3;
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    await Console.Error.WriteLineAsync(
        $"[migrator] Migration failed ({exception.GetType().Name}). Review database diagnostics using the deployment correlation context.");
    return 1;
}
