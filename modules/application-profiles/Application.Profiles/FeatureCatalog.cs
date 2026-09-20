using System.Security.Cryptography;
using System.Text;

namespace Application.Profiles;

/// <summary>
/// Validated, immutable catalog of features shipped in one composed application artifact.
/// </summary>
public sealed class FeatureCatalog
{
    public const int MaximumFeatureCount = 1_024;
    public const int MaximumDependenciesPerFeature = 64;
    public const int MaximumRequestedFeatureCount = 1_024;

    private readonly IReadOnlyDictionary<string, FeatureDefinition> _definitions;
    private readonly IReadOnlyList<FeatureDefinition> _dependencyOrder;

    private FeatureCatalog(
        IReadOnlyDictionary<string, FeatureDefinition> definitions,
        IReadOnlyList<FeatureDefinition> dependencyOrder,
        string fingerprint)
    {
        _definitions = definitions;
        _dependencyOrder = dependencyOrder;
        Fingerprint = fingerprint;
    }

    public string Fingerprint { get; }

    public IReadOnlyList<FeatureDefinition> Definitions => _dependencyOrder;

    public static FeatureCatalog Create(IEnumerable<FeatureDefinition> definitions)
    {
        var supplied = BoundedInput.ReadDefinitions(
            definitions,
            MaximumFeatureCount,
            nameof(definitions));
        var byId = new Dictionary<string, FeatureDefinition>(StringComparer.Ordinal);

        foreach (var definition in supplied)
        {
            if (!byId.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException(
                    $"The feature catalog contains duplicate identifier '{definition.Id}'.",
                    nameof(definitions));
            }
        }

        foreach (var definition in supplied)
        {
            foreach (var dependencyId in definition.Dependencies)
            {
                if (!byId.ContainsKey(dependencyId))
                {
                    throw new ArgumentException(
                        $"Feature '{definition.Id}' depends on undefined feature '{dependencyId}'.",
                        nameof(definitions));
                }
            }
        }

        var dependencyOrder = CreateDependencyOrder(byId);
        var readOnlyDefinitions = new System.Collections.ObjectModel.ReadOnlyDictionary<string, FeatureDefinition>(byId);
        return new FeatureCatalog(
            readOnlyDefinitions,
            Array.AsReadOnly(dependencyOrder),
            CalculateCatalogFingerprint(byId.Values));
    }

    public CompiledFeatureSelection Compile(IEnumerable<string> requestedFeatureIds)
    {
        var requested = BoundedInput.ReadDistinctFeatureIds(
            requestedFeatureIds,
            MaximumRequestedFeatureCount,
            nameof(requestedFeatureIds));

        foreach (var requestedId in requested)
        {
            if (!_definitions.TryGetValue(requestedId, out var definition))
            {
                throw new ArgumentException(
                    $"Requested feature '{requestedId}' is not present in this application catalog.",
                    nameof(requestedFeatureIds));
            }

            if (!definition.IsTenantSelectable)
            {
                throw new ArgumentException(
                    $"Feature '{requestedId}' can only be enabled by a dependency or platform policy.",
                    nameof(requestedFeatureIds));
            }
        }

        var effective = new HashSet<string>(requested, StringComparer.Ordinal);
        foreach (var definition in _dependencyOrder)
        {
            if (definition.IsAlwaysEnabled)
            {
                effective.Add(definition.Id);
            }
        }

        foreach (var featureId in effective.ToArray())
        {
            AddDependencies(featureId, effective);
        }

        var normalizedRequested = requested.Order(StringComparer.Ordinal).ToArray();
        var normalizedEffective = _dependencyOrder
            .Where(definition => effective.Contains(definition.Id))
            .Select(definition => definition.Id)
            .ToArray();

        return new CompiledFeatureSelection(
            Fingerprint,
            CalculateSelectionFingerprint(Fingerprint, normalizedRequested, normalizedEffective),
            normalizedRequested,
            normalizedEffective);
    }

    private void AddDependencies(string featureId, HashSet<string> effective)
    {
        foreach (var dependencyId in _definitions[featureId].Dependencies)
        {
            if (effective.Add(dependencyId))
            {
                AddDependencies(dependencyId, effective);
            }
        }
    }

    private static FeatureDefinition[] CreateDependencyOrder(
        IReadOnlyDictionary<string, FeatureDefinition> definitions)
    {
        var state = new Dictionary<string, VisitState>(StringComparer.Ordinal);
        var path = new List<string>();
        var ordered = new List<FeatureDefinition>(definitions.Count);

        foreach (var featureId in definitions.Keys.Order(StringComparer.Ordinal))
        {
            Visit(featureId);
        }

        return [.. ordered];

        void Visit(string featureId)
        {
            if (state.TryGetValue(featureId, out var existing))
            {
                if (existing == VisitState.Complete)
                {
                    return;
                }

                var cycleStart = path.IndexOf(featureId);
                var cycle = path.Skip(cycleStart).Append(featureId);
                throw new ArgumentException(
                    $"The feature catalog contains a dependency cycle: {string.Join(" -> ", cycle)}.",
                    nameof(definitions));
            }

            state.Add(featureId, VisitState.Visiting);
            path.Add(featureId);

            foreach (var dependencyId in definitions[featureId].Dependencies.Order(StringComparer.Ordinal))
            {
                Visit(dependencyId);
            }

            path.RemoveAt(path.Count - 1);
            state[featureId] = VisitState.Complete;
            ordered.Add(definitions[featureId]);
        }
    }

    private static string CalculateCatalogFingerprint(IEnumerable<FeatureDefinition> definitions)
    {
        var canonical = new StringBuilder("application-feature-catalog-v1\n");
        foreach (var definition in definitions.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            canonical
                .Append(definition.Id).Append('|')
                .Append(definition.IsAlwaysEnabled ? '1' : '0').Append('|')
                .Append(definition.IsTenantSelectable ? '1' : '0').Append('|')
                .AppendJoin(',', definition.Dependencies.Order(StringComparer.Ordinal))
                .Append('\n');
        }

        return CalculateFingerprint(canonical.ToString());
    }

    private static string CalculateSelectionFingerprint(
        string catalogFingerprint,
        IEnumerable<string> requestedFeatureIds,
        IEnumerable<string> effectiveFeatureIds)
    {
        var canonical = new StringBuilder("application-feature-selection-v1\n")
            .Append(catalogFingerprint).Append('\n')
            .Append("requested:").AppendJoin(',', requestedFeatureIds).Append('\n')
            .Append("effective:").AppendJoin(',', effectiveFeatureIds).Append('\n');
        return CalculateFingerprint(canonical.ToString());
    }

    private static string CalculateFingerprint(string canonical)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(digest);
    }

    private enum VisitState
    {
        Visiting,
        Complete,
    }
}
