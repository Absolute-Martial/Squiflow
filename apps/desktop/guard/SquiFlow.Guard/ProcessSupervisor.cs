using System.Diagnostics;

namespace SquiFlow.Guard;

public sealed class ProcessSupervisor
{
    private readonly RestartBudget _restartBudget;
    private readonly TimeSpan _initialRestartDelay;
    private readonly TimeSpan _maximumRestartDelay;

    public ProcessSupervisor(
        RestartBudget restartBudget,
        TimeSpan initialRestartDelay,
        TimeSpan maximumRestartDelay)
    {
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
            Console.WriteLine($"guard.child.started pid={child.Id}");

            try
            {
                await child.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryTerminate(child);
                return 0;
            }

            if (child.ExitCode == 0)
            {
                Console.WriteLine("guard.child.clean-exit");
                return 0;
            }

            Console.Error.WriteLine($"guard.child.crashed exitCode={child.ExitCode}");
            if (!_restartBudget.TryRegister(DateTimeOffset.UtcNow))
            {
                Console.Error.WriteLine("guard.restart-budget.exhausted safeModeRequired=true");
                return child.ExitCode;
            }

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
