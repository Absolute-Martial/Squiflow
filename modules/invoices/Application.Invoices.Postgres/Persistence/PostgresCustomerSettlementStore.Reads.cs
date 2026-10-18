using System.Globalization;
using Application.Tenancy;
using Npgsql;

namespace Application.Invoices.Postgres;

public sealed partial class PostgresCustomerSettlementStore
{
    public Task<FinancialResult<ReceivableSnapshot>> ReadReceivableAsync(TenantContext context, Guid invoiceId, CancellationToken cancellationToken)
    {
        if (invoiceId == Guid.Empty) throw new FinancialValidationException();
        return ReadProtectedAsync(context, async (connection, transaction, ct) =>
        {
            var value = await ReadReceivableAsync(connection, transaction, context, invoiceId, ct).ConfigureAwait(false);
            return value is null ? new(FinancialStatus.NotFound) : new FinancialResult<ReceivableSnapshot>(FinancialStatus.Found, value);
        }, cancellationToken);
    }

    public Task<FinancialResult<CustomerPaymentSnapshot>> ReadPaymentAsync(TenantContext context, Guid paymentId, CancellationToken cancellationToken)
    {
        if (paymentId == Guid.Empty) throw new FinancialValidationException();
        return ReadProtectedAsync(context, async (connection, transaction, ct) =>
        {
            var value = await ReadPaymentAsync(connection, transaction, context, paymentId, ct).ConfigureAwait(false);
            return value is null ? new(FinancialStatus.NotFound) : new FinancialResult<CustomerPaymentSnapshot>(FinancialStatus.Found, value);
        }, cancellationToken);
    }

