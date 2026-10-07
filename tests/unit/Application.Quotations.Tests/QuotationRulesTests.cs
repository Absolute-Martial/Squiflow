using Application.Pricing;
using Xunit;

namespace Application.Quotations.Tests;

public sealed class QuotationRulesTests
{
    private static readonly string[] ForbiddenReferences = ["Microsoft.AspNetCore", "Npgsql", "Microsoft.EntityFrameworkCore", "OpenFga"];
    private static QuotationDraftRequest Input() => new(QuotationPriceMode.Manual, " Offer ", "usd",
        new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.FromHours(5.75)),
        [new(2, " Product ", "ea", 1.2345m)], Terms: " Standard terms ");
    [Fact]
    public void NormalizationPinsInstantsAndSemanticDecimalFactsWithoutMutatingInput()
    {
        var input = Input(); var result = QuotationRules.Normalize(input);
        Assert.Equal("USD", result.CurrencyCode); Assert.Equal("Offer", result.Summary);
        Assert.Equal(TimeSpan.Zero, result.ValidUntil.Offset);
        Assert.Equal(input.ValidUntil, result.ValidUntil);
        Assert.Equal("EA", result.Lines[0].UnitCode);
        Assert.Equal(" Standard terms ", input.Terms);
        var equivalent = QuotationRules.Normalize(input with { Lines = [input.Lines[0] with { Quantity = 2.0000m, UnitPrice = 1.23450m }] });
        Assert.Equal(QuotationRules.Fingerprint(result), QuotationRules.Fingerprint(equivalent));
    }
    [Theory]
    [InlineData("", "summary_invalid")]
    [InlineData("bad\nsummary", "summary_invalid")]
    public void InvalidTextCannotBecomeAnOffer(string value, string code)
    {
        var error = Assert.Throws<QuotationValidationException>(() => QuotationRules.Normalize(Input() with { Summary = value }));
        Assert.Equal(code, error.Code);
    }
    [Fact]
    public void MixedClientPriceAndCatalogFactsAreRejected()
    {
        var input = Input();
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Normalize(input with { Lines = [input.Lines[0] with { ItemId = Guid.NewGuid() }] }));
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Normalize(input with { Mode = QuotationPriceMode.Catalog }));
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Normalize(input with { Lines = [input.Lines[0] with { OverridePrice = 2 }] }));
    }
    [Fact]
    public void BoundsAndUnsupportedPrecisionFailExplicitly()
    {
        var input = Input();
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Normalize(input with { Lines = [] }));
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Normalize(input with { Lines = Enumerable.Repeat(input.Lines[0], 101).ToArray() }));
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Normalize(input with { Lines = [input.Lines[0] with { Quantity = 0.00001m }] }));
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Normalize(input with { ValidUntil = input.ValidUntil.AddTicks(1) }));
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Normalize(input with { CurrencyCode = "US1" }));
    }
    [Fact]
    public void RetainedArithmeticMustMatchTheSharedCalculator()
    {
        var input = QuotationRules.Normalize(Input());
        var amount = SellingPriceArithmetic.LineAmount(2, 1.2345m);
        var offer = new QuotationDraftFacts(input.Mode, input.Summary, input.CurrencyCode, input.ValidUntil,
            [new(1, "Product", 2, "EA", 1.2345m, amount)], amount, null, null, null, false);
        QuotationRules.Validate(offer);
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(offer with { Lines = null! }));
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(offer with { Lines = [null!] }));
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(offer with { CurrencyCode = "usd" }));
        Assert.Equal(2.469m, amount);
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(offer with { Total = 2.47m }));
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(offer with { Lines = [offer.Lines[0] with { LineTotal = 2.47m }] }));
    }
    [Fact]
    public void CoreRemainsProviderAndHostNeutral()
    {
        Assert.DoesNotContain(typeof(QuotationApplication).Assembly.GetReferencedAssemblies(), assembly =>
            ForbiddenReferences.Any(prefix => assembly.Name!.StartsWith(prefix, StringComparison.Ordinal)));
    }
}
