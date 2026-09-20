using System.Reflection;
using SquiFlow.ApplicationProfiles;
using Xunit;

namespace SquiFlow.ApplicationProfiles.Tests;

public sealed class FeatureCatalogTests
{
    [Fact]
    public void CompileIncludesTransitiveDependenciesInDependencyOrder()
    {
        var catalog = FeatureCatalog.Create([
            FeatureDefinition.Create("Platform.Core", isAlwaysEnabled: true, isTenantSelectable: false),
            FeatureDefinition.Create("Sales.Orders", ["Platform.Core"]),
            FeatureDefinition.Create("Sales.Returns", ["Sales.Orders"]),
        ]);

        var selection = catalog.Compile(["Sales.Returns"]);

        Assert.Equal(["Sales.Returns"], selection.RequestedFeatureIds);
        Assert.Equal(
            ["Platform.Core", "Sales.Orders", "Sales.Returns"],
            selection.EffectiveFeatureIds);
        Assert.True(selection.Contains("Sales.Orders"));
    }

    [Fact]
    public void CompileIncludesAlwaysEnabledFeaturesForAnEmptySelection()
    {
        var catalog = FeatureCatalog.Create([
            FeatureDefinition.Create("Platform.Core", isAlwaysEnabled: true, isTenantSelectable: false),
            FeatureDefinition.Create("Sales.Orders", ["Platform.Core"]),
        ]);

        var selection = catalog.Compile([]);

        Assert.Empty(selection.RequestedFeatureIds);
        Assert.Equal(["Platform.Core"], selection.EffectiveFeatureIds);
    }

    [Fact]
    public void CompileRejectsDirectSelectionOfDependencyOnlyFeature()
    {
        var catalog = FeatureCatalog.Create([
            FeatureDefinition.Create("Platform.Core", isTenantSelectable: false),
            FeatureDefinition.Create("Sales.Orders", ["Platform.Core"]),
        ]);

        var exception = Assert.Throws<ArgumentException>(() => catalog.Compile(["Platform.Core"]));

        Assert.Equal("requestedFeatureIds", exception.ParamName);
    }

