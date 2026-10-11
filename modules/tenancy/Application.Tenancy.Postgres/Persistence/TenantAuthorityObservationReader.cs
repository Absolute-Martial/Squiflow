using Application.Tenancy;
using Npgsql;

namespace Application.Tenancy.Postgres;

public sealed record TenantAuthorityObservation(
    int AuthorizationRevision,
    bool IsActive,
    bool IsInitialOwner);

public static class TenantAuthorityObservationReader
{
    private static readonly string Sql = LoadSql();

    public static async Task<TenantAuthorityObservation> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ValidateBorrowedTransaction(connection, transaction);
        ValidateTenantContext(tenantContext);

        await using var command = new NpgsqlCommand(Sql, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.AddWithValue("account_id", tenantContext.AccountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The tenant authority observation query returned no row.");
        }

        var revision = reader.GetInt32(0);
        if (revision < 1)
        {
            throw new InvalidOperationException("The tenant authorization revision is invalid.");
        }

        return new TenantAuthorityObservation(revision, reader.GetBoolean(1), reader.GetBoolean(2));
    }

    private static void ValidateBorrowedTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        if (connection.State != System.Data.ConnectionState.Open || !ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("An open connection and its active transaction are required.");
        }
    }

    private static void ValidateTenantContext(TenantContext tenantContext)
    {
        if (tenantContext.TenantId == Guid.Empty || tenantContext.AccountId == Guid.Empty)
        {
            throw new ArgumentException("Tenant context identifiers cannot be empty.", nameof(tenantContext));
        }
    }

    private static string LoadSql()
    {
        var resource = $"{typeof(TenantAuthorityObservationReader).Namespace}.Sql.ReadTenantAuthorityObservation.sql";
        using var stream = typeof(TenantAuthorityObservationReader).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded Tenancy SQL resource: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
