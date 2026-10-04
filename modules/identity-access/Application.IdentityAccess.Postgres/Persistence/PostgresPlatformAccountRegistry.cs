using Npgsql;
using NpgsqlTypes;

namespace Application.IdentityAccess.Postgres;

public sealed class PostgresPlatformAccountRegistry(NpgsqlDataSource dataSource) : IPlatformAccountRegistry
{
    public async Task<PlatformAccountRegistryEntry?> FindAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
        await using var command = dataSource.CreateCommand(IdentityAccessSql.FindPlatformAccount);
        command.Parameters.AddWithValue("account_id", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadAccount(reader) : null;
    }

    public async Task<PlatformAccountRegistryPage> BrowseAsync(Guid? afterAccountId, int limit, CancellationToken cancellationToken)
    {
        if (afterAccountId == Guid.Empty) throw new ArgumentException("Cursor account identity cannot be empty.", nameof(afterAccountId));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "Registry page size must be from 1 through 100.");
        await using var command = dataSource.CreateCommand(IdentityAccessSql.BrowsePlatformAccounts);
        command.Parameters.Add(new NpgsqlParameter("after_account_id", NpgsqlDbType.Uuid) { Value = afterAccountId is { } value ? value : DBNull.Value });
        command.Parameters.AddWithValue("limit", limit + 1);
        var items = new List<PlatformAccountRegistryEntry>(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadAccount(reader));
        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new PlatformAccountRegistryPage(items, hasMore ? items[^1].AccountId : null);
    }

    private static PlatformAccountRegistryEntry ReadAccount(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), (AccountAvailability)reader.GetInt16(1), reader.GetFieldValue<DateTimeOffset>(2),
        reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3));
}