    [Fact]
    public void CreateRejectsMissingDependency()
    {
        var exception = Assert.Throws<ArgumentException>(() => FeatureCatalog.Create([
            FeatureDefinition.Create("Sales.Orders", ["Platform.Core"]),
        ]));

        Assert.Equal("definitions", exception.ParamName);
        Assert.Contains("undefined feature", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRejectsDependencyCycleWithPath()
    {
        var exception = Assert.Throws<ArgumentException>(() => FeatureCatalog.Create([
            FeatureDefinition.Create("Sales.Orders", ["Sales.Returns"]),
            FeatureDefinition.Create("Sales.Returns", ["Sales.Orders"]),
        ]));

        Assert.Equal("definitions", exception.ParamName);
        Assert.Contains("Sales.Orders -> Sales.Returns -> Sales.Orders", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRejectsDuplicateIdentifiers()
    {
        var exception = Assert.Throws<ArgumentException>(() => FeatureCatalog.Create([
            FeatureDefinition.Create("Sales.Orders"),
            FeatureDefinition.Create("Sales.Orders"),
        ]));

        Assert.Equal("definitions", exception.ParamName);
    }

    [Fact]
    public void DefinitionRejectsDuplicateAndSelfDependencies()
    {
        var duplicate = Assert.Throws<ArgumentException>(() =>
            FeatureDefinition.Create("Sales.Returns", ["Sales.Orders", "Sales.Orders"]));
        var self = Assert.Throws<ArgumentException>(() =>
            FeatureDefinition.Create("Sales.Returns", ["Sales.Returns"]));

        Assert.Equal("dependencies", duplicate.ParamName);
        Assert.Equal("dependencies", self.ParamName);
    }

    [Fact]
    public void CompileRejectsUnknownRequestedFeature()
    {
        var catalog = FeatureCatalog.Create([
            FeatureDefinition.Create("Platform.Core", isAlwaysEnabled: true),
        ]);

        var exception = Assert.Throws<ArgumentException>(() => catalog.Compile(["Sales.Orders"]));

        Assert.Equal("requestedFeatureIds", exception.ParamName);
        Assert.Contains("not present", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FingerprintsAreIndependentOfDefinitionAndRequestOrder()
    {
        var first = FeatureCatalog.Create([
            FeatureDefinition.Create("Sales.Returns", ["Sales.Orders"]),
            FeatureDefinition.Create("Platform.Core", isAlwaysEnabled: true, isTenantSelectable: false),
            FeatureDefinition.Create("Sales.Orders", ["Platform.Core"]),
        ]);
        var second = FeatureCatalog.Create([
            FeatureDefinition.Create("Sales.Orders", ["Platform.Core"]),
            FeatureDefinition.Create("Sales.Returns", ["Sales.Orders"]),
            FeatureDefinition.Create("Platform.Core", isAlwaysEnabled: true, isTenantSelectable: false),
        ]);

        var firstSelection = first.Compile(["Sales.Returns", "Sales.Orders"]);
        var secondSelection = second.Compile(["Sales.Orders", "Sales.Returns"]);

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(firstSelection.SelectionFingerprint, secondSelection.SelectionFingerprint);
        Assert.Equal(firstSelection.EffectiveFeatureIds, secondSelection.EffectiveFeatureIds);
    }

    [Fact]
    public void FeatureMetadataChangesCatalogAndSelectionFingerprints()
    {
        var selectable = FeatureCatalog.Create([
            FeatureDefinition.Create("Platform.Core"),
        ]);
        var alwaysEnabled = FeatureCatalog.Create([
            FeatureDefinition.Create("Platform.Core", isAlwaysEnabled: true),
        ]);

        Assert.NotEqual(selectable.Fingerprint, alwaysEnabled.Fingerprint);
        Assert.NotEqual(
            selectable.Compile([]).SelectionFingerprint,
            alwaysEnabled.Compile([]).SelectionFingerprint);
    }

    [Fact]
    public void SelectionFingerprintPreservesExplicitRequestIntent()
    {
        var catalog = FeatureCatalog.Create([
            FeatureDefinition.Create("Platform.Core"),
            FeatureDefinition.Create("Sales.Orders", ["Platform.Core"]),
        ]);

        var dependencyImplicit = catalog.Compile(["Sales.Orders"]);
        var dependencyExplicit = catalog.Compile(["Platform.Core", "Sales.Orders"]);

        Assert.Equal(dependencyImplicit.EffectiveFeatureIds, dependencyExplicit.EffectiveFeatureIds);
        Assert.NotEqual(dependencyImplicit.SelectionFingerprint, dependencyExplicit.SelectionFingerprint);
    }

    [Theory]
    [InlineData("contains spaces")]
    [InlineData("starts/slash")]
    [InlineData(".starts-with-dot")]
    [InlineData("contains:colon")]
    public void DefinitionRejectsInvalidStableIdentifiers(string featureId)
    {
        Assert.Throws<ArgumentException>(() => FeatureDefinition.Create(featureId));
    }

    [Fact]
    public void InputsAreBoundedBeforeUnrestrictedMaterialization()
    {
        var definitions = Enumerable.Range(0, FeatureCatalog.MaximumFeatureCount + 1)
            .Select(index => FeatureDefinition.Create($"Feature.{index}"));

        var exception = Assert.Throws<ArgumentException>(() => FeatureCatalog.Create(definitions));

        Assert.Equal("definitions", exception.ParamName);
    }

    [Fact]
    public void CapabilityRemainsHostContainerAndProviderNeutral()
    {
        var references = typeof(FeatureCatalog).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Autofac", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Finbuckle", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Volo.Abp", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("OrchardCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("FSH", StringComparison.Ordinal));
    }

    [Fact]
    public void AssemblyUsesLockedProductVersionWithoutCodenameProductMetadata()
    {
        var assembly = typeof(FeatureCatalog).Assembly;

        Assert.Equal(new Version(0, 1, 0, 0), assembly.GetName().Version);
        Assert.Equal(
            "0.1.0",
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        Assert.Null(assembly.GetCustomAttribute<AssemblyProductAttribute>());
        Assert.Null(assembly.GetCustomAttribute<AssemblyCompanyAttribute>());
    }
}
