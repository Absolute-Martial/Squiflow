using Npgsql;
using NpgsqlTypes;

namespace Application.Tenancy.Postgres;

public sealed class PostgresPlatformTenantRegistry(NpgsqlDataSource dataSource) : IPlatformTenantRegistry
{
    public async Task<PlatformTenantRegistryEntry?> FindAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        await using var command = dataSource.CreateCommand(TenantDirectorySql.FindPlatformTenant);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadTenant(reader) : null;
    }

    public async Task<PlatformTenantRegistryPage> BrowseAsync(Guid? afterTenantId, int limit, CancellationToken cancellationToken)
    {
        ValidateLimit(limit);
        if (afterTenantId == Guid.Empty) throw new ArgumentException("Cursor tenant identity cannot be empty.", nameof(afterTenantId));
        await using var command = dataSource.CreateCommand(TenantDirectorySql.BrowsePlatformTenants);
        command.Parameters.Add(new NpgsqlParameter("after_tenant_id", NpgsqlDbType.Uuid) { Value = afterTenantId is { } value ? value : DBNull.Value });
        command.Parameters.AddWithValue("limit", limit + 1);
        var items = new List<PlatformTenantRegistryEntry>(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadTenant(reader));
        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new PlatformTenantRegistryPage(items, hasMore ? items[^1].TenantId : null);
    }

    private static PlatformTenantRegistryEntry ReadTenant(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), (TenantAvailability)reader.GetInt16(2), reader.GetInt32(3),
        reader.GetFieldValue<DateTimeOffset>(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5));

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "Registry page size must be from 1 through 100.");
    }
}

public sealed class PostgresPlatformMembershipRegistry(NpgsqlDataSource dataSource) : IPlatformMembershipRegistry
{
    public async Task<PlatformMembershipRegistryEntry?> FindAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        ValidateIds(tenantId, accountId);
        await using var command = dataSource.CreateCommand(TenantDirectorySql.FindPlatformMembership);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadMembership(reader) : null;
    }

    public async Task<PlatformMembershipRegistryPage> BrowseAsync(Guid tenantId, Guid? afterAccountId, int limit, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        if (afterAccountId == Guid.Empty) throw new ArgumentException("Cursor account identity cannot be empty.", nameof(afterAccountId));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "Registry page size must be from 1 through 100.");
        await using var command = dataSource.CreateCommand(TenantDirectorySql.BrowsePlatformMemberships);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.Add(new NpgsqlParameter("after_account_id", NpgsqlDbType.Uuid) { Value = afterAccountId is { } value ? value : DBNull.Value });
        command.Parameters.AddWithValue("limit", limit + 1);
        var items = new List<PlatformMembershipRegistryEntry>(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadMembership(reader));
        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new PlatformMembershipRegistryPage(items, hasMore ? items[^1].AccountId : null);
    }

    private static void ValidateIds(Guid tenantId, Guid accountId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        if (accountId == Guid.Empty) throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
    }

    private static PlatformMembershipRegistryEntry ReadMembership(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), (MembershipAvailability)reader.GetInt16(2), reader.GetInt32(3),
        reader.GetFieldValue<DateTimeOffset>(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
        reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
        reader.GetBoolean(8));
}
