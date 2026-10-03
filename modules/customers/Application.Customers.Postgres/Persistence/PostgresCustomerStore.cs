using Application.Customers;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Customers.Postgres;

public sealed partial class PostgresCustomerStore(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null)
    : ICustomerStore, ICustomerIndividualStore
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<CreateCustomerOrganizationResult> CreateOrganizationAsync(
        TenantContext tenantContext, CustomerOrganizationIntent intent, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);

        var receipt = await FindOrganizationReceiptAsync(
            session, tenantContext, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            return OrganizationReplay(receipt.Value, intent.Fingerprint);
        }

        var organization = new CustomerOrganizationSnapshot(
            Guid.CreateVersion7(), tenantContext.TenantId, intent.DisplayName, _timeProvider.GetUtcNow());
        await using (var insert = session.CreateCommand(CustomerSql.InsertOrganization))
        {
            insert.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            insert.Parameters.AddWithValue("id", organization.OrganizationId);
            insert.Parameters.AddWithValue("account_id", tenantContext.AccountId);
            insert.Parameters.AddWithValue("display_name", organization.DisplayName);
            insert.Parameters.AddWithValue("created_at", organization.CreatedAt);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var insertReceipt = session.CreateCommand(CustomerSql.InsertOrganizationReceipt))
        {
            insertReceipt.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            insertReceipt.Parameters.AddWithValue("account_id", tenantContext.AccountId);
            insertReceipt.Parameters.AddWithValue("key", idempotencyKey);
            insertReceipt.Parameters.AddWithValue("fingerprint", intent.Fingerprint);
            insertReceipt.Parameters.AddWithValue("organization_id", organization.OrganizationId);
            insertReceipt.Parameters.AddWithValue("created_at", organization.CreatedAt);
            if (await insertReceipt.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(CreateCustomerOrganizationStatus.Created, organization);
            }
        }

        receipt = await FindOrganizationReceiptAsync(
            session, tenantContext, idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The customer organization receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return OrganizationReplay(receipt.Value, intent.Fingerprint);
    }

    public async Task<CreateCustomerProgramResult> CreateProgramAsync(
        TenantContext tenantContext, CustomerProgramIntent intent, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);

        var receipt = await FindProgramReceiptAsync(
            session, tenantContext, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            return ProgramReplay(receipt.Value, intent.Fingerprint);
        }

        if (await FindOrganizationAsync(session, tenantContext.TenantId, intent.OrganizationId,
                cancellationToken).ConfigureAwait(false) is null)
        {
            return new(CreateCustomerProgramStatus.ParentNotFound, null);
        }

        var program = new CustomerProgramSnapshot(
            Guid.CreateVersion7(), tenantContext.TenantId, intent.OrganizationId,
            intent.DisplayName, _timeProvider.GetUtcNow());
        await using (var insert = session.CreateCommand(CustomerSql.InsertProgram))
        {
            insert.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            insert.Parameters.AddWithValue("id", program.ProgramId);
            insert.Parameters.AddWithValue("organization_id", program.OrganizationId);
            insert.Parameters.AddWithValue("account_id", tenantContext.AccountId);
            insert.Parameters.AddWithValue("display_name", program.DisplayName);
            insert.Parameters.AddWithValue("created_at", program.CreatedAt);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var insertReceipt = session.CreateCommand(CustomerSql.InsertProgramReceipt))
        {
            insertReceipt.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            insertReceipt.Parameters.AddWithValue("account_id", tenantContext.AccountId);
            insertReceipt.Parameters.AddWithValue("key", idempotencyKey);
            insertReceipt.Parameters.AddWithValue("fingerprint", intent.Fingerprint);
            insertReceipt.Parameters.AddWithValue("program_id", program.ProgramId);
            insertReceipt.Parameters.AddWithValue("created_at", program.CreatedAt);
            if (await insertReceipt.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(CreateCustomerProgramStatus.Created, program);
            }
        }

        receipt = await FindProgramReceiptAsync(
            session, tenantContext, idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The customer program receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ProgramReplay(receipt.Value, intent.Fingerprint);
    }

    public async Task<CustomerOrganizationSnapshot?> FindOrganizationAsync(
        TenantContext tenantContext, Guid organizationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        return await FindOrganizationAsync(session, tenantContext.TenantId, organizationId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<CustomerProgramSnapshot?> FindProgramAsync(
        TenantContext tenantContext, Guid programId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        return await FindProgramAsync(session, tenantContext.TenantId, programId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<CustomerOrderContext?> ResolveOrderContextAsync(
        TenantContext tenantContext, Guid organizationId, Guid? programId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        if (await FindOrganizationAsync(session, tenantContext.TenantId, organizationId,
                cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        if (programId.HasValue)
        {
            var program = await FindProgramAsync(session, tenantContext.TenantId, programId.Value,
                cancellationToken).ConfigureAwait(false);
            if (program is null || program.OrganizationId != organizationId)
            {
                return null;
            }
        }

        return new CustomerOrderContext(organizationId, programId);
    }

    private static async Task<CustomerOrganizationSnapshot?> FindOrganizationAsync(
        CustomerTenantDbSession session, Guid tenantId, Guid organizationId, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindOrganization);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("organization_id", organizationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new(reader.GetGuid(0), tenantId, reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2))
            : null;
    }

    private static async Task<CustomerProgramSnapshot?> FindProgramAsync(
        CustomerTenantDbSession session, Guid tenantId, Guid programId, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindProgram);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("program_id", programId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new(reader.GetGuid(0), tenantId, reader.GetGuid(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3))
            : null;
    }

    private static async Task<(string Fingerprint, CustomerOrganizationSnapshot Snapshot)?> FindOrganizationReceiptAsync(
        CustomerTenantDbSession session, TenantContext context, string key, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindOrganizationReceipt);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("account_id", context.AccountId);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (reader.GetString(0), new CustomerOrganizationSnapshot(
                reader.GetGuid(1), context.TenantId, reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3)))
            : null;
    }

    private static async Task<(string Fingerprint, CustomerProgramSnapshot Snapshot)?> FindProgramReceiptAsync(
        CustomerTenantDbSession session, TenantContext context, string key, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindProgramReceipt);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("account_id", context.AccountId);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (reader.GetString(0), new CustomerProgramSnapshot(
                reader.GetGuid(1), context.TenantId, reader.GetGuid(2), reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4)))
            : null;
    }

    private static CreateCustomerOrganizationResult OrganizationReplay(
        (string Fingerprint, CustomerOrganizationSnapshot Snapshot) receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(CreateCustomerOrganizationStatus.Replayed, receipt.Snapshot)
            : new(CreateCustomerOrganizationStatus.IdempotencyKeyConflict, null);

    private static CreateCustomerProgramResult ProgramReplay(
        (string Fingerprint, CustomerProgramSnapshot Snapshot) receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(CreateCustomerProgramStatus.Replayed, receipt.Snapshot)
            : new(CreateCustomerProgramStatus.IdempotencyKeyConflict, null);

    public async Task<CustomerOrganizationPage> ListOrganizationsAsync(
        TenantContext tenantContext, ListCustomerOrganizationsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        RequireBoundedPageSize(request.Limit);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        await using var command = session.CreateCommand(CustomerSql.ListOrganizations);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.Add("after_at", NpgsqlDbType.TimestampTz).Value =
            (object?)request.After?.CreatedAt ?? DBNull.Value;
        command.Parameters.Add("after_id", NpgsqlDbType.Uuid).Value =
            (object?)request.After?.OrganizationId ?? DBNull.Value;
        command.Parameters.AddWithValue("limit", request.Limit + 1);
        var rows = new List<CustomerOrganizationSnapshot>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(reader.GetGuid(0), tenantContext.TenantId, reader.GetString(1),
                    reader.GetFieldValue<DateTimeOffset>(2)));
            }
        }

        var hasMore = rows.Count > request.Limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var last = hasMore ? rows[^1] : null;
        return new(rows, last is null ? null : new(last.CreatedAt, last.OrganizationId));
    }

    public async Task<CustomerProgramPage> ListProgramsAsync(
        TenantContext tenantContext, ListCustomerProgramsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        RequireBoundedPageSize(request.Limit);
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        await using var command = session.CreateCommand(CustomerSql.ListPrograms);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.AddWithValue("organization_id", request.OrganizationId);
        command.Parameters.Add("after_at", NpgsqlDbType.TimestampTz).Value =
            (object?)request.After?.CreatedAt ?? DBNull.Value;
        command.Parameters.Add("after_id", NpgsqlDbType.Uuid).Value =
            (object?)request.After?.ProgramId ?? DBNull.Value;
        command.Parameters.AddWithValue("limit", request.Limit + 1);
        var rows = new List<CustomerProgramSnapshot>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(reader.GetGuid(0), tenantContext.TenantId, request.OrganizationId,
                    reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2)));
            }
        }

        var hasMore = rows.Count > request.Limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var last = hasMore ? rows[^1] : null;
        return new(rows, last is null ? null : new(last.CreatedAt, last.ProgramId));
    }

    private static void RequireBoundedPageSize(int limit)
    {
        if (limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Customer page size must be from 1 to 50.");
        }
    }
}
