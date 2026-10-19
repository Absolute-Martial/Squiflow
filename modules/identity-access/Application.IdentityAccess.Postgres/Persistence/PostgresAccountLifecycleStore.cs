using Npgsql;
using NpgsqlTypes;

namespace Application.IdentityAccess.Postgres;

public sealed class PostgresAccountLifecycleStore(NpgsqlDataSource dataSource) : IAccountLifecycleStore
{
    public async Task<AccountLifecycleResult> ExecuteAsync(IdentityAdministrationActor actor,
        AccountLifecycleIntent intent, DateTimeOffset changedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        var storedAt = changedAt.ToUniversalTime().AddTicks(-(changedAt.Ticks % 10));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var keyLock = new NpgsqlCommand(IdentityAccessSql.LockIdempotencyKey, connection, transaction))
        {
            keyLock.Parameters.AddWithValue("scope", $"account-lifecycle:{actor.PrincipalId:N}:{intent.IdempotencyKey}");
            _ = await keyLock.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var receipt = new NpgsqlCommand(IdentityAccessSql.FindAccountLifecycleReceipt, connection, transaction))
        {
            receipt.Parameters.AddWithValue("principal", actor.PrincipalId);
            receipt.Parameters.AddWithValue("key", intent.IdempotencyKey);
            await using var reader = await receipt.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!string.Equals(reader.GetString(0), intent.Fingerprint, StringComparison.Ordinal))
                    return new(AccountLifecycleStatus.IdempotencyKeyConflict, null);
                var snapshot = new AccountLifecycleSnapshot(reader.GetGuid(1), (AccountAvailability)reader.GetInt16(2),
                    reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                    reader.GetGuid(5), reader.GetGuid(6), reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8));
                return new(AccountLifecycleStatus.Changed, snapshot, Replayed: true);
            }
        }
        // The existing identity-link function locks this same account row, serializing linking and suspension.
        await using (var accountLock = new NpgsqlCommand(IdentityAccessSql.LockAccount, connection, transaction))
        {
            accountLock.Parameters.AddWithValue("account_id", intent.AccountId);
            if (await accountLock.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not short)
                return new(AccountLifecycleStatus.AccountNotFound, null);
        }
        await using (var current = new NpgsqlCommand(
                         IdentityAccessSql.FindAccountLifecycleState, connection, transaction))
        {
            current.Parameters.AddWithValue("account", intent.AccountId);
            await using var reader = await current.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("The locked account disappeared.");
            var decision = intent.Evaluate((AccountAvailability)reader.GetInt16(0), reader.GetInt32(1));
            if (decision != AccountLifecycleStatus.Changed) return new(decision, null);
        }
        var disabledAt = intent.TargetAvailability == AccountAvailability.Disabled ? storedAt : (DateTimeOffset?)null;
        await using (var transition = new NpgsqlCommand(
                         IdentityAccessSql.TransitionAccountAvailability, connection, transaction))
        {
            transition.Parameters.AddWithValue("account", intent.AccountId);
            transition.Parameters.AddWithValue("expected", intent.ExpectedRevision);
            transition.Parameters.AddWithValue("availability", (short)intent.TargetAvailability);
            transition.Parameters.AddWithValue("changed_at", storedAt);
            if (await transition.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not int revision ||
                revision != intent.ExpectedRevision + 1)
                throw new InvalidOperationException("The locked account transition was not applied.");
        }
        await using var command = new NpgsqlCommand(IdentityAccessSql.InsertAccountLifecycleReceipt, connection, transaction);
        command.Parameters.AddWithValue("availability", (short)intent.TargetAvailability);
        command.Parameters.AddWithValue("revision", intent.ExpectedRevision + 1);
        command.Parameters.AddWithValue("account", intent.AccountId);
        command.Parameters.Add(new NpgsqlParameter("disabled_at", NpgsqlDbType.TimestampTz) { Value = (object?)disabledAt ?? DBNull.Value });
        command.Parameters.AddWithValue("principal", actor.PrincipalId);
        command.Parameters.AddWithValue("key", intent.IdempotencyKey);
        command.Parameters.AddWithValue("fingerprint", intent.Fingerprint);
        command.Parameters.AddWithValue("device", actor.DeviceId);
        command.Parameters.AddWithValue("reason", intent.Reason);
        command.Parameters.AddWithValue("changed_at", storedAt);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(AccountLifecycleStatus.Changed, new AccountLifecycleSnapshot(intent.AccountId, intent.TargetAvailability,
            intent.ExpectedRevision + 1, disabledAt, actor.PrincipalId, actor.DeviceId, intent.Reason, storedAt));
    }
}
