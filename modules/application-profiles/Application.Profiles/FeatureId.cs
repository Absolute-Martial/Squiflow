namespace Application.Profiles;

internal static class FeatureId
{
    internal const int MaximumLength = 128;

    internal static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        var normalized = value.Trim();
        if (normalized.Length > MaximumLength ||
            !char.IsAsciiLetterOrDigit(normalized[0]) ||
            normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not '.' and not '-' and not '_'))
        {
            throw new ArgumentException(
                $"{parameterName} must begin with an ASCII letter or digit, contain only ASCII letters, digits, '.', '-' or '_', and be at most {MaximumLength} characters.",
                parameterName);
        }

        return normalized;
    }
}
