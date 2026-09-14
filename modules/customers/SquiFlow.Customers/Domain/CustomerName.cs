namespace SquiFlow.Customers.Domain;

public readonly record struct CustomerName
{
    public CustomerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Customer name must not be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}
