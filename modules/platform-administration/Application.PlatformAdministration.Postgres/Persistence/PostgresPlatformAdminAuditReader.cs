using Npgsql;
using NpgsqlTypes;

namespace Application.PlatformAdministration.Postgres;

public sealed class PostgresPlatformAdminAuditReader(NpgsqlDataSource dataSource) : IPlatformAdminAuditReader
{
    public async Task<PlatformAdminAccessAuditPage> BrowseAccessAsync(PlatformAdminAccessAuditQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var command = dataSource.CreateCommand(PlatformAdministrationSql.BrowseAccessAudit);
        command.Parameters.Add(new NpgsqlParameter("before_at", NpgsqlDbType.TimestampTz)
        { Value = (object?)query.Before?.OccurredAt.ToUniversalTime() ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("before_id", NpgsqlDbType.Uuid)
        { Value = (object?)query.Before?.Id ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("principal", NpgsqlDbType.Uuid)
        { Value = (object?)query.PrincipalId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("outcome", NpgsqlDbType.Smallint)
        { Value = query.Outcome is { } outcome ? (short)outcome : DBNull.Value });
        command.Parameters.AddWithValue("limit", query.Limit + 1);
        var items = new List<PlatformAdminAccessAuditSummary>(query.Limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            items.Add(new(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3),
                (PlatformAdminAccessAuditOutcome)reader.GetInt16(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6)));
        var hasMore = items.Count > query.Limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new(items, hasMore ? new(items[^1].OccurredAt, items[^1].Id) : null);
    }
}
