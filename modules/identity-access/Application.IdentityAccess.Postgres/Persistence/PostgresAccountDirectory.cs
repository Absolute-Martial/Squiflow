using Npgsql;

namespace Application.IdentityAccess.Postgres;

public sealed class PostgresAccountDirectory(NpgsqlDataSource dataSource) : IAccountDirectory
{
    public async Task<AccountAvailability?> FindAvailabilityAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));

        await using var command = dataSource.CreateCommand(IdentityAccessSql.FindAccountAvailability);
        command.Parameters.AddWithValue("account_id", accountId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is short raw ? (AccountAvailability)raw : null;
    }
}
