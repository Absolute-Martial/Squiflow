using System.Reflection;
using SquiFlow.Guard.Supervision;
using SquiFlow.Observability.Logging;

namespace SquiFlow.Guard;

internal static class Program
{
    private const string WorkstationPathEnvironmentVariable = "SQUIFLOW_WORKSTATION_PATH";

    public static async Task<int> Main(string[] args)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SquiFlow",
            "Logs");

        using var logger = StructuredLogging.Create("SquiFlow.Guard", version, logDirectory);
        var workstationPath = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable(WorkstationPathEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(workstationPath))
        {
            logger.Error(
                "Guard requires the Workstation executable path as the first argument or {EnvironmentVariable}",
                WorkstationPathEnvironmentVariable);
            return 2;
        }

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Cancel();

        // Exact production thresholds remain a measured Guard decision. These bootstrap values are finite by design.
        var supervisor = new WorkstationSupervisor(
            logger,
            new RestartBudget(maximumRestarts: 3, window: TimeSpan.FromMinutes(2)),
            restartBackoff: TimeSpan.FromSeconds(2));

        logger.Information("Guard supervision starting");
        var result = await supervisor.RunAsync(workstationPath, shutdown.Token).ConfigureAwait(false);
        logger.Information("Guard supervision stopped with result {Result}", result);
        return result;
    }
}
