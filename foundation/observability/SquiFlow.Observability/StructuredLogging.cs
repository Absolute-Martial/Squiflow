using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.OpenTelemetry;

namespace SquiFlow.Observability;

public sealed record StructuredLoggingOptions(
    string ServiceName,
    string ServiceVersion,
    string EnvironmentName,
    string Component,
    string LocalLogDirectory,
    string? OtlpEndpoint,
    LogEventLevel MinimumLevel = LogEventLevel.Information,
    long LocalFileSizeLimitBytes = 5 * 1024 * 1024,
    int LocalRetainedFileCountLimit = 3);

public static class StructuredLogging
{
    public static Serilog.ILogger CreateLogger(StructuredLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var localLogDirectory = EnsureWritableLogDirectory(options.LocalLogDirectory, options.ServiceName);

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(options.MinimumLevel)
            .Enrich.WithProperty("service.name", options.ServiceName)
            .Enrich.WithProperty("service.version", options.ServiceVersion)
            .Enrich.WithProperty("deployment.environment", options.EnvironmentName)
            .Enrich.WithProperty("squiflow.component", options.Component)
            .WriteTo.File(
                new JsonFormatter(renderMessage: true),
                Path.Combine(localLogDirectory, $"{options.ServiceName}-.jsonl"),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: options.LocalFileSizeLimitBytes,
                retainedFileCountLimit: options.LocalRetainedFileCountLimit,
                shared: true);

        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            configuration = configuration.WriteTo.OpenTelemetry(otlp =>
            {
                otlp.Endpoint = options.OtlpEndpoint;
                otlp.Protocol = OtlpProtocol.HttpProtobuf;
                otlp.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = options.ServiceName,
                    ["service.version"] = options.ServiceVersion,
                    ["deployment.environment"] = options.EnvironmentName,
                    ["squiflow.component"] = options.Component
                };
            });
        }

        return configuration.CreateLogger();
    }

    public static string ResolveDefaultLocalLogDirectory(string serviceName)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }

        return Path.Combine(root, "SquiFlow", "Diagnostics", "Logs", serviceName);
    }

    private static string EnsureWritableLogDirectory(string preferredDirectory, string serviceName)
    {
        try
        {
            Directory.CreateDirectory(preferredDirectory);
            return preferredDirectory;
        }
        catch (UnauthorizedAccessException)
        {
            return CreateFallbackDirectory(serviceName);
        }
        catch (IOException)
        {
            return CreateFallbackDirectory(serviceName);
        }
    }

    private static string CreateFallbackDirectory(string serviceName)
    {
        var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localRoot))
        {
            localRoot = Path.GetTempPath();
        }

        var fallback = Path.Combine(localRoot, "SquiFlow", "Diagnostics", "Logs", serviceName);
        Directory.CreateDirectory(fallback);
        return fallback;
    }
}
