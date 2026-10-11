using Application.Catalog;
using Application.Tenancy;
using Npgsql;

namespace Application.Catalog.Postgres;

public static class CatalogUnitTransactionReader
{
    public static async Task<CatalogUnitSnapshot?> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantContext tenantContext,
        Guid unitId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ValidateBorrowedTransaction(connection, transaction);
        if (tenantContext.TenantId == Guid.Empty || tenantContext.AccountId == Guid.Empty)
        {
            throw new ArgumentException("Tenant context identifiers cannot be empty.", nameof(tenantContext));
        }

        if (unitId == Guid.Empty)
        {
            throw new ArgumentException("Catalog unit identity cannot be empty.", nameof(unitId));
        }

        await using var command = new NpgsqlCommand(CatalogSql.FindUnit, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.AddWithValue("unit_id", unitId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var returnedTenantId = tenantContext.TenantId;
        var snapshot = new CatalogUnitSnapshot(
            reader.GetGuid(0), returnedTenantId, reader.GetString(1), reader.GetString(2), reader.GetInt16(3),
            ParseStatus(reader.GetString(4)), reader.GetInt64(5), reader.GetGuid(6), reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9) ? null : reader.GetGuid(9));
        if (snapshot.UnitId != unitId)
        {
            throw new InvalidOperationException("The catalog database returned a different unit identity.");
        }

        return snapshot;
    }

    private static CatalogEntityStatus ParseStatus(string value) => value switch
    {
        "active" => CatalogEntityStatus.Active,
        "retired" => CatalogEntityStatus.Retired,
        _ => throw new InvalidOperationException("The catalog database returned an unsupported status.")
    };

    private static void ValidateBorrowedTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        if (connection.State != System.Data.ConnectionState.Open || !ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("An open connection and its active transaction are required.");
        }
    }

}
