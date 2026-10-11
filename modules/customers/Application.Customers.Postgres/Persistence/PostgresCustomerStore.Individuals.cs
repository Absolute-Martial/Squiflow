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
        if (receipt is not null) return CreateReplay(receipt.Value, intent);

        // An individual insert publishes the same canonical duplicate signals that
        // consolidation, duplicate resolution, representative mutation and import row
        // acceptance serialize on, so it joins the same tenant canonicalization gate
        // instead of racing those readers. Canonical uniqueness is deliberately not
        // claimed here; this gate only orders the writers.
        await LockCustomerCanonicalizationAsync(session, context.TenantId, cancellationToken).ConfigureAwait(false);

        var individual = new CustomerIndividualSnapshot(Guid.CreateVersion7(), context.TenantId,
            intent.DisplayName, intent.Email, intent.Phone, CustomerIndividualAvailability.Active, 1,
            context.AccountId, CurrentStorageTime(), null, null, null, null, intent.CustomerType,
            intent.ExternalRegistrationId, intent.AddressLine1, intent.AddressLine2, intent.City, intent.Notes);
        await using (var insert = session.CreateCommand(CustomerSql.InsertIndividualWithSignals))
        {
            insert.Parameters.AddWithValue("tenant_id", context.TenantId);
            insert.Parameters.AddWithValue("id", individual.IndividualId);
            insert.Parameters.AddWithValue("display_name", individual.DisplayName);
            AddOptionalText(insert, "email", individual.Email);
            AddOptionalText(insert, "phone", individual.Phone);
            insert.Parameters.AddWithValue("customer_type", individual.CustomerType);
            AddOptionalText(insert, "external_registration_id", individual.ExternalRegistrationId);
            insert.Parameters.AddWithValue("normalized_name", CustomerIdentityNormalization.Name(individual.DisplayName));
            AddOptionalText(insert, "normalized_email", individual.Email is null ? null : CustomerIdentityNormalization.Email(individual.Email));
            AddOptionalText(insert, "normalized_phone", individual.Phone is null ? null : CustomerIdentityNormalization.Phone(individual.Phone));
            AddOptionalText(insert, "normalized_external_registration_id", individual.ExternalRegistrationId is null
                ? null : CustomerIdentityNormalization.ExternalRegistrationId(individual.ExternalRegistrationId));
            AddOptionalText(insert, "address_line1", individual.AddressLine1);
            AddOptionalText(insert, "address_line2", individual.AddressLine2);
            AddOptionalText(insert, "city", individual.City);
            AddOptionalText(insert, "notes", individual.Notes);
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
        return CreateReplay(receipt.Value, intent);
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

    public async Task<EditCustomerIndividualContactResult> EditIndividualContactAsync(
        TenantContext context, CustomerIndividualContactIntent intent, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindIndividualReceiptAsync(session, context, "contact", idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (receipt is not null) return ContactReplay(receipt.Value, intent.Fingerprint);

        CustomerIndividualSnapshot? individual;
        await using (var update = session.CreateCommand(CustomerSql.UpdateIndividualContact))
        {
            update.Parameters.AddWithValue("tenant_id", context.TenantId);
            update.Parameters.AddWithValue("individual_id", intent.IndividualId);
            update.Parameters.AddWithValue("expected_revision", intent.ExpectedRevision);
            update.Parameters.AddWithValue("display_name", intent.DisplayName);
            AddOptionalText(update, "email", intent.Email);
            AddOptionalText(update, "phone", intent.Phone);
            AddOptionalText(update, "normalized_name", CustomerIdentityNormalization.Name(intent.DisplayName));
            AddOptionalText(update, "normalized_email", intent.Email is null ? null : CustomerIdentityNormalization.Email(intent.Email));
            AddOptionalText(update, "normalized_phone", intent.Phone is null ? null : CustomerIdentityNormalization.Phone(intent.Phone));
            update.Parameters.AddWithValue("account_id", context.AccountId);
            update.Parameters.AddWithValue("changed_at", CurrentStorageTime());
            await using var reader = await update.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            individual = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? ReadIndividual(reader, context.TenantId) : null;
        }
        if (individual is null)
        {
            receipt = await FindIndividualReceiptAsync(session, context, "contact", idempotencyKey,
                cancellationToken).ConfigureAwait(false);
            if (receipt is not null) return ContactReplay(receipt.Value, intent.Fingerprint);
            var current = await FindIndividualAsync(session, context.TenantId, intent.IndividualId,
                cancellationToken).ConfigureAwait(false);
            if (current is null || current.RedirectTargetIndividualId.HasValue) return new(EditCustomerIndividualContactStatus.NotFound, null);
            if (current.Revision != intent.ExpectedRevision)
                return new(EditCustomerIndividualContactStatus.RevisionConflict, current);
            return new(EditCustomerIndividualContactStatus.NoChange, current);
        }

        if (await InsertIndividualReceiptAsync(session, context, "contact", idempotencyKey,
                intent.Fingerprint, individual, cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(EditCustomerIndividualContactStatus.Changed, individual);
        }
        receipt = await FindIndividualReceiptAsync(session, context, "contact", idempotencyKey,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Individual contact receipt disappeared after conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ContactReplay(receipt.Value, intent.Fingerprint);
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
            if (current is null || current.RedirectTargetIndividualId.HasValue) return new(ChangeCustomerIndividualAvailabilityStatus.NotFound, null);
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
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var fingerprint = reader.GetString(reader.GetOrdinal("fingerprint"));
        var receiptSnapshot = ReadIndividual(reader, context.TenantId);
        await reader.DisposeAsync().ConfigureAwait(false);
        var currentSnapshot = await FindIndividualAsync(session, context.TenantId,
            receiptSnapshot.IndividualId, cancellationToken).ConfigureAwait(false);
        return (fingerprint, currentSnapshot is null
            ? receiptSnapshot
            : receiptSnapshot with
            {
                CustomerType = currentSnapshot.CustomerType,
                ExternalRegistrationId = currentSnapshot.ExternalRegistrationId,
                AddressLine1 = currentSnapshot.AddressLine1,
                AddressLine2 = currentSnapshot.AddressLine2,
                City = currentSnapshot.City,
                Notes = currentSnapshot.Notes,
            });
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
        insert.Parameters.Add("contact_changed_by_account_id", NpgsqlDbType.Uuid).Value =
            (object?)individual.ContactChangedByAccountId ?? DBNull.Value;
        insert.Parameters.Add("contact_changed_at", NpgsqlDbType.TimestampTz).Value =
            (object?)individual.ContactChangedAt ?? DBNull.Value;
        return await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static CustomerIndividualSnapshot ReadIndividual(NpgsqlDataReader reader, Guid tenantId)
    {
        var email = reader.GetOrdinal("email");
        var phone = reader.GetOrdinal("phone");
        var changedBy = reader.GetOrdinal("availability_changed_by_account_id");
        var changedAt = reader.GetOrdinal("availability_changed_at");
        var contactChangedBy = reader.GetOrdinal("contact_changed_by_account_id");
        var contactChangedAt = reader.GetOrdinal("contact_changed_at");
        var customerType = reader.GetOrdinal("customer_type");
        var externalRegistrationId = reader.GetOrdinal("external_registration_id");
        var addressLine1 = reader.GetOrdinal("address_line1");
        var addressLine2 = reader.GetOrdinal("address_line2");
        var city = reader.GetOrdinal("city");
        var notes = reader.GetOrdinal("notes");
        var redirect = reader.GetOrdinal("redirect_target_individual_id");
        return new(reader.GetGuid(reader.GetOrdinal("id")), tenantId,
            reader.GetString(reader.GetOrdinal("display_name")),
            reader.IsDBNull(email) ? null : reader.GetString(email),
            reader.IsDBNull(phone) ? null : reader.GetString(phone),
            (CustomerIndividualAvailability)reader.GetInt32(reader.GetOrdinal("availability")),
            reader.GetInt64(reader.GetOrdinal("revision")),
            reader.GetGuid(reader.GetOrdinal("created_by_account_id")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            reader.IsDBNull(changedBy) ? null : reader.GetGuid(changedBy),
            reader.IsDBNull(changedAt) ? null : reader.GetFieldValue<DateTimeOffset>(changedAt),
            reader.IsDBNull(contactChangedBy) ? null : reader.GetGuid(contactChangedBy),
            reader.IsDBNull(contactChangedAt) ? null : reader.GetFieldValue<DateTimeOffset>(contactChangedAt),
            reader.IsDBNull(customerType) ? "individual" : reader.GetString(customerType),
            reader.IsDBNull(externalRegistrationId) ? null : reader.GetString(externalRegistrationId),
            reader.IsDBNull(addressLine1) ? null : reader.GetString(addressLine1),
            reader.IsDBNull(addressLine2) ? null : reader.GetString(addressLine2),
            reader.IsDBNull(city) ? null : reader.GetString(city),
            reader.IsDBNull(notes) ? null : reader.GetString(notes),
            reader.IsDBNull(redirect) ? null : reader.GetGuid(redirect));
    }

    private static void AddOptionalText(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.Add(name, NpgsqlDbType.Varchar).Value = (object?)value ?? DBNull.Value;

    private DateTimeOffset CurrentStorageTime()
    {
        var now = _timeProvider.GetUtcNow();
        return new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
    }

    private static CreateCustomerIndividualResult CreateReplay(
        (string Fingerprint, CustomerIndividualSnapshot Snapshot) receipt, CustomerIndividualIntent intent)
    {
        var matches = string.Equals(receipt.Fingerprint, intent.Fingerprint, StringComparison.Ordinal);
        // Pre-extension create receipts fingerprinted exactly name/email/phone. Only a
        // default individual with no new attributes can replay that retained intent.
        if (!matches && intent.CustomerType == "individual" && intent.ExternalRegistrationId is null
            && intent.AddressLine1 is null && intent.AddressLine2 is null && intent.City is null && intent.Notes is null)
            matches = receipt.Fingerprint == CustomerIdentityNormalization.Fingerprint("individual", intent.DisplayName, intent.Email ?? "", intent.Phone ?? "");
        return matches ? new(CreateCustomerIndividualStatus.Replayed, receipt.Snapshot)
            : new(CreateCustomerIndividualStatus.IdempotencyKeyConflict, null);
    }

    private static EditCustomerIndividualContactResult ContactReplay(
        (string Fingerprint, CustomerIndividualSnapshot Snapshot) receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(EditCustomerIndividualContactStatus.Replayed, receipt.Snapshot)
            : new(EditCustomerIndividualContactStatus.IdempotencyKeyConflict, null);

    private static ChangeCustomerIndividualAvailabilityResult AvailabilityReplay(
        (string Fingerprint, CustomerIndividualSnapshot Snapshot) receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(ChangeCustomerIndividualAvailabilityStatus.Replayed, receipt.Snapshot)
            : new(ChangeCustomerIndividualAvailabilityStatus.IdempotencyKeyConflict, null);
}
