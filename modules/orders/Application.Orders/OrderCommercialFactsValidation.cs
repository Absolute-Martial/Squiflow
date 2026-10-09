using Application.Catalog;
using Application.Pricing;

namespace Application.Orders;

public static class OrderCommercialFactsValidation
{
    public static void RequireValid(OrderDraftSnapshot order)
    {
        foreach (var line in order.Lines)
        {
            if (line.CommercialFacts is not { } facts) continue;
            var catalog = facts.Catalog;
            var source = facts.PublishedPrice;
            var price = facts.PriceSelection;
            if (catalog is null || source is null || price is null || catalog.Conversion is null || price.Explanation is null)
                throw Invalid();
            var explanation = price.Explanation;
            if (catalog.ItemId == Guid.Empty || catalog.UnitId == Guid.Empty || catalog.ItemRevision < 1 || catalog.UnitRevision < 1 ||
                catalog.Quantity != line.Quantity || catalog.UnitCode != line.UnitCode || catalog.ItemName != line.Description ||
                catalog.UnitId != catalog.Conversion.SourceUnitId || catalog.Conversion.TargetUnitId == Guid.Empty ||
                catalog.Conversion.Revision < 1 || catalog.BaseUnitPrecision is null || catalog.BaseUnitRevision is null ||
                catalog.BaseQuantity is null ||
                !QuantityArithmetic.RoundingSupported(catalog.QuantityArithmeticVersion, catalog.QuantityRounding) ||
                source.TenantId != order.TenantId || source.Key.ItemId != catalog.ItemId || source.Key.UnitId != catalog.UnitId ||
                source.Key.CurrencyCode != order.CurrencyCode || source.State != Application.Pricing.PricePublicationState.Published ||
                price.ItemId != catalog.ItemId || price.UnitId != catalog.UnitId || price.CurrencyCode != order.CurrencyCode ||
                price.UnitPrice != line.UnitPrice || explanation.SelectedPrice != line.UnitPrice ||
                explanation.SelectedRevisionId != source.RevisionId || explanation.SelectedRevision != source.RevisionNumber ||
                explanation.SelectedPriceId != source.PriceId || explanation.SelectedScope != source.Key.Scope ||
                explanation.Policy is null || explanation.PolicyRevision != explanation.Policy.PolicyRevision ||
                !PricingOverridePolicySemantics.Supported(explanation.Policy.EnvelopeSemanticsVersion) ||
                explanation.Candidates is null || explanation.Candidates.Count > 64 || explanation.Context is null ||
                source.Key.UnitConversionRevision != explanation.Context.UnitConversionRevision ||
                explanation.Context.UnitConversionRevision != (catalog.Conversion.SourceUnitId == catalog.Conversion.TargetUnitId ? null : (long?)catalog.Conversion.Revision) ||
                !source.Validity.Contains(explanation.EvaluatedAt)) throw Invalid();
            try
            {
                if (CatalogQuantityConversion.Convert(catalog.QuantityArithmeticVersion, line.Quantity,
                    catalog.UnitPrecision, catalog.BaseUnitPrecision.Value,
                    catalog.Conversion.Numerator, catalog.Conversion.Denominator) != catalog.BaseQuantity) throw Invalid();
                if (explanation.Override is { } evidence && (evidence.UnitPrice != price.UnitPrice ||
                    string.IsNullOrWhiteSpace(evidence.Reason) || evidence.Reason.Length > 500)) throw Invalid();
            }
            catch (CatalogValidationException) { throw Invalid(); }
        }
    }

    private static InvalidOperationException Invalid() => new("The order contains invalid retained commercial facts.");
}
