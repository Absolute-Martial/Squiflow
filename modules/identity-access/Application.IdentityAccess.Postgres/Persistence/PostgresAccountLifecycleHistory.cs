using Npgsql;
using NpgsqlTypes;

namespace Application.IdentityAccess.Postgres;

public sealed class PostgresAccountLifecycleHistory(NpgsqlDataSource dataSource) : IAccountLifecycleHistory
{
    public async Task<AccountLifecycleHistoryPage> BrowseAsync(Guid accountId, int? beforeRevision, int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(accountId, Guid.Empty);
        if (beforeRevision is < 2) throw new ArgumentOutOfRangeException(nameof(beforeRevision));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var command = dataSource.CreateCommand(IdentityAccessSql.BrowseAccountLifecycleHistory);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.Add(new NpgsqlParameter("before", NpgsqlDbType.Integer)
        { Value = (object?)beforeRevision ?? DBNull.Value });
        command.Parameters.AddWithValue("limit", limit + 1);
        var items = new List<AccountLifecycleSnapshot>(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            items.Add(new(accountId, (AccountAvailability)reader.GetInt16(0), reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2), reader.GetGuid(3),
                reader.GetGuid(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6)));
        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new(items, hasMore ? items[^1].Revision : null);
    }
}
