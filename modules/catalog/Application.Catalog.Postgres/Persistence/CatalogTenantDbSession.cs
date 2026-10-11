using System.Data;
using Npgsql;

namespace Application.Catalog.Postgres;

internal sealed class CatalogTenantDbSession : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private bool _completed;

    private CatalogTenantDbSession(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    internal static Task<CatalogTenantDbSession> OpenAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        OpenAsync(dataSource, tenantId, IsolationLevel.ReadCommitted, cancellationToken);

    internal static Task<CatalogTenantDbSession> OpenSnapshotAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        OpenAsync(dataSource, tenantId, IsolationLevel.RepeatableRead, cancellationToken);

    internal static Task<CatalogTenantDbSession> OpenPublicationAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        OpenAsync(dataSource, tenantId, IsolationLevel.ReadCommitted, cancellationToken, lockPublication: true);

    private static async Task<CatalogTenantDbSession> OpenAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken,
        bool lockPublication = false)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
            try
            {
                // Acquire before receipt, row or conversion-pair locks. Queries
                // never request the exclusive lock while a reader pin is held.
                if (lockPublication)
                {
                    await using var pin = new NpgsqlCommand(CatalogSql.LockPublication, connection, transaction);
                    pin.Parameters.AddWithValue("tenant_id", tenantId);
                    await pin.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await using var setTenant = new NpgsqlCommand(CatalogSql.SetTenant, connection, transaction);
                setTenant.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
                await setTenant.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return new CatalogTenantDbSession(connection, transaction);
            }
            catch
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal NpgsqlCommand CreateCommand(string sql)
    {
        if (_completed)
            throw new InvalidOperationException("The Catalog tenant database session has completed.");
        return new NpgsqlCommand(sql, _connection, _transaction);
    }

    internal async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_completed)
            throw new InvalidOperationException("The Catalog tenant database session has completed.");
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    internal async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_completed)
            throw new InvalidOperationException("The Catalog tenant database session has completed.");
        await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        _completed = true;
        try
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
