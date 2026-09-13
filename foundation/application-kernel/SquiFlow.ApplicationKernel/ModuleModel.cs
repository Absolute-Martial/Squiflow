using SquiFlow.ApplicationKernel.Authorization;

namespace SquiFlow.ApplicationKernel.Features
{
    public enum ReleaseChannel
    {
        Internal,
        Preview,
        Beta,
        Stable,
        Deprecated
    }

    public enum OfflineFeaturePolicy
    {
        SnapshotAllowed,
        StableOnly,
        ServerRequired
    }

    public sealed record FeatureDefinition(
        FeatureId Id,
        ModuleId ModuleId,
        IReadOnlyCollection<FeatureId> Dependencies,
        bool IsAlwaysRequired = false,
        ReleaseChannel ReleaseChannel = ReleaseChannel.Stable,
        OfflineFeaturePolicy OfflinePolicy = OfflineFeaturePolicy.SnapshotAllowed,
        IReadOnlySet<HostKind>? SupportedHosts = null)
    {
        public bool Supports(HostKind host) => SupportedHosts is null || SupportedHosts.Contains(host);
    }

    public sealed record ExperimentDefinition(
        string ExperimentId,
        FeatureId FeatureId,
        IReadOnlyList<string> Variants,
        string SubjectKind,
        long Revision)
    {
        public string AssignVariant(string subjectId)
        {
            if (Variants.Count == 0)
            {
                throw new InvalidOperationException($"Experiment '{ExperimentId}' must define at least one variant.");
            }

            var hash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{ExperimentId}:{Revision}:{subjectId}"));
            var bucket = BitConverter.ToUInt32(hash, 0) % (uint)Variants.Count;
            return Variants[(int)bucket];
        }
    }

    public sealed class EffectiveFeatureSnapshot
    {
        private readonly HashSet<FeatureId> _enabled;

        public EffectiveFeatureSnapshot(long revision, IEnumerable<FeatureId> enabled)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            Revision = revision;
            _enabled = enabled.ToHashSet();
        }

        public long Revision { get; }
        public IReadOnlySet<FeatureId> Enabled => _enabled;
        public bool IsEnabled(FeatureId featureId) => _enabled.Contains(featureId);
    }

    public interface IFeatureSnapshotAccessor { EffectiveFeatureSnapshot Current { get; } }

    public static class FeatureSnapshotBuilder
    {
        public static EffectiveFeatureSnapshot Publish(
            IEnumerable<FeatureDefinition> definitions,
            IEnumerable<FeatureId> platformAllowed,
            IEnumerable<FeatureId> tenantRequested,
            long revision,
            HostKind? host = null,
            ReleaseChannel maximumChannel = ReleaseChannel.Stable)
        {
            var byId = definitions.ToDictionary(definition => definition.Id);
            var allowed = platformAllowed.ToHashSet();
            var requested = tenantRequested.ToHashSet();
            var enabled = new HashSet<FeatureId>();

            foreach (var definition in byId.Values.Where(definition => definition.IsAlwaysRequired))
            {
                requested.Add(definition.Id);
            }

            foreach (var featureId in requested)
            {
                AddWithDependencies(featureId, byId, allowed, enabled, new HashSet<FeatureId>(), host, maximumChannel);
            }

            return new EffectiveFeatureSnapshot(revision, enabled);
        }

        private static void AddWithDependencies(
            FeatureId featureId,
            IReadOnlyDictionary<FeatureId, FeatureDefinition> definitions,
            IReadOnlySet<FeatureId> platformAllowed,
            ISet<FeatureId> enabled,
            ISet<FeatureId> visiting,
            HostKind? host,
            ReleaseChannel maximumChannel)
        {
            if (enabled.Contains(featureId)) return;
            if (!definitions.TryGetValue(featureId, out var definition)) throw new InvalidOperationException($"Unknown feature '{featureId}'.");
            if (!platformAllowed.Contains(featureId)) throw new InvalidOperationException($"Feature '{featureId}' exceeds the platform/deployment capability ceiling.");
            if (host is not null && !definition.Supports(host.Value)) throw new InvalidOperationException($"Feature '{featureId}' is not supported by host '{host}'.");
            if (!IsChannelAllowed(definition.ReleaseChannel, maximumChannel)) throw new InvalidOperationException($"Feature '{featureId}' is in release channel '{definition.ReleaseChannel}', above allowed channel '{maximumChannel}'.");
            if (!visiting.Add(featureId)) throw new InvalidOperationException($"Cyclic feature dependency detected at '{featureId}'.");

            foreach (var dependency in definition.Dependencies)
            {
                AddWithDependencies(dependency, definitions, platformAllowed, enabled, visiting, host, maximumChannel);
            }

            visiting.Remove(featureId);
            enabled.Add(featureId);
        }

        private static bool IsChannelAllowed(ReleaseChannel feature, ReleaseChannel maximum)
        {
            static int Rank(ReleaseChannel channel) => channel switch
            {
                ReleaseChannel.Internal => 0,
                ReleaseChannel.Preview => 1,
                ReleaseChannel.Beta => 2,
                ReleaseChannel.Stable => 3,
                ReleaseChannel.Deprecated => 3,
                _ => throw new ArgumentOutOfRangeException(nameof(channel))
            };

            return Rank(feature) >= Rank(maximum);
        }
    }
}

