namespace Application.Tenancy;

public sealed record TenantSnapshot(
    Guid TenantId,
    string DisplayName,
    TenantAvailability Availability);

public interface ITenantDirectory
{
    Task<TenantSnapshot?> FindAsync(
        Guid tenantId,
        CancellationToken cancellationToken);
}
