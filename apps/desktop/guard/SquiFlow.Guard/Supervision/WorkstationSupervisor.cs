using System.Diagnostics;
using Serilog.Core;

namespace SquiFlow.Guard.Supervision;

public sealed class WorkstationSupervisor
{
    private readonly Logger _logger;
    private readonly RestartBudget _restartBudget;
    private readonly TimeSpan _restartBackoff;

    public WorkstationSupervisor(
        Logger logger,
        RestartBudget restartBudget,
        TimeSpan restartBackoff)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _restartBudget = restartBudget ?? throw new ArgumentNullException(nameof(restartBudget));
        _restartBackoff = restartBackoff > TimeSpan.Zero
            ? restartBackoff
            : throw new ArgumentOutOfRangeException(nameof(restartBackoff));
    }

    public async Task<int> RunAsync(string workstationPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workstationPath);

        if (!File.Exists(workstationPath))
        {
            _logger.Error("Workstation executable not found at {WorkstationPath}", workstationPath);
            return 2;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            using var process = Start(workstationPath);
            if (process is null)
            {
                _logger.Error("Operating system failed to start Workstation at {WorkstationPath}", workstationPath);
                return 3;
            }

            _logger.Information("Workstation process started with pid {ProcessId}", process.Id);

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryTerminate(process);
                _logger.Information("Guard shutdown requested; Workstation supervision stopped intentionally");
                return 0;
            }

            if (process.ExitCode == 0)
            {
                _logger.Information("Workstation exited normally");
                return 0;
            }

            _logger.Warning(
                "Workstation exited unexpectedly with code {ExitCode}",
                process.ExitCode);

            if (!_restartBudget.TryRegister(DateTimeOffset.UtcNow))
            {
                _logger.Error("Workstation restart budget exceeded; Guard will not enter an infinite restart loop");
                return 4;
            }

            try
            {
                await Task.Delay(_restartBackoff, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return 0;
            }
        }

        return 0;
    }

    private static Process? Start(string executablePath)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory
        });
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(TimeSpan.FromSeconds(5));
            }
        }
        catch
        {
            // Shutdown is best effort here. Recovery evidence is emitted by the caller's lifecycle log.
        }
    }
}
