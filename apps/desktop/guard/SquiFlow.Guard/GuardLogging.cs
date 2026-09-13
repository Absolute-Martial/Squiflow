using Serilog;
using SquiFlow.Observability;

namespace SquiFlow.Guard;

internal static class GuardLogging
{
    public static Serilog.ILogger Create()
    {
        var serviceVersion = typeof(GuardLogging).Assembly.GetName().Version?.ToString() ?? "unknown";
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        var logDirectory = Environment.GetEnvironmentVariable("SQUIFLOW_LOG_DIRECTORY")
            ?? StructuredLogging.ResolveDefaultLocalLogDirectory("SquiFlow.Guard");

        return StructuredLogging.CreateLogger(new StructuredLoggingOptions(
            ServiceName: "SquiFlow.Guard",
            ServiceVersion: serviceVersion,
            EnvironmentName: environmentName,
            Component: "Guard",
            LocalLogDirectory: logDirectory,
            OtlpEndpoint: Environment.GetEnvironmentVariable("SQUIFLOW_OTLP_LOGS_ENDPOINT")));
    }
}
