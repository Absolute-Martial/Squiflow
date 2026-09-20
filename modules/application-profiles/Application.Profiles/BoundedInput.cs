namespace Application.Profiles;

internal static class BoundedInput
{
    internal static FeatureDefinition[] ReadDefinitions(
        IEnumerable<FeatureDefinition> definitions,
        int maximumCount,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(definitions, parameterName);

        var result = new List<FeatureDefinition>(Math.Min(maximumCount, 32));
        foreach (var definition in definitions)
        {
            if (result.Count == maximumCount)
            {
                throw new ArgumentException(
                    $"A feature catalog cannot contain more than {maximumCount} definitions.",
                    parameterName);
            }

            result.Add(definition ?? throw new ArgumentException(
                "A feature definition cannot be null.",
                parameterName));
        }

        return [.. result];
    }

    internal static string[] ReadDistinctFeatureIds(
        IEnumerable<string> values,
        int maximumCount,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var result = new List<string>(Math.Min(maximumCount, 16));
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (result.Count == maximumCount)
            {
                throw new ArgumentException(
                    $"{parameterName} cannot contain more than {maximumCount} feature identifiers.",
                    parameterName);
            }

            var normalized = FeatureId.Normalize(value, parameterName);
            if (!unique.Add(normalized))
            {
                throw new ArgumentException(
                    $"{parameterName} contains duplicate feature identifier '{normalized}'.",
                    parameterName);
            }

            result.Add(normalized);
        }

        return [.. result];
    }
}
