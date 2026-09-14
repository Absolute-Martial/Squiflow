using SquiFlow.ApplicationKernel.Hosting;

namespace SquiFlow.ApplicationKernel.Features;

public enum ReleaseChannel
{
    Internal,
    Preview,
    Beta,
    Stable,
    Deprecated,
    Removed
}

public enum OfflineFeaturePolicy
{
    SnapshotAllowed,
    StableOnly,
    ServerRequired
}

public sealed record FeatureDefinition(
    FeatureId Id,
    ModuleId OwnerModuleId,
    IReadOnlyCollection<FeatureId> Dependencies,
    IReadOnlySet<HostKind> SupportedHosts,
    ReleaseChannel ReleaseChannel,
    OfflineFeaturePolicy OfflinePolicy,
    bool IsAlwaysRequired = false)
{
    public bool Supports(HostKind host) => SupportedHosts.Contains(host);
}

public sealed class EffectiveFeatureSnapshot
{
    private readonly HashSet<FeatureId> _enabled;

    internal EffectiveFeatureSnapshot(long revision, HostKind host, IEnumerable<FeatureId> enabled)
    {
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        Revision = revision;
        Host = host;
        _enabled = enabled.ToHashSet();
    }

    public long Revision { get; }
    public HostKind Host { get; }
    public IReadOnlySet<FeatureId> Enabled => _enabled;
    public bool IsEnabled(FeatureId featureId) => _enabled.Contains(featureId);
}
