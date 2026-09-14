namespace SquiFlow.ApplicationKernel.Settings;

public enum SettingScope
{
    Platform,
    Tenant,
    User
}

public interface ISettingDefinition
{
    SettingKey Key { get; }
    ModuleId OwnerModuleId { get; }
    Type ValueType { get; }
    bool IsSensitive { get; }
}

public sealed class SettingDefinition<T> : ISettingDefinition
{
    private readonly Func<T, bool> _isValid;

    public SettingDefinition(
        SettingKey key,
        ModuleId ownerModuleId,
        T defaultValue,
        IReadOnlySet<SettingScope> allowedScopes,
        Func<T, bool> isValid,
        bool isSensitive = false)
    {
        Key = key;
        OwnerModuleId = ownerModuleId;
        DefaultValue = defaultValue;
        AllowedScopes = allowedScopes ?? throw new ArgumentNullException(nameof(allowedScopes));
        _isValid = isValid ?? throw new ArgumentNullException(nameof(isValid));
        IsSensitive = isSensitive;

        EnsureValid(defaultValue);
    }

    public SettingKey Key { get; }
    public ModuleId OwnerModuleId { get; }
    public Type ValueType => typeof(T);
    public T DefaultValue { get; }
    public IReadOnlySet<SettingScope> AllowedScopes { get; }
    public bool IsSensitive { get; }

    public T EnsureValid(T value)
    {
        if (!_isValid(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Invalid value for setting '{Key}'.");
        }

        return value;
    }
}

public static class SettingResolver
{
    public static int ResolveCappedInt(
        SettingDefinition<int> definition,
        int platformValue,
        int? tenantOverride)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var platform = definition.EnsureValid(platformValue);
        var requested = definition.EnsureValid(tenantOverride ?? definition.DefaultValue);
        return Math.Min(platform, requested);
    }
}
