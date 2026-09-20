namespace Application.CoreApi.Composition;

internal sealed class ProfileRuntimeOptions
{
    internal const string SectionName = "ProfileRuntime";

    public int MaximumRetainedRuntimes { get; init; }

    public int MaximumConcurrentBuilds { get; init; }

    public TimeSpan IdleRetention { get; init; }

    public TimeSpan MaintenanceInterval { get; init; }

    public TimeSpan ShutdownDrainTimeout { get; init; }
}
