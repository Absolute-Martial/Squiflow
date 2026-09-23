namespace Application.CoreApi.Composition;

internal sealed record ProfileRuntimeKey
{
    internal const int MaximumFingerprintLength = 128;

    internal ProfileRuntimeKey(
        Guid tenantId,
        string implementationFingerprint,
        long implementationRevision)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        }

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

        TenantId = tenantId;
        ImplementationFingerprint = implementationFingerprint;
        ImplementationRevision = implementationRevision;
    }

    internal Guid TenantId { get; }

    internal string ImplementationFingerprint { get; }

    internal long ImplementationRevision { get; }

    public override string ToString() =>
        $"{TenantId:D}/{ImplementationFingerprint}@{ImplementationRevision}";
}
