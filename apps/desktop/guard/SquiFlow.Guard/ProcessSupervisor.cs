using System.Diagnostics;
using Serilog;

namespace SquiFlow.Guard;

public sealed class ProcessSupervisor
{
    private readonly ILogger _logger;
    private readonly RestartBudget _restartBudget;
    private readonly TimeSpan _initialRestartDelay;
    private readonly TimeSpan _maximumRestartDelay;

    public ProcessSupervisor(
        ILogger logger,
        RestartBudget restartBudget,
        TimeSpan initialRestartDelay,
        TimeSpan maximumRestartDelay)
    {
        _logger = logger;
        _restartBudget = restartBudget;
        _initialRestartDelay = initialRestartDelay;
        _maximumRestartDelay = maximumRestartDelay;
    }

    public async Task<int> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var restartDelay = _initialRestartDelay;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var child = Start(executable, arguments);
            _logger.Information(
                "Guard child started {EventName} {ChildProcessId} {Executable}",
                "GUARD.CHILD.STARTED",
                child.Id,
                Path.GetFileName(executable));

            try
            {
                await child.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryTerminate(child);
                _logger.Information("Guard supervision cancelled {EventName}", "GUARD.SUPERVISION.CANCELLED");
                return 0;
            }

            if (child.ExitCode == 0)
            {
                _logger.Information(
                    "Guard child exited cleanly {EventName} {ChildProcessId}",
                    "GUARD.CHILD.CLEAN_EXIT",
                    child.Id);
                return 0;
            }

            _logger.Error(
                "Guard child crashed {EventName} {FailureCode} {ChildProcessId} {ExitCode}",
                "GUARD.CHILD.CRASHED",
                "GUARD.CHILD.NONZERO_EXIT",
                child.Id,
                child.ExitCode);

            if (!_restartBudget.TryRegister(DateTimeOffset.UtcNow))
            {
                _logger.Fatal(
                    "Guard restart budget exhausted {EventName} {FailureCode} {SafeModeRequired}",
                    "GUARD.RESTART.BUDGET_EXHAUSTED",
                    "GUARD.RESTART.BUDGET_EXHAUSTED",
                    true);
                return child.ExitCode;
            }

            _logger.Warning(
                "Guard scheduling child restart {EventName} {RestartDelayMs}",
                "GUARD.CHILD.RESTART_SCHEDULED",
                restartDelay.TotalMilliseconds);

            try
            {
                await Task.Delay(restartDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return 0;
            }

            var nextTicks = Math.Min(restartDelay.Ticks * 2, _maximumRestartDelay.Ticks);
            restartDelay = TimeSpan.FromTicks(nextTicks);
        }

        return 0;
    }

    private static Process Start(string executable, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start '{executable}'.");
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and termination request.
        }
    }
}
