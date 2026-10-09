using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Catalog;
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
    // Retained catalogue facts are validated against the arithmetic and envelope semantics their own
    // snapshot pinned. A reader must never re-derive them with its own current predicate meaning.
    // The retained envelope snapshot pins the semantics version whose evaluator produced its
    // BeyondPolicy decision. These inputs are chosen so the frozen version-1 meaning and the
    // plausible future meaning (rounding the base price before the percentage comparison)
    // DISAGREE. A reader that re-derived the retained decision with current semantics would flip
    // this decision and reject the issued quotation; the pinned version keeps it valid.
    [Fact]
    public void RetainedCatalogueFactsStayValidAfterThePolicyPredicateSemanticsChange()
    {
        // Version 1 compares against the exact base price: 10.005 less 10% is 9.0045, so 9.006 is
        // in-envelope. The plausible future meaning rounds the base price to currency minor units
        // first, giving 9.009, and would call the same issued quotation beyond-policy.
        var offer = CatalogOffer(overridePrice: 9.006m, baseUnitPrice: 10.005m);
        Assert.False(offer.Lines[0].PriceSelection!.Explanation.Override!.BeyondPolicy);
        Assert.True(FutureRoundedMeaningWouldDisagree(offer));
        QuotationRules.Validate(offer);
        QuotationRules.OrderFacts(Issued(offer));

        // Pinning the version did not turn this into unchecked trust of the retained boolean: a
        // decision contradicting the pinned meaning is still rejected.
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(Retained(offer, beyondPolicy: true)));
        QuotationRules.Validate(Retained(offer, beyondPolicy: false));
    }

    [Fact]
    public void RetainedBaseQuantityUsesThePinnedArithmeticVersionNotCurrentRounding()
    {
        // 0.25 at one decimal is 0.2 under the frozen toEven version and 0.3 under half-up.
        var facts = new CatalogLineFacts(Guid.NewGuid(), "SKU", Guid.NewGuid(), "Item", "EA", "Each", 1, 4, 1, 1,
            new(Guid.Empty, Guid.Empty, 1, 1m, 4m), "EA", "Each", 1, 1, 0.2m);
        Assert.Equal(0.2m, CatalogQuantityConversion.Convert(QuantityArithmetic.Version1,
            facts.Quantity, facts.UnitPrecision, facts.BaseUnitPrecision!.Value,
            facts.Conversion.Numerator, facts.Conversion.Denominator));
        Assert.NotEqual(0.2m, decimal.Round(0.25m, 1, MidpointRounding.AwayFromZero));
        // An unreadable version fails closed instead of silently using the current evaluator.
        Assert.Throws<CatalogValidationException>(() => CatalogQuantityConversion.Convert(
            UnsupportedVersion, 1, 4, 1, 1m, 4m));
        Assert.False(QuantityArithmetic.RoundingSupported(QuantityArithmetic.Version1, "awayFromZero"));
    }

    [Fact]
    public void RetainedBeyondPolicyDecisionIsHonouredFromTheRetainedValue()
    {
        var offer = CatalogOffer();
        var evidence = offer.Lines[0].PriceSelection!.Explanation.Override!;
        Assert.True(evidence.BeyondPolicy);
        QuotationRules.Validate(offer);
        // The evaluated decision is read from the retained value, not recomputed from live policy.
        Assert.True(evidence.BeyondPolicy);
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(Retained(offer, beyondPolicy: false)));
        var inside = CatalogOffer(overridePrice: 12m);
        Assert.False(inside.Lines[0].PriceSelection!.Explanation.Override!.BeyondPolicy);
        QuotationRules.Validate(inside);
    }

    [Fact]
    public void RetainedFactsRequiringAnUnknownSemanticsVersionFailClosedInsteadOfUsingCurrentMeaning()
    {
        var offer = CatalogOffer();
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(offer with
        {
            Lines = [offer.Lines[0] with { Catalog = offer.Lines[0].Catalog! with { QuantityArithmeticVersion = UnsupportedVersion } }]
        }));
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(offer with
        {
            Lines = [offer.Lines[0] with { Catalog = offer.Lines[0].Catalog! with { QuantityRounding = "awayFromZero" } }]
        }));
        // A later reader may add a newer semantics version. Version 1 must remain supported
        // forever, because every already-issued quotation pins it; an unreadable version must
        // never be silently evaluated with the reader's current meaning.
        Assert.True(QuantityArithmetic.Supported(QuantityArithmetic.Version1));
        Assert.True(PricingOverridePolicySemantics.Supported(PricingOverridePolicySemantics.Version1));
        Assert.False(QuantityArithmetic.Supported(UnsupportedVersion));
        Assert.False(PricingOverridePolicySemantics.Supported(UnsupportedVersion));
        var newerSnapshot = new PricingOverridePolicy(1, 0, 100, 10, 20, UnsupportedVersion);
        Assert.Throws<PricingValidationException>(() => PricingOverridePolicySemantics.Contains(newerSnapshot, 1, 20));
        // A quotation whose snapshot pins such a version fails closed rather than using current meaning.
        var newerOffer = CatalogOffer();
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(newerOffer with
        {
            Lines = [newerOffer.Lines[0] with { PriceSelection = newerOffer.Lines[0].PriceSelection! with
            {
                Explanation = newerOffer.Lines[0].PriceSelection!.Explanation with
                { Policy = newerSnapshot } } }]
        }));
    }

    // The version is carried inside the retained snapshot, so a future reader evaluates the pinned
    // evaluator rather than its own meaning. This pins that serialization round-trip, which is what
    // makes every already-issued quotation readable by any later reader.
    [Fact]
    public void ThePinnedSemanticsVersionSurvivesTheRetainedFactsSerializationRoundTrip()
    {
        var offer = CatalogOffer();
        var roundTripped = JsonSerializer.Deserialize<QuotationDraftFacts>(JsonSerializer.Serialize(offer))!;
        var line = roundTripped.Lines[0];
        Assert.Equal(QuantityArithmetic.Version1, line.Catalog!.QuantityArithmeticVersion);
        Assert.Equal(QuantityArithmetic.Version1Rounding, line.Catalog.QuantityRounding);
        Assert.Equal(PricingOverridePolicySemantics.Version1, line.PriceSelection!.Explanation.Policy!.EnvelopeSemanticsVersion);
        QuotationRules.Validate(roundTripped);
        Assert.True(line.PriceSelection.Explanation.Override!.BeyondPolicy);

        // Facts written before these fields existed carry neither, and must read back as version 1,
        // so nothing already issued becomes unreadable when a later reader introduces a newer version.
        var legacy = JsonNode.Parse(JsonSerializer.Serialize(offer))!.AsObject();
        legacy["Lines"]!.AsArray()[0]!["Catalog"]!.AsObject().Remove(nameof(CatalogLineFacts.QuantityArithmeticVersion));
        legacy["Lines"]!.AsArray()[0]!["PriceSelection"]!["Explanation"]!["Policy"]!.AsObject()
            .Remove(nameof(PricingOverridePolicy.EnvelopeSemanticsVersion));
        var legacyFacts = JsonSerializer.Deserialize<QuotationDraftFacts>(legacy.ToJsonString())!;
        var legacyLine = legacyFacts.Lines[0];
        Assert.Equal(QuantityArithmetic.Version1, legacyLine.Catalog!.QuantityArithmeticVersion);
        Assert.Equal(QuantityArithmetic.Version1Rounding, legacyLine.Catalog.QuantityRounding);
        Assert.Equal(PricingOverridePolicySemantics.Version1, legacyLine.PriceSelection!.Explanation.Policy!.EnvelopeSemanticsVersion);
        QuotationRules.Validate(legacyFacts);
    }

    [Fact]
    public void CoreRemainsProviderAndHostNeutral()
    {
        Assert.DoesNotContain(typeof(QuotationApplication).Assembly.GetReferencedAssemblies(), assembly =>
            ForbiddenReferences.Any(prefix => assembly.Name!.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private static readonly DateTimeOffset At = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    // A version no reader implements, standing in for one added later by another writer.
    private const int UnsupportedVersion = int.MaxValue;
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UnitId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // A published price plus a conversion whose retained base quantity is self-consistent, so the
    // only thing a retained-fact reader re-derives is the pinned arithmetic and envelope semantics.
    private static QuotationDraftFacts CatalogOffer(decimal overridePrice = 150m, decimal baseUnitPrice = 12m)
    {
        var unitPrice = overridePrice;
        var catalog = new CatalogLineFacts(ItemId, "SKU", UnitId, "Synthetic item", "EA", "Each", 2, 4, 1, 1,
            new(UnitId, UnitId, 1, 1m, 1m), "EA", "Each", 4, 1,
            CatalogQuantityConversion.Convert(QuantityArithmetic.Version1, 2, 4, 4, 1m, 1m));
        var published = new PriceRevision(TenantId, Guid.NewGuid(), 1,
            new(ItemId, "EA", "USD", PriceScope.Default(), UnitId), baseUnitPrice,
            new(At.AddDays(-1)), PricePublicationState.Published, Guid.NewGuid(), At.AddDays(-2), At.AddDays(-1));
        var policy = new PricingOverridePolicy(1, 0, 100, 10, 20);
        var overrideEvidence = new PriceOverrideEvidence(unitPrice, "Synthetic exception",
            PriceSelectionEngine.RetainedBeyondPolicy(policy, unitPrice, baseUnitPrice), null, null);
        var explanation = new PriceSelectionExplanation(unitPrice, published.RevisionId, 1, published.Key.Scope,
            [new(published.RevisionId, 1, published.Key.Scope, PricePublicationState.Published, published.Validity, baseUnitPrice)],
            overrideEvidence, null, policy.PolicyRevision, At, new(), published.PriceId, "Synthetic selection.", policy);
        var selection = new RetainedPriceSelection(ItemId, UnitId, "USD", unitPrice, explanation);
        var amount = SellingPriceArithmetic.LineAmount(2, unitPrice);
        return new(QuotationPriceMode.Catalog, "Synthetic catalogue offer", "USD", At.AddDays(2),
            [new(1, "Synthetic item", 2, "EA", unitPrice, amount, catalog, selection, published)], amount, null, null, null, false);
    }

    private static QuotationDraftFacts Retained(QuotationDraftFacts offer, bool beyondPolicy)
    {
        var line = offer.Lines[0]; var evidence = line.PriceSelection!.Explanation.Override!;
        return offer with
        {
            Lines = [line with
            {
                PriceSelection = line.PriceSelection! with
                {
                    Explanation = line.PriceSelection.Explanation with { Override = evidence with { BeyondPolicy = beyondPolicy } }
                }
            }]
        };
    }

    // The plausible future meaning the pinned version must survive: round the base price to
    // currency minor units before the percentage comparison.
    private static bool FutureRoundedMeaningWouldDisagree(QuotationDraftFacts offer)
    {
        var line = offer.Lines[0]; var policy = line.PriceSelection!.Explanation.Policy!;
        var rounded = decimal.Round(line.PublishedPrice!.BaseUnitPrice, 2, MidpointRounding.AwayFromZero);
        return policy.Contains(line.UnitPrice, rounded) != policy.Contains(line.UnitPrice, line.PublishedPrice.BaseUnitPrice);
    }

    private static QuotationIssuedFacts Issued(QuotationDraftFacts offer) =>
        new(Guid.NewGuid(), Guid.NewGuid(), TenantId, 1, 1, Guid.NewGuid(), At, offer);
}
