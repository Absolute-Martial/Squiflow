using Application.Customers;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Customers.Postgres;

public sealed partial class PostgresCustomerStore
{
    public async Task<CustomerImportSourceLease> BeginSourceAsync(TenantContext context, CustomerImportPlan plan,
        string idempotencyKey, CustomerImportRetention retention, CustomerImportSourcePolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(policy);
        if (plan.ImportId == Guid.Empty || plan.ManifestHash.Length != 64 || plan.ManifestHash.Any(ch => !char.IsAsciiHexDigitLower(ch))
            || plan.ByteLength is < 0 or > CustomerImportCsv.MaxBytes || !Enum.IsDefined(retention))
            throw new CustomerValidationException("import_plan_invalid", "Import plan source metadata is invalid.");
        var now = CurrentStorageTime();
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindImportReceiptAsync(session, context, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            if (!string.Equals(receipt.Value.Fingerprint, plan.Fingerprint, StringComparison.Ordinal)) throw SourceIntentConflict();
            var existing = await ReadSourceByImportAsync(session, context, receipt.Value.ImportId, cancellationToken).ConfigureAwait(false)
                ?? throw SourceUnavailable();
            if (existing.Retention != retention) throw SourceIntentConflict();
            if (existing.State != CustomerImportSourceState.Available || existing.ProviderScope != policy.ProviderScope)
                throw SourceUnavailable();
            return new(Guid.Empty, existing.ObjectKey, false, existing, receipt.Value.ImportId);
        }

        await using (var ensure = session.CreateCommand(CustomerSql.EnsureImportSourceUsage))
        {
            SourceParameters(ensure, context, policy.ProviderScope);
            ensure.Parameters.AddWithValue("maximum_bytes", policy.MaximumRetainedBytes);
            ensure.Parameters.AddWithValue("now", now);
            await ensure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        // A single lock order serializes allowance decisions and all publication/failure/retirement accounting.
        var usage = await LockUsageAsync(session, context.TenantId, policy.ProviderScope, cancellationToken).ConfigureAwait(false);
        var reservation = await FindReservationAsync(session, context, policy.ProviderScope, idempotencyKey, cancellationToken).ConfigureAwait(false);
        var importId = reservation?.ImportId ?? plan.ImportId;
        var objectKey = reservation?.ObjectKey ?? $"imports/{context.TenantId:D}/{importId:N}/{plan.ManifestHash}.csv";
        var source = await LockSourceAsync(session, context.TenantId, objectKey, cancellationToken).ConfigureAwait(false);
        if (reservation is not null && (reservation.Fingerprint != plan.Fingerprint || reservation.ByteLength != plan.ByteLength))
            throw SourceIntentConflict();
        if (source is not null)
        {
            if (source.Snapshot.Retention != retention) throw SourceIntentConflict();
            if (source.Snapshot.ProviderScope != policy.ProviderScope || source.Snapshot.ByteLength != plan.ByteLength
                || source.Snapshot.Sha256 != plan.ManifestHash) throw SourceIntentConflict();
            if (source.Snapshot.State is CustomerImportSourceState.Retired or CustomerImportSourceState.RetirementPending
                || source.StagingExpiresAt is null || source.StagingExpiresAt <= now) throw SourceUnavailable();
            // A caller cannot adopt an unowned historical upload or another caller's explicit import ID.
            if (reservation is null) throw SourceUnavailable();
        }
        if (reservation?.LeaseExpiresAt > now)
            throw new CustomerValidationException("import_source_busy", "The original import upload is still in progress.");
        if (reservation?.State == 2) throw SourceUnavailable();
        if (reservation is null || reservation.State == 3)
        {
            if (plan.ByteLength > usage.Maximum || usage.Reserved > usage.Maximum - plan.ByteLength
                || usage.Retained > usage.Maximum - plan.ByteLength - usage.Reserved)
                throw new CustomerValidationException("import_storage_capacity", "The retained import-byte allowance is exhausted.");
            await ChangeUsageAsync(session, context, policy.ProviderScope, plan.ByteLength, now,
                CustomerSql.ReserveImportSourceUsage, cancellationToken).ConfigureAwait(false);
        }
        if (source is null)
        {
            await using var insert = session.CreateCommand(CustomerSql.InsertImportSource);
            SourceParameters(insert, context, policy.ProviderScope);
            insert.Parameters.AddWithValue("object_key", objectKey);
            insert.Parameters.AddWithValue("byte_length", plan.ByteLength);
            insert.Parameters.AddWithValue("sha256", plan.ManifestHash);
            insert.Parameters.AddWithValue("content_type", "text/csv; charset=utf-8");
            insert.Parameters.AddWithValue("retention", (int)retention);
            insert.Parameters.AddWithValue("created_at", now);
            insert.Parameters.Add("expires_at", NpgsqlDbType.TimestampTz).Value =
                retention == CustomerImportRetention.DefaultSevenDays ? now.AddDays(7) : DBNull.Value;
            insert.Parameters.AddWithValue("staging_expires_at", now.AddDays(7));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        var reservationId = reservation?.ReservationId ?? Guid.CreateVersion7();
        var leaseId = Guid.CreateVersion7();
        var leaseExpiresAt = now.Add(CustomerImportSourceLease.DefaultLeaseDuration);
        long generation;
        await using (var command = session.CreateCommand(reservation is null ? CustomerSql.InsertImportSourceReservation : CustomerSql.ClaimImportSourceUpload))
        {
            SourceParameters(command, context, policy.ProviderScope);
            command.Parameters.AddWithValue("reservation_id", reservationId);
            command.Parameters.AddWithValue("account_id", context.AccountId);
            command.Parameters.AddWithValue("import_id", importId);
            command.Parameters.AddWithValue("key", idempotencyKey);
            command.Parameters.AddWithValue("fingerprint", plan.Fingerprint);
            command.Parameters.AddWithValue("object_key", objectKey);
            command.Parameters.AddWithValue("byte_length", plan.ByteLength);
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("lease_id", leaseId);
            command.Parameters.AddWithValue("lease_expires_at", leaseExpiresAt);
            if (reservation is null)
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                generation = 1;
            }
            else generation = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? throw SourceUnavailable());
        }
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(reservationId, objectKey, true, source?.Snapshot, importId, generation, leaseId, leaseExpiresAt);
    }

