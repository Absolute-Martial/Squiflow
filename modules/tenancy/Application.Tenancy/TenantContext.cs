namespace Application.Tenancy;

public sealed record TenantContext
{
    private TenantContext(Guid tenantId, Guid accountId)
    {
        TenantId = tenantId;
        AccountId = accountId;
    }

    public Guid TenantId { get; }

    public Guid AccountId { get; }

    internal static TenantContext Create(Guid tenantId, Guid accountId) =>
        new(tenantId, accountId);
}

public sealed class ResolveTenantContext(ITenantMembershipDirectory memberships)
{
    public async Task<TenantContext?> ExecuteAsync(
        Guid accountId,
        Guid requestedTenantId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
        }

        if (requestedTenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(requestedTenantId));
        }

        return await memberships
            .IsActiveAsync(accountId, requestedTenantId, cancellationToken)
            .ConfigureAwait(false)
                ? TenantContext.Create(requestedTenantId, accountId)
                : null;
    }
}
