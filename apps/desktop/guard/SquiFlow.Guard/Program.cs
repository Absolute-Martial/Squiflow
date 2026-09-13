using Serilog;
using SquiFlow.Guard;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: SquiFlow.Guard <workstation-executable> [workstation arguments...]");
    return 2;
}

Log.Logger = GuardLogging.Create();

try
{
    Log.Information("Guard started {EventName}", "GUARD.STARTED");

    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };

    var supervisor = new ProcessSupervisor(
        Log.Logger.ForContext<ProcessSupervisor>(),
        new RestartBudget(maximumRestarts: 3, window: TimeSpan.FromMinutes(2)),
        initialRestartDelay: TimeSpan.FromSeconds(1),
        maximumRestartDelay: TimeSpan.FromSeconds(15));

    return await supervisor.RunAsync(args[0], args.Skip(1).ToArray(), shutdown.Token);
}
catch (Exception exception)
{
    Log.Fatal(exception, "Guard unhandled failure {EventName} {FailureCode}", "GUARD.UNHANDLED_FAILURE", "GUARD.PROCESS.UNHANDLED");
    return 1;
}
finally
{
    Log.Information("Guard stopping {EventName}", "GUARD.STOPPING");
    await Log.CloseAndFlushAsync();
}