    public async Task<CreateCustomerImportResult> CompleteSourceAsync(TenantContext context, CustomerImportPlan plan,
        string idempotencyKey, CustomerImportRetention retention, CustomerImportSourcePolicy policy, CustomerImportSourceLease lease,
        CancellationToken cancellationToken)
    {
        var now = CurrentStorageTime();
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindImportReceiptAsync(session, context, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            var existing = await ReadSourceByImportAsync(session, context, receipt.Value.ImportId, cancellationToken).ConfigureAwait(false);
            if (receipt.Value.Fingerprint != plan.Fingerprint || existing?.Retention != retention || existing.ObjectKey != lease.ObjectKey)
                return new(plan.ImportId, false, false, true, plan);
            return await ImportReplayAsync(session, context, receipt.Value, plan, cancellationToken).ConfigureAwait(false);
        }
        await LockUsageAsync(session, context.TenantId, policy.ProviderScope, cancellationToken).ConfigureAwait(false);
        var source = await LockSourceAsync(session, context.TenantId, lease.ObjectKey, cancellationToken).ConfigureAwait(false) ?? throw SourceUnavailable();
        var reservation = await FindReservationByIdAsync(session, context.TenantId, lease.ReservationId, cancellationToken).ConfigureAwait(false);
        if (!OwnsUpload(reservation, context, lease, now) || reservation!.Key != idempotencyKey
            || reservation.Fingerprint != plan.Fingerprint || reservation.ImportId != plan.ImportId
            || reservation.ProviderScope != policy.ProviderScope || source.StagingExpiresAt is null || source.StagingExpiresAt <= now
            || source.Snapshot.State is CustomerImportSourceState.Retired or CustomerImportSourceState.RetirementPending)
            throw SourceUnavailable();
        if (source.Snapshot.Retention != retention || source.Snapshot.ByteLength != plan.ByteLength
            || source.Snapshot.Sha256 != plan.ManifestHash || source.Snapshot.ProviderScope != policy.ProviderScope)
            throw SourceIntentConflict();

        var result = await CreateImportPlanAsync(session, context, plan, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (result.IdempotencyKeyConflict) return result; // Disposing rolls back every effect in this transaction.
        await using (var available = session.CreateCommand(CustomerSql.MarkImportSourceAvailable))
        {
            available.Parameters.AddWithValue("tenant_id", context.TenantId);
            available.Parameters.AddWithValue("object_key", lease.ObjectKey);
            if (await available.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1) throw SourceUnavailable();
        }
        await ChangeUsageAsync(session, context, policy.ProviderScope, plan.ByteLength, now, CustomerSql.CommitImportSourceUsage, cancellationToken).ConfigureAwait(false);
        await ChangeReservationAsync(session, context, lease, policy.ProviderScope, now, CustomerSql.CommitImportSourceReservation, cancellationToken).ConfigureAwait(false);
        await using (var reference = session.CreateCommand(CustomerSql.UpdateImportSourceKey))
        {
            ImportParameters(reference, context.TenantId, result.ImportId);
            reference.Parameters.AddWithValue("object_key", lease.ObjectKey);
            if (await reference.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1) throw SourceUnavailable();
        }
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task MarkSourceFailureAsync(TenantContext context, CustomerImportSourceLease lease,
        CustomerImportSourceFailureKind failureKind, string failureCode, CancellationToken cancellationToken)
    {
        if (lease.ReservationId == Guid.Empty) return;
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var reservation = await FindReservationByIdAsync(session, context.TenantId, lease.ReservationId, cancellationToken).ConfigureAwait(false);
        if (reservation is null) return;
        await LockUsageAsync(session, context.TenantId, reservation.ProviderScope, cancellationToken).ConfigureAwait(false);
        var source = await LockSourceAsync(session, context.TenantId, lease.ObjectKey, cancellationToken).ConfigureAwait(false);
        reservation = await FindReservationByIdAsync(session, context.TenantId, lease.ReservationId, cancellationToken).ConfigureAwait(false);
        var now = CurrentStorageTime();
        if (!OwnsUpload(reservation, context, lease, now) || source?.Snapshot.State is null
            or CustomerImportSourceState.Available or CustomerImportSourceState.Retired or CustomerImportSourceState.RetirementPending) return;
        await using (var failure = session.CreateCommand(CustomerSql.MarkImportSourceFailure))
        {
            failure.Parameters.AddWithValue("tenant_id", context.TenantId);
            failure.Parameters.AddWithValue("object_key", lease.ObjectKey);
            failure.Parameters.AddWithValue("state", failureKind == CustomerImportSourceFailureKind.OutcomeUnknown ? 5 : 4);
            failure.Parameters.AddWithValue("failure_code", failureCode.Length > 128 ? failureCode[..128] : failureCode);
            await failure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        if (failureKind == CustomerImportSourceFailureKind.ProvenAbsent)
            await ChangeUsageAsync(session, context, reservation!.ProviderScope, reservation.ByteLength, now, CustomerSql.ReleaseImportSourceUsage, cancellationToken).ConfigureAwait(false);
        await ChangeReservationAsync(session, context, lease, reservation!.ProviderScope, now,
            failureKind == CustomerImportSourceFailureKind.OutcomeUnknown ? CustomerSql.MarkImportSourceReservationUnknown : CustomerSql.ReleaseImportSourceReservation,
            cancellationToken).ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static CustomerValidationException SourceUnavailable() => new("import_source_unavailable", "The import source has no current upload authority.");
    private static CustomerValidationException SourceIntentConflict() => new("idempotency_key_conflict", "Import content and retention are immutable for this caller's key.");

    private sealed record SourceReservation(Guid ReservationId, string ProviderScope, string ObjectKey, long ByteLength,
        int State, Guid AccountId, Guid ImportId, long Generation, Guid? LeaseId, DateTimeOffset? LeaseExpiresAt, string Key, string Fingerprint);
    private sealed record LockedSource(CustomerImportSourceSnapshot Snapshot, DateTimeOffset? StagingExpiresAt);

    private static bool OwnsUpload(SourceReservation? reservation, TenantContext context, CustomerImportSourceLease lease, DateTimeOffset now) =>
        reservation is not null && reservation.AccountId == context.AccountId && reservation.ObjectKey == lease.ObjectKey
        && reservation.ImportId == lease.ImportId && reservation.State is 1 or 4
        && reservation.Generation == lease.Generation && lease.LeaseId != Guid.Empty && reservation.LeaseId == lease.LeaseId
        && reservation.LeaseExpiresAt > now;

    private static async Task<(long Maximum, long Reserved, long Retained)> LockUsageAsync(CustomerTenantDbSession session,
        Guid tenantId, string providerScope, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.LockImportSourceUsage);
        SourceParameters(command, tenantId, providerScope);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) throw SourceUnavailable();
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private static async Task<LockedSource?> LockSourceAsync(CustomerTenantDbSession session, Guid tenantId, string objectKey, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.LockImportSource);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("object_key", objectKey);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new(ReadSource(reader), reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10)) : null;
    }

    private static async Task<SourceReservation?> FindReservationAsync(CustomerTenantDbSession session, TenantContext context,
        string providerScope, string key, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.FindImportSourceReservation);
        SourceParameters(command, context, providerScope);
        command.Parameters.AddWithValue("account_id", context.AccountId);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new(reader.GetGuid(0), providerScope, reader.GetString(1), reader.GetInt64(2), reader.GetInt32(4), context.AccountId,
                reader.GetGuid(5), reader.GetInt64(6), reader.IsDBNull(7) ? null : reader.GetGuid(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8), key, reader.GetString(3)) : null;
    }

