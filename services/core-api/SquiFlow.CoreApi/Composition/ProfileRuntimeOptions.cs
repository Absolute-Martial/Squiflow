namespace SquiFlow.CoreApi.Composition;

internal sealed class ProfileRuntimeOptions
{
    internal const string SectionName = "ProfileRuntime";

    public int MaximumRetainedRuntimes { get; init; } = 64;

    public int MaximumConcurrentBuilds { get; init; } = 4;

    public TimeSpan IdleRetention { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan MaintenanceInterval { get; init; } = TimeSpan.FromMinutes(1);

    public TimeSpan ShutdownDrainTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
