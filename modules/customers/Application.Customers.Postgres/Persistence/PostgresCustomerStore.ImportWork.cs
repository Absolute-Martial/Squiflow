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
        // Every branch states its own terminal status. The retained row is the decision
        // record an auditor reads, so a cause may not borrow another cause's status:
        // `Rejected` means an operator declined this row, `Failed` means a system fault
        // that needs operator review. There is deliberately no shared default.
        var status = CustomerImportRowStatus.Failed;
        Guid? customerId = null;
        string? errorCode = null;
        await using (var savepoint = session.CreateCommand(CustomerSql.ImportSavepoint))
            await savepoint.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            switch (row.Decision)
            {
                case CustomerImportDecisionKind.MapToExisting:
                    // The accepted decision is immutable, so a target consolidated away
                    // after acceptance is a system fault, not an operator rejection.
                    if (!row.MappingCustomerId.HasValue || !await CanonicalImportTargetAsync(session, currentContext, row.MappingCustomerId.Value, cancellationToken).ConfigureAwait(false))
                        errorCode = "mapping_target_unavailable";
                    else { status = CustomerImportRowStatus.MappedToExisting; customerId = row.MappingCustomerId; }
                    break;
                case CustomerImportDecisionKind.CreateNew:
                    var signals = CustomerDuplicateSignals.Create(row.Intent.DisplayName, row.Intent.Email, row.Intent.Phone, row.Intent.ExternalRegistrationId);
                    if (!row.RequiresDecision && await HasImportDuplicateAsync(session, claim.TenantId, signals, cancellationToken).ConfigureAwait(false))
                    { status = CustomerImportRowStatus.Rejected; errorCode = "new_duplicate_requires_new_plan"; }
                    else
                    {
                        customerId = row.RowId ?? throw new InvalidOperationException("Import row has no stable identity.");
                        await InsertImportedCustomerAsync(session, currentContext, customerId.Value, row.Intent, signals, cancellationToken).ConfigureAwait(false);
                        status = CustomerImportRowStatus.Imported;
                    }
                    break;
                case CustomerImportDecisionKind.Reject:
                    status = CustomerImportRowStatus.Rejected; errorCode = "operator_rejected"; break;
                default: throw new InvalidOperationException("Import decision is unsupported.");
            }
        }
        catch (PostgresException exception) when (exception.SqlState is not (PostgresErrorCodes.QueryCanceled or PostgresErrorCodes.AdminShutdown))
        {
            await using var rollback = session.CreateCommand(CustomerSql.ImportRollbackSavepoint);
            await rollback.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            status = CustomerImportRowStatus.Failed;
            customerId = null;
            errorCode = "row_processing_failed"; // Never persist the provider exception or source PII.
        }
        await using (var complete = session.CreateCommand(CustomerSql.CompleteImportRow))
        {
            ImportParameters(complete, claim.TenantId, claim.ImportId);
            complete.Parameters.AddWithValue("row_number", row.RowNumber);
            complete.Parameters.AddWithValue("status", (int)status);
            AddNullableUuid(complete, "customer_id", customerId);
            AddNullableText(complete, "error_code", errorCode);
            AddNullableText(complete, "error_message", errorCode is null ? null : "Import row requires review.");
            await complete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return CustomerImportRowExecutionStatus.Processed;
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
