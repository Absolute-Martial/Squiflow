using Application.Catalog;
using Application.Tenancy;
using PriceContracts = Application.Pricing;

namespace Application.Orders;

public interface IOrderPricingAuthorityReader
{
    Task<OrderPricingAuthority> ReadAsync(TenantContext context, CancellationToken ct);
    async Task<bool> CanOverrideBeyondPolicyAsync(TenantContext context, CancellationToken ct) =>
        (await ReadAsync(context, ct).ConfigureAwait(false)).CanOverrideBeyondPolicy;
}

public sealed class OrderCommercialCommitGuard(SelectCatalogLineFacts catalog,
    PriceContracts.PricingApplication pricing, IOrderPricingAuthorityReader authority,
    IOrderAcceptedQuotationReader? acceptedQuotations = null) : IOrderCommercialCommitGuard
{
    public async Task<bool> IsCompatibleAsync(TenantContext context, OrderDraftSnapshot order, CancellationToken ct)
    {
        if (order.QuotationOrigin is { } origin)
        {
            if (acceptedQuotations is null) return false;
            var accepted = await acceptedQuotations.ReadAsync(context, order.OrderId, origin, ct).ConfigureAwait(false);
            if (accepted is null) return false;
            accepted.RequireValid(context.TenantId);
            return accepted.Matches(order);
        }
        var currentAuthority = order.Lines.Any(line => line.CommercialFacts?.PriceSelection.Explanation.Override is not null)
            ? await authority.ReadAsync(context, ct).ConfigureAwait(false)
            : new OrderPricingAuthority(false, false);
        foreach (var line in order.Lines)
        {
            if (line.CommercialFacts is not { } retained) continue;
            var oldCatalog = retained.Catalog;
            var conversion = oldCatalog.Conversion.SourceUnitId == oldCatalog.Conversion.TargetUnitId
                ? (long?)null : oldCatalog.Conversion.Revision;
            var selected = await catalog.ExecuteAsync(context,
                new(oldCatalog.ItemId, oldCatalog.UnitId, line.Quantity, conversion), ct).ConfigureAwait(false);
            if (selected.Status != CatalogLineFactsStatus.Available || selected.Facts is not { } currentCatalog ||
                !Compatible(oldCatalog, currentCatalog)) return false;

            var retainedPrice = retained.PriceSelection;
            var priceOverride = retainedPrice.Explanation.Override;
            PriceContracts.PriceResolution current;
            try
            {
                current = await pricing.ResolveAsync(new(context.TenantId, context.AccountId), retainedPrice.ItemId,
                    retainedPrice.UnitId, retainedPrice.CurrencyCode, retainedPrice.Explanation.Context,
                    null, null, false, false, ct).ConfigureAwait(false);
                if (priceOverride is not null)
                {
                    var canBeyond = currentAuthority.CanOverrideBeyondPolicy;
                    if (currentAuthority.CanOverride && PriceContracts.PriceSelectionEngine.RequiresElevatedOverride(current, priceOverride.UnitPrice))
                        canBeyond = await authority.CanOverrideBeyondPolicyAsync(context, ct).ConfigureAwait(false);
                    current = PriceContracts.PriceSelectionEngine.ApplyOverride(current,
                        new(priceOverride.UnitPrice, priceOverride.Reason, currentAuthority.CanOverride, canBeyond));
                }
            }
            catch (PriceContracts.PricingValidationException)
            {
                return false;
            }
            if (PriceContracts.PricingRevalidation.Compare(retainedPrice, current).Status != PriceContracts.PricingRevalidationStatus.Unchanged)
                return false;
        }
        return true;
    }

    // Renaming alone does not invalidate commercial compatibility or rewrite the
    // frozen labels; retirement, availability and conversion validity do invalidate.
    private static bool Compatible(CatalogLineFacts retained, CatalogLineFacts current) =>
        retained.ItemId == current.ItemId && retained.UnitId == current.UnitId &&
        retained.UnitPrecision == current.UnitPrecision && retained.BaseUnitPrecision == current.BaseUnitPrecision &&
        retained.Conversion == current.Conversion && retained.BaseQuantity == current.BaseQuantity &&
        retained.QuantityArithmeticVersion == current.QuantityArithmeticVersion && retained.QuantityRounding == current.QuantityRounding;
}
