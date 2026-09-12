using SquiFlow.Guard;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: SquiFlow.Guard <workstation-executable> [workstation arguments...]");
    return 2;
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

var supervisor = new ProcessSupervisor(
    new RestartBudget(maximumRestarts: 3, window: TimeSpan.FromMinutes(2)),
    initialRestartDelay: TimeSpan.FromSeconds(1),
    maximumRestartDelay: TimeSpan.FromSeconds(15));

return await supervisor.RunAsync(args[0], args.Skip(1).ToArray(), shutdown.Token);
