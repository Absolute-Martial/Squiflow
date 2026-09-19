namespace SquiFlow.IdentityAccess;

public enum AccountAvailability
{
    Active = 1,
    Disabled = 2,
}

public sealed record AccountBinding(Guid AccountId, AccountAvailability Availability);

public interface IAccountBindingDirectory
{
    Task<AccountBinding?> FindAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken);
}
