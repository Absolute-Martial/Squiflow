using System.Globalization;
using System.Text.Json;
using Application.Customers;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Customers.Postgres;

public sealed partial class PostgresCustomerStore
{
    public async Task<CreateCustomerImportResult> CreateImportPlanAsync(TenantContext context,
        CustomerImportPlan plan, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.ContractVersion != CustomerImportCsv.ContractVersion || plan.Rows.Count > CustomerImportCsv.MaxRows
            || plan.ByteLength is < 0 or > CustomerImportCsv.MaxBytes || plan.ImportId == Guid.Empty
            || plan.Rows.Select(row => row.RowNumber).Distinct().Count() != plan.Rows.Count
            || plan.ManifestHash.Length != 64 || plan.ManifestHash.Any(ch => !char.IsAsciiHexDigitLower(ch))
            || plan.Fingerprint != CustomerIdentityNormalization.Fingerprint("customer-import", plan.ContractVersion,
                plan.ManifestHash, string.Join('|', plan.Rows.Select(row => $"{row.RowNumber}:{row.SourceRowHash}:{(int)row.Status}"))))
            throw new CustomerValidationException("import_plan_invalid", "Import plan is invalid.");
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindImportReceiptAsync(session, context, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null) return await ImportReplayAsync(session, context, receipt.Value, plan, cancellationToken).ConfigureAwait(false);
        await using (var insert = session.CreateCommand(CustomerSql.InsertImport))
        {
            ImportParameters(insert, context.TenantId, plan.ImportId);
            insert.Parameters.AddWithValue("account_id", context.AccountId);
            insert.Parameters.AddWithValue("key", idempotencyKey);
            insert.Parameters.AddWithValue("fingerprint", plan.Fingerprint);
            insert.Parameters.AddWithValue("contract_version", plan.ContractVersion);
            insert.Parameters.AddWithValue("manifest_hash", plan.ManifestHash);
            insert.Parameters.AddWithValue("byte_length", plan.ByteLength);
            insert.Parameters.AddWithValue("row_count", plan.Rows.Count);
            insert.Parameters.AddWithValue("created_at", CurrentStorageTime());
            if (await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 0)
            {
                receipt = await FindImportReceiptAsync(session, context, idempotencyKey, cancellationToken).ConfigureAwait(false);
                return receipt is null ? new(plan.ImportId, false, false, true, plan)
                    : await ImportReplayAsync(session, context, receipt.Value, plan, cancellationToken).ConfigureAwait(false);
            }
        }
        foreach (var row in plan.Rows)
        {
            if (row.RowNumber < 2 || row.SourceRowHash.Length != 64 || row.SourceRowHash.Any(ch => !char.IsAsciiHexDigitLower(ch))
                || row.Status is not (CustomerImportRowStatus.Pending or CustomerImportRowStatus.Rejected))
                throw new CustomerValidationException("import_plan_invalid", "Import row is invalid.");
            if ((row.Status == CustomerImportRowStatus.Pending) != (row.Intent is not null))
                throw new CustomerValidationException("import_plan_invalid", "Import row validation state is inconsistent.");
            if (row.Intent is { } intent && ValidateImportIntent(intent) != intent)
                throw new CustomerValidationException("import_plan_invalid", "Import intent must be normalized and validated.");
        }
        await using (var insertRows = session.CreateCommand(CustomerSql.InsertImportRowsBatch))
        {
            ImportParameters(insertRows, context.TenantId, plan.ImportId);
            var retainedRows = plan.Rows.Select(row =>
            {
                var signals = row.Intent is null ? null : CustomerDuplicateSignals.Create(row.Intent.DisplayName, row.Intent.Email,
                    row.Intent.Phone, row.Intent.ExternalRegistrationId);
                return new
                {
                    row_number = row.RowNumber,
                    source_row_hash = row.SourceRowHash,
                    row_id = Guid.CreateVersion7(),
                    name = row.Intent?.DisplayName,
                    customer_type = row.Intent?.CustomerType,
                    external_id = row.Intent?.ExternalRegistrationId,
                    email = row.Intent?.Email,
                    phone = row.Intent?.Phone,
                    address_line1 = row.Intent?.AddressLine1,
                    address_line2 = row.Intent?.AddressLine2,
                    city = row.Intent?.City,
                    notes = row.Intent?.Notes,
                    status = (int)row.Status,
                    error_code = row.ErrorCode,
                    error_message = row.ErrorMessage,
                    name_signal = signals?.NormalizedName,
                    email_signal = signals?.NormalizedEmail,
                    phone_signal = signals?.NormalizedPhone,
                    external_id_signal = signals?.NormalizedExternalRegistrationId
                };
            });
            insertRows.Parameters.Add("rows", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(retainedRows);
            await insertRows.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var validate = session.CreateCommand(CustomerSql.ValidateImportDuplicates))
        {
            ImportParameters(validate, context.TenantId, plan.ImportId);
            await validate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        var validated = await LoadImportPlanAsync(session, context, plan.ImportId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Retained import plan disappeared.");
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(plan.ImportId, true, false, false, validated);
    }

    public async Task<ExecuteCustomerImportResult> ExecuteImportAsync(TenantContext context,
        ExecuteCustomerImportRequest request, string idempotencyKey, long authorizationRevision, CancellationToken cancellationToken)
    {
        if (authorizationRevision < 1) throw new CustomerValidationException("authority_revision_invalid", "Current authority is required.");
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        await using (var gate = session.CreateCommand(CustomerSql.LockImport))
        {
            ImportParameters(gate, context.TenantId, request.ImportId);
            if (await gate.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                throw new CustomerValidationException("import_not_found", "Import not found in this tenant.");
        }
        var existing = await FindImportWorkAsync(session, context, request.ImportId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            await using var revalidate = session.CreateCommand(CustomerSql.HasUnplannedImportDuplicate);
            ImportParameters(revalidate, context.TenantId, request.ImportId);
            if (true.Equals(await revalidate.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)))
                throw new CustomerValidationException("plan_outdated_requires_replan", "New duplicate evidence requires a new validated plan.");
        }
        var plan = await LoadImportPlanAsync(session, context, request.ImportId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Locked import disappeared.");
        if (plan.Rows.Any(row => row.DuplicateEvidence == "legacy_plan_requires_replan"))
            throw new CustomerValidationException("legacy_plan_requires_replan", "Legacy pending import requires a new deliberate plan.");
        foreach (var row in plan.Rows.Where(row => row.Intent is not null)) _ = ValidateImportIntent(row.Intent!);
        if ((request.Decisions?.Count ?? 0) + (request.ExplicitMappings?.Count ?? 0) > CustomerImportCsv.MaxRows)
            throw new CustomerValidationException("decision_invalid", "Import decisions exceed the row limit.");
        var rowByNumber = plan.Rows.ToDictionary(row => row.RowNumber);
        var decisions = (request.Decisions ?? []).ToList();
        if (request.ExplicitMappings is { } mappings)
            foreach (var mapping in mappings)
            {
                var row = rowByNumber.GetValueOrDefault(mapping.Key)
                    ?? throw new CustomerValidationException("mapping_invalid", "Mapping references no import row.");
                decisions.Add(new(row.RowNumber, row.SourceRowHash, CustomerImportDecisionKind.MapToExisting, mapping.Value));
            }
        if (decisions.Count > CustomerImportCsv.MaxRows || decisions.Select(item => item.RowNumber).Distinct().Count() != decisions.Count)
            throw new CustomerValidationException("decision_invalid", "Each import row may be decided once.");
        var decisionByNumber = decisions.ToDictionary(decision => decision.RowNumber);
        foreach (var decision in decisions)
        {
            var row = rowByNumber.GetValueOrDefault(decision.RowNumber);
            if (row?.Intent is null || row.SourceRowHash != decision.SourceRowHash || !Enum.IsDefined(decision.Kind)
                || (decision.Kind == CustomerImportDecisionKind.MapToExisting) != decision.CustomerId.HasValue
                || decision.CustomerId == Guid.Empty)
                throw new CustomerValidationException("decision_invalid", "Decision must bind a validated row and its exact hash.");
            if (existing is null && decision.CustomerId.HasValue
                && !await CanonicalImportTargetAsync(session, context, decision.CustomerId.Value, cancellationToken).ConfigureAwait(false))
                throw new CustomerValidationException("mapping_target_not_found", "Mapping requires an active canonical customer in this tenant.");
        }
        foreach (var row in plan.Rows.Where(row => row.Intent is not null))
            if (row.RequiresDecision && !decisionByNumber.ContainsKey(row.RowNumber))
                throw new CustomerValidationException("duplicate_decision_required", "Every possible duplicate requires an explicit decision.");
        var fingerprint = CustomerIdentityNormalization.Fingerprint("customer-import-accept", plan.Fingerprint,
            string.Join('|', decisions.OrderBy(item => item.RowNumber).Select(item =>
                string.Create(CultureInfo.InvariantCulture, $"{item.RowNumber}:{item.SourceRowHash}:{(int)item.Kind}:{item.CustomerId:N}"))));
        if (existing is not null)
        {
            if (existing.Value.Fingerprint != fingerprint || existing.Value.AccountId != context.AccountId)
                throw new CustomerValidationException("idempotency_key_conflict", "Accepted import decisions are immutable.");
            await using var resume = session.CreateCommand(CustomerSql.ResumeImportWork);
            ImportParameters(resume, context.TenantId, request.ImportId);
            resume.Parameters.AddWithValue("account_id", context.AccountId);
            resume.Parameters.AddWithValue("authority_revision", authorizationRevision);
            await resume.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await using (var set = session.CreateCommand(CustomerSql.SetImportDecisionsBatch))
            {
                ImportParameters(set, context.TenantId, request.ImportId);
                var retainedDecisions = plan.Rows.Where(row => row.Intent is not null).Select(row =>
                {
                    var decision = decisionByNumber.GetValueOrDefault(row.RowNumber)
                        ?? new(row.RowNumber, row.SourceRowHash, CustomerImportDecisionKind.CreateNew);
                    return new { row_number = row.RowNumber, decision = (int)decision.Kind, customer_id = decision.CustomerId };
                });
                set.Parameters.Add("decisions", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(retainedDecisions);
                await set.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await using var insert = session.CreateCommand(CustomerSql.InsertImportWork);
            ImportParameters(insert, context.TenantId, request.ImportId);
            insert.Parameters.AddWithValue("work_id", Guid.CreateVersion7());
            insert.Parameters.AddWithValue("account_id", context.AccountId);
            insert.Parameters.AddWithValue("key", idempotencyKey);
            insert.Parameters.AddWithValue("fingerprint", fingerprint);
            insert.Parameters.AddWithValue("created_at", CurrentStorageTime());
            insert.Parameters.AddWithValue("authority_revision", authorizationRevision);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var completeEmpty = session.CreateCommand(CustomerSql.CompleteEmptyImport))
        {
            ImportParameters(completeEmpty, context.TenantId, request.ImportId);
            await completeEmpty.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        var summary = await ImportSummaryAsync(session, context, request.ImportId, cancellationToken).ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(summary!.Work!, []); // Durable acceptance only; no customer effect in HTTP.
    }

    public async Task<CustomerImportSummary?> ReadImportSummaryAsync(TenantContext context, Guid importId, CancellationToken cancellationToken)
    {
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        return await ImportSummaryAsync(session, context, importId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CustomerImportRowPage> ReadImportRowsPageAsync(TenantContext context, Guid importId,
        int afterRowNumber, int limit, CancellationToken cancellationToken)
    {
        RequireBoundedPageSize(limit);
        if (afterRowNumber < 0) throw new CustomerValidationException("cursor_invalid", "Row cursor cannot be negative.");
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        await RequireSupportedImportContractAsync(session, context, importId, cancellationToken).ConfigureAwait(false);
        var rows = await ReadImportRowsAsync(session, context, importId, afterRowNumber, limit + 1, cancellationToken).ConfigureAwait(false);
        return new(rows.Take(limit).ToArray(), rows.Count > limit ? rows[limit - 1].RowNumber : null);
    }

    private static async Task<CreateCustomerImportResult> ImportReplayAsync(CustomerTenantDbSession session,
        TenantContext context, (Guid ImportId, string Fingerprint) receipt, CustomerImportPlan plan, CancellationToken ct)
    {
        var retained = await LoadImportPlanAsync(session, context, receipt.ImportId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Import receipt has no plan.");
        if (retained.ManifestHash != plan.ManifestHash || retained.ContractVersion != plan.ContractVersion)
            return new(plan.ImportId, false, false, true, plan);
        return new(receipt.ImportId, false, true, false, retained);
    }

    private static async Task<CustomerImportSummary?> ImportSummaryAsync(CustomerTenantDbSession session,
        TenantContext context, Guid importId, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.ReadImportSummary);
        ImportParameters(command, context.TenantId, importId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        RequireSupportedContract(reader.GetString(0));
        var work = reader.IsDBNull(8) ? null : new CustomerImportWorkSnapshot(reader.GetGuid(8), importId,
            (CustomerImportWorkStatus)reader.GetInt32(9), reader.GetInt32(4) + reader.GetInt32(5) + reader.GetInt32(6),
            reader.GetInt32(3) + reader.GetInt32(7), reader.GetFieldValue<DateTimeOffset>(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11), ReadOptional(reader, 12));
        return new(importId, reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4),
            reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), work);
    }

    private static async Task<(Guid ImportId, string Fingerprint)?> FindImportReceiptAsync(CustomerTenantDbSession session,
        TenantContext context, string key, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.FindImportReceipt);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("account_id", context.AccountId);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? (reader.GetGuid(0), reader.GetString(1)) : null;
    }

    private static async Task<CustomerImportRow?> FindImportAsync(CustomerTenantDbSession session, TenantContext context, Guid importId, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.FindImport);
        ImportParameters(command, context.TenantId, importId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return !await reader.ReadAsync(ct).ConfigureAwait(false) ? null : new()
        {
            TenantId = context.TenantId,
            ImportId = reader.GetGuid(0),
            AccountId = reader.GetGuid(1),
            IdempotencyKey = reader.GetString(2),
            Fingerprint = reader.GetString(3),
            ContractVersion = reader.GetString(4),
            ManifestHash = reader.GetString(5),
            ByteLength = reader.GetInt64(6),
            RowCount = reader.GetInt32(7),
            CreatedByAccountId = reader.GetGuid(8),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(9),
        };
    }

    private static async Task<CustomerImportPlan?> LoadImportPlanAsync(CustomerTenantDbSession session, TenantContext context, Guid importId, CancellationToken ct)
    {
        var import = await FindImportAsync(session, context, importId, ct).ConfigureAwait(false);
        if (import is null) return null;
        RequireSupportedContract(import.ContractVersion);
        return new(importId, import.ContractVersion, import.ManifestHash, import.ByteLength,
            await ReadImportRowsAsync(session, context, importId, 0, CustomerImportCsv.MaxRows, ct).ConfigureAwait(false), import.Fingerprint);
    }

    // The database CHECK pins the contract version, so this is the typed application guard for a
    // plan whose rows, intents or fingerprint cannot be interpreted under current semantics.
    private static void RequireSupportedContract(string contractVersion)
    {
        if (!string.Equals(contractVersion, CustomerImportCsv.ContractVersion, StringComparison.Ordinal))
            throw new CustomerImportContractUnsupportedException(contractVersion);
    }

    private static async Task RequireSupportedImportContractAsync(CustomerTenantDbSession session,
        TenantContext context, Guid importId, CancellationToken ct)
    {
        var import = await FindImportAsync(session, context, importId, ct).ConfigureAwait(false);
        if (import is not null) RequireSupportedContract(import.ContractVersion);
    }

    private static async Task<(CustomerImportWorkSnapshot Snapshot, string Fingerprint, Guid AccountId)?> FindImportWorkAsync(
        CustomerTenantDbSession session, TenantContext context, Guid importId, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.FindImportWork);
        ImportParameters(command, context.TenantId, importId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        return (new(reader.GetGuid(0), importId, (CustomerImportWorkStatus)reader.GetInt32(2), 0, 0,
            reader.GetFieldValue<DateTimeOffset>(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4), ReadOptional(reader, 5)),
            reader.GetString(6), reader.GetGuid(8));
    }

    private static async Task<List<CustomerImportRowPlan>> ReadImportRowsAsync(CustomerTenantDbSession session,
        TenantContext context, Guid importId, int after, int limit, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.FindImportRows);
        ImportParameters(command, context.TenantId, importId);
        command.Parameters.AddWithValue("after_row", after);
        command.Parameters.AddWithValue("limit", limit);
        var rows = new List<CustomerImportRowPlan>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) rows.Add(ReadImportRow(reader));
        return rows;
    }

    private static CustomerImportRowPlan ReadImportRow(NpgsqlDataReader reader)
    {
        CustomerIndividualIntent? intent = null;
        if (!reader.IsDBNull(2))
        {
            var name = reader.GetString(2); var email = ReadOptional(reader, 5); var phone = ReadOptional(reader, 6);
            var type = ReadOptional(reader, 3) ?? "individual"; var external = ReadOptional(reader, 4);
            var address1 = ReadOptional(reader, 7); var address2 = ReadOptional(reader, 8); var city = ReadOptional(reader, 9); var notes = ReadOptional(reader, 10);
            // Retained historical fields are not reinterpreted by today's stricter phone validator.
            // Execution revalidates current intent; legacy pending plans are explicitly blocked.
            intent = new(name, email, phone, type, external, address1, address2, city, notes,
                CustomerIdentityNormalization.Fingerprint("individual", name, email ?? "", phone ?? "", type,
                    external ?? "", address1 ?? "", address2 ?? "", city ?? "", notes ?? ""));
        }
        return new(reader.GetInt32(0), reader.GetString(1), (CustomerImportRowStatus)reader.GetInt32(11), intent,
            ReadOptional(reader, 12), ReadOptional(reader, 13), reader.GetGuid(16), reader.IsDBNull(14) ? null : reader.GetGuid(14),
            reader.GetBoolean(17), ReadOptional(reader, 18), reader.IsDBNull(19) ? null : (CustomerImportDecisionKind)reader.GetInt32(19),
            reader.IsDBNull(20) ? null : reader.GetGuid(20), reader.GetInt32(21));
    }

    private static async Task<bool> CanonicalImportTargetAsync(CustomerTenantDbSession session, TenantContext context, Guid id, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.FindCanonicalImportTarget);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is Guid;
    }

    private static void ImportParameters(NpgsqlCommand command, Guid tenantId, Guid importId)
    {
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("import_id", importId);
    }

    private static string? ReadOptional(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static CustomerIndividualIntent ValidateImportIntent(CustomerIndividualIntent intent) =>
        CustomerIndividualIntent.Create(new(intent.DisplayName, intent.Email, intent.Phone, intent.ExternalRegistrationId,
            intent.AddressLine1, intent.AddressLine2, intent.City, intent.Notes, intent.CustomerType));
}
