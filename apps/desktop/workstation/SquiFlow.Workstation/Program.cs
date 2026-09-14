using System.Reflection;
using Avalonia;
using SquiFlow.Observability.Logging;

namespace SquiFlow.Workstation;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SquiFlow",
            "Logs");

        using var logger = StructuredLogging.Create("SquiFlow.Workstation", version, logDirectory);
        logger.Information("Workstation starting");

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            logger.Fatal(exception, "Workstation terminated unexpectedly");
            return 1;
        }
        finally
        {
            logger.Information("Workstation stopped");
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
