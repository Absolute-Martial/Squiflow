namespace Application.Tenancy;

public sealed record PlatformTenantRegistryEntry(
    Guid TenantId,
    string DisplayName,
    TenantAvailability Availability,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SuspendedAt);

public sealed record PlatformTenantRegistryPage(
    IReadOnlyList<PlatformTenantRegistryEntry> Items,
    Guid? NextTenantId);

public interface IPlatformTenantRegistry
{
    Task<PlatformTenantRegistryEntry?> FindAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<PlatformTenantRegistryPage> BrowseAsync(
        Guid? afterTenantId,
        int limit,
        CancellationToken cancellationToken);
}

public sealed record PlatformMembershipRegistryEntry(
    Guid TenantId,
    Guid AccountId,
    MembershipAvailability Availability,
    int Revision,
    DateTimeOffset InvitedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? SuspendedAt,
    DateTimeOffset? RemovedAt,
    bool IsInitialOwner);

public sealed record PlatformMembershipRegistryPage(
    IReadOnlyList<PlatformMembershipRegistryEntry> Items,
    Guid? NextAccountId);

public interface IPlatformMembershipRegistry
{
    Task<PlatformMembershipRegistryEntry?> FindAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken);

    Task<PlatformMembershipRegistryPage> BrowseAsync(
        Guid tenantId,
        Guid? afterAccountId,
        int limit,
        CancellationToken cancellationToken);
}
