using System.Data;
using Npgsql;

namespace Application.Tenancy.Postgres;

public sealed class PostgresTenantProvisioningStore(NpgsqlDataSource dataSource)
    : ITenantProvisioningStore
{
    public async Task<ProvisionTenantResult> ProvisionAsync(
        TenantProvisioningActor actor,
        TenantProvisioningIntent intent,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        var receipt = await FindReceiptAsync(
            connection, transaction, actor.PrincipalId, intent.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Replay(receipt.Value, intent.Fingerprint);
        }

        var tenant = new TenantProvisioningSnapshot(
            Guid.CreateVersion7(),
            intent.DisplayName,
            TenantAvailability.Active,
            actor.PrincipalId,
            actor.DeviceId,
            StorageTimestamp(activatedAt));

        await using (var insertTenant = new NpgsqlCommand(
                         TenantProvisioningSql.InsertTenant, connection, transaction))
        {
            insertTenant.Parameters.AddWithValue("tenant_id", tenant.TenantId);
            insertTenant.Parameters.AddWithValue("display_name", tenant.DisplayName);
            insertTenant.Parameters.AddWithValue("availability", (short)tenant.Availability);
            insertTenant.Parameters.AddWithValue("activated_at", tenant.ActivatedAt);
            _ = await insertTenant.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var receiptInserted = false;
        await using (var insertReceipt = new NpgsqlCommand(
                         TenantProvisioningSql.InsertReceipt, connection, transaction))
        {
            insertReceipt.Parameters.AddWithValue("principal_id", actor.PrincipalId);
            insertReceipt.Parameters.AddWithValue("idempotency_key", intent.IdempotencyKey);
            insertReceipt.Parameters.AddWithValue("fingerprint", intent.Fingerprint);
            insertReceipt.Parameters.AddWithValue("tenant_id", tenant.TenantId);
            insertReceipt.Parameters.AddWithValue("display_name", tenant.DisplayName);
            insertReceipt.Parameters.AddWithValue("device_id", actor.DeviceId);
            insertReceipt.Parameters.AddWithValue("activated_at", tenant.ActivatedAt);
            receiptInserted = await insertReceipt.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
        }

        if (receiptInserted)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ProvisionTenantResult(ProvisionTenantStatus.Created, tenant);
        }

        receipt = await FindReceiptAsync(
            connection, transaction, actor.PrincipalId, intent.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The tenant provisioning receipt disappeared after an idempotency conflict.");
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return Replay(receipt.Value, intent.Fingerprint);
    }

    private static async Task<(string Fingerprint, TenantProvisioningSnapshot Tenant)?> FindReceiptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid principalId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(TenantProvisioningSql.FindReceipt, connection, transaction);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (
            reader.GetString(reader.GetOrdinal("request_fingerprint")),
            new TenantProvisioningSnapshot(
                reader.GetGuid(reader.GetOrdinal("tenant_id")),
                reader.GetString(reader.GetOrdinal("display_name")),
                TenantAvailability.Active,
                reader.GetGuid(reader.GetOrdinal("provisioned_by_principal_id")),
                reader.GetGuid(reader.GetOrdinal("provisioned_by_device_id")),
                reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("activated_at"))));
    }

    private static DateTimeOffset StorageTimestamp(DateTimeOffset timestamp)
    {
        var utc = timestamp.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }

    private static ProvisionTenantResult Replay(
        (string Fingerprint, TenantProvisioningSnapshot Tenant) receipt,
        string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new ProvisionTenantResult(ProvisionTenantStatus.Replayed, receipt.Tenant)
            : new ProvisionTenantResult(ProvisionTenantStatus.IdempotencyKeyConflict, null);
}
