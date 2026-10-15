using Microsoft.EntityFrameworkCore;

namespace Application.Tenancy.Postgres;

public sealed class PostgresTenantMembershipDirectory(TenancyDbContext database)
    : ITenantMembershipDirectory, IBoundedTenantMembershipDirectory
{
    public async Task<TenantMembershipPage> BrowseActiveAsync(
        Guid accountId, Guid? afterTenantId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || afterTenantId == Guid.Empty)
        {
            throw new ArgumentException("Account and continuation identities cannot be empty.");
        }

        var rows = await ActiveTenants(accountId)
            .Where(tenant => !afterTenantId.HasValue || tenant.Id.CompareTo(afterTenantId.Value) > 0)
            .OrderBy(tenant => tenant.Id)
            .Take(TenantMembershipPage.MaximumCount + 1)
            .Select(tenant => new TenantMembership(tenant.Id, tenant.DisplayName))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var items = rows.Take(TenantMembershipPage.MaximumCount).ToArray();
        return new TenantMembershipPage(items, rows.Length > items.Length ? items[^1].TenantId : null);
    }

    public Task<TenantMembership?> FindActiveAsync(
        Guid accountId, Guid tenantId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new ArgumentException("Account and tenant identities cannot be empty.");
        }

        return ActiveTenants(accountId).Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new TenantMembership(tenant.Id, tenant.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<TenantRow> ActiveTenants(Guid accountId) => database.Memberships.AsNoTracking()
        .Where(membership => membership.AccountId == accountId && membership.Availability == MembershipAvailability.Active)
        .Join(database.Tenants.AsNoTracking().Where(tenant => tenant.Availability == TenantAvailability.Active),
            membership => membership.TenantId, tenant => tenant.Id, (_, tenant) => tenant);

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
