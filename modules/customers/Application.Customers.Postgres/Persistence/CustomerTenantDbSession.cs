using System.Data;
using Npgsql;

namespace Application.Customers.Postgres;

internal sealed class CustomerTenantDbSession : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private bool _completed;

    private CustomerTenantDbSession(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    internal static async Task<CustomerTenantDbSession> OpenAsync(
        NpgsqlDataSource dataSource, Guid tenantId, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                await using var setTenant = new NpgsqlCommand(
                    "SELECT set_config('app.current_tenant', @tenant_id, true)", connection, transaction);
                setTenant.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
                await setTenant.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return new CustomerTenantDbSession(connection, transaction);
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
            throw new InvalidOperationException("The Customers tenant database session has completed.");
        }

        return new NpgsqlCommand(sql, _connection, _transaction);
    }

    internal async Task CommitAsync(CancellationToken cancellationToken)
    {
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    internal async Task RollbackAsync(CancellationToken cancellationToken)
    {
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
