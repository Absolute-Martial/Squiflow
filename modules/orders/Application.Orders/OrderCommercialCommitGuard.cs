using Application.Catalog;
using Application.Tenancy;
using PriceContracts = Application.Pricing;

namespace Application.Orders;

public interface IOrderPricingAuthorityReader
{
    Task<OrderPricingAuthority> ReadAsync(TenantContext context, CancellationToken ct);

    // Denied unless a trusted current-authority adapter resolves it. Elevated
    // authority is a separate current external decision; it is never inferred
    // from the ordinary override permission or from a cached flag.
    Task<bool> CanOverrideBeyondPolicyAsync(TenantContext context, CancellationToken ct) =>
        Task.FromResult(false);
}

// A point-in-time external authorization observation produced by the trusted
// current-authority reader immediately before the caller takes its publication
// pin, and consumed by the pinned comparison that follows. It is not durable
// state, not wire ingress and never a source of authority for retained facts.
public sealed record OrderCommercialCommitAuthority(bool CanOverride, bool CanOverrideBeyondPolicy)
{
    public static readonly OrderCommercialCommitAuthority None = new(false, false);
}

public sealed class OrderCommercialCommitGuard(SelectCatalogLineFacts catalog,
    PriceContracts.PricingApplication pricing, IOrderPricingAuthorityReader authority) : IOrderCommercialCommitGuard
{
    // External authorization is resolved BEFORE the caller takes the shared
    // publication pin. A slow or degraded authorization provider must never hold
    // tenant-wide publication exclusion; it is not Catalog/Pricing state, so the
    // publication pin does not protect it and cannot make it fresher.
    public async Task<OrderCommercialCommitAuthority> ResolveCurrentAuthorityAsync(
        TenantContext context, OrderDraftSnapshot order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        var retainedOverrides = order.Lines
            .Select(line => line.CommercialFacts?.PriceSelection.Explanation.Override)
            .OfType<PriceContracts.PriceOverrideEvidence>()
            .ToArray();
        if (retainedOverrides.Length is 0) return OrderCommercialCommitAuthority.None;
        var current = await authority.ReadAsync(context, ct).ConfigureAwait(false);
        if (!current.CanOverride) return OrderCommercialCommitAuthority.None;
        // Retained beyond-policy evidence is the only signal available before the
        // pin. Reading elevated authority on that basis, or not reading it, can
        // only change the outcome into a rejection: a changed policy revision,
        // base price or selection context already fails the pinned comparison, and
        // an elevation requirement the retained facts do not record is rejected by
        // the same comparison whether or not the provider was asked.
        return retainedOverrides.Any(retained => retained.BeyondPolicy)
            ? new(true, await authority.CanOverrideBeyondPolicyAsync(context, ct).ConfigureAwait(false))
            : new(true, false);
    }

    // Database reads only. The caller owns the shared publication pin across this
    // comparison and the effect, so no external provider call may appear here.
    public async Task<bool> IsCompatibleAsync(TenantContext context, OrderDraftSnapshot order,
        OrderCommercialCommitAuthority currentAuthority, CancellationToken ct)
    {
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
                    var canBeyond = currentAuthority.CanOverrideBeyondPolicy &&
                        PriceContracts.PriceSelectionEngine.RequiresElevatedOverride(current, priceOverride.UnitPrice);
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
