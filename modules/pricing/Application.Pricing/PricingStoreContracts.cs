namespace Application.Pricing;

public interface IPricingPublicationStore
{
    Task<PriceRevision?> FindAsync(PricingActorContext actor, Guid revisionId, CancellationToken cancellationToken);
    Task<CreatePriceDraftResult> CreateDraftAsync(
        PricingActorContext actor,
        CreatePriceDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<PublishPriceResult> PublishAsync(
        PricingActorContext actor,
        PublishPriceRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<RetirePriceResult> RetireAsync(
        PricingActorContext actor,
        RetirePriceRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PriceRevision>> ListRevisionsAsync(
        PricingActorContext actor,
        PriceLookupRequest request,
        CancellationToken cancellationToken);
}

public interface IPricingCandidateReader
{
    Task<IReadOnlyList<PriceRevision>> GetPublishedCandidatesAsync(
        PricingActorContext actor,
        PriceLookupRequest request,
        CancellationToken cancellationToken);
}

public sealed class PriceResolver(IPricingCandidateReader candidateReader)
{
    public async Task<PriceResolution> ResolveAsync(
        PricingActorContext actor,
        PriceSelectionRequest request,
        PricingOverridePolicy overridePolicy,
        PriceOverrideRequest? overrideRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(overridePolicy);
        if (actor.TenantId != request.TenantId)
            throw new ArgumentException("The pricing actor and selection tenant must match.", nameof(request));
        var candidates = await candidateReader.GetPublishedCandidatesAsync(
                actor,
                 new PriceLookupRequest(request.ItemId, request.UnitCode, request.CurrencyCode, request.UnitId, request.Context, at: request.At),
                cancellationToken)
            .ConfigureAwait(false);
        return PriceSelectionEngine.Resolve(request, candidates, overridePolicy, overrideRequest);
    }
}