    private static async Task<SourceReservation?> FindReservationByIdAsync(CustomerTenantDbSession session, Guid tenantId, Guid id, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.FindImportSourceReservationById);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("reservation_id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new(id, reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt32(3),
                reader.IsDBNull(4) ? Guid.Empty : reader.GetGuid(4), reader.IsDBNull(5) ? Guid.Empty : reader.GetGuid(5), reader.GetInt64(6),
                reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                reader.GetString(9), reader.GetString(10)) : null;
    }

    private static async Task ChangeUsageAsync(CustomerTenantDbSession session, TenantContext context, string providerScope,
        long byteLength, DateTimeOffset now, string sql, CancellationToken ct)
    {
        await using var command = session.CreateCommand(sql);
        SourceParameters(command, context, providerScope);
        command.Parameters.AddWithValue("byte_length", byteLength);
        command.Parameters.AddWithValue("now", now);
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1) throw SourceUnavailable();
    }

    private static async Task ChangeReservationAsync(CustomerTenantDbSession session, TenantContext context, CustomerImportSourceLease lease,
        string providerScope, DateTimeOffset now, string sql, CancellationToken ct)
    {
        await using var command = session.CreateCommand(sql);
        SourceParameters(command, context, providerScope);
        command.Parameters.AddWithValue("reservation_id", lease.ReservationId);
        command.Parameters.AddWithValue("now", now);
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1) throw SourceUnavailable();
    }

    public async Task<CustomerImportSourceSnapshot?> ReadSourceAsync(
        TenantContext context, Guid importId, CancellationToken cancellationToken)
    {
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken)
            .ConfigureAwait(false);
        var result = await ReadSourceByImportAsync(session, context, importId, cancellationToken).ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<CustomerImportSourceRetirementLease?> ClaimExpiredSourceRetirementAsync(
        TenantContext context, DateTimeOffset now, CancellationToken cancellationToken)
        => await ClaimExpiredSourceRetirementAsync(context.TenantId, now, cancellationToken).ConfigureAwait(false);

    public async Task<CustomerImportSourceRetirementLease?> ClaimExpiredSourceRetirementAsync(
        Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, tenantId, cancellationToken)
            .ConfigureAwait(false);
        var leaseId = Guid.CreateVersion7();
        var leaseExpiresAt = now.Add(CustomerImportSourceRetirementLease.DefaultLeaseDuration);
        await using var command = session.CreateCommand(CustomerSql.ClaimExpiredImportSourceRetirement);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("lease_id", leaseId);
        command.Parameters.AddWithValue("lease_expires_at", leaseExpiresAt);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new CustomerImportSourceRetirementLease(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetString(3),
                reader.GetInt64(4), reader.GetGuid(5), reader.GetFieldValue<DateTimeOffset>(6))
            : null;
        await reader.DisposeAsync().ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<bool> CompleteSourceRetirementAsync(
        TenantContext context,
        CustomerImportSourceRetirementLease lease,
        bool deleted,
        string? failureCode,
        CancellationToken cancellationToken)
        => await CompleteSourceRetirementAsync(context.TenantId, lease, deleted, failureCode, cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> CompleteSourceRetirementAsync(
        Guid tenantId,
        CustomerImportSourceRetirementLease lease,
        bool deleted,
        string? failureCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var now = CurrentStorageTime();
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, tenantId, cancellationToken)
            .ConfigureAwait(false);
        var state = deleted ? (int)CustomerImportSourceState.Retired : (int)CustomerImportSourceState.Orphaned;
        await LockUsageAsync(session, tenantId, lease.ProviderScope, cancellationToken).ConfigureAwait(false);
        bool completed;
        await using (var complete = session.CreateCommand(CustomerSql.CompleteImportSourceRetirement))
        {
            complete.Parameters.AddWithValue("tenant_id", tenantId);
            complete.Parameters.AddWithValue("object_key", lease.ObjectKey);
            complete.Parameters.AddWithValue("state", state);
            complete.Parameters.AddWithValue("generation", lease.Generation);
            complete.Parameters.AddWithValue("lease_id", lease.LeaseId);
            complete.Parameters.AddWithValue("now", now);
            complete.Parameters.AddWithValue("deleted", deleted);
            complete.Parameters.Add("failure_code", NpgsqlTypes.NpgsqlDbType.Varchar).Value =
                (object?)(failureCode is null ? DBNull.Value : failureCode.Length > 128 ? failureCode[..128] : failureCode) ?? DBNull.Value;
            completed = true.Equals(await complete.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        }
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return completed;
    }

    private static async Task<CustomerImportSourceSnapshot?> ReadSourceByImportAsync(
        CustomerTenantDbSession session, TenantContext context, Guid importId, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindImportSourceByImport);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("import_id", importId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadSource(reader) : null;
    }

    private static CustomerImportSourceSnapshot ReadSource(NpgsqlDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetString(3), reader.GetString(4),
        (CustomerImportSourceState)reader.GetInt32(5), (CustomerImportRetention)reader.GetInt32(6),
        reader.GetFieldValue<DateTimeOffset>(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
        reader.IsDBNull(9) ? null : reader.GetString(9));

    private static void SourceParameters(NpgsqlCommand command, TenantContext context, string providerScope) =>
        SourceParameters(command, context.TenantId, providerScope);

    private static void SourceParameters(NpgsqlCommand command, Guid tenantId, string providerScope)
    {
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("provider_scope", providerScope);
    }
}
