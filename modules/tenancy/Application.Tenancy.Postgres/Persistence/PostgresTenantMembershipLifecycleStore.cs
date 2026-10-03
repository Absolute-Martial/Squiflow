using System.Data;
using Npgsql;

namespace Application.Tenancy.Postgres;

public sealed class PostgresTenantMembershipLifecycleStore(NpgsqlDataSource dataSource)
    : ITenantMembershipLifecycleStore
{
    private const short ActiveAccountAvailability = 1;

    public async Task<TenantMembershipLifecycleResult> ExecuteAsync(
        TenantMembershipAdministrationActor actor,
        TenantMembershipLifecycleIntent intent,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        var storedAt = StorageTimestamp(occurredAt);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await AcquireIdempotencyLockAsync(connection, transaction, actor.PrincipalId, intent.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        var receipt = await FindReceiptAsync(connection, transaction, actor.PrincipalId, intent.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return string.Equals(receipt.Value.Fingerprint, intent.Fingerprint, StringComparison.Ordinal)
                ? receipt.Value.Result with { Replayed = true }
                : new TenantMembershipLifecycleResult(MembershipLifecycleStatus.IdempotencyKeyConflict, null);
        }

        await AcquireMembershipLockAsync(connection, transaction, intent, cancellationToken).ConfigureAwait(false);

        var result = intent.Operation is MembershipLifecycleOperation.Invite or MembershipLifecycleOperation.BootstrapOwner
            ? await CreateMembershipAsync(connection, transaction, intent, storedAt, cancellationToken).ConfigureAwait(false)
            : await TransitionAsync(connection, transaction, intent, storedAt, cancellationToken).ConfigureAwait(false);

        if (result.Status is not (MembershipLifecycleStatus.Invited or MembershipLifecycleStatus.Activated or
            MembershipLifecycleStatus.Suspended or MembershipLifecycleStatus.Removed or
            MembershipLifecycleStatus.OwnerBootstrapped))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }

        await InsertReceiptAsync(connection, transaction, actor, intent, result, storedAt, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static async Task<TenantMembershipLifecycleResult> CreateMembershipAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantMembershipLifecycleIntent intent,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using (var tenant = new NpgsqlCommand(
                         MembershipLifecycleSql.TenantAvailability,
                         connection, transaction))
        {
            tenant.Parameters.AddWithValue("tenant_id", intent.TenantId);
            var raw = await tenant.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (raw is not short availability || (TenantAvailability)availability != TenantAvailability.Active)
                return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.TenantNotFound, null);
        }

        await using (var account = new NpgsqlCommand(
                         MembershipLifecycleSql.AccountAvailability,
                         connection, transaction))
        {
            account.Parameters.AddWithValue("account_id", intent.AccountId);
            var raw = await account.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (raw is not short availability)
                return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.AccountNotFound, null);
            if (availability != ActiveAccountAvailability)
                return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.AccountUnavailable, null);
        }

        var isInitialOwner = intent.Operation == MembershipLifecycleOperation.BootstrapOwner;
        if (isInitialOwner)
        {
            await using var owner = new NpgsqlCommand(
                MembershipLifecycleSql.InitialOwnerExists,
                connection, transaction);
            owner.Parameters.AddWithValue("tenant_id", intent.TenantId);
            if (await owner.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true)
                return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.InitialOwnerAlreadyExists, null);
        }

        var existing = await FindMembershipAsync(connection, transaction, intent.TenantId, intent.AccountId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
            return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.MembershipAlreadyExists, existing);

        await using var insert = new NpgsqlCommand(
            MembershipLifecycleSql.InsertMembership,
            connection, transaction);
        insert.Parameters.AddWithValue("tenant_id", intent.TenantId);
        insert.Parameters.AddWithValue("account_id", intent.AccountId);
        insert.Parameters.AddWithValue(
            "availability",
            (short)(isInitialOwner ? MembershipAvailability.Active : MembershipAvailability.Invited));
        insert.Parameters.AddWithValue("created_at", occurredAt);
        insert.Parameters.AddWithValue("activated_at", isInitialOwner ? occurredAt : DBNull.Value);
        insert.Parameters.AddWithValue("is_initial_owner", isInitialOwner);
        await transaction.SaveAsync("membership_invite", cancellationToken).ConfigureAwait(false);
        try
        {
            _ = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation &&
            exception.ConstraintName == "fk_memberships_accounts_account_id")
        {
            await transaction.RollbackAsync("membership_invite", cancellationToken).ConfigureAwait(false);
            return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.AccountNotFound, null);
        }

        return new TenantMembershipLifecycleResult(
            isInitialOwner ? MembershipLifecycleStatus.OwnerBootstrapped : MembershipLifecycleStatus.Invited,
            new TenantMembershipLifecycleSnapshot(
                intent.TenantId,
                intent.AccountId,
                isInitialOwner ? MembershipAvailability.Active : MembershipAvailability.Invited,
                1,
                occurredAt,
                isInitialOwner ? occurredAt : null,
                null,
                null,
                isInitialOwner));
    }

    private static async Task<TenantMembershipLifecycleResult> TransitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantMembershipLifecycleIntent intent,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (intent.Operation == MembershipLifecycleOperation.Activate)
        {
            await using var tenant = new NpgsqlCommand(
                MembershipLifecycleSql.TenantAvailability, connection, transaction);
            tenant.Parameters.AddWithValue("tenant_id", intent.TenantId);
            var rawTenant = await tenant.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (rawTenant is not short tenantAvailability ||
                (TenantAvailability)tenantAvailability != TenantAvailability.Active)
                return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.TenantNotFound, null);
        }

        var current = await FindMembershipAsync(connection, transaction, intent.TenantId, intent.AccountId, cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
            return new TenantMembershipLifecycleResult(MembershipLifecycleStatus.MembershipNotFound, null);
        var result = current.Transition(intent, occurredAt);
        if (result.Status is MembershipLifecycleStatus.RevisionConflict or
            MembershipLifecycleStatus.InvalidTransition or MembershipLifecycleStatus.RevisionLimitReached)
            return result;
        var updated = result.Membership!;

        await using var update = new NpgsqlCommand(
            MembershipLifecycleSql.UpdateMembership,
            connection, transaction);
        update.Parameters.AddWithValue("availability", (short)updated.Availability);
        update.Parameters.AddWithValue("revision", updated.Revision);
        update.Parameters.AddWithValue("activated_at", (object?)updated.ActivatedAt ?? DBNull.Value);
        update.Parameters.AddWithValue("suspended_at", (object?)updated.SuspendedAt ?? DBNull.Value);
        update.Parameters.AddWithValue("removed_at", (object?)updated.RemovedAt ?? DBNull.Value);
        update.Parameters.AddWithValue("tenant_id", intent.TenantId);
        update.Parameters.AddWithValue("account_id", intent.AccountId);
        _ = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static async Task<TenantMembershipLifecycleSnapshot?> FindMembershipAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var sql = MembershipLifecycleSql.FindMembership;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return new TenantMembershipLifecycleSnapshot(
            reader.GetGuid(0), reader.GetGuid(1), (MembershipAvailability)reader.GetInt16(2),
            reader.GetInt32(3), reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetBoolean(8));
    }

    private static async Task InsertReceiptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantMembershipAdministrationActor actor,
        TenantMembershipLifecycleIntent intent,
        TenantMembershipLifecycleResult result,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var membership = result.Membership;
        await using var command = new NpgsqlCommand(
            MembershipLifecycleSql.InsertMembershipReceipt,
            connection, transaction);
        command.Parameters.AddWithValue("principal_id", actor.PrincipalId);
        command.Parameters.AddWithValue("key", intent.IdempotencyKey);
        command.Parameters.AddWithValue("fingerprint", intent.Fingerprint);
        command.Parameters.AddWithValue("operation", (short)intent.Operation);
        command.Parameters.AddWithValue("tenant_id", intent.TenantId);
        command.Parameters.AddWithValue("account_id", intent.AccountId);
        command.Parameters.AddWithValue("status", (short)result.Status);
        command.Parameters.AddWithValue("availability", membership is null ? DBNull.Value : (object)(short)membership.Availability);
        command.Parameters.AddWithValue("revision", (object?)membership?.Revision ?? DBNull.Value);
        command.Parameters.AddWithValue("invited_at", (object?)membership?.InvitedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("activated_at", (object?)membership?.ActivatedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("suspended_at", (object?)membership?.SuspendedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("removed_at", (object?)membership?.RemovedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("is_initial_owner", membership?.IsInitialOwner ?? false);
        command.Parameters.AddWithValue("device_id", actor.DeviceId);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(string Fingerprint, TenantMembershipLifecycleResult Result)?> FindReceiptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid principalId,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            MembershipLifecycleSql.FindMembershipReceipt,
            connection, transaction);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        TenantMembershipLifecycleSnapshot? snapshot = null;
        if (!reader.IsDBNull(4))
        {
            snapshot = new TenantMembershipLifecycleSnapshot(
                reader.GetGuid(2), reader.GetGuid(3), (MembershipAvailability)reader.GetInt16(4),
                reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
                reader.GetBoolean(10));
        }
        return (reader.GetString(0),
            new TenantMembershipLifecycleResult((MembershipLifecycleStatus)reader.GetInt16(1), snapshot));
    }

    private static async Task AcquireIdempotencyLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid principalId,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            MembershipLifecycleSql.AcquireLifecycleLock,
            connection, transaction);
        command.Parameters.AddWithValue("scope", $"membership:{principalId:N}:{key}");
        _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task AcquireMembershipLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantMembershipLifecycleIntent intent,
        CancellationToken cancellationToken)
    {
        if (intent.Operation == MembershipLifecycleOperation.BootstrapOwner)
        {
            await using var owner = new NpgsqlCommand(
                MembershipLifecycleSql.AcquireLifecycleLock, connection, transaction);
            owner.Parameters.AddWithValue("scope", $"membership-initial-owner:{intent.TenantId:N}");
            _ = await owner.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
        await using var membership = new NpgsqlCommand(
            MembershipLifecycleSql.AcquireLifecycleLock, connection, transaction);
        membership.Parameters.AddWithValue(
            "scope",
            $"membership-resource:{intent.TenantId:N}:{intent.AccountId:N}");
        _ = await membership.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset StorageTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().AddTicks(-(value.Ticks % 10));
}
