using Microsoft.EntityFrameworkCore;

namespace Application.Tenancy.Postgres;

public sealed class PostgresTenantMembershipDirectory(TenancyDbContext database)
    : ITenantMembershipDirectory
{
    public async Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
        }

        return await database.Memberships
            .AsNoTracking()
            .Where(membership =>
                membership.AccountId == accountId &&
                membership.Availability == MembershipAvailability.Active)
            .Join(
                database.Tenants.AsNoTracking().Where(tenant => tenant.Availability == TenantAvailability.Active),
                membership => membership.TenantId,
                tenant => tenant.Id,
                (_, tenant) => new { tenant.Id, tenant.DisplayName })
            .OrderBy(tenant => tenant.DisplayName)
            .ThenBy(tenant => tenant.Id)
            .Select(tenant => new TenantMembership(tenant.Id, tenant.DisplayName))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> IsActiveAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        }

        return database.Memberships
            .AsNoTracking()
            .Where(membership =>
                membership.AccountId == accountId &&
                membership.TenantId == tenantId &&
                membership.Availability == MembershipAvailability.Active)
            .Join(
                database.Tenants.AsNoTracking().Where(tenant => tenant.Availability == TenantAvailability.Active),
                membership => membership.TenantId,
                tenant => tenant.Id,
                (_, _) => true)
            .AnyAsync(cancellationToken);
    }
}