namespace SquiFlow.ApplicationKernel.Settings
{
    public interface ISettingDefinition { SettingKey Key { get; } Type ValueType { get; } }

    public sealed class SettingDefinition<T> : ISettingDefinition
    {
        private readonly Func<T, bool> _isValid;
        public SettingDefinition(SettingKey key, T defaultValue, Func<T, bool> isValid)
        {
            Key = key; DefaultValue = defaultValue; _isValid = isValid ?? throw new ArgumentNullException(nameof(isValid)); EnsureValid(defaultValue);
        }
        public SettingKey Key { get; }
        public Type ValueType => typeof(T);
        public T DefaultValue { get; }
        public T EnsureValid(T value)
        {
            if (!_isValid(value)) throw new ArgumentOutOfRangeException(nameof(value), $"Invalid value for setting '{Key}'.");
            return value;
        }
    }

    public static class SettingResolver
    {
        public static int ResolveBoundedInt(SettingDefinition<int> definition, int platformCeiling, int? tenantOverride)
        {
            ArgumentNullException.ThrowIfNull(definition); definition.EnsureValid(platformCeiling);
            return Math.Min(definition.EnsureValid(tenantOverride ?? definition.DefaultValue), platformCeiling);
        }
    }
}

namespace SquiFlow.ApplicationKernel.Modules
{
    using SquiFlow.ApplicationKernel.Features;
    using SquiFlow.ApplicationKernel.Settings;

    public sealed record ModuleDescriptor(
        ModuleId Id, Version Version, IReadOnlyCollection<ModuleId> Dependencies, IReadOnlySet<HostKind> SupportedHosts,
        IReadOnlyCollection<FeatureDefinition> Features, IReadOnlyCollection<PermissionDefinition> Permissions, IReadOnlyCollection<ISettingDefinition> Settings)
    {
        public bool Supports(HostKind host) => SupportedHosts.Contains(host);
    }

    public sealed class ModuleGraph
    {
        private readonly IReadOnlyList<ModuleDescriptor> _ordered;
        private ModuleGraph(IReadOnlyList<ModuleDescriptor> ordered) => _ordered = ordered;
        public IReadOnlyList<ModuleDescriptor> OrderedModules => _ordered;
        public IReadOnlyList<ModuleDescriptor> ForHost(HostKind host) => _ordered.Where(module => module.Supports(host)).ToArray();

        public static ModuleGraph Build(IEnumerable<ModuleDescriptor> modules)
        {
            ArgumentNullException.ThrowIfNull(modules); var list = modules.ToArray(); ValidateDescriptors(list);
            var byId = list.ToDictionary(module => module.Id);
            foreach (var module in list) foreach (var dependency in module.Dependencies)
                if (!byId.ContainsKey(dependency)) throw new InvalidOperationException($"Module '{module.Id}' depends on missing module '{dependency}'.");
            var visiting = new HashSet<ModuleId>(); var visited = new HashSet<ModuleId>(); var ordered = new List<ModuleDescriptor>(list.Length);
            foreach (var module in list.OrderBy(module => module.Id.Value, StringComparer.Ordinal)) Visit(module, byId, visiting, visited, ordered);
            return new ModuleGraph(ordered);
        }

        private static void Visit(ModuleDescriptor module, IReadOnlyDictionary<ModuleId, ModuleDescriptor> byId, ISet<ModuleId> visiting, ISet<ModuleId> visited, ICollection<ModuleDescriptor> ordered)
        {
            if (visited.Contains(module.Id)) return;
            if (!visiting.Add(module.Id)) throw new InvalidOperationException($"Cyclic module dependency detected at '{module.Id}'.");
            foreach (var dependencyId in module.Dependencies) Visit(byId[dependencyId], byId, visiting, visited, ordered);
            visiting.Remove(module.Id); visited.Add(module.Id); ordered.Add(module);
        }

        private static void ValidateDescriptors(IReadOnlyCollection<ModuleDescriptor> modules)
        {
            var duplicateModule = modules.GroupBy(module => module.Id).FirstOrDefault(group => group.Count() > 1);
            if (duplicateModule is not null) throw new InvalidOperationException($"Duplicate module id '{duplicateModule.Key}'.");
            foreach (var module in modules)
            {
                if (string.IsNullOrWhiteSpace(module.Id.Value)) throw new InvalidOperationException("Module ids must be non-empty.");
                if (module.SupportedHosts.Count == 0) throw new InvalidOperationException($"Module '{module.Id}' must support at least one host.");
            }
            ValidateUniqueIds(modules.SelectMany(module => module.Features.Select(feature => feature.Id.Value)), "feature");
            ValidateUniqueIds(modules.SelectMany(module => module.Permissions.Select(permission => permission.Id.Value)), "permission");
            ValidateUniqueIds(modules.SelectMany(module => module.Settings.Select(setting => setting.Key.Value)), "setting");
        }

        private static void ValidateUniqueIds(IEnumerable<string> ids, string kind)
        {
            var duplicate = ids.GroupBy(value => value, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null) throw new InvalidOperationException($"Duplicate {kind} id '{duplicate.Key}'.");
        }
    }
}
