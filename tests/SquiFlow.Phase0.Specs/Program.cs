using SquiFlow.ApplicationKernel;
using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Hosting;
using SquiFlow.ApplicationKernel.Modules;
using SquiFlow.ApplicationKernel.Permissions;
using SquiFlow.ApplicationKernel.Settings;
using SquiFlow.Customers;
using SquiFlow.Customers.Domain;
using SquiFlow.Guard.Supervision;

var specs = new (string Name, Action Run)[]
{
    ("module graph orders dependencies deterministically", ModuleGraphOrdersDependencies),
    ("module graph rejects missing dependencies", ModuleGraphRejectsMissingDependency),
    ("module graph rejects dependency cycles", ModuleGraphRejectsCycle),
    ("host filtering is explicit", HostFilteringIsExplicit),
    ("feature publication closes dependencies", FeaturePublicationClosesDependencies),
    ("feature publication respects platform ceiling", FeaturePublicationRespectsPlatformCeiling),
    ("feature publication respects host", FeaturePublicationRespectsHost),
    ("feature publication respects release policy", FeaturePublicationRespectsReleasePolicy),
    ("typed setting respects platform ceiling", SettingResolutionIsCapped),
    ("experiment assignment is stable", ExperimentAssignmentIsStable),
    ("customer name protects documented invariant", CustomerNameProtectsInvariant),
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

static void ModuleGraphRejectsMissingDependency() =>
    Throws<InvalidOperationException>(() =>
        ModuleGraph.Build([Descriptor("child", [new ModuleId("missing")])]));

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
    var module = new ModuleId("sample");
    var baseFeature = Feature(module, "sample.base");
    var childFeature = Feature(module, "sample.child", [baseFeature.Id]);

    var snapshot = FeatureSnapshotBuilder.Publish(
        [baseFeature, childFeature],
        HostKind.Workstation,
        [baseFeature.Id, childFeature.Id],
        [childFeature.Id],
        new HashSet<ReleaseChannel> { ReleaseChannel.Stable },
        revision: 7);

    True(snapshot.IsEnabled(baseFeature.Id));
    True(snapshot.IsEnabled(childFeature.Id));
    Equal(7L, snapshot.Revision);
}

static void FeaturePublicationRespectsPlatformCeiling()
{
    var feature = Feature(new ModuleId("sample"), "sample.feature");
    Throws<InvalidOperationException>(() => FeatureSnapshotBuilder.Publish(
        [feature],
        HostKind.Workstation,
        platformAllowed: [],
        tenantRequested: [feature.Id],
        allowedChannels: new HashSet<ReleaseChannel> { ReleaseChannel.Stable },
        revision: 1));
}

static void FeaturePublicationRespectsHost()
{
    var module = new ModuleId("sample");
    var feature = new FeatureDefinition(
        new FeatureId("sample.server-only"),
        module,
        [],
        new HashSet<HostKind> { HostKind.CoreApi },
        ReleaseChannel.Stable,
        OfflineFeaturePolicy.ServerRequired);

    Throws<InvalidOperationException>(() => FeatureSnapshotBuilder.Publish(
        [feature],
        HostKind.Workstation,
        [feature.Id],
        [feature.Id],
        new HashSet<ReleaseChannel> { ReleaseChannel.Stable },
        revision: 1));
}

static void FeaturePublicationRespectsReleasePolicy()
{
    var module = new ModuleId("sample");
    var feature = new FeatureDefinition(
        new FeatureId("sample.beta"),
        module,
        [],
        new HashSet<HostKind> { HostKind.Workstation },
        ReleaseChannel.Beta,
        OfflineFeaturePolicy.SnapshotAllowed);

    Throws<InvalidOperationException>(() => FeatureSnapshotBuilder.Publish(
        [feature],
        HostKind.Workstation,
        [feature.Id],
        [feature.Id],
        new HashSet<ReleaseChannel> { ReleaseChannel.Stable },
        revision: 1));
}

static void SettingResolutionIsCapped()
{
    Equal(75, SettingResolver.ResolveCappedInt(CustomersModule.SearchResultLimit, 100, 75));
    Equal(100, SettingResolver.ResolveCappedInt(CustomersModule.SearchResultLimit, 100, 250));
}

static void ExperimentAssignmentIsStable()
{
    var experiment = new ExperimentId("workstation.navigation");
    var first = StableExperimentAssignment.Bucket(experiment, "subject-1");
    var second = StableExperimentAssignment.Bucket(experiment, "subject-1");
    Equal(first, second);
    True(first is >= 0 and < 10_000);
}

static void CustomerNameProtectsInvariant()
{
    Equal("ABC Printing", new CustomerName("  ABC Printing  ").Value);
    Throws<ArgumentException>(() => _ = new CustomerName("   "));
}

static void GuardRestartBudgetIsBounded()
{
    var budget = new RestartBudget(2, TimeSpan.FromMinutes(1));
    var now = DateTimeOffset.Parse("2026-09-14T00:00:00Z");
    True(budget.TryRegister(now));
    True(budget.TryRegister(now.AddSeconds(1)));
    False(budget.TryRegister(now.AddSeconds(2)));
    True(budget.TryRegister(now.AddMinutes(2)));
}

static FeatureDefinition Feature(ModuleId owner, string id, IReadOnlyCollection<FeatureId>? dependencies = null) => new(
    new FeatureId(id),
    owner,
    dependencies ?? Array.Empty<FeatureId>(),
    new HashSet<HostKind> { HostKind.Workstation },
    ReleaseChannel.Stable,
    OfflineFeaturePolicy.SnapshotAllowed);

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
    if (!value)
    {
        throw new InvalidOperationException("Expected true.");
    }
}

static void False(bool value)
{
    if (value)
    {
        throw new InvalidOperationException("Expected false.");
    }
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
