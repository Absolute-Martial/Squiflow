using SquiFlow.ApplicationKernel;
using SquiFlow.ApplicationKernel.Authorization;
using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Modules;
using SquiFlow.ApplicationKernel.Settings;
using SquiFlow.Customers;
using SquiFlow.Guard;

var specs = new (string Name, Action Run)[]
{
    ("module graph orders dependencies", ModuleGraphOrdersDependencies),
    ("module graph rejects missing dependencies", ModuleGraphRejectsMissingDependency),
    ("module graph rejects cycles", ModuleGraphRejectsCycle),
    ("host filtering is explicit", HostFilteringIsExplicit),
    ("feature publication closes dependencies", FeaturePublicationClosesDependencies),
    ("feature publication respects platform ceiling", FeaturePublicationRespectsCeiling),
    ("typed setting respects tenant override and platform ceiling", SettingResolutionIsBounded),
    ("guard restart budget is bounded", GuardRestartBudgetIsBounded)
};

var failures = new List<string>();
foreach (var spec in specs)
{
    try
    {
        spec.Run();
        Console.WriteLine($"PASS {spec.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {spec.Name}: {exception.Message}");
    }
}

foreach (var failure in failures)
{
    Console.Error.WriteLine(failure);
}

return failures.Count == 0 ? 0 : 1;

static void ModuleGraphOrdersDependencies()
{
    var root = Descriptor("root");
    var child = Descriptor("child", [root.Id]);
    var graph = ModuleGraph.Build([child, root]);
    Equal("root", graph.OrderedModules[0].Id.Value);
    Equal("child", graph.OrderedModules[1].Id.Value);
}

static void ModuleGraphRejectsMissingDependency()
{
    Throws<InvalidOperationException>(() =>
        ModuleGraph.Build([Descriptor("child", [new ModuleId("missing")])]));
}

static void ModuleGraphRejectsCycle()
{
    var a = Descriptor("a", [new ModuleId("b")]);
    var b = Descriptor("b", [new ModuleId("a")]);
    Throws<InvalidOperationException>(() => ModuleGraph.Build([a, b]));
}

static void HostFilteringIsExplicit()
{
    var graph = ModuleGraph.Build([CustomersModule.Descriptor]);
    Equal(1, graph.ForHost(HostKind.Workstation).Count);
    Equal(0, graph.ForHost(HostKind.Guard).Count);
}

static void FeaturePublicationClosesDependencies()
{
    var baseFeature = new FeatureDefinition(new FeatureId("base"), new ModuleId("m"), []);
    var childFeature = new FeatureDefinition(new FeatureId("child"), new ModuleId("m"), [baseFeature.Id]);

    var snapshot = FeatureSnapshotBuilder.Publish(
        [baseFeature, childFeature],
        [baseFeature.Id, childFeature.Id],
        [childFeature.Id],
        revision: 7);

    True(snapshot.IsEnabled(baseFeature.Id));
    True(snapshot.IsEnabled(childFeature.Id));
    Equal(7L, snapshot.Revision);
}

static void FeaturePublicationRespectsCeiling()
{
    var feature = new FeatureDefinition(new FeatureId("feature"), new ModuleId("m"), []);
    Throws<InvalidOperationException>(() =>
        FeatureSnapshotBuilder.Publish([feature], [], [feature.Id], revision: 1));
}

static void SettingResolutionIsBounded()
{
    Equal(75, SettingResolver.ResolveBoundedInt(CustomersModule.SearchResultLimit, 100, 75));
    Equal(100, SettingResolver.ResolveBoundedInt(CustomersModule.SearchResultLimit, 100, 250));
}

static void GuardRestartBudgetIsBounded()
{
    var budget = new RestartBudget(2, TimeSpan.FromMinutes(1));
    var now = DateTimeOffset.Parse("2026-09-12T00:00:00Z");
    True(budget.TryRegister(now));
    True(budget.TryRegister(now.AddSeconds(1)));
    False(budget.TryRegister(now.AddSeconds(2)));
    True(budget.TryRegister(now.AddMinutes(2)));
}

static ModuleDescriptor Descriptor(string id, IReadOnlyCollection<ModuleId>? dependencies = null) => new(
    new ModuleId(id),
    new Version(1, 0),
    dependencies ?? Array.Empty<ModuleId>(),
    new HashSet<HostKind> { HostKind.CoreApi },
    Array.Empty<FeatureDefinition>(),
    Array.Empty<PermissionDefinition>(),
    Array.Empty<ISettingDefinition>());

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void True(bool value)
{
    if (!value) throw new InvalidOperationException("Expected true.");
}

static void False(bool value)
{
    if (value) throw new InvalidOperationException("Expected false.");
}

static void Throws<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}
