using System.Reflection;
using Application.Pricing;
using Xunit;

namespace Application.Pricing.Tests;

public sealed class PriceSelectionEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ItemId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly PriceSelectionContext FullContext = new(
        Guid.Parse("00000000-0000-0000-0000-000000000001"),
        Guid.Parse("00000000-0000-0000-0000-000000000002"),
        Guid.Parse("00000000-0000-0000-0000-000000000003"),
        null,
        Guid.Parse("00000000-0000-0000-0000-000000000005"),
        Guid.Parse("00000000-0000-0000-0000-000000000006"), true);

    [Fact]
    public void PrecedenceIsExplicitAndNeverNewestRecordWins()
    {
        var scopes = new[]
        {
            PriceScope.Default(),
             PriceScope.Wholesale(),
            PriceScope.Organization(FullContext.OrganizationId!.Value),
            PriceScope.Program(FullContext.ProgramId!.Value),
            PriceScope.Customer(FullContext.CustomerId!.Value),
            PriceScope.CommittedAgreement(FullContext.CommittedAgreementId!.Value),
        };
        var candidates = scopes.Select((scope, index) => Revision(scope, 100m + index, index + 1)).ToArray();
        var request = SelectionRequest(FullContext, 9);

        var result = PriceSelectionEngine.Resolve(request, candidates, new PricingOverridePolicy(9, 1, 1000));

        var resolved = Assert.IsType<PriceResolved>(result);
        Assert.Equal(105m, resolved.UnitPrice);
        Assert.Equal(PriceScopeKind.CommittedAgreement, resolved.BasePrice.Key.Scope.Kind);
        Assert.Equal(105m, resolved.Explanation.SelectedPrice);
        Assert.Equal(PriceResolutionStatus.PriceResolved, result.Status);
    }

    [Fact]
    public void TwoApplicableFactsAtTheSameScopeAreAConflictEvenWhenOneIsNewer()
    {
        var customer = PriceScope.Customer(FullContext.CustomerId!.Value);
        var candidates = new[]
        {
            Revision(customer, 40m, 1),
            Revision(customer, 35m, 2),
            Revision(PriceScope.Default(), 10m, 3),
        };

        var result = PriceSelectionEngine.Resolve(
            SelectionRequest(FullContext, 1), candidates, new PricingOverridePolicy(1, 1, 100));

        var conflict = Assert.IsType<PriceConflict>(result);
        Assert.Equal(PriceResolutionStatus.PriceConflict, conflict.Status);
        Assert.Equal(2, conflict.Explanation.Candidates.Count);
        Assert.All(conflict.Explanation.Candidates, candidate => Assert.Null(candidate.BaseUnitPrice));
    }

    [Fact]
    public void MissingAndExpiredHaveDistinctOutcomes()
    {
        var policy = new PricingOverridePolicy(7, 1, 100);
        var missing = PriceSelectionEngine.Resolve(
            SelectionRequest(FullContext, 7), [], policy);
        Assert.IsType<PriceMissing>(missing);

        var expired = PriceSelectionEngine.Resolve(
            SelectionRequest(FullContext, 7),
            [Revision(PriceScope.Default(), 12m, 1, new PriceValidity(Now.AddDays(-2), Now.AddDays(-1)))],
            policy);
        Assert.IsType<PriceExpired>(expired);
        Assert.Equal(PriceResolutionStatus.PriceExpired, expired.Status);
    }

    [Fact]
    public void OverrideRequiresPermissionAndReasonAndKeepsBaseFactSeparate()
    {
        var basePrice = Revision(PriceScope.Default(), 20m, 1);
        var policy = new PricingOverridePolicy(11, 10, 30);
        var denied = PriceSelectionEngine.Resolve(
            SelectionRequest(FullContext, 11),
            [basePrice],
            policy,
            new PriceOverrideRequest(22m, "customer request", false, false));
        Assert.IsType<PriceOverrideRequired>(denied);

        var approved = PriceSelectionEngine.Resolve(
            SelectionRequest(FullContext, 11),
            [basePrice],
            policy,
            new PriceOverrideRequest(22m, "customer request", true, false));
        var resolved = Assert.IsType<PriceResolved>(approved);
        Assert.Equal(22m, resolved.UnitPrice);
        Assert.Equal(20m, resolved.BasePrice.BaseUnitPrice);
        Assert.Equal(22m, resolved.Explanation.SelectedPrice);
        Assert.Equal("customer request", resolved.AppliedOverride?.Reason);
        Assert.Null(resolved.Explanation.Discount);
        Assert.Equal(11, resolved.Explanation.PolicyRevision);
    }

    [Fact]
    public void OutOfEnvelopeOverrideRequiresElevatedCapabilityWithoutInventedApproval()
    {
        var basePrice = Revision(PriceScope.Default(), 20m, 1);
        var policy = new PricingOverridePolicy(12, 10, 30);
        var withoutElevation = PriceSelectionEngine.Resolve(
            SelectionRequest(FullContext, 12),
            [basePrice],
            policy,
            new PriceOverrideRequest(40m, "manager approval", true, false, "approval-1", Guid.NewGuid()));
        Assert.IsType<PriceOverrideRequired>(withoutElevation);

        var withElevation = PriceSelectionEngine.Resolve(
            SelectionRequest(FullContext, 12),
            [basePrice],
            policy,
            new PriceOverrideRequest(40m, "elevated discount", true, true));
        var resolved = Assert.IsType<PriceResolved>(withElevation);
        Assert.True(resolved.AppliedOverride?.BeyondPolicy);
    }

    [Fact]
    public void CoreAssemblyHasNoProviderOrHostReferences()
    {
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql", "OpenFga" };
        var references = typeof(PriceSelectionEngine).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
        Assert.DoesNotContain(references, reference => forbidden.Any(reference.StartsWith));
    }

    [Fact]
    public void FingerprintsAreCultureIndependentAndStableUnitIdentityChangesIntent()
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            var request = new CreatePriceDraftRequest(ItemId, "EA", "USD", PriceScope.Default(), 12.34m, new(Now), Guid.NewGuid());
            System.Globalization.CultureInfo.CurrentCulture = new("fr-FR");
            var french = request.Fingerprint;
            System.Globalization.CultureInfo.CurrentCulture = new("en-US");
            Assert.Equal(french, request.Fingerprint);
            Assert.NotEqual(french, (request with { UnitId = Guid.NewGuid() }).Fingerprint);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = originalCulture; }
    }

    [Fact]
    public void AbsoluteAndPercentEnvelopeBothApplyAndElevatedBranchDoesNotNeedApproval()
    {
        var candidate = Revision(PriceScope.Default(), 20, 1);
        var policy = new PricingOverridePolicy(1, 0, 100, 10, 20);
        Assert.False(policy.Contains(15, 20));
        Assert.True(policy.Contains(18, 20));
        Assert.False(policy.Contains(25, 20));
        var denied = PriceSelectionEngine.Resolve(SelectionRequest(FullContext), [candidate], policy, new(15, "reason", true, false));
        Assert.IsType<PriceOverrideRequired>(denied);
        var elevated = PriceSelectionEngine.Resolve(SelectionRequest(FullContext), [candidate], policy, new(15, "reason", true, true));
        Assert.IsType<PriceResolved>(elevated);
    }

    [Fact]
    public void BookIngressRejectsCommittedOrGuessedWholesaleIdentityAndRevalidationPreservesManualEntry()
    {
        Assert.Throws<PricingValidationException>(() => PricingApplication.RequireBookScope(PriceScope.CommittedAgreement(Guid.NewGuid())));
        Assert.Throws<PricingValidationException>(() => PricingApplication.RequireBookScope(PriceScope.Wholesale(Guid.NewGuid())));
        Assert.Throws<PricingValidationException>(() => PricingApplication.RequirePublicContext(new(CommittedQuotationId: Guid.NewGuid())));
        Assert.Equal(PricingRevalidationStatus.ManualEntry, PricingRevalidation.Compare(null, null).Status);
        var resolved = Assert.IsType<PriceResolved>(PriceSelectionEngine.Resolve(SelectionRequest(new()),
            [Revision(PriceScope.Default(), 20, 1)], new(1, 0, 100)));
        var retained = new RetainedPriceSelection(ItemId, Guid.Empty, "USD", 20, resolved.Explanation);
        Assert.Equal(PricingRevalidationStatus.Unchanged, PricingRevalidation.Compare(retained, resolved).Status);
        Assert.Equal(PricingRevalidationStatus.Changed, PricingRevalidation.Compare(retained with { UnitPrice = 19 }, resolved).Status);
        Assert.Equal(PricingRevalidationStatus.NotResolved, PricingRevalidation.Compare(retained, new PriceMissing(resolved.Explanation)).Status);
    }

    [Fact]
    public void StableUnitIdentityNotDisplayCodeDeterminesApplicabilityAndPolicyEnvelopeIsRetained()
    {
        var unit = Guid.NewGuid();
        var revision = new PriceRevision(TenantId, Guid.NewGuid(), 1, new(ItemId, "EA", "USD", PriceScope.Default(), unit),
            20, new(Now.AddDays(-1)), PricePublicationState.Published, Guid.NewGuid(), Now.AddDays(-2), Now.AddDays(-1));
        var policy = new PricingOverridePolicy(1, 5, 100, 10, 30);
        var sameUnit = new PriceSelectionRequest(TenantId, ItemId, "RENAMED", "USD", new(), Now, 1, unit);
        var resolved = Assert.IsType<PriceResolved>(PriceSelectionEngine.Resolve(sameUnit, [revision], policy));
        Assert.Equal(policy, resolved.Explanation.Policy);
        var otherUnit = new PriceSelectionRequest(TenantId, ItemId, "EA", "USD", new(), Now, 1, Guid.NewGuid());
        Assert.IsType<PriceMissing>(PriceSelectionEngine.Resolve(otherUnit, [revision], policy));
        var convertedKey = new PriceKey(ItemId, "EA", "USD", PriceScope.Default(), unit, 3);
        Assert.True(revision.Key.HasSameDimensions(convertedKey));
        Assert.NotEqual(revision.Key, convertedKey);
    }

    private static PriceSelectionRequest SelectionRequest(PriceSelectionContext context, long policyRevision = 1) =>
        new(TenantId, ItemId, "EA", "USD", context, Now, policyRevision);

    [Fact]
    public void APriceForOneConversionRevisionCannotBeSelectedForAnotherUnitMeaning()
    {
        var unit = Guid.NewGuid();
        var price = new PriceRevision(TenantId, Guid.NewGuid(), 1,
            new(ItemId, "BOX", "USD", PriceScope.Default(), unit, 1), 20,
            new(Now.AddDays(-1)), PricePublicationState.Published, Guid.NewGuid(), Now.AddDays(-2), Now.AddDays(-1));
        var policy = new PricingOverridePolicy(1, 0, 100);
        var incompatible = new PriceSelectionRequest(TenantId, ItemId, "BOX", "USD",
            new(UnitConversionRevision: 2), Now, 1, unit);
        Assert.IsType<PriceMissing>(PriceSelectionEngine.Resolve(incompatible, [price], policy));
        var compatible = new PriceSelectionRequest(TenantId, ItemId, "BOX", "USD",
            new(UnitConversionRevision: 1), Now, 1, unit);
        Assert.Equal(price, Assert.IsType<PriceResolved>(PriceSelectionEngine.Resolve(compatible, [price], policy)).BasePrice);
    }

    private static PriceRevision Revision(
        PriceScope scope,
        decimal price,
        int revision,
        PriceValidity? validity = null) =>
        new(
            TenantId,
            Guid.Parse($"00000000-0000-0000-0000-{revision:000000000000}"),
            revision,
            new PriceKey(ItemId, "EA", "USD", scope),
            price,
            validity ?? new PriceValidity(Now.AddDays(-1), Now.AddDays(1)),
            PricePublicationState.Published,
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Now.AddDays(-2),
            Now.AddDays(-1));
}
