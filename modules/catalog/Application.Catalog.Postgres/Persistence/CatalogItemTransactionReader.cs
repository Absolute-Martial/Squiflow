using Application.Catalog;
using Application.Tenancy;
using Npgsql;

namespace Application.Catalog.Postgres;

public static class CatalogItemTransactionReader
{
    public static async Task<CatalogItemSnapshot?> ReadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, TenantContext context, Guid itemId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(context);
        if (connection.State != System.Data.ConnectionState.Open || !ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException("An open connection and its active transaction are required.");
        if (context.TenantId == Guid.Empty || context.AccountId == Guid.Empty)
            throw new ArgumentException("Tenant context identifiers cannot be empty.");
        ArgumentOutOfRangeException.ThrowIfEqual(itemId, Guid.Empty);
        await using var command = new NpgsqlCommand(CatalogSql.FindItem, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("item_id", itemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var item = PostgresCatalogStore.ReadItem(reader, context.TenantId);
        if (item.ItemId != itemId)
            throw new InvalidOperationException("Catalog returned another resource's item facts.");
        return item;
    }
}
