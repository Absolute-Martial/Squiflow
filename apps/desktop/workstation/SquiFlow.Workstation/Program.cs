using Avalonia;
using Avalonia.Fonts.Inter;
using Serilog;

namespace SquiFlow.Workstation;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = WorkstationLogging.Create();

        try
        {
            Log.Information("Workstation started {EventName}", "WORKSTATION.STARTED");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Workstation unhandled failure {EventName} {FailureCode}", "WORKSTATION.UNHANDLED_FAILURE", "WORKSTATION.PROCESS.UNHANDLED");
            throw;
        }
        finally
        {
            Log.Information("Workstation stopping {EventName}", "WORKSTATION.STOPPING");
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
