namespace SquiFlow.ApplicationKernel;

public readonly record struct ModuleId
{
    public ModuleId(string value) => Value = StableIdentifier.Require(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct FeatureId
{
    public FeatureId(string value) => Value = StableIdentifier.Require(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct PermissionId
{
    public PermissionId(string value) => Value = StableIdentifier.Require(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct SettingKey
{
    public SettingKey(string value) => Value = StableIdentifier.Require(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct TenantId
{
    public TenantId(string value) => Value = StableIdentifier.Require(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct SubjectId
{
    public SubjectId(string value) => Value = StableIdentifier.Require(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct ExperimentId
{
    public ExperimentId(string value) => Value = StableIdentifier.Require(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

internal static class StableIdentifier
{
    public static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Stable identifiers must be non-empty.", parameterName);
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Stable identifiers must not contain leading or trailing whitespace.", parameterName);
        }

        return value;
    }
}
