using Npgsql;

namespace Application.Catalog.Postgres;

public static class CatalogPublicationPin
{
    private static readonly string AcquireSharedSql = LoadSql();

    public static async Task AcquireSharedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);

        await using var command = new NpgsqlCommand(AcquireSharedSql, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string LoadSql()
    {
        var resourceName = $"{typeof(CatalogPublicationPin).Namespace}.Sql.PinPublicationRead.sql";
        using var stream = typeof(CatalogPublicationPin).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded Catalog SQL resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
