using Npgsql;

namespace Application.Tenancy.Postgres;

public sealed class PostgresTenantDirectory(NpgsqlDataSource dataSource) : ITenantDirectory
{
    public async Task<TenantSnapshot?> FindAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(TenantDirectorySql.FindTenant, connection);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new TenantSnapshot(
            reader.GetGuid(0),
            reader.GetString(1),
            (TenantAvailability)reader.GetInt16(2));
    }
}
