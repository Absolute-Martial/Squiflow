namespace Application.Catalog;

public static class CatalogLineFactSelection
{
    public static CatalogLineFactsResult Assess(CatalogLineSelection selection,
        CatalogItemSnapshot? item, CatalogUnitSnapshot? source, CatalogUnitSnapshot? target,
        CatalogConversionSnapshot? conversion)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (item is null) return new(CatalogLineFactsStatus.ItemNotFound, null);
        if (item.Status == CatalogEntityStatus.Retired) return new(CatalogLineFactsStatus.ItemRetired, null);
        if (item.StockMode == CatalogStockMode.AvailabilityOnly && item.Availability != CatalogAvailability.Available)
            return new(CatalogLineFactsStatus.ItemUnavailable, null);
        if (source is null || target is null) return new(CatalogLineFactsStatus.UnitNotFound, null);
        if (source.Status == CatalogEntityStatus.Retired || target.Status == CatalogEntityStatus.Retired)
            return new(CatalogLineFactsStatus.UnitRetired, null);
        if (item.ItemId != selection.ItemId || source.UnitId != selection.UnitId || target.UnitId != item.BaseUnitId ||
            item.TenantId != source.TenantId || item.TenantId != target.TenantId)
            throw new InvalidOperationException("Catalog selection returned inconsistent identities.");

        var identity = source.UnitId == target.UnitId;
        if (!identity && selection.ConversionRevision is null)
            return new(CatalogLineFactsStatus.ConversionRevisionRequired, null);
        if (!identity && conversion is null) return new(CatalogLineFactsStatus.ConversionNotFound, null);
        if (identity && selection.ConversionRevision is not null) return new(CatalogLineFactsStatus.UnitMismatch, null);
        if (conversion is not null && (conversion.TenantId != item.TenantId || conversion.SourceUnitId != source.UnitId ||
            conversion.TargetUnitId != target.UnitId || conversion.Revision != selection.ConversionRevision))
            throw new InvalidOperationException("Catalog conversion returned inconsistent identities.");

        var facts = identity
            ? new CatalogConversionFacts(source.UnitId, target.UnitId, 1, 1m, 1m)
            : new CatalogConversionFacts(source.UnitId, target.UnitId, conversion!.Revision, conversion.Numerator, conversion.Denominator);
        try
        {
            var baseQuantity = CatalogQuantityConversion.Convert(selection.Quantity, source.Precision,
                target.Precision, facts.Numerator, facts.Denominator);
            return new(CatalogLineFactsStatus.Available, new CatalogLineFacts(item.ItemId, item.Code, source.UnitId,
                item.Name, source.Code, source.Name, selection.Quantity, source.Precision, item.Revision, source.Revision,
                facts, QuantityArithmetic.Version1, QuantityArithmetic.Version1Rounding,
                target.Code, target.Name, target.Precision, target.Revision, baseQuantity));
        }
        catch (CatalogValidationException)
        {
            return new(CatalogLineFactsStatus.QuantityInvalid, null);
        }
    }
}
