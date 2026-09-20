namespace Application.Profiles;

/// <summary>
/// Immutable result of validating one requested selection against one exact shipped catalog.
/// This is not durable tenant profile authority and does not grant authorization.
/// </summary>
public sealed class CompiledFeatureSelection
{
    internal CompiledFeatureSelection(
        string catalogFingerprint,
        string selectionFingerprint,
        string[] requestedFeatureIds,
        string[] effectiveFeatureIds)
    {
        CatalogFingerprint = catalogFingerprint;
        SelectionFingerprint = selectionFingerprint;
        RequestedFeatureIds = Array.AsReadOnly(requestedFeatureIds);
        EffectiveFeatureIds = Array.AsReadOnly(effectiveFeatureIds);
    }

    public string CatalogFingerprint { get; }

    public string SelectionFingerprint { get; }

    public IReadOnlyList<string> RequestedFeatureIds { get; }

    public IReadOnlyList<string> EffectiveFeatureIds { get; }

    public bool Contains(string featureId)
    {
        var normalized = FeatureId.Normalize(featureId, nameof(featureId));
        return EffectiveFeatureIds.Contains(normalized, StringComparer.Ordinal);
    }
}
