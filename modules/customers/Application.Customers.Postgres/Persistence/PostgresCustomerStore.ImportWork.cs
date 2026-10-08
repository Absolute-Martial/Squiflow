using Application.Customers;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Customers.Postgres;

public sealed partial class PostgresCustomerStore
{
    public async Task<CustomerImportClaim?> ClaimImportAsync(Guid tenantId, Guid workerId, TimeSpan lease, CancellationToken cancellationToken)
    {
        if (workerId == Guid.Empty || lease < TimeSpan.FromSeconds(10) || lease > RunCustomerImportBatch.ClaimLease)
            throw new CustomerValidationException("claim_invalid", "Worker claim parameters are invalid.");
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, tenantId, cancellationToken).ConfigureAwait(false);
        await using var command = session.CreateCommand(CustomerSql.ClaimImport);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("worker_id", workerId);
        command.Parameters.AddWithValue("lease", lease);
        CustomerImportClaim? claim;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            claim = !await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? null
                : new(tenantId, reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetInt64(3),
                    workerId, reader.GetInt64(4), reader.GetFieldValue<DateTimeOffset>(5));
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return claim;
    }

    public async Task<CustomerImportRowExecutionStatus> ProcessNextImportRowAsync(CustomerImportClaim claim, TenantContext currentContext, CancellationToken cancellationToken)
    {
        if (claim.TenantId != currentContext.TenantId || claim.AccountId != currentContext.AccountId)
            throw new CustomerImportAuthorityException();
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, claim.TenantId, cancellationToken).ConfigureAwait(false);
        await using (var gate = session.CreateCommand(CustomerSql.LockImportClaim))
        {
            ClaimParameters(gate, claim);
            if (await gate.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null) throw new CustomerImportClaimLostException();
        }
        CustomerImportRowPlan? row;
        await using (var next = session.CreateCommand(CustomerSql.NextImportRow))
        {
            ImportParameters(next, claim.TenantId, claim.ImportId);
            await using var reader = await next.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            row = await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadImportRow(reader) : null;
        }
        if (row is null)
        {
            var summary = await ImportSummaryAsync(session, currentContext, claim.ImportId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Claimed import disappeared.");
            return summary.Pending + summary.Failed == 0 ? CustomerImportRowExecutionStatus.Complete : CustomerImportRowExecutionStatus.WaitingForRetry;
        }
        if (row.Intent is null || row.Decision is null) throw new InvalidOperationException("Accepted import row has no validated decision.");
        row = row with { Intent = ValidateImportIntent(row.Intent) };
        var status = CustomerImportRowStatus.Rejected;
        Guid? customerId = null;
        string? errorCode = null;
        await using (var savepoint = session.CreateCommand(CustomerSql.ImportSavepoint))
            await savepoint.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            switch (row.Decision)
            {
                case CustomerImportDecisionKind.MapToExisting:
                    if (!row.MappingCustomerId.HasValue || !await CanonicalImportTargetAsync(session, currentContext, row.MappingCustomerId.Value, cancellationToken).ConfigureAwait(false))
                        errorCode = "mapping_target_unavailable";
                    else { status = CustomerImportRowStatus.MappedToExisting; customerId = row.MappingCustomerId; }
                    break;
                case CustomerImportDecisionKind.CreateNew:
                    var signals = CustomerDuplicateSignals.Create(row.Intent.DisplayName, row.Intent.Email, row.Intent.Phone, row.Intent.ExternalRegistrationId);
                    if (!row.RequiresDecision && await HasImportDuplicateAsync(session, claim.TenantId, signals, cancellationToken).ConfigureAwait(false))
                        errorCode = "new_duplicate_requires_new_plan";
                    else
                    {
                        customerId = row.RowId ?? throw new InvalidOperationException("Import row has no stable identity.");
                        await InsertImportedCustomerAsync(session, currentContext, customerId.Value, row.Intent, signals, cancellationToken).ConfigureAwait(false);
                        status = CustomerImportRowStatus.Imported;
                    }
                    break;
                case CustomerImportDecisionKind.Reject: errorCode = "operator_rejected"; break;
                default: throw new InvalidOperationException("Import decision is unsupported.");
            }
        }
        catch (PostgresException exception) when (exception.SqlState is not (PostgresErrorCodes.QueryCanceled or PostgresErrorCodes.AdminShutdown)
            && !IsImportInfrastructureFault(exception.SqlState))
        {
            await using var rollback = session.CreateCommand(CustomerSql.ImportRollbackSavepoint);
            await rollback.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            status = CustomerImportRowStatus.Failed;
            customerId = null;
            errorCode = "row_processing_failed"; // Never persist the provider exception or source PII.
        }
        try
        {
            await using (var complete = session.CreateCommand(CustomerSql.CompleteImportRow))
            {
                ClaimParameters(complete, claim);
                complete.Parameters.AddWithValue("import_id", claim.ImportId);
                complete.Parameters.AddWithValue("row_number", row.RowNumber);
                complete.Parameters.AddWithValue("status", (int)status);
                AddNullableUuid(complete, "customer_id", customerId);
                AddNullableText(complete, "error_code", errorCode);
                AddNullableText(complete, "error_message", errorCode is null ? null : "Import row requires review.");
                if (1 != await complete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false))
                    throw new CustomerImportClaimLostException();
            }
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState is not (PostgresErrorCodes.QueryCanceled or PostgresErrorCodes.AdminShutdown))
        {
            // A failed completion or commit cannot record its own outcome: the transaction is over.
            // Record the bounded row-failure attempt in its own fenced transaction so a commit-side
            // fault can never re-admit the same row forever without consuming an attempt.
            await RecordCommitSideRowFailureAsync(claim, row.RowNumber).ConfigureAwait(false);
            throw;
        }
        return CustomerImportRowExecutionStatus.Processed;
    }

    // Class 08 connection exception plus the shutdown/serialization/deadlock/connection-slot states:
    // the provider, not this row's accepted intent, caused the fault. Consuming the row's bounded
    // attempt budget for them would permanently fail a still-valid row after a transient outage, so
    // they leave the transaction rolled back with no attempt spent and stay claimable.
    private static bool IsImportInfrastructureFault(string sqlState) =>
        sqlState.StartsWith("08", StringComparison.Ordinal)
        || sqlState is PostgresErrorCodes.AdminShutdown or PostgresErrorCodes.CrashShutdown
            or PostgresErrorCodes.CannotConnectNow or PostgresErrorCodes.TooManyConnections
            or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;

    // Best effort on its own short budget: the already-recorded or unreachable database must not
    // replace the surfaced fault, and a terminal row (the commit may have succeeded) matches nothing.
    private async Task RecordCommitSideRowFailureAsync(CustomerImportClaim claim, int rowNumber)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, claim.TenantId, budget.Token).ConfigureAwait(false);
            await using var fail = session.CreateCommand(CustomerSql.CompleteImportRow);
            ClaimParameters(fail, claim);
            fail.Parameters.AddWithValue("import_id", claim.ImportId);
            fail.Parameters.AddWithValue("row_number", rowNumber);
            fail.Parameters.AddWithValue("status", (int)CustomerImportRowStatus.Failed);
            AddNullableUuid(fail, "customer_id", null);
            AddNullableText(fail, "error_code", "row_processing_failed"); // Never persist the provider exception or source PII.
            AddNullableText(fail, "error_message", "Import row requires review.");
            await fail.ExecuteNonQueryAsync(budget.Token).ConfigureAwait(false);
            await session.CommitAsync(budget.Token).ConfigureAwait(false);
        }
        catch (NpgsqlException) { /* The surfaced fault stays the authoritative failure. */ }
        catch (OperationCanceledException) { /* Only this recovery's own budget can cancel it. */ }
    }

    public async Task ReleaseImportClaimAsync(CustomerImportClaim claim, bool authorityDenied, CancellationToken cancellationToken)
    {
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, claim.TenantId, cancellationToken).ConfigureAwait(false);
        await using var release = session.CreateCommand(CustomerSql.ReleaseImportClaim);
        ClaimParameters(release, claim);
        release.Parameters.AddWithValue("denied", authorityDenied);
        await release.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ClaimParameters(NpgsqlCommand command, CustomerImportClaim claim)
    {
        command.Parameters.AddWithValue("tenant_id", claim.TenantId);
        command.Parameters.AddWithValue("work_id", claim.WorkId);
        command.Parameters.AddWithValue("worker_id", claim.WorkerId);
        command.Parameters.AddWithValue("generation", claim.Generation);
    }

    private static async Task<bool> HasImportDuplicateAsync(CustomerTenantDbSession session, Guid tenantId,
        CustomerDuplicateSignals signals, CancellationToken ct)
    {
        await using var command = session.CreateCommand(CustomerSql.HasCurrentImportDuplicate);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        AddNullableText(command, "name", signals.NormalizedName);
        AddNullableText(command, "email", signals.NormalizedEmail);
        AddNullableText(command, "phone", signals.NormalizedPhone);
        AddNullableText(command, "external_id", signals.NormalizedExternalRegistrationId);
        return true.Equals(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    private async Task InsertImportedCustomerAsync(CustomerTenantDbSession session, TenantContext context, Guid id,
        CustomerIndividualIntent intent, CustomerDuplicateSignals signals, CancellationToken ct)
    {
        await using var insert = session.CreateCommand(CustomerSql.InsertIndividualWithSignals);
        insert.Parameters.AddWithValue("tenant_id", context.TenantId);
        insert.Parameters.AddWithValue("id", id);
        insert.Parameters.AddWithValue("display_name", intent.DisplayName);
        insert.Parameters.AddWithValue("account_id", context.AccountId);
        insert.Parameters.AddWithValue("created_at", CurrentStorageTime());
        insert.Parameters.AddWithValue("customer_type", intent.CustomerType);
        AddNullableText(insert, "external_registration_id", intent.ExternalRegistrationId);
        AddNullableText(insert, "email", intent.Email);
        AddNullableText(insert, "phone", intent.Phone);
        AddNullableText(insert, "normalized_name", signals.NormalizedName);
        AddNullableText(insert, "normalized_email", signals.NormalizedEmail);
        AddNullableText(insert, "normalized_phone", signals.NormalizedPhone);
        AddNullableText(insert, "normalized_external_registration_id", signals.NormalizedExternalRegistrationId);
        AddNullableText(insert, "address_line1", intent.AddressLine1);
        AddNullableText(insert, "address_line2", intent.AddressLine2);
        AddNullableText(insert, "city", intent.City);
        AddNullableText(insert, "notes", intent.Notes);
        await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
