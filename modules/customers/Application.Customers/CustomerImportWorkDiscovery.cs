namespace Application.Customers;

// Discovery is a privileged executor boundary, not a business directory or tenant authority.
// A candidate still requires a fresh actor/membership/permission/revision check for each row.
public sealed record CustomerImportTenantPage(IReadOnlyList<Guid> TenantIds, Guid? NextTenantId);

public interface ICustomerImportWorkDiscovery
{
    const int MaximumTenantPageSize = 50;

    Task<CustomerImportTenantPage> DiscoverRunnableTenantsAsync(
        Guid? afterTenantId, int limit, CancellationToken cancellationToken);
}
