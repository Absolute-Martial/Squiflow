namespace Application.IdentityAccess;

public sealed class ResolveAccountBinding(IAccountBindingDirectory directory)
{
    public Task<AccountBinding?> ExecuteAsync(
        ExternalIdentity authenticatedIdentity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authenticatedIdentity);
        return directory.FindAsync(authenticatedIdentity, cancellationToken);
    }
}
