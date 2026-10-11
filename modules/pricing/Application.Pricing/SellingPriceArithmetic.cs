namespace Application.Pricing;

// Shared by price-bearing capabilities; source selection and authority stay separate.
public static class SellingPriceArithmetic
{
    public const decimal MaximumAmount = 999_999_999_999_999.9999m;

    public static decimal LineAmount(decimal quantity, decimal unitPrice)
    {
        if (!IsSupported(quantity) || quantity <= 0 || !IsSupported(unitPrice) || unitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Supported positive quantity and nonnegative price are required.");
        var amount = decimal.Round(checked(quantity * unitPrice), 4, MidpointRounding.ToEven);
        if (amount > MaximumAmount) throw new OverflowException("The selling amount exceeds the supported range.");
        return amount;
    }

    public static bool IsSupported(decimal value) =>
        value >= -MaximumAmount && value <= MaximumAmount && decimal.Round(value, 4) == value;
}
