namespace SquiFlow.Tenancy;

public enum TenantAvailability
{
    Active = 1,
    Suspended = 2,
}

public enum MembershipAvailability
{
    Active = 1,
    Suspended = 2,
}

public sealed record TenantMembership(Guid TenantId, string DisplayName);

public interface ITenantMembershipDirectory
{
    Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
        Guid accountId,
        CancellationToken cancellationToken);

    Task<bool> IsActiveAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken);
}
