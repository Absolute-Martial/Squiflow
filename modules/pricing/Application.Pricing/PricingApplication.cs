namespace Application.Pricing;

public sealed class PricingValidationException(string code, string message) : ArgumentException(message)
{
    public string Code { get; } = code;
}

// Implemented by a host adapter through Catalog/Customers public queries, never private tables.
public interface IPricingReferenceReader
{
    Task<string> RequireCompatibleUnitAsync(PricingActorContext actor, Guid itemId, Guid unitId, long? conversionRevision, CancellationToken ct);
    Task RequireContextAsync(PricingActorContext actor, PriceSelectionContext context, CancellationToken ct);
}

public sealed record PricingPolicySnapshot(Guid TenantId, PricingOverridePolicy Policy, Guid CreatedByAccountId, DateTimeOffset CreatedAt);
public sealed record PublishPricingPolicyRequest(long ExpectedRevision, decimal MinimumUnitPrice, decimal MaximumUnitPrice,
    decimal? MaximumDecreasePercent = null, decimal? MaximumIncreasePercent = null)
{
    public string Fingerprint => PricingFingerprint.Compute(System.FormattableString.Invariant(
        $"policy-v1|{ExpectedRevision}|{MinimumUnitPrice:0.0000}|{MaximumUnitPrice:0.0000}|{MaximumDecreasePercent:0.0000}|{MaximumIncreasePercent:0.0000}"));
}
public enum PublishPricingPolicyStatus { Published, Replayed, RevisionConflict, IdempotencyKeyConflict }
public sealed record PublishPricingPolicyResult(PublishPricingPolicyStatus Status, PricingPolicySnapshot? Snapshot);
public interface IPricingPolicyStore
{
    Task<PricingPolicySnapshot?> GetCurrentPolicyAsync(PricingActorContext actor, CancellationToken ct);
    Task<PublishPricingPolicyResult> PublishPolicyAsync(PricingActorContext actor, PublishPricingPolicyRequest request, string key, CancellationToken ct);
}

// No committed-fact identifier is accepted by price-book ingress. A future COM-009
// owning capability must supply retained facts through this trusted in-process seam.
public interface ICommittedPricingFactReader
{
    Task<IReadOnlyList<PriceRevision>> ReadCommittedFactsAsync(PricingActorContext actor, Guid itemId, Guid unitId,
        string currencyCode, PriceSelectionContext context, CancellationToken ct);
}

