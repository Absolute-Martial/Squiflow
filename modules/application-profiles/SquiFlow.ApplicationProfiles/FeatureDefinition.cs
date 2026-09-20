namespace SquiFlow.ApplicationProfiles;

/// <summary>
/// Describes one shipped application feature. Feature identifiers become stable compatibility
/// vocabulary once a production catalog publishes them.
/// </summary>
public sealed class FeatureDefinition
{
    private FeatureDefinition(
        string id,
        IReadOnlyList<string> dependencies,
        bool isAlwaysEnabled,
        bool isTenantSelectable)
    {
        Id = id;
        Dependencies = dependencies;
        IsAlwaysEnabled = isAlwaysEnabled;
        IsTenantSelectable = isTenantSelectable;
    }

    public string Id { get; }

    public IReadOnlyList<string> Dependencies { get; }

    public bool IsAlwaysEnabled { get; }

    public bool IsTenantSelectable { get; }

    public static FeatureDefinition Create(
        string id,
        IEnumerable<string>? dependencies = null,
        bool isAlwaysEnabled = false,
        bool isTenantSelectable = true)
    {
        var normalizedId = FeatureId.Normalize(id, nameof(id));
        var normalizedDependencies = BoundedInput.ReadDistinctFeatureIds(
            dependencies ?? [],
            FeatureCatalog.MaximumDependenciesPerFeature,
            nameof(dependencies));

        if (normalizedDependencies.Contains(normalizedId, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Feature '{normalizedId}' cannot depend on itself.",
                nameof(dependencies));
        }

        return new FeatureDefinition(
            normalizedId,
            Array.AsReadOnly(normalizedDependencies),
            isAlwaysEnabled,
            isTenantSelectable);
    }
}
