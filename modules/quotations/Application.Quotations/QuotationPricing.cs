using Application.Catalog;
using Application.Customers;
using Application.Pricing;
using Application.Tenancy;

namespace Application.Quotations;

public sealed class QuotationPricing(SelectCatalogLineFacts catalog, PricingApplication pricing,
    ResolveCustomerOrderContext customers, IQuotationAuthority authority, ICustomerCanonicalDirectory canonicalCustomers)
{
    public async Task<QuotationDraftFacts> SelectAsync(TenantContext context, QuotationDraftRequest request, CancellationToken ct)
    {
        var customerId = request.CustomerId;
        if (customerId is { } id)
        {
            var current = await canonicalCustomers.ResolveCurrentCustomerAsync(context, id, ct).ConfigureAwait(false);
            if (current is null || current.TenantId != context.TenantId || current.Availability != CustomerIndividualAvailability.Active ||
                current.RedirectTargetIndividualId.HasValue) throw new QuotationValidationException("customer_context_invalid", "Customer context is unavailable.");
            customerId = current.IndividualId;
        }
        if (request.CustomerContext is { } customer &&
            await customers.ExecuteAsync(context, customer.OrganizationId, customer.ProgramId, ct).ConfigureAwait(false) is null)
            throw new QuotationValidationException("customer_context_invalid", "Customer context is unavailable.");
        var lines = new List<QuotationLineFacts>(request.Lines.Count);
        decimal total = 0;
        foreach (var input in request.Lines)
        {
            CatalogLineFacts? selected = null;
            RetainedPriceSelection? retained = null;
            PriceRevision? published = null;
            var description = input.Description;
            var unitCode = input.UnitCode;
            var unitPrice = input.UnitPrice;
            if (request.Mode == QuotationPriceMode.Catalog)
            {
                var selection = await catalog.ExecuteAsync(context, new(input.ItemId!.Value, input.UnitId!.Value,
                    input.Quantity, input.ConversionRevision), ct).ConfigureAwait(false);
                selected = selection.Facts;
                if (selection.Status != CatalogLineFactsStatus.Available || selected is null)
                    throw new QuotationValidationException("catalog_unavailable", "Catalog facts are not currently selectable.");
                if (selected.ItemId != input.ItemId || selected.UnitId != input.UnitId || selected.Quantity != input.Quantity)
                    throw new InvalidOperationException("The catalog selector returned inconsistent facts.");
                var priceContext = new PriceSelectionContext(CustomerId: customerId, OrganizationId: request.CustomerContext?.OrganizationId,
                    ProgramId: request.CustomerContext?.ProgramId, WholesaleApplicable: request.WholesaleApplicable,
                    UnitConversionRevision: input.ConversionRevision);
                var result = await pricing.ResolveAsync(new(context.TenantId, context.AccountId), input.ItemId.Value, input.UnitId.Value,
                    request.CurrencyCode, priceContext, null, null, false, false, ct).ConfigureAwait(false);
                if (input.OverridePrice is { } final)
                {
                    await RequireAsync(context, QuotationCapability.Override, ct).ConfigureAwait(false);
                    var beyond = PriceSelectionEngine.RequiresElevatedOverride(result, final);
                    if (beyond) await RequireAsync(context, QuotationCapability.OverrideBeyondPolicy, ct).ConfigureAwait(false);
                    result = PriceSelectionEngine.ApplyOverride(result, new(final, input.OverrideReason!, true, beyond));
                }
                if (result is not PriceResolved resolved) throw new QuotationValidationException("price_unavailable", "Price selection is unresolved.");
                if (resolved.BasePrice.TenantId != context.TenantId || resolved.BasePrice.Key.ItemId != input.ItemId ||
                    resolved.BasePrice.Key.UnitId != input.UnitId || resolved.BasePrice.Key.CurrencyCode != request.CurrencyCode)
                    throw new InvalidOperationException("The price selector returned inconsistent facts.");
                description = selected.ItemName; unitCode = selected.UnitCode; unitPrice = resolved.UnitPrice;
                published = resolved.BasePrice;
                retained = new(selected.ItemId, selected.UnitId, request.CurrencyCode, resolved.UnitPrice, resolved.Explanation);
            }
            decimal amount;
            try
            {
                amount = SellingPriceArithmetic.LineAmount(input.Quantity, unitPrice!.Value);
                total = checked(total + amount);
                if (total > SellingPriceArithmetic.MaximumAmount) throw new OverflowException();
            }
            catch (OverflowException) { throw new QuotationValidationException("amount_invalid", "The quotation amount exceeds the supported range."); }
            lines.Add(new(lines.Count + 1, description!, input.Quantity, unitCode!, unitPrice!.Value, amount, selected, retained, published));
        }
        var offer = new QuotationDraftFacts(request.Mode, request.Summary, request.CurrencyCode, request.ValidUntil,
            Array.AsReadOnly(lines.ToArray()), total, request.Terms, customerId, request.CustomerContext, request.WholesaleApplicable);
        QuotationRules.Validate(offer, context.TenantId);
        return offer;
    }

    public async Task<bool> IsCompatibleAsync(TenantContext context, QuotationDraftFacts offer,
        QuotationFrozenAuthority frozen, CancellationToken ct)
    {
        // Authority is already resolved: only database comparison may run under the publication pin.
        if (frozen != QuotationFrozenAuthority.RequiredFor(offer)) return false;
        if (offer.CustomerContext is { } customer &&
            await customers.ExecuteAsync(context, customer.OrganizationId, customer.ProgramId, ct).ConfigureAwait(false) is null) return false;
        if (offer.CustomerId is { } id)
        {
            var current = await canonicalCustomers.ResolveCurrentCustomerAsync(context, id, ct).ConfigureAwait(false);
            if (current is null || current.IndividualId != id || current.Availability != CustomerIndividualAvailability.Active ||
                current.RedirectTargetIndividualId.HasValue) return false;
        }
        foreach (var line in offer.Lines)
        {
            if (line.Catalog is not { } old) continue;
            var conversion = old.Conversion.SourceUnitId == old.Conversion.TargetUnitId ? (long?)null : old.Conversion.Revision;
            var selected = await catalog.ExecuteAsync(context, new(old.ItemId, old.UnitId, line.Quantity, conversion), ct).ConfigureAwait(false);
            if (selected.Status != CatalogLineFactsStatus.Available || selected.Facts is not { } current ||
                old.ItemId != current.ItemId || old.UnitId != current.UnitId || old.Quantity != current.Quantity ||
                old.UnitPrecision != current.UnitPrecision || old.BaseUnitPrecision != current.BaseUnitPrecision ||
                old.Conversion != current.Conversion || old.BaseQuantity != current.BaseQuantity ||
                old.QuantityArithmeticVersion != current.QuantityArithmeticVersion || old.QuantityRounding != current.QuantityRounding) return false;
            try
            {
                var retained = line.PriceSelection!;
                var resolution = await pricing.ResolveAsync(new(context.TenantId, context.AccountId), retained.ItemId, retained.UnitId,
                    offer.CurrencyCode, retained.Explanation.Context, null, null, false, false, ct).ConfigureAwait(false);
                if (retained.Explanation.Override is { } priceOverride)
                {
                    // A re-resolution can newly require elevated authority the retained override did
                    // not. Frozen authority was resolved outside the pin, so deny rather than call out.
                    var elevated = PriceSelectionEngine.RequiresElevatedOverride(resolution, priceOverride.UnitPrice);
                    if (elevated && !frozen.OverrideBeyondPolicy) throw new QuotationCapabilityDeniedException();
                    resolution = PriceSelectionEngine.ApplyOverride(resolution, new(priceOverride.UnitPrice, priceOverride.Reason, true, elevated));
                }
                if (PricingRevalidation.Compare(retained, resolution).Status != PricingRevalidationStatus.Unchanged) return false;
            }
            catch (PricingValidationException) { return false; }
        }
        return true;
    }

    // Resolves every outbound authorization check the retained offer needs. Callers run this
    // before opening the effect transaction: the shared commercial-publication pin is tenant
    // wide, so an outbound round trip held under it stalls every publication for that tenant.
    public async Task<QuotationFrozenAuthority> ResolveFrozenAuthorityAsync(TenantContext context,
        QuotationDraftFacts offer, CancellationToken ct)
    {
        var required = QuotationFrozenAuthority.RequiredFor(offer);
        if (required.ManualPricing && !await authority.CheckAsync(context, QuotationCapability.ManualPricing, ct).ConfigureAwait(false))
            throw new QuotationCapabilityDeniedException();
        if (required.CatalogView && !await authority.CheckAsync(context, QuotationCapability.CatalogView, ct).ConfigureAwait(false))
            throw new QuotationCapabilityDeniedException();
        if (required.PricingView && !await authority.CheckAsync(context, QuotationCapability.PricingView, ct).ConfigureAwait(false))
            throw new QuotationCapabilityDeniedException();
        if (required.Override && !await authority.CheckAsync(context, QuotationCapability.Override, ct).ConfigureAwait(false))
            throw new QuotationCapabilityDeniedException();
        if (required.OverrideBeyondPolicy &&
            !await authority.CheckAsync(context, QuotationCapability.OverrideBeyondPolicy, ct).ConfigureAwait(false))
            throw new QuotationCapabilityDeniedException();
        return required;
    }
    public async Task RequireInputAuthorityAsync(TenantContext context, QuotationDraftRequest input, CancellationToken ct)
    {
        await RequireModeAsync(context, input.Mode, ct).ConfigureAwait(false);
        if (input.Lines.Any(line => line.OverridePrice.HasValue)) await RequireAsync(context, QuotationCapability.Override, ct).ConfigureAwait(false);
    }
    public async Task RequireAsync(TenantContext context, QuotationCapability permission, CancellationToken ct)
    { if (!await authority.CheckAsync(context, permission, ct).ConfigureAwait(false)) throw new QuotationCapabilityDeniedException(); }
    private async Task RequireModeAsync(TenantContext context, QuotationPriceMode mode, CancellationToken ct)
    {
        if (mode == QuotationPriceMode.Manual) await RequireAsync(context, QuotationCapability.ManualPricing, ct).ConfigureAwait(false);
        else
        {
            await RequireAsync(context, QuotationCapability.CatalogView, ct).ConfigureAwait(false);
            await RequireAsync(context, QuotationCapability.PricingView, ct).ConfigureAwait(false);
        }
    }
}
