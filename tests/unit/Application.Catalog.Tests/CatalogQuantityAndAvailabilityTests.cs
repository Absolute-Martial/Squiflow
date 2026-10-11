using Application.Catalog;
using Xunit;

namespace Application.Catalog.Tests;

public sealed class CatalogQuantityAndAvailabilityTests
{
    [Theory]
    [InlineData(1, 1, 8, 2, 0.12)]
    [InlineData(1, 3, 8, 2, 0.38)]
    [InlineData(1, 1, 3, 4, 0.3333)]
    [InlineData(2.5, 12, 1, 0, 30)]
    public void ExplicitConversionRoundsOnlyAtDeclaredTargetPrecision(decimal quantity, decimal numerator,
        decimal denominator, int targetPrecision, decimal expected) =>
        Assert.Equal(expected, CatalogQuantityConversion.Convert(quantity, 2, targetPrecision, numerator, denominator));

    [Fact]
    public void ExactRatioAvoidsIntermediateOverflowAndAcceptsMathematicallyIntegralQuantity() =>
        Assert.Equal(1_000_000_000_000_000_000m,
            CatalogQuantityConversion.Convert(1_000_000_000_000_000_000.0m, 0, 0, 9_999_999_999m, 9_999_999_999m));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    public void InvalidOrRoundedToZeroConversionIsRejected(decimal quantity) =>
        Assert.Throws<CatalogValidationException>(() => CatalogQuantityConversion.Convert(quantity, 3, 0, 1, 1000));

    [Fact]
    public void SelectionPinsConversionAndRetainsBothUnitsAndRoundedBaseQuantity()
    {
        var (item, source, target) = Snapshots(CatalogStockMode.NonStock);
        var conversion = new CatalogConversionSnapshot(item.TenantId, source.UnitId, target.UnitId, 7,
            1, 8, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var result = CatalogLineFactSelection.Assess(new(item.ItemId, source.UnitId, 1, 7), item, source, target, conversion);
        Assert.Equal(CatalogLineFactsStatus.Available, result.Status);
        Assert.Equal(0.12m, result.Facts?.BaseQuantity);
        Assert.Equal(7, result.Facts?.Conversion.Revision);
        Assert.Equal(2, result.Facts?.BaseUnitPrecision);
        Assert.Equal("Square metre", result.Facts?.BaseUnitName);
        Assert.Equal(CatalogLineFactsStatus.ConversionRevisionRequired,
            CatalogLineFactSelection.Assess(new(item.ItemId, source.UnitId, 1), item, source, target, null).Status);
    }

    [Fact]
    public void AvailabilityOnlySelectionRejectsUnavailableWhilePreciseModeDoesNotInventBalance()
    {
        var (item, source, target) = Snapshots(CatalogStockMode.AvailabilityOnly);
        var conversion = new CatalogConversionSnapshot(item.TenantId, source.UnitId, target.UnitId, 1,
            1, 1, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(CatalogLineFactsStatus.ItemUnavailable,
            CatalogLineFactSelection.Assess(new(item.ItemId, source.UnitId, 1, 1), item, source, target, conversion).Status);
        var available = item with { Availability = CatalogAvailability.Available };
        Assert.Equal(CatalogLineFactsStatus.Available,
            CatalogLineFactSelection.Assess(new(item.ItemId, source.UnitId, 1, 1), available, source, target, conversion).Status);
        var precise = item with { StockMode = CatalogStockMode.PreciseStock, Availability = null };
        Assert.Equal(CatalogLineFactsStatus.Available,
            CatalogLineFactSelection.Assess(new(item.ItemId, source.UnitId, 1, 1), precise, source, target, conversion).Status);
    }

    [Fact]
    public void MalformedUtf16NamesFailWithOwnedValidationNotNormalizerExceptions() =>
        Assert.Equal("unit_name_invalid", Assert.Throws<CatalogValidationException>(() =>
            CatalogUnitIntent.Create(new("EA", "broken\ud800", 0))).Code);

    [Fact]
    public void CatalogHasNoTransportOrProviderDependencies()
    {
        var dependencies = typeof(CatalogUnitSnapshot).Assembly.GetReferencedAssemblies().Select(name => name.Name!);
        Assert.DoesNotContain(dependencies, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
            name.StartsWith("Npgsql", StringComparison.Ordinal) || name.StartsWith("OpenFga", StringComparison.Ordinal));
    }

    private static (CatalogItemSnapshot Item, CatalogUnitSnapshot Source, CatalogUnitSnapshot Target) Snapshots(CatalogStockMode mode)
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var source = new CatalogUnitSnapshot(Guid.NewGuid(), tenant, "FT2", "Square foot", 2, CatalogEntityStatus.Active, 3, actor, now);
        var target = new CatalogUnitSnapshot(Guid.NewGuid(), tenant, "M2", "Square metre", 2, CatalogEntityStatus.Active, 5, actor, now);
        var item = new CatalogItemSnapshot(Guid.NewGuid(), tenant, "BANNER", "Banner", null, CatalogItemKind.Product,
            CatalogEntityStatus.Active, target.UnitId, mode, 2, actor, now,
            Availability: mode == CatalogStockMode.AvailabilityOnly ? CatalogAvailability.Unavailable : null);
        return (item, source, target);
    }
}
