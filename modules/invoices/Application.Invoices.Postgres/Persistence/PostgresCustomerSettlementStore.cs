using Application.Tenancy;
using Npgsql;

namespace Application.Invoices.Postgres;

public sealed partial class PostgresCustomerSettlementStore(NpgsqlDataSource source,
    IFinancialAuthorityReader authority, IFinancialTransactionalAuthorityReader transactionalAuthority,
    IFinancialDebtorReader debtors) : ICustomerSettlementStore
{
    public Task<FinancialResult<FinancialCommandReceipt>> RecordAsync(TenantContext context, Guid paymentId,
        CustomerPaymentRecord record, string idempotencyKey, CancellationToken cancellationToken) =>
        MutateAsync(context, paymentId, CustomerSettlementRules.Normalize(record), null,
            CustomerSettlementRules.Key(idempotencyKey), cancellationToken);

    public Task<FinancialResult<FinancialCommandReceipt>> ExecuteAsync(TenantContext context, Guid paymentId,
        CustomerPaymentMutation mutation, string idempotencyKey, CancellationToken cancellationToken) =>
        MutateAsync(context, paymentId, null, CustomerSettlementRules.Normalize(mutation),
            CustomerSettlementRules.Key(idempotencyKey), cancellationToken);

    private async Task<FinancialResult<FinancialCommandReceipt>> MutateAsync(TenantContext context, Guid paymentId,
        CustomerPaymentRecord? record, CustomerPaymentMutation? mutation, string key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (paymentId == Guid.Empty) throw new FinancialValidationException();
        var operation = mutation?.Operation ?? CustomerPaymentOperation.Record;
        var permission = CustomerSettlementRules.Permission(operation);
        var fingerprint = record is not null ? CustomerSettlementRules.Fingerprint(paymentId, record)
            : CustomerSettlementRules.Fingerprint(paymentId, mutation!);
        var committing = false;
        try
        {
            var admission = await authority.CheckAsync(context, permission, ct).ConfigureAwait(false);
            if (admission.Access != FinancialAccess.Allowed) return Denial<FinancialCommandReceipt>(admission);
            await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
            await ExecuteSqlAsync(connection, transaction, "LockFinancialReceipt", ct,
                ("tenant", context.TenantId), ("actor", context.AccountId), ("key", key)).ConfigureAwait(false);
            await ExecuteSqlAsync(connection, transaction, "LockCustomerPayment", ct,
                ("tenant", context.TenantId), ("payment", paymentId)).ConfigureAwait(false);
            var current = await transactionalAuthority.CheckAsync(connection, transaction, context, permission, ct).ConfigureAwait(false);
            if (current.Access != FinancialAccess.Allowed) return Denial<FinancialCommandReceipt>(current);
            var replay = await ReadReceiptAsync(connection, transaction, context, fingerprint, key, ct).ConfigureAwait(false);
            if (replay is not null)
            {
                current = await transactionalAuthority.CheckAsync(connection, transaction, context, permission, ct).ConfigureAwait(false);
                return current.Access == FinancialAccess.Allowed ? replay : Denial<FinancialCommandReceipt>(current);
            }
            var payment = await ReadPaymentAsync(connection, transaction, context, paymentId, ct).ConfigureAwait(false);
            Guid? receivableId = mutation?.ReceivableId;
            decimal? amount = mutation?.Amount;
            if (record is not null)
            {
                if (payment is not null) return new(FinancialStatus.RevisionConflict);
                var retained = await ScalarAsync(connection, transaction, "HasRecognizedCustomerDebtor", ct,
                    ("tenant", context.TenantId), ("kind", (int)record.Debtor.Kind), ("debtor", record.Debtor.Id)).ConfigureAwait(false);
                if (retained is not bool retainedDebtor) throw InvalidHistory();
                if (!retainedDebtor && !await debtors.ExistsAsync(connection, transaction, context, record.Debtor, ct).ConfigureAwait(false))
                    return new(FinancialStatus.DebtorNotFound);
            }
            else
            {
                if (payment is null) return new(FinancialStatus.NotFound);
                var accepted = CustomerSettlementRules.CanApply(payment, mutation!, context.AccountId);
                if (accepted != FinancialStatus.Applied) return new(accepted);
                if (operation == CustomerPaymentOperation.Release)
                {
                    var allocation = await FindAllocationAsync(connection, transaction, context, paymentId, mutation!.AllocationId!.Value, ct)
                        .ConfigureAwait(false);
                    if (allocation is null) return new(FinancialStatus.InvalidTransition);
                    receivableId = allocation.ReceivableId;
                    amount = allocation.Amount;
                }
                if (receivableId is { } invoice)
                {
                    await ExecuteSqlAsync(connection, transaction, "LockCustomerReceivable", ct,
                        ("tenant", context.TenantId), ("invoice", invoice)).ConfigureAwait(false);
                    var receivable = await ReadReceivableAsync(connection, transaction, context, invoice, ct).ConfigureAwait(false);
                    if (receivable is null) return new(FinancialStatus.NotFound);
                    if (receivable.Debtor != payment.Debtor || receivable.CurrencyCode != payment.CurrencyCode)
                        return new(FinancialStatus.DebtorMismatch);
                    if (operation == CustomerPaymentOperation.Allocate && amount > receivable.OutstandingAmount)
                        return new(FinancialStatus.OverAllocation);
                }
            }
            // Lock waits may outlive membership, grants or strong-authentication evidence.
            current = await transactionalAuthority.CheckAsync(connection, transaction, context, permission, ct).ConfigureAwait(false);
            if (current.Access != FinancialAccess.Allowed) return Denial<FinancialCommandReceipt>(current);
            var at = new DateTimeOffset((DateTime)(await ScalarAsync(connection, transaction, "CaptureIssueTime", ct).ConfigureAwait(false)
                ?? throw InvalidHistory()));
            var eventId = Guid.CreateVersion7();
            var revision = payment is null ? 1 : payment.Revision + 1;
            if (record is not null)
                await ExecuteSqlAsync(connection, transaction, "InsertCustomerPayment", ct,
                    ("tenant", context.TenantId), ("payment", paymentId), ("kind", (int)record.Debtor.Kind), ("debtor", record.Debtor.Id),
                    ("currency", record.CurrencyCode), ("amount", record.Amount), ("channel", (int)record.Channel), ("evidence", record.Evidence),
                    ("reference", record.ExternalReference ?? (object)DBNull.Value), ("actor", context.AccountId), ("at", at)).ConfigureAwait(false);
            await ExecuteSqlAsync(connection, transaction, "InsertCustomerPaymentEvent", ct,
                ("tenant", context.TenantId), ("payment", paymentId), ("event", eventId), ("revision", revision), ("operation", (int)operation),
                ("state", (int)CustomerSettlementRules.NextState(payment?.State ?? CustomerPaymentState.Pending, operation)),
                ("actor", context.AccountId), ("at", at), ("authorization", current.AuthorizationRevision),
                ("evidence", mutation?.Evidence ?? (object)DBNull.Value), ("receivable", receivableId ?? (object)DBNull.Value),
                ("amount", amount ?? (object)DBNull.Value), ("allocation", mutation?.AllocationId ?? (object)DBNull.Value)).ConfigureAwait(false);
            await ExecuteSqlAsync(connection, transaction, "InsertFinancialReceipt", ct,
                ("tenant", context.TenantId), ("actor", context.AccountId), ("key", key), ("fingerprint", fingerprint),
                ("payment", paymentId), ("event", eventId)).ConfigureAwait(false);
            current = await transactionalAuthority.CheckAsync(connection, transaction, context, permission, ct).ConfigureAwait(false);
            if (current.Access != FinancialAccess.Allowed) return Denial<FinancialCommandReceipt>(current);
            committing = true;
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            committing = false;
            return new(FinancialStatus.Applied, new(paymentId, eventId, revision));
        }
        catch (FinancialHistoryException) { return new(FinancialStatus.HistoryInvalid); }
        catch (Exception error) when (error is InvalidCastException or OverflowException or FormatException)
        { return new(FinancialStatus.HistoryInvalid); }
        catch (PostgresException error) when (error.SqlState == "23505" && error.ConstraintName == "bank_receipt_reference")
        {
            var current = await authority.CheckAsync(context, permission, ct).ConfigureAwait(false);
            return current.Access == FinancialAccess.Allowed ? new(FinancialStatus.DuplicateReference) : Denial<FinancialCommandReceipt>(current);
        }
        catch (PostgresException) { return new(FinancialStatus.Unavailable); }
        catch (Exception error) when (committing && error is NpgsqlException or OperationCanceledException)
        { return new(FinancialStatus.OutcomeUnknown); }
        catch (NpgsqlException) { return new(FinancialStatus.Unavailable); }
    }

    private static async Task<FinancialResult<FinancialCommandReceipt>?> ReadReceiptAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, TenantContext context, string fingerprint, string key, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "ReadFinancialReceipt",
            ("tenant", context.TenantId), ("actor", context.AccountId), ("key", key));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        if (reader.IsDBNull(4) || !reader.GetBoolean(4)) throw InvalidHistory();
        return reader.GetString(0) == fingerprint
            ? new(FinancialStatus.Replayed, new(reader.GetGuid(1), reader.GetGuid(2), reader.GetInt64(3)))
            : new(FinancialStatus.IdempotencyConflict);
    }

    private static async Task<CustomerPaymentEvent?> FindAllocationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, Guid paymentId, Guid allocationId, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "ReadActiveCustomerAllocation",
            ("tenant", context.TenantId), ("payment", paymentId), ("allocation", allocationId));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Event(reader, 0) : null;
    }

    private static FinancialResult<T> Denial<T>(FinancialAuthority result) => new(
        result.Access == FinancialAccess.Denied ? FinancialStatus.Denied : FinancialStatus.Unavailable);

    private static Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context, CancellationToken ct) =>
        ExecuteSqlAsync(connection, transaction, "SetTenant", ct, ("tenant", context.TenantId.ToString("D")));

    private static async Task ExecuteSqlAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string resource,
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
        using var stream = typeof(PostgresCustomerSettlementStore).Assembly.GetManifestResourceStream($"Application.Invoices.Postgres.Sql.{resource}.sql")
            ?? throw new InvalidOperationException("A customer financial statement is missing.");
        using var reader = new StreamReader(stream);
        var command = new NpgsqlCommand(reader.ReadToEnd(), connection, transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return command;
    }

    private static FinancialHistoryException InvalidHistory() => new();
    private sealed class FinancialHistoryException : Exception;
}
