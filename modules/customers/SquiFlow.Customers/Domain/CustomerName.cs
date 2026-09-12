namespace SquiFlow.Customers.Domain;

public readonly record struct CustomerName
{
    private CustomerName(string value) => Value = value;

    public string Value { get; }

    public static CustomerName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Customer name is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Customer name cannot exceed 120 characters.");
        }

        return new CustomerName(normalized);
    }

    public override string ToString() => Value;
}
