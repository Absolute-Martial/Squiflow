using Application.CoreApi.Authorization;
using Application.Customers;
using Application.IdentityAccess;
using Application.Tenancy;

namespace Application.CoreApi.ImportExecution;

internal sealed class CurrentCustomerImportAuthority(
    IAccountDirectory accounts,
    ITenantDirectory tenants,
    ResolveTenantContext resolveContext,
    ITenantCustomerAuthorization authorization,
    ITenantAuthorizationAdministrationStore revisions) : ICustomerImportAuthority
{
    public async Task<CustomerImportAuthoritySnapshot?> CheckAsync(
        Guid tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty || accountId == Guid.Empty) return null;
        if (await accounts.FindAvailabilityAsync(accountId, cancellationToken).ConfigureAwait(false) != AccountAvailability.Active) return null;
        var tenant = await tenants.FindAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant?.TenantId != tenantId || tenant.Availability != TenantAvailability.Active) return null;
        var context = await resolveContext.ExecuteAsync(accountId, tenantId, cancellationToken).ConfigureAwait(false);
        if (context is null) return null;
        // Read on both sides of the provider check: a concurrent local authorization mutation
        // cannot be stamped with its later revision using an earlier provider allowance.
        var before = await revisions.GetAuthorizationRevisionAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (before is null or < 1) return null;
        if (!await authorization.CanImportCustomersAsync(accountId, tenantId, cancellationToken).ConfigureAwait(false))
            return null;
        var after = await revisions.GetAuthorizationRevisionAsync(tenantId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return after == before ? new(context, before.Value) : null;
        // Provider unavailability remains an explicit exception, not durable authority denial.
    }
}
