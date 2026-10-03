using System.Data;
using Npgsql;

namespace Application.IdentityAccess.Postgres;

public sealed class PostgresAccountOnboardingStore(NpgsqlDataSource dataSource) : IAccountOnboardingStore
{
    public async Task<AccountOnboardingReceiptResult> FindOnboardingReceiptAsync(
        IdentityAdministrationActor actor,
        AccountOnboardingIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var receipt = await FindAccountReceiptAsync(
                connection,
                transaction: null,
                actor.PrincipalId,
                intent.IdempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (receipt is null)
        {
            return new AccountOnboardingReceiptResult(
                IdentityAdministrationReceiptStatus.Missing,
                null);
        }

        return string.Equals(receipt.Value.Fingerprint, intent.Fingerprint, StringComparison.Ordinal)
            ? new AccountOnboardingReceiptResult(
                IdentityAdministrationReceiptStatus.Replayed,
                receipt.Value.Snapshot)
            : new AccountOnboardingReceiptResult(
                IdentityAdministrationReceiptStatus.IdempotencyKeyConflict,
                null);
    }

    public async Task<ExternalIdentityLinkReceiptResult> FindLinkReceiptAsync(
        IdentityAdministrationActor actor,
        ExternalIdentityLinkIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var receipt = await FindLinkReceiptAsync(
                connection,
                transaction: null,
                actor.PrincipalId,
                intent.IdempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (receipt is null)
        {
            return new ExternalIdentityLinkReceiptResult(
                IdentityAdministrationReceiptStatus.Missing,
                null);
        }

        return string.Equals(receipt.Value.Fingerprint, intent.Fingerprint, StringComparison.Ordinal)
            ? new ExternalIdentityLinkReceiptResult(
                IdentityAdministrationReceiptStatus.Replayed,
                receipt.Value.Snapshot)
            : new ExternalIdentityLinkReceiptResult(
                IdentityAdministrationReceiptStatus.IdempotencyKeyConflict,
                null);
    }

    public async Task<OnboardAccountResult> OnboardAsync(
        IdentityAdministrationActor actor,
        AccountOnboardingIntent intent,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        var storedAt = StorageTimestamp(createdAt);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await AcquireIdempotencyLockAsync(connection, transaction, "account", actor.PrincipalId, intent.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        var receipt = await FindAccountReceiptAsync(connection, transaction, actor.PrincipalId, intent.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return string.Equals(receipt.Value.Fingerprint, intent.Fingerprint, StringComparison.Ordinal)
                ? new OnboardAccountResult(OnboardAccountStatus.Replayed, receipt.Value.Snapshot)
                : new OnboardAccountResult(OnboardAccountStatus.IdempotencyKeyConflict, null);
        }

        if (await FindBoundAccountIdAsync(connection, transaction, intent.Identity, cancellationToken).ConfigureAwait(false) is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new OnboardAccountResult(OnboardAccountStatus.IdentityAlreadyBound, null);
        }

        var accountId = Guid.CreateVersion7();
        try
        {
            await using (var account = new NpgsqlCommand(IdentityAccessSql.InsertAccount, connection, transaction))
            {
                account.Parameters.AddWithValue("id", accountId);
                account.Parameters.AddWithValue("availability", (short)AccountAvailability.Active);
                account.Parameters.AddWithValue("created_at", storedAt);
                _ = await account.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var binding = new NpgsqlCommand(IdentityAccessSql.InsertBinding, connection, transaction))
            {
                binding.Parameters.AddWithValue("issuer", intent.Identity.Issuer);
                binding.Parameters.AddWithValue("subject", intent.Identity.Subject);
                binding.Parameters.AddWithValue("account_id", accountId);
                binding.Parameters.AddWithValue("created_at", storedAt);
                _ = await binding.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var receiptInsert = new NpgsqlCommand(IdentityAccessSql.InsertAccountReceipt, connection, transaction))
            {
                receiptInsert.Parameters.AddWithValue("principal_id", actor.PrincipalId);
                receiptInsert.Parameters.AddWithValue("idempotency_key", intent.IdempotencyKey);
                receiptInsert.Parameters.AddWithValue("fingerprint", intent.Fingerprint);
                receiptInsert.Parameters.AddWithValue("account_id", accountId);
                receiptInsert.Parameters.AddWithValue("issuer", intent.Identity.Issuer);
                receiptInsert.Parameters.AddWithValue("subject", intent.Identity.Subject);
                receiptInsert.Parameters.AddWithValue("device_id", actor.DeviceId);
                receiptInsert.Parameters.AddWithValue("created_at", storedAt);
                _ = await receiptInsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new OnboardAccountResult(
                OnboardAccountStatus.Created,
                new AccountOnboardingSnapshot(
                    accountId,
                    intent.Identity,
                    AccountAvailability.Active,
                    actor.PrincipalId,
                    actor.DeviceId,
                    storedAt));
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == "pk_external_identity_bindings")
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new OnboardAccountResult(OnboardAccountStatus.IdentityAlreadyBound, null);
        }
    }

    public async Task<LinkExternalIdentityResult> LinkAsync(
        IdentityAdministrationActor actor,
        ExternalIdentityLinkIntent intent,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        var storedAt = StorageTimestamp(linkedAt);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await AcquireIdempotencyLockAsync(connection, transaction, "link", actor.PrincipalId, intent.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        var receipt = await FindLinkReceiptAsync(connection, transaction, actor.PrincipalId, intent.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return string.Equals(receipt.Value.Fingerprint, intent.Fingerprint, StringComparison.Ordinal)
                ? new LinkExternalIdentityResult(LinkExternalIdentityStatus.Replayed, receipt.Value.Snapshot)
                : new LinkExternalIdentityResult(LinkExternalIdentityStatus.IdempotencyKeyConflict, null);
        }

        var availability = await LockAccountAsync(connection, transaction, intent.AccountId, cancellationToken)
            .ConfigureAwait(false);
        if (availability is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new LinkExternalIdentityResult(LinkExternalIdentityStatus.AccountNotFound, null);
        }

        if (availability != AccountAvailability.Active)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new LinkExternalIdentityResult(LinkExternalIdentityStatus.AccountDisabled, null);
        }

        var boundAccount = await FindBoundAccountIdAsync(connection, transaction, intent.Identity, cancellationToken)
            .ConfigureAwait(false);
        if (boundAccount is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new LinkExternalIdentityResult(
                boundAccount == intent.AccountId
                    ? LinkExternalIdentityStatus.IdentityAlreadyLinked
                    : LinkExternalIdentityStatus.IdentityBoundElsewhere,
                null);
        }

        try
        {
            await using (var binding = new NpgsqlCommand(IdentityAccessSql.InsertBinding, connection, transaction))
            {
                binding.Parameters.AddWithValue("issuer", intent.Identity.Issuer);
                binding.Parameters.AddWithValue("subject", intent.Identity.Subject);
                binding.Parameters.AddWithValue("account_id", intent.AccountId);
                binding.Parameters.AddWithValue("created_at", storedAt);
                _ = await binding.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var receiptInsert = new NpgsqlCommand(IdentityAccessSql.InsertLinkReceipt, connection, transaction))
            {
                receiptInsert.Parameters.AddWithValue("principal_id", actor.PrincipalId);
                receiptInsert.Parameters.AddWithValue("idempotency_key", intent.IdempotencyKey);
                receiptInsert.Parameters.AddWithValue("fingerprint", intent.Fingerprint);
                receiptInsert.Parameters.AddWithValue("account_id", intent.AccountId);
                receiptInsert.Parameters.AddWithValue("issuer", intent.Identity.Issuer);
                receiptInsert.Parameters.AddWithValue("subject", intent.Identity.Subject);
                receiptInsert.Parameters.AddWithValue("device_id", actor.DeviceId);
                receiptInsert.Parameters.AddWithValue("linked_at", storedAt);
                _ = await receiptInsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new LinkExternalIdentityResult(
                LinkExternalIdentityStatus.Linked,
                new ExternalIdentityLinkSnapshot(
                    intent.AccountId,
                    intent.Identity,
                    actor.PrincipalId,
                    actor.DeviceId,
                    storedAt));
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == "pk_external_identity_bindings")
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            await using var lookupConnection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            var existing = await FindBoundAccountIdAsync(lookupConnection, null, intent.Identity, cancellationToken)
                .ConfigureAwait(false);
            return new LinkExternalIdentityResult(
                existing == intent.AccountId
                    ? LinkExternalIdentityStatus.IdentityAlreadyLinked
                    : LinkExternalIdentityStatus.IdentityBoundElsewhere,
                null);
        }
    }

    private static async Task AcquireIdempotencyLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string operation,
        Guid principalId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(IdentityAccessSql.LockIdempotencyKey, connection, transaction);
        command.Parameters.AddWithValue("scope", $"identity:{operation}:{principalId:N}:{idempotencyKey}");
        _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Guid?> FindBoundAccountIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        ExternalIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(IdentityAccessSql.FindBinding, connection, transaction);
        command.Parameters.AddWithValue("issuer", identity.Issuer);
        command.Parameters.AddWithValue("subject", identity.Subject);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is Guid accountId ? accountId : null;
    }

    private static async Task<AccountAvailability?> LockAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(IdentityAccessSql.LockAccount, connection, transaction);
        command.Parameters.AddWithValue("account_id", accountId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is short raw ? (AccountAvailability)raw : null;
    }

    private static async Task<(string Fingerprint, AccountOnboardingSnapshot Snapshot)?> FindAccountReceiptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid principalId,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(IdentityAccessSql.FindAccountReceipt, connection, transaction);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return (
            reader.GetString(0),
            new AccountOnboardingSnapshot(
                reader.GetGuid(1),
                ExternalIdentity.Create(reader.GetString(2), reader.GetString(3)),
                AccountAvailability.Active,
                reader.GetGuid(4),
                reader.GetGuid(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
    }

    private static async Task<(string Fingerprint, ExternalIdentityLinkSnapshot Snapshot)?> FindLinkReceiptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid principalId,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(IdentityAccessSql.FindLinkReceipt, connection, transaction);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return (
            reader.GetString(0),
            new ExternalIdentityLinkSnapshot(
                reader.GetGuid(1),
                ExternalIdentity.Create(reader.GetString(2), reader.GetString(3)),
                reader.GetGuid(4),
                reader.GetGuid(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
    }

    private static DateTimeOffset StorageTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().AddTicks(-(value.Ticks % 10));
}
