namespace SquiFlow.CoreApi.Composition;

internal sealed record ProfileRuntimeKey
{
    internal const int MaximumFingerprintLength = 128;

    internal ProfileRuntimeKey(string implementationFingerprint, long implementationRevision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(implementationFingerprint);

        if (implementationFingerprint.Length > MaximumFingerprintLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(implementationFingerprint),
                $"The implementation fingerprint cannot exceed {MaximumFingerprintLength} characters.");
        }

        if (implementationRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(implementationRevision),
                "The implementation revision must be greater than zero.");
        }

        ImplementationFingerprint = implementationFingerprint;
        ImplementationRevision = implementationRevision;
    }

    internal string ImplementationFingerprint { get; }

    internal long ImplementationRevision { get; }

    public override string ToString() =>
        $"{ImplementationFingerprint}@{ImplementationRevision}";
}
