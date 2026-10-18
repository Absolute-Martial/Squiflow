using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Tenancy;
using Npgsql;

namespace Application.Invoices.Postgres;

public sealed partial class PostgresInvoiceIssueStore(NpgsqlDataSource source, IInvoiceIssueAuthority issueAuthority,
    IInvoiceViewAuthority viewAuthority, IInvoiceIssueTransactionalAuthority transactionalAuthority,
    IInvoiceIssueTransactionalSourceReader sourceReader) : IInvoiceIssueStore, IInvoiceIssuedFactsReader
{
    private const int MaximumDebtorBytes = 8192;
    private const int MaximumReceiptBytes = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
    };

    public async Task<InvoiceIssueReceiptResult> FindReceiptAsync(TenantContext tenantContext, string idempotencyKey,
        string fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        var key = InvoiceIssueIntent.NormalizeIdempotencyKey(idempotencyKey);
        ValidateFingerprint(fingerprint);
        try
        {
            var admission = await issueAuthority.CheckAsync(tenantContext, cancellationToken).ConfigureAwait(false);
            if (admission != InvoiceIssueAuthorityStatus.Allowed) return ReceiptDenial(admission);
            await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await SetTenantAsync(connection, transaction, tenantContext, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, "LockReceipt", cancellationToken,
                ("tenant", tenantContext.TenantId), ("actor", tenantContext.AccountId), ("key", key)).ConfigureAwait(false);
            var current = await transactionalAuthority.CheckAsync(connection, transaction, tenantContext, cancellationToken).ConfigureAwait(false);
            if (current != InvoiceIssueAuthorityStatus.Allowed) return ReceiptDenial(current);
            var receipt = await ReadReceiptAsync(connection, transaction, tenantContext, key, fingerprint, cancellationToken).ConfigureAwait(false);
            current = await transactionalAuthority.CheckAsync(connection, transaction, tenantContext, cancellationToken).ConfigureAwait(false);
            return current == InvoiceIssueAuthorityStatus.Allowed ? receipt : ReceiptDenial(current);
        }
        catch (InvoiceHistoryException) { return new(InvoiceIssueReceiptStatus.HistoricalReceiptInvalid); }
        catch (NpgsqlException) { return new(InvoiceIssueReceiptStatus.Unavailable); }
    }

    public async Task<InvoiceIssueCommitResult> CommitAsync(TenantContext tenantContext, InvoiceIssueCandidate candidate,
        string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(candidate);
        var key = InvoiceIssueIntent.NormalizeIdempotencyKey(idempotencyKey);
        ValidateFingerprint(fingerprint);
        var customerContext = candidate.SourceCustomerContext;
        if (customerContext is null || candidate.TenantId != tenantContext.TenantId || candidate.IssuedByAccountId != tenantContext.AccountId ||
            Fingerprint(candidate.SourceOrderId, candidate.SourceOrderRevision, candidate.Debtor) != fingerprint)
            return new(InvoiceIssueCommitStatus.InvalidSource);
        var committing = false;
        try
        {
            var admission = await issueAuthority.CheckAsync(tenantContext, cancellationToken).ConfigureAwait(false);
            if (admission != InvoiceIssueAuthorityStatus.Allowed) return CommitDenial(admission);
            await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await SetTenantAsync(connection, transaction, tenantContext, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, "LockReceipt", cancellationToken,
                ("tenant", tenantContext.TenantId), ("actor", tenantContext.AccountId), ("key", key)).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, "LockOrder", cancellationToken,
                ("tenant", tenantContext.TenantId), ("order", candidate.SourceOrderId)).ConfigureAwait(false);
            var current = await transactionalAuthority.CheckAsync(connection, transaction, tenantContext, cancellationToken).ConfigureAwait(false);
            if (current != InvoiceIssueAuthorityStatus.Allowed) return CommitDenial(current);
            var receipt = await ReadReceiptAsync(connection, transaction, tenantContext, key, fingerprint, cancellationToken).ConfigureAwait(false);
            if (receipt.Status != InvoiceIssueReceiptStatus.Missing)
            {
                current = await transactionalAuthority.CheckAsync(connection, transaction, tenantContext, cancellationToken).ConfigureAwait(false);
                return current == InvoiceIssueAuthorityStatus.Allowed ? MapReceipt(receipt) : CommitDenial(current);
            }
            if (await FindOrderInvoiceIdAsync(connection, transaction, tenantContext, candidate.SourceOrderId, cancellationToken).ConfigureAwait(false) is not null)
                return new(InvoiceIssueCommitStatus.OrderAlreadyInvoiced);
            var observed = await sourceReader.ReadOrderAsync(connection, transaction, tenantContext, candidate.SourceOrderId, cancellationToken).ConfigureAwait(false);
            if (observed.Status == InvoiceSourceOrderReadStatus.Unavailable) return new(InvoiceIssueCommitStatus.Unavailable);
            if (observed.Status != InvoiceSourceOrderReadStatus.Found || observed.SourceOrder is null ||
                InvoiceIssueSourceValidation.Validate(tenantContext, candidate, observed.SourceOrder) != InvoiceIssueSourceValidationStatus.Valid)
                return new(InvoiceIssueCommitStatus.InvalidSource);
            var debtor = await sourceReader.ValidateDebtorAsync(connection, transaction, tenantContext, candidate, cancellationToken).ConfigureAwait(false);
            if (debtor == InvoiceIssueDebtorObservationStatus.Unavailable) return new(InvoiceIssueCommitStatus.Unavailable);
            if (debtor != InvoiceIssueDebtorObservationStatus.Valid) return new(InvoiceIssueCommitStatus.InvalidDebtor);
            var organization = customerContext.OrganizationId;
            await ExecuteAsync(connection, transaction, "LockOrganization", cancellationToken,
                ("tenant", tenantContext.TenantId), ("organization", organization)).ConfigureAwait(false);
            current = await transactionalAuthority.CheckAsync(connection, transaction, tenantContext, cancellationToken).ConfigureAwait(false);
            if (current != InvoiceIssueAuthorityStatus.Allowed) return CommitDenial(current);
            var invoiceId = Guid.CreateVersion7();
            var debtorJson = JsonSerializer.Serialize(DebtorV1.From(candidate.Debtor), JsonOptions);
            var receiptJson = JsonSerializer.Serialize(new ReceiptV1(1, invoiceId), JsonOptions);
            if (!await RetainedFactsFitAsync(connection, transaction, debtorJson, receiptJson, cancellationToken).ConfigureAwait(false))
                return new(InvoiceIssueCommitStatus.ResourceLimit);
            var issuedAt = new DateTimeOffset((DateTime)(await ScalarAsync(connection, transaction, "CaptureIssueTime", cancellationToken).ConfigureAwait(false)
                ?? throw InvalidHistory()));
            var businessDate = DateOnly.FromDateTime(issuedAt.UtcDateTime);
            var ordinalValue = await ScalarAsync(connection, transaction, "AllocateOrdinal", cancellationToken,
                ("tenant", tenantContext.TenantId), ("organization", organization)).ConfigureAwait(false);
            if (ordinalValue is not long ordinal) return new(InvoiceIssueCommitStatus.ResourceLimit);
            var invoice = new InvoiceIssuedFacts(invoiceId, candidate.TenantId, candidate.SourceOrderId,
                candidate.SourceOrderRevision, candidate.SourceCustomerContext, candidate.CurrencyCode,
                candidate.Lines, candidate.Total, candidate.Debtor, candidate.IssuedByAccountId, issuedAt, businessDate, ordinal);
            await ExecuteAsync(connection, transaction, "InsertInvoice", cancellationToken,
                ("tenant", invoice.TenantId), ("invoice", invoice.InvoiceId), ("order", invoice.SourceOrderId),
                ("revision", invoice.SourceOrderRevision), ("organization", organization),
                ("program", customerContext.ProgramId ?? (object)DBNull.Value), ("ordinal", ordinal),
                ("currency", invoice.CurrencyCode), ("total", invoice.Total), ("count", invoice.Lines.Count),
                ("debtor", debtorJson), ("actor", invoice.IssuedByAccountId), ("at", issuedAt), ("date", businessDate)).ConfigureAwait(false);
            foreach (var line in invoice.Lines)
                await ExecuteAsync(connection, transaction, "InsertLine", cancellationToken,
                    ("tenant", invoice.TenantId), ("invoice", invoice.InvoiceId), ("position", line.Position),
                    ("description", line.Description), ("quantity", line.Quantity), ("unit", line.UnitCode),
                    ("price", line.UnitPrice), ("total", line.LineTotal)).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, "InsertReceipt", cancellationToken,
                ("tenant", invoice.TenantId), ("actor", tenantContext.AccountId), ("key", key),
                ("fingerprint", fingerprint), ("invoice", invoice.InvoiceId), ("response", receiptJson)).ConfigureAwait(false);
            var financialDebtor = FinancialDebtor.From(invoice.Debtor);
            await ExecuteAsync(connection, transaction, "InsertCustomerReceivable", cancellationToken,
                ("tenant", invoice.TenantId), ("invoice", invoice.InvoiceId), ("kind", (int)financialDebtor.Kind),
                ("debtor", financialDebtor.Id), ("currency", invoice.CurrencyCode), ("amount", invoice.Total),
                ("date", invoice.BusinessDate)).ConfigureAwait(false);
            committing = true;
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            committing = false;
            return new(InvoiceIssueCommitStatus.Issued, invoice);
        }
        catch (InvoiceHistoryException) { return new(InvoiceIssueCommitStatus.HistoricalReceiptInvalid); }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            // The failed transaction is disposed before recovery leases a fresh session.
            var receipt = await FindReceiptAsync(tenantContext, key, fingerprint, cancellationToken).ConfigureAwait(false);
            if (receipt.Status != InvoiceIssueReceiptStatus.Missing) return MapReceipt(receipt);
            return await RecoverOrderConflictAsync(tenantContext, candidate.SourceOrderId, cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException) { return new(InvoiceIssueCommitStatus.Unavailable); }
        catch (Exception exception) when (committing && exception is NpgsqlException or OperationCanceledException)
        {
            // Transport/cancellation during COMMIT is not evidence of rollback. Same-key receipt lookup recovers it.
            return new(InvoiceIssueCommitStatus.OutcomeUnknown);
        }
        catch (NpgsqlException) { return new(InvoiceIssueCommitStatus.Unavailable); }
    }

    private async Task<InvoiceIssueCommitResult> RecoverOrderConflictAsync(TenantContext context, Guid orderId, CancellationToken ct)
    {
        try
        {
            var admission = await issueAuthority.CheckAsync(context, ct).ConfigureAwait(false);
            if (admission != InvoiceIssueAuthorityStatus.Allowed) return CommitDenial(admission);
            await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
            var current = await transactionalAuthority.CheckAsync(connection, transaction, context, ct).ConfigureAwait(false);
            if (current != InvoiceIssueAuthorityStatus.Allowed) return CommitDenial(current);
            var existing = await FindOrderInvoiceIdAsync(connection, transaction, context, orderId, ct).ConfigureAwait(false);
            current = await transactionalAuthority.CheckAsync(connection, transaction, context, ct).ConfigureAwait(false);
            if (current != InvoiceIssueAuthorityStatus.Allowed) return CommitDenial(current);
            return new(existing is null ? InvoiceIssueCommitStatus.Unavailable : InvoiceIssueCommitStatus.OrderAlreadyInvoiced);
        }
        catch (NpgsqlException) { return new(InvoiceIssueCommitStatus.Unavailable); }
    }

    private static InvoiceIssueReceiptResult ReceiptDenial(InvoiceIssueAuthorityStatus authority) => new(
        authority == InvoiceIssueAuthorityStatus.Denied ? InvoiceIssueReceiptStatus.NotAuthorized : InvoiceIssueReceiptStatus.Unavailable);

    private static InvoiceIssueCommitResult CommitDenial(InvoiceIssueAuthorityStatus authority) => new(
        authority == InvoiceIssueAuthorityStatus.Denied ? InvoiceIssueCommitStatus.NotAuthorized : InvoiceIssueCommitStatus.Unavailable);

    private static InvoiceIssueCommitResult MapReceipt(InvoiceIssueReceiptResult receipt) => receipt.Status switch
    {
        InvoiceIssueReceiptStatus.Replayed => new(InvoiceIssueCommitStatus.Replayed, receipt.Invoice),
        InvoiceIssueReceiptStatus.IdempotencyKeyConflict => new(InvoiceIssueCommitStatus.IdempotencyKeyConflict),
        InvoiceIssueReceiptStatus.NotAuthorized => new(InvoiceIssueCommitStatus.NotAuthorized),
        InvoiceIssueReceiptStatus.HistoricalReceiptInvalid => new(InvoiceIssueCommitStatus.HistoricalReceiptInvalid),
        _ => new(InvoiceIssueCommitStatus.Unavailable),
    };

    private static async Task<bool> RetainedFactsFitAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string debtor, string receipt, CancellationToken ct)
    {
        if (Encoding.UTF8.GetByteCount(debtor) > MaximumDebtorBytes || Encoding.UTF8.GetByteCount(receipt) > MaximumReceiptBytes) return false;
        return (bool)(await ScalarAsync(connection, transaction, "CheckRetainedBytes", ct,
            ("debtor", debtor), ("receipt", receipt)).ConfigureAwait(false) ?? throw InvalidHistory());
    }

    private static Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context, CancellationToken ct) =>
        ExecuteAsync(connection, transaction, "SetTenant", ct, ("tenant", context.TenantId.ToString("D")));

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string resource,
        CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(connection, transaction, resource, parameters);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string resource,
        CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(connection, transaction, resource, parameters);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction transaction, string resource,
        params (string Name, object Value)[] parameters)
    {
        using var stream = typeof(PostgresInvoiceIssueStore).Assembly.GetManifestResourceStream($"Application.Invoices.Postgres.Sql.{resource}.sql")
            ?? throw new InvalidOperationException("An invoice SQL resource is missing.");
        using var reader = new StreamReader(stream);
        var command = new NpgsqlCommand(reader.ReadToEnd(), connection, transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return command;
    }

    private static void ValidateFingerprint(string fingerprint)
    {
        if (fingerprint is null || fingerprint.Length != 64 || fingerprint.Any(character => !char.IsAsciiHexDigitLower(character)))
            throw new InvoiceIssueValidationException("fingerprint_invalid", "Invoice request fingerprint is invalid.");
    }

    private static string Fingerprint(Guid orderId, long revision, InvoiceDebtorFacts debtor) =>
        InvoiceIssueIntent.Fingerprint(new(orderId, revision, debtor switch
        {
            InvoiceDebtorFacts.Organization => new InvoiceDebtorSelection.DefaultOrganization(),
            InvoiceDebtorFacts.Program => new InvoiceDebtorSelection.AttributedProgram(),
            InvoiceDebtorFacts.Individual individual => new InvoiceDebtorSelection.Individual(individual.IndividualId),
            _ => throw InvalidHistory(),
        }, "retained-validation"));

    private sealed class InvoiceHistoryException : Exception;
    private static InvoiceHistoryException InvalidHistory() => new();
}
