namespace Application.IdentityAccess;

public sealed record PlatformAccountRegistryEntry(
    Guid AccountId,
    AccountAvailability Availability,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DisabledAt);

public sealed record PlatformAccountRegistryPage(
    IReadOnlyList<PlatformAccountRegistryEntry> Items,
    Guid? NextAccountId);

public interface IPlatformAccountRegistry
{
    Task<PlatformAccountRegistryEntry?> FindAsync(
        Guid accountId,
        CancellationToken cancellationToken);

    Task<PlatformAccountRegistryPage> BrowseAsync(
        Guid? afterAccountId,
        int limit,
        CancellationToken cancellationToken);
}
