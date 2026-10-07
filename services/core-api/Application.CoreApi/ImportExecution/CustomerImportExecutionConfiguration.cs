using System.Globalization;

namespace Application.CoreApi.ImportExecution;

internal sealed record CustomerImportExecutionConfiguration(
    bool Enabled, TimeSpan PollInterval, int TenantPageSize, int BatchRows, TimeSpan OperationTimeout)
{
    internal const string SectionName = "CustomerImports:Execution";

    internal static CustomerImportExecutionConfiguration From(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var rawEnabled = section["Enabled"];
        if (rawEnabled is not null && !bool.TryParse(rawEnabled, out _))
            throw new InvalidOperationException($"{SectionName}:Enabled must be true or false.");
        var enabled = rawEnabled is not null && bool.Parse(rawEnabled);
        return new(enabled,
            TimeSpan.FromSeconds(Read(section, "PollIntervalSeconds", 5, 1, 300)),
            Read(section, "TenantPageSize", 10, 1, 50),
            Read(section, "BatchRows", 25, 1, 50),
            TimeSpan.FromSeconds(Read(section, "OperationTimeoutSeconds", 20, 1, 60)));
    }

    private static int Read(IConfigurationSection section, string key, int fallback, int minimum, int maximum)
    {
        var raw = section[key];
        if (raw is null) return fallback;
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || value < minimum || value > maximum)
            throw new InvalidOperationException($"{SectionName}:{key} must be between {minimum} and {maximum}.");
        return value;
    }
}
