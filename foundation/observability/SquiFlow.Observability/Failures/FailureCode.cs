namespace SquiFlow.Observability.Failures;

public readonly record struct FailureCode
{
    public FailureCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Failure code must be non-empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}
