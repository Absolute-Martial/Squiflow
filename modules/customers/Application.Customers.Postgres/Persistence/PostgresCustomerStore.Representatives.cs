using Application.Customers;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Customers.Postgres;

public sealed partial class PostgresCustomerStore
{
    public async Task<LinkCustomerRepresentativeResult> LinkRepresentativeAsync(
        TenantContext context, CustomerRepresentativeLinkIntent intent, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindRepresentativeReceiptAsync(
            session, context, "link", idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
            return LinkReplay(receipt.Value, intent.Fingerprint);

        await LockCustomerCanonicalizationAsync(session, context.TenantId, cancellationToken).ConfigureAwait(false);
        receipt = await FindRepresentativeReceiptAsync(session, context, "link", idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null) return LinkReplay(receipt.Value, intent.Fingerprint);

        if (await FindOrganizationAsync(session, context.TenantId, intent.OrganizationId,
                cancellationToken).ConfigureAwait(false) is null)
        {
            return new(LinkCustomerRepresentativeStatus.TargetNotFound, null);
        }

        if (intent.ProgramId is Guid programId)
        {
            var program = await FindProgramAsync(
                session, context.TenantId, programId, cancellationToken).ConfigureAwait(false);
            if (program is null || program.OrganizationId != intent.OrganizationId)
                return new(LinkCustomerRepresentativeStatus.TargetNotFound, null);
        }

        var individual = await FindIndividualAsync(
            session, context.TenantId, intent.IndividualId, cancellationToken).ConfigureAwait(false);
        if (individual is null)
            return new(LinkCustomerRepresentativeStatus.IndividualNotFound, null);
        if (individual.Availability != CustomerIndividualAvailability.Active || individual.RedirectTargetIndividualId.HasValue)
            return new(LinkCustomerRepresentativeStatus.IndividualInactive, null);

        var existing = await FindActiveRepresentativeAsync(
            session, context.TenantId, intent.OrganizationId, intent.ProgramId, intent.IndividualId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return new(LinkCustomerRepresentativeStatus.AlreadyLinked, existing);

        var createdAt = CurrentStorageTime();
        CustomerRepresentativeSnapshot? representative;
        await using (var insert = session.CreateCommand(CustomerSql.InsertRepresentative))
        {
            insert.Parameters.AddWithValue("tenant_id", context.TenantId);
            insert.Parameters.AddWithValue("representative_id", Guid.CreateVersion7());
            insert.Parameters.AddWithValue("organization_id", intent.OrganizationId);
            AddOptionalUuid(insert, "program_id", intent.ProgramId);
            insert.Parameters.AddWithValue("individual_id", intent.IndividualId);
            insert.Parameters.AddWithValue("account_id", context.AccountId);
            insert.Parameters.AddWithValue("created_at", createdAt);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            representative = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? ReadRepresentative(reader, context.TenantId)
                : null;
        }

        if (representative is null)
        {
            receipt = await FindRepresentativeReceiptAsync(
                session, context, "link", idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (receipt is not null)
                return LinkReplay(receipt.Value, intent.Fingerprint);

            individual = await FindIndividualAsync(
                session, context.TenantId, intent.IndividualId, cancellationToken).ConfigureAwait(false);
            if (individual is null)
                return new(LinkCustomerRepresentativeStatus.IndividualNotFound, null);
            if (individual.Availability != CustomerIndividualAvailability.Active || individual.RedirectTargetIndividualId.HasValue)
                return new(LinkCustomerRepresentativeStatus.IndividualInactive, null);

            existing = await FindActiveRepresentativeAsync(
                session, context.TenantId, intent.OrganizationId, intent.ProgramId, intent.IndividualId,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "Representative link conflicted but no active relationship or receipt is visible.");
            return new(LinkCustomerRepresentativeStatus.AlreadyLinked, existing);
        }

        if (await InsertRepresentativeReceiptAsync(
                session, context, "link", idempotencyKey, intent.Fingerprint, representative,
                cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(LinkCustomerRepresentativeStatus.Created, representative);
        }

        receipt = await FindRepresentativeReceiptAsync(
            session, context, "link", idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Representative link receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return LinkReplay(receipt.Value, intent.Fingerprint);
    }

    public async Task<CustomerRepresentativeSnapshot?> FindRepresentativeAsync(
        TenantContext context, Guid representativeId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        return await FindRepresentativeAsync(
            session, context.TenantId, representativeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UnlinkCustomerRepresentativeResult> UnlinkRepresentativeAsync(
        TenantContext context, CustomerRepresentativeUnlinkIntent intent, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindRepresentativeReceiptAsync(
            session, context, "unlink", idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
            return UnlinkReplay(receipt.Value, intent.Fingerprint);

        await LockCustomerCanonicalizationAsync(session, context.TenantId, cancellationToken).ConfigureAwait(false);
        receipt = await FindRepresentativeReceiptAsync(session, context, "unlink", idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null) return UnlinkReplay(receipt.Value, intent.Fingerprint);

        CustomerRepresentativeSnapshot? representative;
        await using (var update = session.CreateCommand(CustomerSql.UpdateRepresentativeAvailability))
        {
            update.Parameters.AddWithValue("tenant_id", context.TenantId);
            update.Parameters.AddWithValue("organization_id", intent.OrganizationId);
            update.Parameters.AddWithValue("representative_id", intent.RepresentativeId);
            update.Parameters.AddWithValue("expected_revision", intent.ExpectedRevision);
            update.Parameters.AddWithValue("account_id", context.AccountId);
            update.Parameters.AddWithValue("changed_at", CurrentStorageTime());
            await using var reader = await update.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            representative = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? ReadRepresentative(reader, context.TenantId)
                : null;
        }

        if (representative is null)
        {
            receipt = await FindRepresentativeReceiptAsync(
                session, context, "unlink", idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (receipt is not null)
                return UnlinkReplay(receipt.Value, intent.Fingerprint);

            var current = await FindRepresentativeAsync(
                session, context.TenantId, intent.RepresentativeId, cancellationToken).ConfigureAwait(false);
            if (current is null || current.OrganizationId != intent.OrganizationId)
                return new(UnlinkCustomerRepresentativeStatus.NotFound, null);
            if (current.Revision != intent.ExpectedRevision)
                return new(UnlinkCustomerRepresentativeStatus.RevisionConflict, current);
            return new(UnlinkCustomerRepresentativeStatus.AlreadyInactive, current);
        }

        if (await InsertRepresentativeReceiptAsync(
                session, context, "unlink", idempotencyKey, intent.Fingerprint, representative,
                cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(UnlinkCustomerRepresentativeStatus.Changed, representative);
        }

        receipt = await FindRepresentativeReceiptAsync(
            session, context, "unlink", idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Representative unlink receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return UnlinkReplay(receipt.Value, intent.Fingerprint);
    }

    private static async Task<CustomerRepresentativeSnapshot?> FindRepresentativeAsync(
        CustomerTenantDbSession session, Guid tenantId, Guid representativeId,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindRepresentative);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("representative_id", representativeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadRepresentative(reader, tenantId)
            : null;
    }

    private static async Task<CustomerRepresentativeSnapshot?> FindActiveRepresentativeAsync(
        CustomerTenantDbSession session, Guid tenantId, Guid organizationId, Guid? programId,
        Guid individualId, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindActiveRepresentative);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("organization_id", organizationId);
        AddOptionalUuid(command, "program_id", programId);
        command.Parameters.AddWithValue("individual_id", individualId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadRepresentative(reader, tenantId)
            : null;
    }

    private static async Task<(string Fingerprint, CustomerRepresentativeSnapshot Snapshot)?>
        FindRepresentativeReceiptAsync(
            CustomerTenantDbSession session, TenantContext context, string operation, string key,
            CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindRepresentativeReceipt);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("account_id", context.AccountId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (reader.GetString(0), ReadRepresentative(reader, context.TenantId))
            : null;
    }

    private static async Task<bool> InsertRepresentativeReceiptAsync(
        CustomerTenantDbSession session, TenantContext context, string operation, string key,
        string fingerprint, CustomerRepresentativeSnapshot representative,
        CancellationToken cancellationToken)
    {
        await using var insert = session.CreateCommand(CustomerSql.InsertRepresentativeReceipt);
        insert.Parameters.AddWithValue("tenant_id", context.TenantId);
        insert.Parameters.AddWithValue("account_id", context.AccountId);
        insert.Parameters.AddWithValue("operation", operation);
        insert.Parameters.AddWithValue("key", key);
        insert.Parameters.AddWithValue("fingerprint", fingerprint);
        insert.Parameters.AddWithValue("representative_id", representative.RepresentativeId);
        insert.Parameters.AddWithValue("organization_id", representative.OrganizationId);
        AddOptionalUuid(insert, "program_id", representative.ProgramId);
        insert.Parameters.AddWithValue("individual_id", representative.IndividualId);
        insert.Parameters.AddWithValue("availability", (int)representative.Availability);
        insert.Parameters.AddWithValue("revision", representative.Revision);
        insert.Parameters.AddWithValue("created_by_account_id", representative.CreatedByAccountId);
        insert.Parameters.AddWithValue("created_at", representative.CreatedAt);
        insert.Parameters.Add("changed_by_account_id", NpgsqlDbType.Uuid).Value =
            (object?)representative.ChangedByAccountId ?? DBNull.Value;
        insert.Parameters.Add("changed_at", NpgsqlDbType.TimestampTz).Value =
            (object?)representative.ChangedAt ?? DBNull.Value;
        return await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static CustomerRepresentativeSnapshot ReadRepresentative(
        NpgsqlDataReader reader, Guid tenantId)
    {
        var program = reader.GetOrdinal("program_id");
        var changedBy = reader.GetOrdinal("changed_by_account_id");
        var changedAt = reader.GetOrdinal("changed_at");
        return new(
            reader.GetGuid(reader.GetOrdinal("id")),
            tenantId,
            reader.GetGuid(reader.GetOrdinal("organization_id")),
            reader.IsDBNull(program) ? null : reader.GetGuid(program),
            reader.GetGuid(reader.GetOrdinal("individual_id")),
            (CustomerRepresentativeAvailability)reader.GetInt32(reader.GetOrdinal("availability")),
            reader.GetInt64(reader.GetOrdinal("revision")),
            reader.GetGuid(reader.GetOrdinal("created_by_account_id")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            reader.IsDBNull(changedBy) ? null : reader.GetGuid(changedBy),
            reader.IsDBNull(changedAt) ? null : reader.GetFieldValue<DateTimeOffset>(changedAt));
    }

    private static void AddOptionalUuid(NpgsqlCommand command, string name, Guid? value) =>
        command.Parameters.Add(name, NpgsqlDbType.Uuid).Value = (object?)value ?? DBNull.Value;

    private static LinkCustomerRepresentativeResult LinkReplay(
        (string Fingerprint, CustomerRepresentativeSnapshot Snapshot) receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(LinkCustomerRepresentativeStatus.Replayed, receipt.Snapshot)
            : new(LinkCustomerRepresentativeStatus.IdempotencyKeyConflict, null);

    private static UnlinkCustomerRepresentativeResult UnlinkReplay(
        (string Fingerprint, CustomerRepresentativeSnapshot Snapshot) receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(UnlinkCustomerRepresentativeStatus.Replayed, receipt.Snapshot)
            : new(UnlinkCustomerRepresentativeStatus.IdempotencyKeyConflict, null);
}
