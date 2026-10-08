using Application.Tenancy;

namespace Application.Customers;

public enum CustomerImportSourceReadStatus
{
    Readable = 1,
    NotFound = 2,
    Unavailable = 3,
}

public sealed record CustomerImportSourceReadResult(
    CustomerImportSourceReadStatus Status,
    CustomerImportSourceSnapshot? Source = null);

public sealed class ReadCustomerImportSource(
    ICustomerImportSourceStore source,
    TimeProvider timeProvider)
{
    public async Task<CustomerImportSourceReadResult> ExecuteAsync(
        TenantContext context,
        Guid importId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var metadata = await source.ReadSourceAsync(context, importId, cancellationToken).ConfigureAwait(false);
        if (metadata is null || metadata.State is CustomerImportSourceState.Retired)
            return new(CustomerImportSourceReadStatus.NotFound);

        if (metadata.State is not CustomerImportSourceState.Available || !IsWithinRetention(metadata))
            return new(CustomerImportSourceReadStatus.Unavailable);

        return new(CustomerImportSourceReadStatus.Readable, metadata);
    }

    private bool IsWithinRetention(CustomerImportSourceSnapshot metadata) => metadata.Retention switch
    {
        CustomerImportRetention.DefaultSevenDays => metadata.ExpiresAt is { } expiresAt
            && expiresAt > timeProvider.GetUtcNow(),
        CustomerImportRetention.TenantArchived => true,
        _ => false,
    };
}
