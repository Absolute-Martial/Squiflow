using Application.Customers;
using Application.Tenancy;
using Npgsql;

namespace Application.Customers.Postgres;

public sealed partial class PostgresCustomerStore
{
    public async Task<CustomerImportSourceLease> BeginSourceAsync(
        TenantContext context,
        CustomerImportPlan plan,
        string idempotencyKey,
        CustomerImportRetention retention,
        CustomerImportSourcePolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(policy);
        if (plan.ManifestHash.Length != 64 || plan.ByteLength is < 0 or > CustomerImportCsv.MaxBytes)
            throw new CustomerValidationException("import_plan_invalid", "Import plan source metadata is invalid.");

        var objectKey = $"imports/{context.TenantId:D}/{plan.ImportId:N}/{plan.ManifestHash}.csv";
        var now = CurrentStorageTime();
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var existingReceipt = await FindImportReceiptAsync(session, context, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existingReceipt is not null)
        {
            if (!string.Equals(existingReceipt.Value.Fingerprint, plan.Fingerprint, StringComparison.Ordinal))
                throw new CustomerValidationException("idempotency_key_conflict", "The import idempotency key is bound to different content.");
            var existing = await ReadSourceByImportAsync(session, context, existingReceipt.Value.ImportId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new CustomerValidationException("import_source_unavailable", "The existing import has no retained source.");
            if (existing.State is CustomerImportSourceState.Retired or CustomerImportSourceState.RetirementPending)
                throw new CustomerValidationException("import_source_unavailable", "The existing import source is no longer publishable.");
            if (existing.Retention != retention)
                throw new CustomerValidationException("idempotency_key_conflict", "Import retention is immutable for an existing idempotency key.");
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(Guid.Empty, existing.ObjectKey, existing.State != CustomerImportSourceState.Available, existing);
        }

        var existingSource = await ReadSourceAsync(session, context, objectKey, cancellationToken).ConfigureAwait(false);
        if (existingSource is not null && (existingSource.ByteLength != plan.ByteLength
            || !string.Equals(existingSource.Sha256, plan.ManifestHash, StringComparison.Ordinal)
            || !string.Equals(existingSource.ProviderScope, policy.ProviderScope, StringComparison.Ordinal)))
        {
            throw new CustomerValidationException("import_source_conflict", "The immutable source key is bound to different bytes.");
        }
        if (existingSource is not null && existingSource.Retention != retention)
            throw new CustomerValidationException("import_retention_conflict", "Import retention is immutable for a retained source.");
        if (existingSource?.State is CustomerImportSourceState.Retired or CustomerImportSourceState.RetirementPending)
            throw new CustomerValidationException("import_source_unavailable", "The existing import source is no longer publishable.");

        if (existingSource?.State == CustomerImportSourceState.Available)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(Guid.Empty, objectKey, false, existingSource);
        }

        await using (var ensure = session.CreateCommand(CustomerSql.EnsureImportSourceUsage))
        {
            SourceParameters(ensure, context, policy.ProviderScope);
            ensure.Parameters.AddWithValue("maximum_bytes", policy.MaximumRetainedBytes);
            ensure.Parameters.AddWithValue("now", now);
            await ensure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var reservation = await FindReservationAsync(session, context, policy.ProviderScope, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (reservation is not null)
        {
            if (!string.Equals(reservation.Value.Fingerprint, plan.Fingerprint, StringComparison.Ordinal)
                || !string.Equals(reservation.Value.ObjectKey, objectKey, StringComparison.Ordinal)
                || reservation.Value.ByteLength != plan.ByteLength)
                throw new CustomerValidationException("idempotency_key_conflict", "The import idempotency key is bound to different content.");
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(reservation.Value.ReservationId, objectKey,
                reservation.Value.State != 2, existingSource);
        }

        long maximum;
        long reserved;
        long retained;
        await using (var usage = session.CreateCommand(CustomerSql.LockImportSourceUsage))
        {
            SourceParameters(usage, context, policy.ProviderScope);
            await using var reader = await usage.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Import source usage row disappeared after creation.");
            maximum = reader.GetInt64(0); reserved = reader.GetInt64(1); retained = reader.GetInt64(2);
        }

        if (plan.ByteLength > maximum || reserved > maximum - plan.ByteLength || retained > maximum - plan.ByteLength - reserved)
            throw new CustomerValidationException("import_storage_capacity", "The retained import-byte allowance is exhausted.");

        var reservationId = Guid.CreateVersion7();
        var expiresAt = retention == CustomerImportRetention.DefaultSevenDays
            ? now.AddDays(7)
            : (DateTimeOffset?)null;
        if (existingSource is null)
        {
            await using var insertSource = session.CreateCommand(CustomerSql.InsertImportSource);
            SourceParameters(insertSource, context, policy.ProviderScope);
            insertSource.Parameters.AddWithValue("object_key", objectKey);
            insertSource.Parameters.AddWithValue("byte_length", plan.ByteLength);
            insertSource.Parameters.AddWithValue("sha256", plan.ManifestHash);
            insertSource.Parameters.AddWithValue("content_type", "text/csv; charset=utf-8");
            insertSource.Parameters.AddWithValue("retention", (int)retention);
            insertSource.Parameters.AddWithValue("created_at", now);
            insertSource.Parameters.Add("expires_at", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value =
                (object?)expiresAt ?? DBNull.Value;
            await insertSource.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var reserve = session.CreateCommand(CustomerSql.ReserveImportSourceUsage))
        {
            SourceParameters(reserve, context, policy.ProviderScope);
            reserve.Parameters.AddWithValue("byte_length", plan.ByteLength);
            reserve.Parameters.AddWithValue("now", now);
            await reserve.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var insertReservation = session.CreateCommand(CustomerSql.InsertImportSourceReservation))
        {
            SourceParameters(insertReservation, context, policy.ProviderScope);
            insertReservation.Parameters.AddWithValue("reservation_id", reservationId);
            insertReservation.Parameters.AddWithValue("key", idempotencyKey);
            insertReservation.Parameters.AddWithValue("fingerprint", plan.Fingerprint);
            insertReservation.Parameters.AddWithValue("object_key", objectKey);
            insertReservation.Parameters.AddWithValue("byte_length", plan.ByteLength);
            insertReservation.Parameters.AddWithValue("now", now);
            if (await insertReservation.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 0)
            {
                var concurrent = await FindReservationAsync(session, context, policy.ProviderScope, idempotencyKey, cancellationToken)
                    .ConfigureAwait(false) ?? throw new InvalidOperationException("Import source reservation disappeared after a conflict.");
                await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new(concurrent.ReservationId, concurrent.ObjectKey, concurrent.State != 2, existingSource);
            }
        }

        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(reservationId, objectKey, true, existingSource);
    }

    public async Task<CreateCustomerImportResult> CompleteSourceAsync(
        TenantContext context,
        CustomerImportPlan plan,
        string idempotencyKey,
        CustomerImportRetention retention,
        CustomerImportSourcePolicy policy,
        CustomerImportSourceLease lease,
        CancellationToken cancellationToken)
    {
        var now = CurrentStorageTime();
        await using (var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken)
            .ConfigureAwait(false))
        {
            await using (var source = session.CreateCommand(CustomerSql.LockImportSource))
            {
                SourceParameters(source, context, policy.ProviderScope);
                source.Parameters.AddWithValue("object_key", lease.ObjectKey);
                await using var reader = await source.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    throw new CustomerValidationException("import_source_unavailable", "The import source reservation is missing.");
                var sourceState = (CustomerImportSourceState)reader.GetInt32(0);
                if (sourceState is CustomerImportSourceState.Retired or CustomerImportSourceState.RetirementPending)
                    throw new CustomerValidationException("import_source_unavailable", "The import source is no longer publishable.");
                if (reader.GetInt32(1) != (int)retention
                    || reader.GetInt64(2) != plan.ByteLength
                    || !string.Equals(reader.GetString(3), plan.ManifestHash, StringComparison.Ordinal))
                    throw new CustomerValidationException("import_source_conflict", "The retained source metadata is immutable.");
            }

            await using (var available = session.CreateCommand(CustomerSql.MarkImportSourceAvailable))
            {
                available.Parameters.AddWithValue("tenant_id", context.TenantId);
                available.Parameters.AddWithValue("object_key", lease.ObjectKey);
                await available.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (lease.ReservationId != Guid.Empty)
            {
                await using var reservation = session.CreateCommand(CustomerSql.FindImportSourceReservationById);
                reservation.Parameters.AddWithValue("tenant_id", context.TenantId);
                reservation.Parameters.AddWithValue("reservation_id", lease.ReservationId);
                await using var reader = await reservation.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && reader.GetInt32(2) is 1 or 4)
                {
                    var providerScope = reader.GetString(0);
                    var byteLength = reader.GetInt64(1);
                    await reader.DisposeAsync().ConfigureAwait(false);
                    await using var commitUsage = session.CreateCommand(CustomerSql.CommitImportSourceUsage);
                    SourceParameters(commitUsage, context, providerScope);
                    commitUsage.Parameters.AddWithValue("byte_length", byteLength);
                    commitUsage.Parameters.AddWithValue("now", now);
                    await commitUsage.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    await using var commitReservation = session.CreateCommand(CustomerSql.CommitImportSourceReservation);
                    SourceParameters(commitReservation, context, providerScope);
                    commitReservation.Parameters.AddWithValue("reservation_id", lease.ReservationId);
                    commitReservation.Parameters.AddWithValue("now", now);
                    await commitReservation.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = await CreateImportPlanAsync(context, plan, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (result.IdempotencyKeyConflict)
            return result;
        await using var referenceSession = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken)
            .ConfigureAwait(false);
        await using (var reference = referenceSession.CreateCommand(CustomerSql.UpdateImportSourceKey))
        {
            reference.Parameters.AddWithValue("tenant_id", context.TenantId);
            reference.Parameters.AddWithValue("import_id", result.ImportId);
            reference.Parameters.AddWithValue("object_key", lease.ObjectKey);
            await reference.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await referenceSession.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task MarkSourceFailureAsync(
        TenantContext context,
        CustomerImportSourceLease lease,
        CustomerImportSourceFailureKind failureKind,
        string failureCode,
        CancellationToken cancellationToken)
    {
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken)
            .ConfigureAwait(false);
        var state = failureKind == CustomerImportSourceFailureKind.OutcomeUnknown ? 5 : 4;
        await using (var source = session.CreateCommand(CustomerSql.MarkImportSourceFailure))
        {
            source.Parameters.AddWithValue("tenant_id", context.TenantId);
            source.Parameters.AddWithValue("object_key", lease.ObjectKey);
            source.Parameters.AddWithValue("state", state);
            source.Parameters.AddWithValue("failure_code", failureCode.Length > 128 ? failureCode[..128] : failureCode);
            await source.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        if (lease.ReservationId != Guid.Empty)
        {
            await using var reservation = session.CreateCommand(CustomerSql.FindImportSourceReservationById);
            reservation.Parameters.AddWithValue("tenant_id", context.TenantId);
            reservation.Parameters.AddWithValue("reservation_id", lease.ReservationId);
            await using var reader = await reservation.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && reader.GetInt32(2) is 1 or 4)
            {
                var providerScope = reader.GetString(0); var byteLength = reader.GetInt64(1);
                await reader.DisposeAsync().ConfigureAwait(false);
                if (failureKind == CustomerImportSourceFailureKind.OutcomeUnknown)
                {
                    await using var unknown = session.CreateCommand(CustomerSql.MarkImportSourceReservationUnknown);
                    unknown.Parameters.AddWithValue("tenant_id", context.TenantId);
                    unknown.Parameters.AddWithValue("reservation_id", lease.ReservationId);
                    unknown.Parameters.AddWithValue("now", CurrentStorageTime());
                    await unknown.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await using var releaseUsage = session.CreateCommand(CustomerSql.ReleaseImportSourceUsage);
                    SourceParameters(releaseUsage, context, providerScope);
                    releaseUsage.Parameters.AddWithValue("byte_length", byteLength);
                    releaseUsage.Parameters.AddWithValue("now", CurrentStorageTime());
                    await releaseUsage.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    await using var release = session.CreateCommand(CustomerSql.ReleaseImportSourceReservation);
                    SourceParameters(release, context, providerScope);
                    release.Parameters.AddWithValue("reservation_id", lease.ReservationId);
                    release.Parameters.AddWithValue("now", CurrentStorageTime());
                    await release.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task CompleteSourceRetirementAsync(
        TenantContext context,
        CustomerImportSourceRetirementLease lease,
        bool deleted,
        string? failureCode,
        CancellationToken cancellationToken)
        => await CompleteSourceRetirementAsync(context.TenantId, lease, deleted, failureCode, cancellationToken)
            .ConfigureAwait(false);

    public async Task CompleteSourceRetirementAsync(
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
            await complete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
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

    private static async Task<CustomerImportSourceSnapshot?> ReadSourceAsync(
        CustomerTenantDbSession session, TenantContext context, string objectKey, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindImportSource);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("object_key", objectKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadSource(reader) : null;
    }

    private static CustomerImportSourceSnapshot ReadSource(NpgsqlDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetString(3), reader.GetString(4),
        (CustomerImportSourceState)reader.GetInt32(5), (CustomerImportRetention)reader.GetInt32(6),
        reader.GetFieldValue<DateTimeOffset>(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
        reader.IsDBNull(9) ? null : reader.GetString(9));

    private static async Task<(Guid ReservationId, string ObjectKey, long ByteLength, string Fingerprint, int State)?> FindReservationAsync(
        CustomerTenantDbSession session, TenantContext context, string providerScope, string key, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindImportSourceReservation);
        SourceParameters(command, context, providerScope);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (reader.GetGuid(0), reader.GetString(1), reader.GetInt64(2), reader.GetString(3), reader.GetInt32(4))
            : null;
    }

    private static void SourceParameters(NpgsqlCommand command, TenantContext context, string providerScope) =>
        SourceParameters(command, context.TenantId, providerScope);

    private static void SourceParameters(NpgsqlCommand command, Guid tenantId, string providerScope)
    {
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("provider_scope", providerScope);
    }
}
