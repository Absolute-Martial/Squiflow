namespace Application.Orders.Pricing;

// Calculates supplied draft selling prices; selecting or authorizing a price is a separate responsibility.
internal static class OrderDraftPriceCalculator
{
    internal static (OrderDraftLine[] Lines, decimal Total) Calculate(IReadOnlyList<OrderDraftLineInput>? inputs)
    {
        if (inputs is null || inputs.Count is < 1 or > 100)
        {
            throw new OrderDraftValidationException(
                "lines_invalid",
                "An order draft requires between 1 and 100 lines.");
        }

        var lines = new OrderDraftLine[inputs.Count];
        decimal total = 0;
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index]
                ?? throw new OrderDraftValidationException("line_invalid", "Order lines cannot be null.");
            var description = OrderDraftRules.NormalizeRequiredText(
                input.Description,
                300,
                "line_description_invalid",
                "Line description is required and cannot exceed 300 characters.");
            var unitCode = OrderDraftRules.NormalizeCode(
                input.UnitCode,
                1,
                16,
                "unit_code_invalid",
                "Unit code must contain between 1 and 16 ASCII letters or digits.");
            OrderDraftRules.RequirePositiveDecimal(input.Quantity, "quantity_invalid");
            OrderDraftRules.RequireNonNegativeDecimal(input.UnitPrice, "unit_price_invalid");

            decimal lineTotal;
            try
            {
                lineTotal = decimal.Round(
                    checked(input.Quantity * input.UnitPrice),
                    4,
                    MidpointRounding.ToEven);
                OrderDraftRules.RequireNonNegativeDecimal(lineTotal, "line_total_invalid");
                total = checked(total + lineTotal);
                OrderDraftRules.RequireNonNegativeDecimal(total, "order_total_invalid");
            }
            catch (OverflowException)
            {
                throw new OrderDraftValidationException(
                    "amount_out_of_range",
                    "The order amount exceeds the supported range.");
            }

            lines[index] = new OrderDraftLine(
                index + 1,
                description,
                input.Quantity,
                unitCode,
                input.UnitPrice,
                lineTotal);
        }

        return (lines, total);
    }
}
