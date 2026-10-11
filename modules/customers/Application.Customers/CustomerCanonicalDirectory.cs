using Application.Tenancy;

namespace Application.Customers;

public interface ICustomerCanonicalDirectory
{
    // For current mutable attribution only, from one consistent read snapshot.
    // Successors advance and remain one hop; historical reads/receipt replays retain
    // the originally referenced identity and frozen facts.
    Task<CustomerIndividualSnapshot?> ResolveCurrentCustomerAsync(TenantContext context, Guid customerId, CancellationToken cancellationToken);
}
