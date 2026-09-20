using System.Diagnostics.Metrics;

namespace Application.CoreApi.Composition;

internal static class ProfileRuntimeTelemetry
{
    internal const string MeterName = "Application.CoreApi.ProfileRuntime";

    private static readonly Meter Meter = new(MeterName);

    internal static readonly Counter<long> Acquisitions = Meter.CreateCounter<long>(
        "application.profile_runtime.acquisitions",
        description: "Profile runtime operation leases acquired.");

    internal static readonly Counter<long> Builds = Meter.CreateCounter<long>(
        "application.profile_runtime.builds",
        description: "Profile runtime builds completed successfully.");

    internal static readonly Counter<long> BuildFailures = Meter.CreateCounter<long>(
        "application.profile_runtime.build_failures",
        description: "Profile runtime builds that failed.");

    internal static readonly Counter<long> CapacityRejections = Meter.CreateCounter<long>(
        "application.profile_runtime.capacity_rejections",
        description: "Profile runtime builds rejected by the retained-runtime limit.");

    internal static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "application.profile_runtime.cache_hits",
        description: "Profile runtime acquisitions that found an existing local runtime slot.");

    internal static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "application.profile_runtime.cache_misses",
        description: "Profile runtime acquisitions that created a new local runtime slot.");

    internal static readonly Counter<long> Retirements = Meter.CreateCounter<long>(
        "application.profile_runtime.retirements",
        description: "Profile runtimes retired from the local cache.");

    internal static readonly Counter<long> DisposalFailures = Meter.CreateCounter<long>(
        "application.profile_runtime.disposal_failures",
        description: "Profile runtime scope disposals that failed.");

    internal static readonly UpDownCounter<long> RetainedRuntimes = Meter.CreateUpDownCounter<long>(
        "application.profile_runtime.retained",
        description: "Profile runtimes currently building, ready, or retiring.");

    internal static readonly UpDownCounter<long> BuildsInProgress = Meter.CreateUpDownCounter<long>(
        "application.profile_runtime.builds_in_progress",
        description: "Profile runtime builds currently in progress.");

    internal static readonly UpDownCounter<long> ActiveLeases = Meter.CreateUpDownCounter<long>(
        "application.profile_runtime.active_leases",
        description: "Active operation scopes leased from profile runtimes.");

    internal static readonly Histogram<double> BuildDuration = Meter.CreateHistogram<double>(
        "application.profile_runtime.build_duration",
        unit: "ms",
        description: "Time used to build an Autofac profile runtime.");

    internal static readonly Histogram<double> IdleAgeAtRetirement = Meter.CreateHistogram<double>(
        "application.profile_runtime.idle_age_at_retirement",
        unit: "s",
        description: "Idle age of a profile runtime selected for automatic retirement.");
}
