namespace SquiFlow.Observability.Context;

public readonly record struct CorrelationId
{
    public CorrelationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Correlation id must be non-empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public static CorrelationId New() => new(Guid.NewGuid().ToString("N"));
    public override string ToString() => Value;
}
