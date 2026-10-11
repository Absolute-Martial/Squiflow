using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Customers;
using Application.Pricing;
using Application.Tenancy;

namespace Application.Orders;

public sealed record OrderQuotationOrigin(Guid QuotationId, Guid IssuedRevisionId, long Number, long RevisionNumber);
// Only the quotation owner supplies these accepted facts; no Order ingress accepts this contract.
public sealed record AcceptedQuotationOrder(OrderQuotationOrigin Origin, string Summary, string CurrencyCode,
    decimal Total, IReadOnlyList<OrderDraftLine> Lines, CustomerOrderContext? CustomerContext)
{
    private static readonly JsonSerializerOptions CanonicalJson = CreateOptions();
    public bool Matches(OrderDraftSnapshot order) => order.QuotationOrigin == Origin &&
        order.Summary == Summary && order.CurrencyCode == CurrencyCode && order.Total == Total &&
        order.CustomerContext == CustomerContext &&
        JsonSerializer.Serialize(Lines, CanonicalJson) == JsonSerializer.Serialize(order.Lines, CanonicalJson);
    public void RequireValid(Guid tenantId)
    {
        if (Origin is null || Origin.QuotationId == Guid.Empty || Origin.IssuedRevisionId == Guid.Empty ||
            Origin.Number < 1 || Origin.RevisionNumber < 1 || string.IsNullOrWhiteSpace(Summary) || Summary.Length > 200 ||
            CurrencyCode is null || CurrencyCode.Length != 3 || CurrencyCode.Any(c => !char.IsAsciiLetterUpper(c)) ||
            Lines is null || Lines.Count is < 1 or > 100 || Lines.Any(l => l is null) || !SellingPriceArithmetic.IsSupported(Total))
            throw new InvalidOperationException("Accepted quotation Order facts are invalid.");
        decimal total = 0;
        for (var i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            if (line.Position != i + 1 || line.Quantity <= 0 || line.UnitPrice < 0 ||
                !SellingPriceArithmetic.IsSupported(line.Quantity) || !SellingPriceArithmetic.IsSupported(line.UnitPrice) ||
                SellingPriceArithmetic.LineAmount(line.Quantity, line.UnitPrice) != line.LineTotal ||
                string.IsNullOrWhiteSpace(line.Description) || line.Description.Length > 300 ||
                string.IsNullOrWhiteSpace(line.UnitCode) || line.UnitCode.Length > 64)
                throw new InvalidOperationException("Accepted quotation Order lines are invalid.");
            total = checked(total + line.LineTotal);
        }
        if (total != Total || CustomerContext is { } c && (c.OrganizationId == Guid.Empty || c.ProgramId == Guid.Empty))
            throw new InvalidOperationException("Accepted quotation Order totals or attribution are invalid.");
        OrderCommercialFactsValidation.RequireValid(new(Origin.QuotationId, tenantId, Guid.Empty, Summary, CurrencyCode,
            Total, 1, DateTimeOffset.UnixEpoch, Lines, CustomerContext: CustomerContext, QuotationOrigin: Origin));
    }
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(); options.Converters.Add(new CanonicalDecimal()); return options;
    }
    private sealed class CanonicalDecimal : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }
}
public interface IOrderAcceptedQuotationReader
{
    Task<AcceptedQuotationOrder?> ReadAsync(TenantContext context, Guid orderId, OrderQuotationOrigin origin, CancellationToken ct);
}
