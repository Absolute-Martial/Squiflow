using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Pricing;
using Application.Catalog;

namespace Application.Quotations;

public static class QuotationRules
{
    public static QuotationDraftRequest Normalize(QuotationDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Mode)) Fail("price_mode_invalid");
        var summary = Text(request.Summary, 200, "summary_invalid");
        var currency = Text(request.CurrencyCode, 3, "currency_invalid").ToUpperInvariant();
        if (currency.Length != 3 || currency.Any(c => !char.IsAsciiLetter(c))) Fail("currency_invalid");
        if (request.ValidUntil == default || request.ValidUntil.Ticks % 10 != 0) Fail("validity_invalid");
        if (request.Lines is null || request.Lines.Count is < 1 or > 100) Fail("lines_invalid");
        if (request.CustomerId == Guid.Empty || request.CustomerContext is { } customer &&
            (customer.OrganizationId == Guid.Empty || customer.ProgramId == Guid.Empty)) Fail("customer_context_invalid");
        var lines = request.Lines.Select(line =>
        {
            if (line is null || line.Quantity <= 0 || !SellingPriceArithmetic.IsSupported(line.Quantity)) Fail("quantity_invalid");
            if (request.Mode == QuotationPriceMode.Manual)
            {
                if (line.ItemId.HasValue || line.UnitId.HasValue || line.ConversionRevision.HasValue || line.OverridePrice.HasValue ||
                    line.OverrideReason is not null || line.UnitPrice is null || line.UnitPrice < 0 ||
                    !SellingPriceArithmetic.IsSupported(line.UnitPrice.Value)) Fail("manual_line_invalid");
                var code = Text(line.UnitCode!, 16, "unit_invalid").ToUpperInvariant();
                if (code.Any(c => !char.IsAsciiLetterOrDigit(c))) Fail("unit_invalid");
                return line with
                {
                    Quantity = Decimal(line.Quantity),
                    UnitPrice = Decimal(line.UnitPrice!.Value),
                    Description = Text(line.Description!, 300, "description_invalid"),
                    UnitCode = code
                };
            }
            if (line.ItemId is null || line.ItemId == Guid.Empty || line.UnitId is null || line.UnitId == Guid.Empty ||
                line.ConversionRevision is < 1 || line.UnitPrice.HasValue || line.Description is not null || line.UnitCode is not null)
                Fail("catalog_line_invalid");
            if (line.OverridePrice is { } price && (price < 0 || !SellingPriceArithmetic.IsSupported(price))) Fail("override_invalid");
            if (!line.OverridePrice.HasValue && line.OverrideReason is not null) Fail("override_invalid");
            return line with
            {
                Quantity = Decimal(line.Quantity),
                OverridePrice = line.OverridePrice.HasValue ? Decimal(line.OverridePrice.Value) : null,
                OverrideReason = line.OverridePrice.HasValue ? Text(line.OverrideReason!, 500, "override_reason_invalid") : null
            };
        }).ToArray();
        return request with
        {
            Summary = summary,
            CurrencyCode = currency,
            ValidUntil = request.ValidUntil.ToUniversalTime(),
            Lines = Array.AsReadOnly(lines),
            Terms = request.Terms is null ? null : Text(request.Terms, 2000, "terms_invalid")
        };
    }

    public static string Text(string value, int maximum, string code)
    {
        if (string.IsNullOrWhiteSpace(value)) Fail(code);
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            { if (++i >= value.Length || !char.IsLowSurrogate(value[i])) Fail(code); }
            else if (char.IsLowSurrogate(value[i])) Fail(code);
        }
        var normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > maximum || normalized.Any(char.IsControl)) Fail(code);
        return normalized;
    }

    public static string Key(string value) => Text(value, 128, "idempotency_key_invalid");
    public static Application.Orders.AcceptedQuotationOrder OrderFacts(QuotationIssuedFacts issued)
    {
        Validate(issued.Offer, issued.TenantId);
        return new(new(issued.QuotationId, issued.RevisionId, issued.Number, issued.RevisionNumber), issued.Offer.Summary,
            issued.Offer.CurrencyCode, issued.Offer.Total, Array.AsReadOnly(issued.Offer.Lines.Select(line => new Application.Orders.OrderDraftLine(
                line.Position, line.Description, line.Quantity, line.UnitCode, line.UnitPrice, line.LineTotal,
                line.Catalog is null ? null : new(line.Catalog, line.PublishedPrice!, line.PriceSelection!))).ToArray()), issued.Offer.CustomerContext);
    }
    public static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    public static void RequireIdentity(Guid id, long version)
    { if (id == Guid.Empty || version < 1) Fail("revision_request_invalid"); }
    public static void Validate(QuotationDraftFacts offer, Guid? tenantId = null)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (offer.Lines is null || offer.Lines.Count is < 1 or > 100 || offer.Lines.Any(line => line is null) ||
            !Enum.IsDefined(offer.Mode) || offer.ValidUntil.Offset != TimeSpan.Zero) Fail("retained_facts_invalid");
        foreach (var line in offer.Lines)
        {
            if (Text(line.Description, 300, "retained_facts_invalid") != line.Description ||
                Text(line.UnitCode, 64, "retained_facts_invalid") != line.UnitCode) Fail("retained_facts_invalid");
            if (offer.Mode == QuotationPriceMode.Manual)
            {
                if (line.Catalog is not null || line.PriceSelection is not null || line.PublishedPrice is not null) Fail("retained_facts_invalid");
                continue;
            }
            if (line.Catalog is not { Conversion: { } conversion } catalog || line.PriceSelection is not { Explanation: { } explanation } price ||
                line.PublishedPrice is not { } source || explanation.Context is not { } context ||
                catalog.ItemId == Guid.Empty || catalog.UnitId == Guid.Empty || catalog.ItemRevision < 1 || catalog.UnitRevision < 1 ||
                catalog.UnitCode != line.UnitCode || catalog.ItemName != line.Description || catalog.Quantity != line.Quantity ||
                conversion.SourceUnitId != catalog.UnitId || conversion.TargetUnitId == Guid.Empty || conversion.Revision < 1 ||
                catalog.BaseUnitPrecision is null || catalog.BaseUnitRevision is null || catalog.BaseQuantity is null ||
                !QuantityArithmetic.RoundingSupported(catalog.QuantityArithmeticVersion, catalog.QuantityRounding) ||
                tenantId.HasValue && source.TenantId != tenantId || source.State != PricePublicationState.Published ||
                source.Key.ItemId != catalog.ItemId || source.Key.UnitId != catalog.UnitId || source.Key.UnitCode != line.UnitCode ||
                source.Key.CurrencyCode != offer.CurrencyCode || price.UnitPrice != line.UnitPrice || price.CurrencyCode != offer.CurrencyCode ||
                price.ItemId != catalog.ItemId || price.UnitId != catalog.UnitId || explanation.SelectedPrice != line.UnitPrice ||
                explanation.SelectedRevisionId != source.RevisionId || explanation.SelectedRevision != source.RevisionNumber ||
                explanation.SelectedPriceId != source.PriceId || explanation.SelectedScope != source.Key.Scope ||
                explanation.Policy is null || explanation.PolicyRevision != explanation.Policy.PolicyRevision ||
                !PricingOverridePolicySemantics.Supported(explanation.Policy.EnvelopeSemanticsVersion) ||
                explanation.Candidates is null || explanation.Candidates.Count > 64 ||
                source.Key.UnitConversionRevision != context.UnitConversionRevision ||
                context.UnitConversionRevision != (conversion.SourceUnitId == conversion.TargetUnitId ? null : (long?)conversion.Revision) ||
                context.CustomerId != offer.CustomerId || context.OrganizationId != offer.CustomerContext?.OrganizationId ||
                context.ProgramId != offer.CustomerContext?.ProgramId || context.WholesaleApplicable != offer.WholesaleApplicable ||
                context.CommittedQuotationId.HasValue || context.CommittedAgreementId.HasValue || context.WholesaleTierId.HasValue ||
                !source.Validity.Contains(explanation.EvaluatedAt))
                throw new QuotationValidationException("retained_facts_invalid", "Retained quotation source facts are inconsistent.");
            if (CatalogQuantityConversion.Convert(catalog.QuantityArithmeticVersion, line.Quantity, catalog.UnitPrecision,
                catalog.BaseUnitPrecision.Value, conversion.Numerator, conversion.Denominator) != catalog.BaseQuantity)
                Fail("retained_facts_invalid");
            if (explanation.Discount is not null || explanation.Override is null && source.BaseUnitPrice != line.UnitPrice)
                Fail("retained_facts_invalid");
            // The retained BeyondPolicy decision is validated against the retained envelope snapshot
            // through the semantics version that snapshot pinned, not through this reader's current
            // engine meaning. A reader that cannot evaluate that version fails closed above.
            if (explanation.Override is { } evidence && (evidence.UnitPrice != line.UnitPrice ||
                evidence.BeyondPolicy != PriceSelectionEngine.RetainedBeyondPolicy(explanation.Policy,
                    line.UnitPrice, source.BaseUnitPrice) ||
                evidence.ApprovalReference is not null || evidence.ApprovedByAccountId.HasValue ||
                Text(evidence.Reason, 500, "retained_facts_invalid") != evidence.Reason)) Fail("retained_facts_invalid");
        }
        var input = new QuotationDraftRequest(offer.Mode, offer.Summary, offer.CurrencyCode, offer.ValidUntil,
            offer.Lines.Select(line => offer.Mode == QuotationPriceMode.Manual
                ? new QuotationLineInput(line.Quantity, line.Description, line.UnitCode, line.UnitPrice)
                : new QuotationLineInput(line.Quantity, ItemId: line.Catalog!.ItemId, UnitId: line.Catalog.UnitId,
                    ConversionRevision: line.Catalog.Conversion.SourceUnitId == line.Catalog.Conversion.TargetUnitId ? null : line.Catalog.Conversion.Revision,
                    OverridePrice: line.PriceSelection!.Explanation.Override?.UnitPrice,
                    OverrideReason: line.PriceSelection.Explanation.Override?.Reason)).ToArray(),
            offer.Terms, offer.CustomerId, offer.CustomerContext, offer.WholesaleApplicable);
        var normalized = Normalize(input);
        if (normalized.Summary != offer.Summary || normalized.CurrencyCode != offer.CurrencyCode || normalized.Terms != offer.Terms)
            Fail("retained_facts_invalid");
        decimal total = 0;
        for (var i = 0; i < offer.Lines.Count; i++)
        {
            var line = offer.Lines[i];
            if (line.Position != i + 1 || line.LineTotal != SellingPriceArithmetic.LineAmount(line.Quantity, line.UnitPrice)) Fail("retained_facts_invalid");
            total = checked(total + line.LineTotal);
            if (total > SellingPriceArithmetic.MaximumAmount) Fail("amount_invalid");
        }
        if (total != offer.Total) Fail("retained_facts_invalid");
    }
    private static decimal Decimal(decimal value) => decimal.Parse(value.ToString("0.####", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string code) => throw new QuotationValidationException(code, "The quotation contains invalid or unsupported facts.");
}
