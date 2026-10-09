using Application.Orders;
using Application.Tenancy;
using Npgsql;

namespace Application.Orders.Postgres;

public static class CommittedOrderLineTransactionReader
{
    private static readonly string Sql = LoadSql();

    public static async Task<CommittedOrderLineReference?> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantContext tenantContext,
        Guid orderId,
        int linePosition,
        long expectedRevision,
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

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order identity cannot be empty.", nameof(orderId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(linePosition, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(linePosition, 100);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedRevision, 1);

        await using var command = new NpgsqlCommand(Sql, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.AddWithValue("order_id", orderId);
        command.Parameters.AddWithValue("line_position", linePosition);
        command.Parameters.AddWithValue("expected_revision", expectedRevision);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var reference = new CommittedOrderLineReference(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetInt32(3));
        if (reference.TenantId != tenantContext.TenantId || reference.OrderId != orderId ||
            reference.OrderRevision != expectedRevision || reference.LinePosition != linePosition)
        {
            throw new InvalidOperationException("The Orders database returned a mismatched committed line reference.");
        }

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The Orders database returned duplicate committed line references.");
        }

        return reference;
    }

    private static void ValidateBorrowedTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        if (connection.State != System.Data.ConnectionState.Open || !ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("An open connection and its active transaction are required.");
        }
    }

    private static string LoadSql()
    {
        var resource = $"{typeof(CommittedOrderLineTransactionReader).Namespace}.Sql.ReadCommittedOrderLineReference.sql";
        using var stream = typeof(CommittedOrderLineTransactionReader).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded Orders SQL resource: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
