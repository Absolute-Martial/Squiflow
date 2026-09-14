using SquiFlow.ApplicationKernel.Hosting;

namespace SquiFlow.ApplicationKernel.Features;

public static class FeatureSnapshotBuilder
{
    public static EffectiveFeatureSnapshot Publish(
        IEnumerable<FeatureDefinition> definitions,
        HostKind host,
        IEnumerable<FeatureId> platformAllowed,
        IEnumerable<FeatureId> tenantRequested,
        IReadOnlySet<ReleaseChannel> allowedChannels,
        long revision)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(platformAllowed);
        ArgumentNullException.ThrowIfNull(tenantRequested);
        ArgumentNullException.ThrowIfNull(allowedChannels);

        var definitionArray = definitions.ToArray();
        var duplicate = definitionArray
            .GroupBy(definition => definition.Id)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate feature id '{duplicate.Key}'.");
        }

        var byId = definitionArray.ToDictionary(definition => definition.Id);
        var platformCeiling = platformAllowed.ToHashSet();
        var requested = tenantRequested.ToHashSet();
        var enabled = new HashSet<FeatureId>();

        foreach (var definition in definitionArray.Where(definition => definition.IsAlwaysRequired && definition.Supports(host)))
        {
            requested.Add(definition.Id);
        }

        foreach (var featureId in requested)
        {
            Enable(featureId, host, byId, platformCeiling, allowedChannels, enabled, new HashSet<FeatureId>());
        }

        return new EffectiveFeatureSnapshot(revision, host, enabled);
    }

    private static void Enable(
        FeatureId featureId,
        HostKind host,
        IReadOnlyDictionary<FeatureId, FeatureDefinition> definitions,
        IReadOnlySet<FeatureId> platformCeiling,
        IReadOnlySet<ReleaseChannel> allowedChannels,
        ISet<FeatureId> enabled,
        ISet<FeatureId> visiting)
    {
        if (enabled.Contains(featureId))
        {
            return;
        }

        if (!definitions.TryGetValue(featureId, out var definition))
        {
            throw new InvalidOperationException($"Unknown feature '{featureId}'.");
        }

        if (!definition.Supports(host))
        {
            throw new InvalidOperationException($"Feature '{featureId}' is not supported by host '{host}'.");
        }

        if (!platformCeiling.Contains(featureId))
        {
            throw new InvalidOperationException($"Feature '{featureId}' exceeds the platform/deployment capability ceiling.");
        }

        if (definition.ReleaseChannel is ReleaseChannel.Removed || !allowedChannels.Contains(definition.ReleaseChannel))
        {
            throw new InvalidOperationException($"Feature '{featureId}' is not available in the selected release channel policy.");
        }

        if (!visiting.Add(featureId))
        {
            throw new InvalidOperationException($"Cyclic feature dependency detected at '{featureId}'.");
        }

        foreach (var dependency in definition.Dependencies)
        {
            Enable(dependency, host, definitions, platformCeiling, allowedChannels, enabled, visiting);
        }

        visiting.Remove(featureId);
        enabled.Add(featureId);
    }
}
