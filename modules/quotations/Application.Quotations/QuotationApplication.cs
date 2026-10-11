using Application.Tenancy;

namespace Application.Quotations;

public sealed partial class QuotationApplication(IQuotationStore store, QuotationPricing pricing, IQuotationResponseStore? responses = null)
{
    public async Task<QuotationCommandResult> CreateAsync(TenantContext context, QuotationDraftRequest input, string key, CancellationToken ct)
    {
        await pricing.RequireAsync(context, QuotationCapability.Create, ct).ConfigureAwait(false);
        input = QuotationRules.Normalize(input); key = QuotationRules.Key(key);
        await pricing.RequireInputAuthorityAsync(context, input, ct).ConfigureAwait(false);
        var fingerprint = QuotationRules.Fingerprint(input);
        var receipt = await store.FindReceiptAsync(context, "create", key, fingerprint, ct).ConfigureAwait(false);
        if (receipt is not null) return await ReplayAsync(context, receipt, ct).ConfigureAwait(false);
        var offer = await pricing.SelectAsync(context, input, ct).ConfigureAwait(false);
        return await ReplayAsync(context, await store.CreateDraftAsync(context, offer, key, fingerprint, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
    }
    public async Task<QuotationCommandResult> ReviseAsync(TenantContext context, Guid id, long expectedVersion,
        QuotationDraftRequest input, string key, CancellationToken ct)
    {
        await pricing.RequireAsync(context, QuotationCapability.Edit, ct).ConfigureAwait(false);
        QuotationRules.RequireIdentity(id, expectedVersion);
        input = QuotationRules.Normalize(input); key = QuotationRules.Key(key);
        await pricing.RequireInputAuthorityAsync(context, input, ct).ConfigureAwait(false);
        var fingerprint = QuotationRules.Fingerprint(new { id, expectedVersion, input });
        var receipt = await store.FindReceiptAsync(context, "revise", key, fingerprint, ct).ConfigureAwait(false);
        if (receipt is not null) return await ReplayAsync(context, receipt, ct).ConfigureAwait(false);
        var offer = await pricing.SelectAsync(context, input, ct).ConfigureAwait(false);
        return await ReplayAsync(context, await store.ReviseDraftAsync(context, id, expectedVersion, offer, key, fingerprint, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
    }
    public async Task<QuotationCommandResult> IssueAsync(TenantContext context, Guid id, long expectedVersion, string key, CancellationToken ct)
    {
        await pricing.RequireAsync(context, QuotationCapability.Issue, ct).ConfigureAwait(false);
        QuotationRules.RequireIdentity(id, expectedVersion); key = QuotationRules.Key(key);
        // Frozen authority is resolved before the store is called: the shared commercial-publication
        // pin is tenant wide, so an outbound authorization call taken under it stalls every catalog,
        // pricing and quotation publication for that tenant. The store still revalidates the retained
        // draft against current sources inside the pinned transaction that writes the effect.
        var frozen = await ResolveFrozenAuthorityAsync(context, id, expectedVersion, ct).ConfigureAwait(false);
        var result = await store.IssueAsync(context, id, expectedVersion, key, QuotationRules.Fingerprint(new { id, expectedVersion }),
            frozen is null
                // A draft appeared between the authority read and the effect transaction. Fail closed.
                ? (_, _) => Task.FromResult(false)
                : (Func<QuotationDraftFacts, CancellationToken, Task<bool>>)((offer, token) => pricing.IsCompatibleAsync(context, offer, frozen, token)),
            ct).ConfigureAwait(false);
        return await ReplayAsync(context, result, ct).ConfigureAwait(false);
    }
    // Returns null when this request cannot reach the comparison: a missing quotation, an already
    // issued one, an accepted family or a stale revision is decided by the store under its row lock,
    // and an unchanged draft is the only offer whose authority may be resolved outside the pin.
    private async Task<QuotationFrozenAuthority?> ResolveFrozenAuthorityAsync(TenantContext context,
        Guid id, long expectedVersion, CancellationToken ct) =>
        await store.FindAsync(context, id, ct).ConfigureAwait(false) is { Version: var version, Draft: { } draft } &&
            version == expectedVersion
            ? await pricing.ResolveFrozenAuthorityAsync(context, draft, ct).ConfigureAwait(false)
            : null;
    public async Task<QuotationSnapshot?> FindAsync(TenantContext context, Guid id, CancellationToken ct)
    {
        await pricing.RequireAsync(context, QuotationCapability.View, ct).ConfigureAwait(false);
        if (id == Guid.Empty) throw new QuotationValidationException("quotation_id_invalid", "Quotation identity is required.");
        return await store.FindAsync(context, id, ct).ConfigureAwait(false);
    }
    public async Task<QuotationIssuedPage> ListIssuedAsync(TenantContext context, Guid id, long afterRevision, int limit, CancellationToken ct)
    {
        await pricing.RequireAsync(context, QuotationCapability.View, ct).ConfigureAwait(false);
        if (id == Guid.Empty || afterRevision < 0 || limit is < 1 or > 50)
            throw new QuotationValidationException("page_invalid", "A bounded quotation history request is required.");
        return await store.ListIssuedAsync(context, id, afterRevision, limit, ct).ConfigureAwait(false);
    }
    private async Task<QuotationCommandResult> ReplayAsync(TenantContext context, QuotationCommandResult result, CancellationToken ct)
    {
        if (result.Quotation is { } snapshot && snapshot.TenantId != context.TenantId)
            throw new InvalidOperationException("The quotation store returned a foreign snapshot.");
        if (result.Status == QuotationCommandStatus.Replayed)
        {
            var offer = result.Quotation?.Draft ?? result.Quotation?.CurrentIssued?.Offer
                ?? throw new InvalidOperationException("The quotation receipt has no retained priced facts.");
            await pricing.ResolveFrozenAuthorityAsync(context, offer, ct).ConfigureAwait(false);
        }
        return result;
    }
}
