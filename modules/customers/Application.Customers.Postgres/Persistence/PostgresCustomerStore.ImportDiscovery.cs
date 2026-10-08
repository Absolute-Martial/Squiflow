using Application.Customers;
using NpgsqlTypes;

namespace Application.Customers.Postgres;

public sealed partial class PostgresCustomerStore
{
    public async Task<CustomerImportTenantPage> DiscoverRunnableTenantsAsync(
        Guid? afterTenantId, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > ICustomerImportWorkDiscovery.MaximumTenantPageSize)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (afterTenantId == Guid.Empty) throw new ArgumentException("Discovery cursor cannot be empty.", nameof(afterTenantId));

        // No tenant setting or bypass capability is supplied to the runtime connection.
        // The sole cross-tenant surface is the separately granted fixed-shape function.
        await using var command = dataSource.CreateCommand(CustomerSql.DiscoverImportTenants);
        command.Parameters.Add("after_tenant_id", NpgsqlDbType.Uuid).Value = (object?)afterTenantId ?? DBNull.Value;
        command.Parameters.AddWithValue("limit", limit);
        var tenants = new List<Guid>(limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) tenants.Add(reader.GetGuid(0));
        return new(tenants, tenants.Count == limit ? tenants[^1] : null);
    }
}
