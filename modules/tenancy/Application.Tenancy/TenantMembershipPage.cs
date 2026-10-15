namespace Application.Tenancy;

public sealed record TenantMembershipPage(IReadOnlyList<TenantMembership> Items, Guid? NextTenantId)
{
    public const int MaximumCount = 50;
}

public interface IBoundedTenantMembershipDirectory
{
    Task<TenantMembershipPage> BrowseActiveAsync(Guid accountId, Guid? afterTenantId, CancellationToken cancellationToken);
    Task<TenantMembership?> FindActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
}