public sealed class PricingApplication(IPricingPublicationStore publication, PriceResolver resolver,
    IPricingPolicyStore policies, IPricingReferenceReader references, TimeProvider time)
{
    public async Task<CreatePriceDraftResult> CreateDraftAsync(PricingActorContext actor, CreatePriceDraftRequest request, string key, CancellationToken ct)
    {
        RequireBookScope(request.Scope);
        if (request.Validity.ValidFrom == default || request.Validity.ValidFrom.Ticks % 10 != 0 ||
            (request.Validity.ValidTo.HasValue && request.Validity.ValidTo.Value.Ticks % 10 != 0))
            throw new PricingValidationException("pricing_validity_invalid", "Validity requires explicit microsecond-precision timestamps.");
        await references.RequireContextAsync(actor, ContextFor(request.Scope), ct).ConfigureAwait(false);
        var code = await references.RequireCompatibleUnitAsync(actor, request.ItemId, request.UnitId, request.UnitConversionRevision, ct).ConfigureAwait(false);
        return await publication.CreateDraftAsync(actor, request with { UnitCode = code }, key, ct).ConfigureAwait(false);
    }

    public async Task<PublishPriceResult> PublishAsync(PricingActorContext actor, PublishPriceRequest request, string key, CancellationToken ct)
    {
        var draft = await publication.FindAsync(actor, request.RevisionId, ct).ConfigureAwait(false);
        if (draft is null) return new(PublishPriceStatus.NotFound, null);
        RequireBookScope(draft.Key.Scope);
        await references.RequireContextAsync(actor, ContextFor(draft.Key.Scope), ct).ConfigureAwait(false);
        await references.RequireCompatibleUnitAsync(actor, draft.Key.ItemId, draft.Key.UnitId, draft.Key.UnitConversionRevision, ct).ConfigureAwait(false);
        return await publication.PublishAsync(actor, request, key, ct).ConfigureAwait(false);
    }

    public async Task<PriceResolution> ResolveAsync(PricingActorContext actor, Guid itemId, Guid unitId, string currency,
        PriceSelectionContext context, decimal? overridePrice, string? reason, bool canOverride, bool canBeyondPolicy, CancellationToken ct)
    {
        RequirePublicContext(context);
        await references.RequireContextAsync(actor, context, ct).ConfigureAwait(false);
        var code = await references.RequireCompatibleUnitAsync(actor, itemId, unitId, context.UnitConversionRevision, ct).ConfigureAwait(false);
        var snapshot = await policies.GetCurrentPolicyAsync(actor, ct).ConfigureAwait(false)
            ?? throw new PricingValidationException("pricing_policy_missing", "A published pricing policy is required.");
        if (snapshot.TenantId != actor.TenantId) throw new InvalidOperationException("Pricing policy tenant mismatch.");
        var request = new PriceSelectionRequest(actor.TenantId, itemId, code, currency, context,
            time.GetUtcNow(), snapshot.Policy.PolicyRevision, unitId);
        var result = await resolver.ResolveAsync(actor, request, snapshot.Policy,
            overridePrice.HasValue ? new PriceOverrideRequest(overridePrice.Value, reason ?? "", canOverride, canBeyondPolicy) : null, ct).ConfigureAwait(false);
        return result;
    }

    public static void RequirePublicContext(PriceSelectionContext context)
    {
        if (context.CommittedAgreementId.HasValue || context.CommittedQuotationId.HasValue || context.WholesaleTierId.HasValue)
            throw new PricingValidationException("pricing_context_unsupported", "Committed facts and arbitrary wholesale tier identities are not accepted.");
        if (context.CustomerId == Guid.Empty || context.ProgramId == Guid.Empty || context.OrganizationId == Guid.Empty ||
            context.UnitConversionRevision is < 1 ||
            (context.ProgramId.HasValue && !context.OrganizationId.HasValue))
            throw new PricingValidationException("pricing_context_invalid", "Pricing context identities or parent relationship are invalid.");
    }

    public static void RequireBookScope(PriceScope scope)
    {
        if (scope.Kind is PriceScopeKind.CommittedAgreement or PriceScopeKind.CommittedQuotation)
            throw new PricingValidationException("pricing_scope_unsupported", "A committed owning fact is not a publishable price-book tier.");
        if (scope.Kind == PriceScopeKind.Wholesale && scope.TargetId != PriceScope.WholesaleIdentity)
            throw new PricingValidationException("pricing_scope_unsupported", "Wholesale is explicit applicability, not an arbitrary tier identity.");
    }

    public static PriceSelectionContext ContextFor(PriceScope scope) => scope.Kind switch
    {
        PriceScopeKind.Customer => new(CustomerId: scope.TargetId),
        PriceScopeKind.Program => new(ProgramId: scope.TargetId),
        PriceScopeKind.Organization => new(OrganizationId: scope.TargetId),
        PriceScopeKind.Wholesale => new(WholesaleApplicable: true),
        _ => new(),
    };

}

public sealed record RetainedPriceSelection(Guid ItemId, Guid UnitId, string CurrencyCode, decimal UnitPrice,
    PriceSelectionExplanation Explanation);
public enum PricingRevalidationStatus { Unchanged, Changed, NotResolved, ManualEntry }
public sealed record PricingRevalidationResult(PricingRevalidationStatus Status, PriceResolution? Current);
public static class PricingRevalidation
{
    // Manual prices retain their separate Orders permission/arithmetic contract.
    public static PricingRevalidationResult Compare(RetainedPriceSelection? retained, PriceResolution? current)
    {
        if (retained is null) return new(PricingRevalidationStatus.ManualEntry, null);
        if (current is not PriceResolved resolved) return new(PricingRevalidationStatus.NotResolved, current);
        var same = retained.UnitPrice == resolved.UnitPrice &&
            retained.ItemId == resolved.BasePrice.Key.ItemId && retained.UnitId == resolved.BasePrice.Key.UnitId &&
            retained.CurrencyCode == resolved.BasePrice.Key.CurrencyCode &&
            retained.Explanation.SelectedRevisionId == resolved.Explanation.SelectedRevisionId &&
            retained.Explanation.PolicyRevision == resolved.Explanation.PolicyRevision &&
            retained.Explanation.Context == resolved.Explanation.Context &&
            retained.Explanation.Override == resolved.Explanation.Override;
        return new(same ? PricingRevalidationStatus.Unchanged : PricingRevalidationStatus.Changed, current);
    }
}
