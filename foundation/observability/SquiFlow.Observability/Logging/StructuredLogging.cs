using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.OpenTelemetry;

namespace SquiFlow.Observability.Logging;

public static class StructuredLogging
{
    public const string OtlpEndpointEnvironmentVariable = "SQUIFLOW_OTLP_LOGS_ENDPOINT";
    public const string LogDirectoryEnvironmentVariable = "SQUIFLOW_LOG_DIRECTORY";

    public static Logger Create(
        string serviceName,
        string serviceVersion,
        string? defaultLogDirectory = null,
        LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);

        var configuredDirectory = Environment.GetEnvironmentVariable(LogDirectoryEnvironmentVariable);
        var logDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? defaultLogDirectory
            : configuredDirectory;

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.WithProperty("service.name", serviceName)
            .Enrich.WithProperty("service.version", serviceVersion)
            .WriteTo.Console();

        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
            configuration = configuration.WriteTo.File(
                new JsonFormatter(),
                Path.Combine(logDirectory, $"{serviceName}-.jsonl"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(2));
        }

        var otlpEndpoint = Environment.GetEnvironmentVariable(OtlpEndpointEnvironmentVariable);
        if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
        {
            configuration = configuration.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = endpoint.ToString();
                options.Protocol = OtlpProtocol.HttpProtobuf;
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = serviceName,
                    ["service.version"] = serviceVersion
                };
            });
        }

        return configuration.CreateLogger();
    }
}
