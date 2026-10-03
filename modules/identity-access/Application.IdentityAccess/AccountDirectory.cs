namespace Application.IdentityAccess;

public interface IAccountDirectory
{
    Task<AccountAvailability?> FindAvailabilityAsync(
        Guid accountId,
        CancellationToken cancellationToken);
}
