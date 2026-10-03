using Application.Customers;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Customers.Postgres;

public sealed partial class PostgresCustomerStore
{
    public async Task<CreateCustomerIndividualResult> CreateIndividualAsync(TenantContext context,
        CustomerIndividualIntent intent, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);

        var receipt = await FindIndividualReceiptAsync(session, context, "create", idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (receipt is not null) return CreateReplay(receipt.Value, intent.Fingerprint);

        var individual = new CustomerIndividualSnapshot(Guid.CreateVersion7(), context.TenantId,
            intent.DisplayName, intent.Email, intent.Phone, CustomerIndividualAvailability.Active, 1,
            context.AccountId, CurrentStorageTime(), null, null);
        await using (var insert = session.CreateCommand(CustomerSql.InsertIndividual))
        {
            insert.Parameters.AddWithValue("tenant_id", context.TenantId);
            insert.Parameters.AddWithValue("id", individual.IndividualId);
            insert.Parameters.AddWithValue("display_name", individual.DisplayName);
            AddOptionalText(insert, "email", individual.Email);
            AddOptionalText(insert, "phone", individual.Phone);
            insert.Parameters.AddWithValue("account_id", context.AccountId);
            insert.Parameters.AddWithValue("created_at", individual.CreatedAt);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        if (await InsertIndividualReceiptAsync(session, context, "create", idempotencyKey,
                intent.Fingerprint, individual, cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(CreateCustomerIndividualStatus.Created, individual);
        }
        receipt = await FindIndividualReceiptAsync(session, context, "create", idempotencyKey,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Individual create receipt disappeared after conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return CreateReplay(receipt.Value, intent.Fingerprint);
    }

    public async Task<CustomerIndividualSnapshot?> FindIndividualAsync(TenantContext context,
        Guid individualId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        return await FindIndividualAsync(session, context.TenantId, individualId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ChangeCustomerIndividualAvailabilityResult> ChangeIndividualAvailabilityAsync(
        TenantContext context, CustomerIndividualAvailabilityIntent intent, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindIndividualReceiptAsync(session, context, "availability", idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (receipt is not null) return AvailabilityReplay(receipt.Value, intent.Fingerprint);

        CustomerIndividualSnapshot? individual;
        await using (var update = session.CreateCommand(CustomerSql.UpdateIndividualAvailability))
        {
            update.Parameters.AddWithValue("tenant_id", context.TenantId);
            update.Parameters.AddWithValue("individual_id", intent.IndividualId);
            update.Parameters.AddWithValue("expected_revision", intent.ExpectedRevision);
            update.Parameters.AddWithValue("availability", (int)intent.Availability);
            update.Parameters.AddWithValue("account_id", context.AccountId);
            update.Parameters.AddWithValue("changed_at", CurrentStorageTime());
            await using var reader = await update.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            individual = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? ReadIndividual(reader, context.TenantId) : null;
        }
        if (individual is null)
        {
            receipt = await FindIndividualReceiptAsync(session, context, "availability", idempotencyKey,
                cancellationToken).ConfigureAwait(false);
            if (receipt is not null) return AvailabilityReplay(receipt.Value, intent.Fingerprint);
            var current = await FindIndividualAsync(session, context.TenantId, intent.IndividualId,
                cancellationToken).ConfigureAwait(false);
            if (current is null) return new(ChangeCustomerIndividualAvailabilityStatus.NotFound, null);
            if (current.Revision != intent.ExpectedRevision)
                return new(ChangeCustomerIndividualAvailabilityStatus.RevisionConflict, current);
            return new(ChangeCustomerIndividualAvailabilityStatus.AlreadyInState, current);
        }

        if (await InsertIndividualReceiptAsync(session, context, "availability", idempotencyKey,
                intent.Fingerprint, individual, cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(ChangeCustomerIndividualAvailabilityStatus.Changed, individual);
        }
        receipt = await FindIndividualReceiptAsync(session, context, "availability", idempotencyKey,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Individual availability receipt disappeared after conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return AvailabilityReplay(receipt.Value, intent.Fingerprint);
    }

    private static async Task<CustomerIndividualSnapshot?> FindIndividualAsync(CustomerTenantDbSession session,
        Guid tenantId, Guid individualId, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindIndividual);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("individual_id", individualId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadIndividual(reader, tenantId) : null;
    }

    private static async Task<(string Fingerprint, CustomerIndividualSnapshot Snapshot)?> FindIndividualReceiptAsync(
        CustomerTenantDbSession session, TenantContext context, string operation, string key,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindIndividualReceipt);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("account_id", context.AccountId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (reader.GetString(reader.GetOrdinal("fingerprint")), ReadIndividual(reader, context.TenantId)) : null;
    }

    private static async Task<bool> InsertIndividualReceiptAsync(CustomerTenantDbSession session,
        TenantContext context, string operation, string key, string fingerprint,
        CustomerIndividualSnapshot individual, CancellationToken cancellationToken)
    {
        await using var insert = session.CreateCommand(CustomerSql.InsertIndividualReceipt);
        insert.Parameters.AddWithValue("tenant_id", context.TenantId);
        insert.Parameters.AddWithValue("account_id", context.AccountId);
        insert.Parameters.AddWithValue("operation", operation);
        insert.Parameters.AddWithValue("key", key);
        insert.Parameters.AddWithValue("fingerprint", fingerprint);
        insert.Parameters.AddWithValue("individual_id", individual.IndividualId);
        insert.Parameters.AddWithValue("display_name", individual.DisplayName);
        AddOptionalText(insert, "email", individual.Email);
        AddOptionalText(insert, "phone", individual.Phone);
        insert.Parameters.AddWithValue("availability", (int)individual.Availability);
        insert.Parameters.AddWithValue("revision", individual.Revision);
        insert.Parameters.AddWithValue("created_by_account_id", individual.CreatedByAccountId);
        insert.Parameters.AddWithValue("created_at", individual.CreatedAt);
        insert.Parameters.Add("availability_changed_by_account_id", NpgsqlDbType.Uuid).Value =
            (object?)individual.AvailabilityChangedByAccountId ?? DBNull.Value;
        insert.Parameters.Add("availability_changed_at", NpgsqlDbType.TimestampTz).Value =
            (object?)individual.AvailabilityChangedAt ?? DBNull.Value;
        return await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static CustomerIndividualSnapshot ReadIndividual(NpgsqlDataReader reader, Guid tenantId)
    {
        var email = reader.GetOrdinal("email");
        var phone = reader.GetOrdinal("phone");
        var changedBy = reader.GetOrdinal("availability_changed_by_account_id");
        var changedAt = reader.GetOrdinal("availability_changed_at");
        return new(reader.GetGuid(reader.GetOrdinal("id")), tenantId,
            reader.GetString(reader.GetOrdinal("display_name")),
            reader.IsDBNull(email) ? null : reader.GetString(email),
            reader.IsDBNull(phone) ? null : reader.GetString(phone),
            (CustomerIndividualAvailability)reader.GetInt32(reader.GetOrdinal("availability")),
            reader.GetInt64(reader.GetOrdinal("revision")),
            reader.GetGuid(reader.GetOrdinal("created_by_account_id")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            reader.IsDBNull(changedBy) ? null : reader.GetGuid(changedBy),
            reader.IsDBNull(changedAt) ? null : reader.GetFieldValue<DateTimeOffset>(changedAt));
    }

    private static void AddOptionalText(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.Add(name, NpgsqlDbType.Varchar).Value = (object?)value ?? DBNull.Value;

    private DateTimeOffset CurrentStorageTime()
    {
        var now = _timeProvider.GetUtcNow();
        return new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
    }

    private static CreateCustomerIndividualResult CreateReplay(
        (string Fingerprint, CustomerIndividualSnapshot Snapshot) receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(CreateCustomerIndividualStatus.Replayed, receipt.Snapshot)
            : new(CreateCustomerIndividualStatus.IdempotencyKeyConflict, null);

    private static ChangeCustomerIndividualAvailabilityResult AvailabilityReplay(
        (string Fingerprint, CustomerIndividualSnapshot Snapshot) receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(ChangeCustomerIndividualAvailabilityStatus.Replayed, receipt.Snapshot)
            : new(ChangeCustomerIndividualAvailabilityStatus.IdempotencyKeyConflict, null);
}
