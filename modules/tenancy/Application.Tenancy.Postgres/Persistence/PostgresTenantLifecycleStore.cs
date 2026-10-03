using System.Data;
using Npgsql;

namespace Application.Tenancy.Postgres;

public sealed class PostgresTenantLifecycleStore(NpgsqlDataSource dataSource) : ITenantLifecycleStore
{
    public async Task<TenantLifecycleResult> ExecuteAsync(
        TenantMembershipAdministrationActor actor,
        TenantLifecycleIntent intent,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        var storedAt = occurredAt.ToUniversalTime().AddTicks(-(occurredAt.Ticks % 10));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        await using (var idempotencyLock = new NpgsqlCommand(
                         TenantLifecycleSql.AcquireLifecycleLock, connection, transaction))
        {
            idempotencyLock.Parameters.AddWithValue(
                "scope",
                $"tenant-lifecycle:{actor.PrincipalId:N}:{intent.IdempotencyKey}");
            _ = await idempotencyLock.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }

        var receipt = await FindReceiptAsync(
            connection, transaction, actor.PrincipalId, intent.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return string.Equals(receipt.Value.Fingerprint, intent.Fingerprint, StringComparison.Ordinal)
                ? receipt.Value.Result with { Replayed = true }
                : new TenantLifecycleResult(TenantLifecycleStatus.IdempotencyKeyConflict, null);
        }

        TenantLifecycleSnapshot? current;
        await using (var find = new NpgsqlCommand(
                         TenantLifecycleSql.FindTenantForUpdate, connection, transaction))
        {
            find.Parameters.AddWithValue("tenant_id", intent.TenantId);
            await using var reader = await find.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            current = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? new TenantLifecycleSnapshot(
                    reader.GetGuid(0),
                    (TenantAvailability)reader.GetInt16(1),
                    reader.GetInt32(2),
                    reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3))
                : null;
        }

        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new TenantLifecycleResult(TenantLifecycleStatus.TenantNotFound, null);
        }
        if (current.Revision != intent.ExpectedRevision)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new TenantLifecycleResult(TenantLifecycleStatus.RevisionConflict, current);
        }
        if (current.Revision == int.MaxValue)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new TenantLifecycleResult(TenantLifecycleStatus.RevisionLimitReached, current);
        }

        var valid = intent.Operation switch
        {
            TenantLifecycleOperation.Suspend => current.Availability == TenantAvailability.Active,
            TenantLifecycleOperation.Reactivate => current.Availability == TenantAvailability.Suspended,
            _ => false,
        };
        if (!valid)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new TenantLifecycleResult(TenantLifecycleStatus.InvalidTransition, current);
        }

        var availability = intent.Operation == TenantLifecycleOperation.Suspend
            ? TenantAvailability.Suspended
            : TenantAvailability.Active;
        var updated = new TenantLifecycleSnapshot(
            intent.TenantId,
            availability,
            current.Revision + 1,
            availability == TenantAvailability.Suspended ? storedAt : null);
        await using (var update = new NpgsqlCommand(
                         TenantLifecycleSql.UpdateTenantLifecycle, connection, transaction))
        {
            update.Parameters.AddWithValue("tenant_id", updated.TenantId);
            update.Parameters.AddWithValue("availability", (short)updated.Availability);
            update.Parameters.AddWithValue("revision", updated.Revision);
            update.Parameters.AddWithValue("suspended_at", (object?)updated.SuspendedAt ?? DBNull.Value);
            _ = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var insert = new NpgsqlCommand(
                         TenantLifecycleSql.InsertTenantLifecycleReceipt, connection, transaction))
        {
            insert.Parameters.AddWithValue("principal_id", actor.PrincipalId);
            insert.Parameters.AddWithValue("key", intent.IdempotencyKey);
            insert.Parameters.AddWithValue("fingerprint", intent.Fingerprint);
            insert.Parameters.AddWithValue("operation", (short)intent.Operation);
            insert.Parameters.AddWithValue("tenant_id", updated.TenantId);
            insert.Parameters.AddWithValue("availability", (short)updated.Availability);
            insert.Parameters.AddWithValue("revision", updated.Revision);
            insert.Parameters.AddWithValue("suspended_at", (object?)updated.SuspendedAt ?? DBNull.Value);
            insert.Parameters.AddWithValue("device_id", actor.DeviceId);
            insert.Parameters.AddWithValue("occurred_at", storedAt);
            _ = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new TenantLifecycleResult(
            intent.Operation == TenantLifecycleOperation.Suspend
                ? TenantLifecycleStatus.Suspended
                : TenantLifecycleStatus.Reactivated,
            updated);
    }

    private static async Task<(string Fingerprint, TenantLifecycleResult Result)?> FindReceiptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid principalId,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            TenantLifecycleSql.FindTenantLifecycleReceipt, connection, transaction);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var operation = (TenantLifecycleOperation)reader.GetInt16(1);
        var snapshot = new TenantLifecycleSnapshot(
            reader.GetGuid(2),
            (TenantAvailability)reader.GetInt16(3),
            reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5));
        return (
            reader.GetString(0),
            new TenantLifecycleResult(
                operation == TenantLifecycleOperation.Suspend
                    ? TenantLifecycleStatus.Suspended
                    : TenantLifecycleStatus.Reactivated,
                snapshot));
    }
}
