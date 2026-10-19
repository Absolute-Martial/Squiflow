namespace Application.IdentityAccess;

public sealed record AccountLifecycleHistoryPage(IReadOnlyList<AccountLifecycleSnapshot> Items, int? NextRevision);

public interface IAccountLifecycleHistory
{
    Task<AccountLifecycleHistoryPage> BrowseAsync(Guid accountId, int? beforeRevision, int limit,
        CancellationToken cancellationToken);
}
