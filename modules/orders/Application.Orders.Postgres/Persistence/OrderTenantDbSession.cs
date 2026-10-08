using System.Data;
using Npgsql;

namespace Application.Orders.Postgres;

// A session is ready for Orders commands only after the transaction-local RLS context is set.
internal sealed class OrderTenantDbSession : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private bool _completed;
    private readonly bool _ownsResources;

    private OrderTenantDbSession(NpgsqlConnection connection, NpgsqlTransaction transaction, bool ownsResources = true)
    {
        _connection = connection;
        _transaction = transaction;
        _ownsResources = ownsResources;
    }

    internal static async Task<OrderTenantDbSession> BorrowAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || connection.State != ConnectionState.Open || transaction.Connection != connection)
            throw new InvalidOperationException("An active caller-owned transaction is required.");
        await using var command = new NpgsqlCommand(OrderSql.SetTenant, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return new(connection, transaction, ownsResources: false);
    }

    // Commands keep ReadCommitted: they rely on row-level conflict handling and atomic receipts.
    // A multi-statement read that must see one consistent state opens a snapshot session instead.
    internal static Task<OrderTenantDbSession> OpenAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        OpenAsync(dataSource, tenantId, IsolationLevel.ReadCommitted, cancellationToken);

    internal static Task<OrderTenantDbSession> OpenSnapshotAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        OpenAsync(dataSource, tenantId, IsolationLevel.RepeatableRead, cancellationToken);

    private static async Task<OrderTenantDbSession> OpenAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = await connection
                .BeginTransactionAsync(isolationLevel, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                await using var command = new NpgsqlCommand(OrderSql.SetTenant, connection, transaction);
                command.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return new OrderTenantDbSession(connection, transaction);
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

    internal NpgsqlConnection Connection => _connection;
    internal NpgsqlTransaction Transaction => _transaction;

    internal NpgsqlCommand CreateCommand(string sql)
    {
        if (_completed)
        {
            throw new InvalidOperationException("The Orders tenant database session has completed.");
        }

        return new NpgsqlCommand(sql, _connection, _transaction);
    }


    internal NpgsqlBatch CreateBatch()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The Orders tenant database session has completed.");
        }

        return new NpgsqlBatch(_connection, _transaction);
    }

    internal async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (!_ownsResources) throw new InvalidOperationException("Only the caller may complete a borrowed transaction.");
        if (_completed)
        {
            throw new InvalidOperationException("The Orders tenant database session has completed.");
        }

        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    internal async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (!_ownsResources) throw new InvalidOperationException("Only the caller may complete a borrowed transaction.");
        if (_completed)
        {
            throw new InvalidOperationException("The Orders tenant database session has completed.");
        }

        await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        _completed = true;
        if (!_ownsResources) return;
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