    public Task<FinancialResult<FinancialPage<ReceivableSnapshot>>> BrowseReceivablesAsync(TenantContext context,
        FinancialDebtor? debtor, string? cursor, CancellationToken cancellationToken)
    {
        var after = ParseListCursor(context, "r1", debtor, cursor);
        return ReadProtectedAsync(context, async (connection, transaction, ct) =>
        {
            var items = new List<ReceivableSnapshot>();
            await using var command = Command(connection, transaction, "BrowseCustomerReceivables", ListParameters(context, debtor, after));
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) items.Add(Receivable(reader));
            var more = items.Count > CustomerSettlementRules.PageSize;
            if (more) items.RemoveAt(CustomerSettlementRules.PageSize);
            return new FinancialResult<FinancialPage<ReceivableSnapshot>>(FinancialStatus.Found,
                new(items.AsReadOnly(), more ? ListCursor(context, "r1", debtor, items[^1].InvoiceId) : null));
        }, cancellationToken);
    }

    public Task<FinancialResult<FinancialPage<CustomerPaymentSnapshot>>> BrowsePaymentsAsync(TenantContext context,
        FinancialDebtor? debtor, string? cursor, CancellationToken cancellationToken)
    {
        var after = ParseListCursor(context, "p1", debtor, cursor);
        return ReadProtectedAsync(context, async (connection, transaction, ct) =>
        {
            var items = new List<CustomerPaymentSnapshot>();
            await using var command = Command(connection, transaction, "BrowseCustomerPayments", ListParameters(context, debtor, after));
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) items.Add(Payment(reader));
            var more = items.Count > CustomerSettlementRules.PageSize;
            if (more) items.RemoveAt(CustomerSettlementRules.PageSize);
            return new FinancialResult<FinancialPage<CustomerPaymentSnapshot>>(FinancialStatus.Found,
                new(items.AsReadOnly(), more ? ListCursor(context, "p1", debtor, items[^1].PaymentId) : null));
        }, cancellationToken);
    }

    public Task<FinancialResult<FinancialPage<CustomerPaymentEvent>>> ReadHistoryAsync(TenantContext context, Guid paymentId,
        string? cursor, CancellationToken cancellationToken)
    {
        if (paymentId == Guid.Empty) throw new FinancialValidationException();
        var prefix = $"h1/{context.TenantId:N}/{paymentId:N}/";
        var after = 0L;
        if (cursor is not null && (cursor.Length > 160 || !cursor.StartsWith(prefix, StringComparison.Ordinal) ||
            !long.TryParse(cursor.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out after) ||
            after is < 1 or > CustomerSettlementRules.MaximumEventsPerPayment ||
            cursor != prefix + after.ToString(CultureInfo.InvariantCulture))) throw new FinancialValidationException();
        return ReadProtectedAsync(context, async (connection, transaction, ct) =>
        {
            var items = new List<CustomerPaymentEvent>();
            await using var command = Command(connection, transaction, "ReadCustomerPaymentHistory",
                ("tenant", context.TenantId), ("payment", paymentId), ("after", after));
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                return new FinancialResult<FinancialPage<CustomerPaymentEvent>>(FinancialStatus.NotFound);
            do
            {
                if (reader.IsDBNull(0) || !reader.GetBoolean(0)) throw InvalidHistory();
                if (!reader.IsDBNull(1)) items.Add(Event(reader, 1));
            } while (await reader.ReadAsync(ct).ConfigureAwait(false));
            var more = items.Count > CustomerSettlementRules.PageSize;
            if (more) items.RemoveAt(CustomerSettlementRules.PageSize);
            return new FinancialResult<FinancialPage<CustomerPaymentEvent>>(FinancialStatus.Found,
                new(items.AsReadOnly(), more ? prefix + items[^1].Revision.ToString(CultureInfo.InvariantCulture) : null));
        }, cancellationToken);
    }

    public Task<FinancialResult<DebtorBalance>> ReadBalanceAsync(TenantContext context, FinancialDebtor debtor, CancellationToken cancellationToken)
    {
        CustomerSettlementRules.RequireDebtor(debtor);
        return ReadProtectedAsync(context, async (connection, transaction, ct) =>
        {
            await using var command = Command(connection, transaction, "ReadCustomerDebtorBalance",
                ("tenant", context.TenantId), ("kind", (int)debtor.Kind), ("debtor", debtor.Id));
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false) || reader.IsDBNull(3) || !reader.GetBoolean(3)) throw InvalidHistory();
            var balance = new DebtorBalance(debtor, "NPR", reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2));
            if (balance.RecognizedAmount < balance.OutstandingAmount || balance.OutstandingAmount < 0m || balance.ConfirmedUnappliedAmount < 0m)
                throw InvalidHistory();
            return new FinancialResult<DebtorBalance>(FinancialStatus.Found, balance);
        }, cancellationToken);
    }

    private async Task<FinancialResult<T>> ReadProtectedAsync<T>(TenantContext context,
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<FinancialResult<T>>> query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var admission = await authority.CheckAsync(context, FinancialCapability.View, ct).ConfigureAwait(false);
            if (admission.Access != FinancialAccess.Allowed) return Denial<T>(admission);
            await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
            var current = await transactionalAuthority.CheckAsync(connection, transaction, context, FinancialCapability.View, ct).ConfigureAwait(false);
            if (current.Access != FinancialAccess.Allowed) return Denial<T>(current);
            // Each query returns its complete financial projection in one MVCC snapshot.
            var result = await query(connection, transaction, ct).ConfigureAwait(false);
            current = await transactionalAuthority.CheckAsync(connection, transaction, context, FinancialCapability.View, ct).ConfigureAwait(false);
            return current.Access == FinancialAccess.Allowed ? result : Denial<T>(current);
        }
        catch (FinancialHistoryException) { return new(FinancialStatus.HistoryInvalid); }
        catch (Exception error) when (error is InvalidCastException or OverflowException or FormatException)
        { return new(FinancialStatus.HistoryInvalid); }
        catch (PostgresException error) when (error.SqlState is "22P02" or "22003")
        { return new(FinancialStatus.HistoryInvalid); }
        catch (NpgsqlException) { return new(FinancialStatus.Unavailable); }
    }

    private static async Task<ReceivableSnapshot?> ReadReceivableAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, Guid invoiceId, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "ReadCustomerReceivable", ("tenant", context.TenantId), ("invoice", invoiceId));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Receivable(reader) : null;
    }

    private static async Task<CustomerPaymentSnapshot?> ReadPaymentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, Guid paymentId, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "ReadCustomerPayment", ("tenant", context.TenantId), ("payment", paymentId));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Payment(reader) : null;
    }

    private static ReceivableSnapshot Receivable(NpgsqlDataReader reader)
    {
        if (reader.IsDBNull(9) || !reader.GetBoolean(9)) throw InvalidHistory();
        var result = new ReceivableSnapshot(reader.GetGuid(1), reader.GetGuid(0),
            new((InvoiceDebtorKind)reader.GetInt32(2), reader.GetGuid(3)), reader.GetString(4), reader.GetDecimal(5),
            reader.GetFieldValue<DateOnly>(6), new(reader.GetDateTime(7)), reader.GetDecimal(8));
        if (result.InvoiceId == Guid.Empty || result.TenantId == Guid.Empty || !Enum.IsDefined(result.Debtor.Kind) ||
            result.Debtor.Id == Guid.Empty || result.CurrencyCode != "NPR" || !CustomerSettlementRules.IsAmount(result.OriginalAmount, true) ||
            result.AllocatedAmount < 0m || result.OutstandingAmount < 0m) throw InvalidHistory();
        return result;
    }

    private static CustomerPaymentSnapshot Payment(NpgsqlDataReader reader)
    {
        if (reader.IsDBNull(15) || !reader.GetBoolean(15)) throw InvalidHistory();
        var result = new CustomerPaymentSnapshot(reader.GetGuid(1), reader.GetGuid(0),
            new((InvoiceDebtorKind)reader.GetInt32(2), reader.GetGuid(3)), reader.GetString(4), reader.GetDecimal(5),
            (CustomerPaymentChannel)reader.GetInt32(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetGuid(9), new(reader.GetDateTime(10)), reader.GetInt64(11), (CustomerPaymentState)reader.GetInt32(12),
            reader.GetDecimal(13), reader.IsDBNull(14) ? null : reader.GetString(14));
        if (result.PaymentId == Guid.Empty || result.TenantId == Guid.Empty || result.Revision is < 1 or > CustomerSettlementRules.MaximumEventsPerPayment ||
            !Enum.IsDefined(result.Debtor.Kind) || result.Debtor.Id == Guid.Empty || !Enum.IsDefined(result.State) || !Enum.IsDefined(result.Channel) ||
            result.CurrencyCode != "NPR" || !CustomerSettlementRules.IsAmount(result.Amount) || result.AllocatedAmount < 0m || result.AllocatedAmount > result.Amount)
            throw InvalidHistory();
        return result;
    }

    private static CustomerPaymentEvent Event(NpgsqlDataReader reader, int offset)
    {
        if (reader.GetInt32(offset + 12) != 1) throw InvalidHistory();
        var result = new CustomerPaymentEvent(reader.GetGuid(offset), reader.GetGuid(offset + 1), reader.GetInt64(offset + 2),
            (CustomerPaymentOperation)reader.GetInt32(offset + 3), (CustomerPaymentState)reader.GetInt32(offset + 4),
            reader.GetGuid(offset + 5), new(reader.GetDateTime(offset + 6)), reader.GetInt64(offset + 7),
            reader.IsDBNull(offset + 8) ? null : reader.GetString(offset + 8), reader.IsDBNull(offset + 9) ? null : reader.GetGuid(offset + 9),
            reader.IsDBNull(offset + 10) ? null : reader.GetDecimal(offset + 10), reader.IsDBNull(offset + 11) ? null : reader.GetGuid(offset + 11));
        if (!Enum.IsDefined(result.Operation) || !Enum.IsDefined(result.State) || result.Revision is < 1 or > CustomerSettlementRules.MaximumEventsPerPayment ||
            result.EventId == Guid.Empty || result.PaymentId == Guid.Empty || result.ActorAccountId == Guid.Empty || result.AuthorizationRevision < 1)
            throw InvalidHistory();
        return result;
    }

    private static (string Name, object Value)[] ListParameters(TenantContext context, FinancialDebtor? debtor, Guid? after) =>
        [("tenant", context.TenantId), ("kind", debtor is { } value ? (int)value.Kind : 0),
            ("debtor", debtor?.Id ?? (object)DBNull.Value), ("after", after ?? (object)DBNull.Value)];

    private static string ListPrefix(TenantContext context, string kind, FinancialDebtor? debtor) =>
        $"{kind}/{context.TenantId:N}/{(debtor is { } value ? ((int)value.Kind).ToString(CultureInfo.InvariantCulture) : "0")}/{(debtor?.Id.ToString("N") ?? "-")}/";

    private static string ListCursor(TenantContext context, string kind, FinancialDebtor? debtor, Guid after) => ListPrefix(context, kind, debtor) + after.ToString("N");

    private static Guid? ParseListCursor(TenantContext context, string kind, FinancialDebtor? debtor, string? cursor)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (debtor is { } filter) CustomerSettlementRules.RequireDebtor(filter);
        if (cursor is null) return null;
        var prefix = ListPrefix(context, kind, debtor);
        if (cursor.Length > 160 || !cursor.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(cursor.AsSpan(prefix.Length), "N", out var after) || after == Guid.Empty)
            throw new FinancialValidationException();
        return after;
    }
}
