using Application.Orders;
using Application.Orders.Pricing;
using Xunit;

namespace Application.Orders.Tests;

public sealed class OrderDraftPricePreviewTests
{
    [Fact]
    public void PreviewMatchesIntentForNormalizedCurrencyLinesAndPerLineRounding()
    {
        IReadOnlyList<OrderDraftLineInput> lines =
        [
            new(" Poster ", 2.5m, " ea ", 12.3456m),
            new("Small amount", 0.5m, "ea", 0.0001m),
            new("Another small amount", 0.5m, "ea", 0.0001m),
        ];

        var preview = PreviewOrderDraftPrice.Execute(new OrderDraftPricePreviewRequest(" usd ", lines));
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest("Order", " usd ", lines));

        Assert.Equal(intent.CurrencyCode, preview.CurrencyCode);
        Assert.Equal(intent.Lines, preview.Lines);
        Assert.Equal(intent.Total, preview.Total);
        Assert.Equal("USD", preview.CurrencyCode);
        Assert.Equal("Poster", preview.Lines[0].Description);
        Assert.Equal(30.864m, preview.Lines[0].LineTotal);
        Assert.Equal(0m, preview.Lines[1].LineTotal);
        Assert.Equal(30.864m, preview.Total);
    }

    [Fact]
    public void PreviewRejectsInvalidCurrency()
    {
        var error = Assert.Throws<OrderDraftValidationException>(() =>
            PreviewOrderDraftPrice.Execute(new OrderDraftPricePreviewRequest(
                "US1",
                [new OrderDraftLineInput("Line", 1m, "EA", 1m)])));

        Assert.Equal("currency_code_invalid", error.Code);
    }

    [Fact]
    public void PreviewRejectsNullEmptyOversizedAndNullLineInputs()
    {
        var empty = Assert.Throws<OrderDraftValidationException>(() => PreviewOrderDraftPrice.Execute(
            new OrderDraftPricePreviewRequest("USD", [])));
        var nullLines = Assert.Throws<OrderDraftValidationException>(() => PreviewOrderDraftPrice.Execute(
            new OrderDraftPricePreviewRequest("USD", null!)));
        var oversized = Assert.Throws<OrderDraftValidationException>(() => PreviewOrderDraftPrice.Execute(
            new OrderDraftPricePreviewRequest("USD", Enumerable.Repeat(
                new OrderDraftLineInput("Line", 1m, "EA", 1m), 101).ToArray())));
        var nullLine = Assert.Throws<OrderDraftValidationException>(() => PreviewOrderDraftPrice.Execute(
            new OrderDraftPricePreviewRequest("USD", [null!])));

        Assert.Equal("lines_invalid", empty.Code);
        Assert.Equal("lines_invalid", nullLines.Code);
        Assert.Equal("lines_invalid", oversized.Code);
        Assert.Equal("line_invalid", nullLine.Code);
    }

    [Fact]
    public void PreviewRejectsNegativePriceExcessQuantityPrecisionAndOverflow()
    {
        AssertInvalid(1m, -0.0001m, "unit_price_invalid");
        AssertInvalid(0.00001m, 1m, "quantity_invalid");
        AssertInvalid(999_999_999_999_999.9999m, 999_999_999_999_999.9999m, "amount_out_of_range");
    }

    private static void AssertInvalid(decimal quantity, decimal unitPrice, string expectedCode)
    {
        var error = Assert.Throws<OrderDraftValidationException>(() =>
            PreviewOrderDraftPrice.Execute(new OrderDraftPricePreviewRequest(
                "USD",
                [new OrderDraftLineInput("Line", quantity, "EA", unitPrice)])));

        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public void PreviewLinesDoNotChangeWhenInputCollectionChanges()
    {
        var inputs = new List<OrderDraftLineInput> { new("Original", 2m, "EA", 3m) };
        var preview = PreviewOrderDraftPrice.Execute(new OrderDraftPricePreviewRequest("USD", inputs));

        inputs[0] = new OrderDraftLineInput("Changed", 9m, "HR", 99m);

        Assert.Equal("Original", preview.Lines[0].Description);
        Assert.Equal(2m, preview.Lines[0].Quantity);
        Assert.Equal(6m, preview.Lines[0].LineTotal);
        Assert.Equal(6m, preview.Total);
    }
}
