using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Hosting;
using SquiFlow.ApplicationKernel.Permissions;
using SquiFlow.ApplicationKernel.Settings;

namespace SquiFlow.ApplicationKernel.Modules;

public sealed class ModuleGraph
{
    private readonly IReadOnlyList<ModuleDescriptor> _ordered;

    private ModuleGraph(IReadOnlyList<ModuleDescriptor> ordered) => _ordered = ordered;

    public IReadOnlyList<ModuleDescriptor> OrderedModules => _ordered;

    public IReadOnlyList<ModuleDescriptor> ForHost(HostKind host) =>
        _ordered.Where(module => module.Supports(host)).ToArray();

    public static ModuleGraph Build(IEnumerable<ModuleDescriptor> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var list = modules.ToArray();
        ValidateDescriptors(list);

        var byId = list.ToDictionary(module => module.Id);
        foreach (var module in list)
        {
            foreach (var dependency in module.Dependencies)
            {
                if (!byId.ContainsKey(dependency))
                {
                    throw new InvalidOperationException($"Module '{module.Id}' depends on missing module '{dependency}'.");
                }

                if (dependency == module.Id)
                {
                    throw new InvalidOperationException($"Module '{module.Id}' cannot depend on itself.");
                }
            }
        }

        ValidateFeatureReferences(list, byId);

        var visiting = new HashSet<ModuleId>();
        var visited = new HashSet<ModuleId>();
        var ordered = new List<ModuleDescriptor>(list.Length);

        foreach (var module in list.OrderBy(module => module.Id.Value, StringComparer.Ordinal))
        {
            Visit(module, byId, visiting, visited, ordered);
        }

        return new ModuleGraph(ordered);
    }

    private static void Visit(
        ModuleDescriptor module,
        IReadOnlyDictionary<ModuleId, ModuleDescriptor> byId,
        ISet<ModuleId> visiting,
        ISet<ModuleId> visited,
        ICollection<ModuleDescriptor> ordered)
    {
        if (visited.Contains(module.Id))
        {
            return;
        }

        if (!visiting.Add(module.Id))
        {
            throw new InvalidOperationException($"Cyclic module dependency detected at '{module.Id}'.");
        }

        foreach (var dependencyId in module.Dependencies.OrderBy(id => id.Value, StringComparer.Ordinal))
        {
            Visit(byId[dependencyId], byId, visiting, visited, ordered);
        }

        visiting.Remove(module.Id);
        visited.Add(module.Id);
        ordered.Add(module);
    }

    private static void ValidateDescriptors(IReadOnlyCollection<ModuleDescriptor> modules)
    {
        var duplicateModule = modules.GroupBy(module => module.Id).FirstOrDefault(group => group.Count() > 1);
        if (duplicateModule is not null)
        {
            throw new InvalidOperationException($"Duplicate module id '{duplicateModule.Key}'.");
        }

        foreach (var module in modules)
        {
            if (module.Version is null)
            {
                throw new InvalidOperationException($"Module '{module.Id}' must declare a version.");
            }

            if (module.SupportedHosts.Count == 0)
            {
                throw new InvalidOperationException($"Module '{module.Id}' must support at least one host.");
            }

            foreach (var feature in module.Features)
            {
                if (feature.OwnerModuleId != module.Id)
                {
                    throw new InvalidOperationException($"Feature '{feature.Id}' is declared by '{module.Id}' but owned by '{feature.OwnerModuleId}'.");
                }
            }

            foreach (var permission in module.Permissions)
            {
                if (permission.OwnerModuleId != module.Id)
                {
                    throw new InvalidOperationException($"Permission '{permission.Id}' is declared by '{module.Id}' but owned by '{permission.OwnerModuleId}'.");
                }
            }

            foreach (var setting in module.Settings)
            {
                if (setting.OwnerModuleId != module.Id)
                {
                    throw new InvalidOperationException($"Setting '{setting.Key}' is declared by '{module.Id}' but owned by '{setting.OwnerModuleId}'.");
                }
            }
        }

        ValidateUnique(modules.SelectMany(module => module.Features.Select(feature => feature.Id.Value)), "feature");
        ValidateUnique(modules.SelectMany(module => module.Permissions.Select(permission => permission.Id.Value)), "permission");
        ValidateUnique(modules.SelectMany(module => module.Settings.Select(setting => setting.Key.Value)), "setting");
    }

    private static void ValidateFeatureReferences(
        IReadOnlyCollection<ModuleDescriptor> modules,
        IReadOnlyDictionary<ModuleId, ModuleDescriptor> modulesById)
    {
        var featureOwners = modules
            .SelectMany(module => module.Features.Select(feature => (feature.Id, module.Id)))
            .ToDictionary(item => item.Id, item => item.Id1);

        foreach (var module in modules)
        {
            foreach (var feature in module.Features)
            {
                foreach (var dependency in feature.Dependencies)
                {
                    if (!featureOwners.TryGetValue(dependency, out var owner))
                    {
                        throw new InvalidOperationException($"Feature '{feature.Id}' depends on missing feature '{dependency}'.");
                    }

                    if (owner != module.Id && !module.Dependencies.Contains(owner))
                    {
                        throw new InvalidOperationException($"Feature '{feature.Id}' depends on feature '{dependency}' from module '{owner}' without a module dependency.");
                    }
                }
            }

            foreach (var permission in module.Permissions)
            {
                if (permission.RequiredFeature is not FeatureId required)
                {
                    continue;
                }

                if (!featureOwners.TryGetValue(required, out var owner))
                {
                    throw new InvalidOperationException($"Permission '{permission.Id}' requires missing feature '{required}'.");
                }

                if (owner != module.Id && !module.Dependencies.Contains(owner))
                {
                    throw new InvalidOperationException($"Permission '{permission.Id}' requires feature '{required}' from module '{owner}' without a module dependency.");
                }
            }
        }
    }

    private static void ValidateUnique(IEnumerable<string> values, string kind)
    {
        var duplicate = values
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate {kind} id '{duplicate.Key}'.");
        }
    }
}
