namespace Application.Orders.Pricing;

public sealed record OrderDraftPricePreviewRequest(
    string CurrencyCode,
    IReadOnlyList<OrderDraftLineInput> Lines);

public sealed record OrderDraftPricePreview(
    string CurrencyCode,
    IReadOnlyList<OrderDraftLine> Lines,
    decimal Total);

// A preview calculates supplied selling prices; it does not select prices or authorize a commitment.
public static class PreviewOrderDraftPrice
{
    public static OrderDraftPricePreview Execute(OrderDraftPricePreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var currencyCode = OrderDraftRules.NormalizeCurrencyCode(request.CurrencyCode);
        var (lines, total) = OrderDraftPriceCalculator.Calculate(request.Lines);
        return new OrderDraftPricePreview(currencyCode, Array.AsReadOnly(lines), total);
    }
}
