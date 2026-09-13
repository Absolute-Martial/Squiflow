using SquiFlow.Observability;

namespace SquiFlow.Workstation;

internal static class WorkstationLogging
{
    public static Serilog.ILogger Create()
    {
        var serviceVersion = typeof(WorkstationLogging).Assembly.GetName().Version?.ToString() ?? "unknown";
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        var logDirectory = Environment.GetEnvironmentVariable("SQUIFLOW_LOG_DIRECTORY")
            ?? StructuredLogging.ResolveDefaultLocalLogDirectory("SquiFlow.Workstation");

        return StructuredLogging.CreateLogger(new StructuredLoggingOptions(
            ServiceName: "SquiFlow.Workstation",
            ServiceVersion: serviceVersion,
            EnvironmentName: environmentName,
            Component: "Workstation",
            LocalLogDirectory: logDirectory,
            OtlpEndpoint: Environment.GetEnvironmentVariable("SQUIFLOW_OTLP_LOGS_ENDPOINT")));
    }
}
