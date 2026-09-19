using Microsoft.EntityFrameworkCore;

namespace SquiFlow.IdentityAccess.Postgres;

public sealed class PostgresAccountBindingDirectory(IdentityAccessDbContext database)
    : IAccountBindingDirectory
{
    public Task<AccountBinding?> FindAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return database.ExternalIdentityBindings
            .AsNoTracking()
            .Where(binding => binding.Issuer == identity.Issuer && binding.Subject == identity.Subject)
            .Join(
                database.Accounts.AsNoTracking(),
                binding => binding.AccountId,
                account => account.Id,
                (_, account) => new AccountBinding(account.Id, account.Availability))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
