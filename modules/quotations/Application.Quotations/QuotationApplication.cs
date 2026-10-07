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
        var result = await store.IssueAsync(context, id, expectedVersion, key, QuotationRules.Fingerprint(new { id, expectedVersion }),
            (offer, token) => pricing.IsCompatibleAsync(context, offer, token), ct).ConfigureAwait(false);
        return await ReplayAsync(context, result, ct).ConfigureAwait(false);
    }
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
            await pricing.RequireFrozenAuthorityAsync(context, offer, ct).ConfigureAwait(false);
        }
        return result;
    }
}
