using System.Data;
using Npgsql;

namespace Application.Orders.Postgres;

// A session is ready for Orders commands only after the transaction-local RLS context is set.
internal sealed class OrderTenantDbSession : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private bool _completed;

    private OrderTenantDbSession(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
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

    internal NpgsqlCommand CreateCommand(string sql)
    {
        if (_completed)
        {
            throw new InvalidOperationException("The Orders tenant database session has completed.");
        }

        return new NpgsqlCommand(sql, _connection, _transaction);
    }

    internal async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_completed)
        {
            throw new InvalidOperationException("The Orders tenant database session has completed.");
        }

        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    internal async Task RollbackAsync(CancellationToken cancellationToken)
    {
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
