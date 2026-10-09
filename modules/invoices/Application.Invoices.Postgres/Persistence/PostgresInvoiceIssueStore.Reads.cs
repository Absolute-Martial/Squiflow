using System.Text;
using System.Text.Json;
using Application.Customers;
using Application.Tenancy;
using Npgsql;

namespace Application.Invoices.Postgres;

public sealed partial class PostgresInvoiceIssueStore
{
    public Task<InvoiceReadResult> FindByIdAsync(TenantContext context, Guid invoiceId, CancellationToken cancellationToken) =>
        FindInvoiceAsync(context, invoiceId, false, cancellationToken);

    public Task<InvoiceReadResult> FindByOrderAsync(TenantContext context, Guid orderId, CancellationToken cancellationToken) =>
        FindInvoiceAsync(context, orderId, true, cancellationToken);

    private async Task<InvoiceReadResult> FindInvoiceAsync(TenantContext context, Guid id, bool byOrder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (id == Guid.Empty) throw new InvoiceIssueValidationException("invoice_identity_invalid", "Invoice lookup identity is required.");
        try
        {
            var admission = await viewAuthority.CheckAsync(context, ct).ConfigureAwait(false);
            if (admission != InvoiceIssueAuthorityStatus.Allowed) return ReadDenial(admission);
            await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
            var current = await transactionalAuthority.CheckViewAsync(connection, transaction, context, ct).ConfigureAwait(false);
            if (current != InvoiceIssueAuthorityStatus.Allowed) return ReadDenial(current);
            Guid? invoiceId = byOrder
                ? await FindOrderInvoiceIdAsync(connection, transaction, context, id, ct).ConfigureAwait(false)
                : id;
            var invoice = invoiceId is { } retainedId
                ? await ReadInvoiceAsync(connection, transaction, context.TenantId, retainedId, ct).ConfigureAwait(false)
                : null;
            current = await transactionalAuthority.CheckViewAsync(connection, transaction, context, ct).ConfigureAwait(false);
            if (current != InvoiceIssueAuthorityStatus.Allowed) return ReadDenial(current);
            return invoice is null ? new(InvoiceReadStatus.Missing) : new(InvoiceReadStatus.Found, invoice);
        }
        catch (InvoiceHistoryException) { return new(InvoiceReadStatus.HistoricalInvalid); }
        catch (NpgsqlException) { return new(InvoiceReadStatus.Unavailable); }
    }

    private static InvoiceReadResult ReadDenial(InvoiceIssueAuthorityStatus authority) => new(
        authority == InvoiceIssueAuthorityStatus.Denied ? InvoiceReadStatus.NotAuthorized : InvoiceReadStatus.Unavailable);

    private static async Task<Guid?> FindOrderInvoiceIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, Guid orderId, CancellationToken ct)
    {
        var value = await ScalarAsync(connection, transaction, "FindOrderInvoice", ct,
            ("tenant", context.TenantId), ("order", orderId)).ConfigureAwait(false);
        return value is Guid invoiceId ? invoiceId : null;
    }

    private static async Task<InvoiceIssueReceiptResult> ReadReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, string key, string fingerprint, CancellationToken ct)
    {
        Guid invoiceId;
        string retainedFingerprint;
        await using (var command = Command(connection, transaction, "ReadReceipt",
            ("tenant", context.TenantId), ("actor", context.AccountId), ("key", key)))
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return new(InvoiceIssueReceiptStatus.Missing);
            retainedFingerprint = reader.GetString(0);
            invoiceId = reader.GetGuid(1);
            var envelope = Deserialize<ReceiptV1>(reader.GetString(2), MaximumReceiptBytes);
            if (envelope.SchemaVersion != 1 || envelope.InvoiceId != invoiceId) throw InvalidHistory();
        }
        // A corrupt receipt cannot be presented as an ordinary changed-intent conflict.
        var invoice = await ReadInvoiceAsync(connection, transaction, context.TenantId, invoiceId, ct).ConfigureAwait(false)
            ?? throw InvalidHistory();
        if (invoice.IssuedByAccountId != context.AccountId ||
            retainedFingerprint != Fingerprint(invoice.SourceOrderId, invoice.SourceOrderRevision, invoice.Debtor)) throw InvalidHistory();
        return retainedFingerprint == fingerprint
            ? new(InvoiceIssueReceiptStatus.Replayed, invoice)
            : new(InvoiceIssueReceiptStatus.IdempotencyKeyConflict);
    }

    private static async Task<InvoiceIssuedFacts?> ReadInvoiceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, Guid invoiceId, CancellationToken ct)
    {
        try { return await DecodeInvoiceAsync(connection, transaction, tenantId, invoiceId, ct).ConfigureAwait(false); }
        catch (ArgumentException) { throw InvalidHistory(); }
        catch (OverflowException) { throw InvalidHistory(); }
        catch (InvalidCastException) { throw InvalidHistory(); }
        catch (FormatException) { throw InvalidHistory(); }
    }

    private static async Task<InvoiceIssuedFacts?> DecodeInvoiceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, Guid invoiceId, CancellationToken ct)
    {
        Header header;
        await using (var command = Command(connection, transaction, "ReadInvoice", ("tenant", tenantId), ("invoice", invoiceId)))
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
            if (reader.GetInt32(13) != 1 || reader.GetGuid(0) != invoiceId) throw InvalidHistory();
            var debtor = Deserialize<DebtorV1>(reader.GetString(9), MaximumDebtorBytes).ToFacts();
            header = new(reader.GetGuid(1), reader.GetInt64(2), new(reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4)),
                reader.GetInt64(5), reader.GetString(6), reader.GetDecimal(7), reader.GetInt32(8), debtor,
                reader.GetGuid(10), new DateTimeOffset(reader.GetDateTime(11)), reader.GetFieldValue<DateOnly>(12));
        }
        if (header.LineCount is < 1 or > 100 || header.Ordinal < 1 ||
            header.Debtor is InvoiceDebtorFacts.Organization organization && organization.OrganizationId != header.Context.OrganizationId ||
            header.Debtor is InvoiceDebtorFacts.Program program && (program.OrganizationId != header.Context.OrganizationId || program.ProgramId != header.Context.ProgramId))
            throw InvalidHistory();
        var lines = new List<InvoiceIssuedLine>(header.LineCount);
        await using (var command = Command(connection, transaction, "ReadInvoiceLines", ("tenant", tenantId), ("invoice", invoiceId)))
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                lines.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5)));
        if (lines.Count != header.LineCount) throw InvalidHistory();
        var counter = await ScalarAsync(connection, transaction, "ReadCounter", ct,
            ("tenant", tenantId), ("organization", header.Context.OrganizationId)).ConfigureAwait(false);
        if (counter is not long last || last < header.Ordinal) throw InvalidHistory();
        await using (var command = Command(connection, transaction, "ReadInvoiceReceipt", ("tenant", tenantId), ("invoice", invoiceId)))
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false) || reader.GetGuid(0) != header.Actor) throw InvalidHistory();
            var key = reader.GetString(1);
            try
            {
                if (InvoiceIssueIntent.NormalizeIdempotencyKey(key) != key) throw InvalidHistory();
                ValidateFingerprint(reader.GetString(2));
            }
            catch (ArgumentException) { throw InvalidHistory(); }
            var envelope = Deserialize<ReceiptV1>(reader.GetString(3), MaximumReceiptBytes);
            if (envelope.SchemaVersion != 1 || envelope.InvoiceId != invoiceId ||
                reader.GetString(2) != Fingerprint(header.OrderId, header.Revision, header.Debtor) || await reader.ReadAsync(ct).ConfigureAwait(false))
                throw InvalidHistory();
        }
        try
        {
            return new(invoiceId, tenantId, header.OrderId, header.Revision, header.Context, header.Currency,
                lines, header.Total, header.Debtor, header.Actor, header.IssuedAt, header.BusinessDate, header.Ordinal);
        }
        catch (ArgumentException) { throw InvalidHistory(); }
        catch (OverflowException) { throw InvalidHistory(); }
    }

    private sealed record Header(Guid OrderId, long Revision, CustomerOrderContext Context, long Ordinal,
        string Currency, decimal Total, int LineCount, InvoiceDebtorFacts Debtor, Guid Actor, DateTimeOffset IssuedAt, DateOnly BusinessDate);

    private sealed record ReceiptV1(int SchemaVersion, Guid InvoiceId);

    private sealed record DebtorV1(int SchemaVersion, int Kind, Guid? OrganizationId, string? OrganizationDisplayName,
        Guid? ProgramId, string? ProgramDisplayName, Guid? IndividualId, long? ObservedRevision, string? DisplayName)
    {
        internal static DebtorV1 From(InvoiceDebtorFacts debtor) => debtor switch
        {
            InvoiceDebtorFacts.Organization organization => new(1, 1, organization.OrganizationId, organization.OrganizationDisplayName, null, null, null, null, null),
            InvoiceDebtorFacts.Program program => new(1, 2, program.OrganizationId, program.OrganizationDisplayName, program.ProgramId, program.ProgramDisplayName, null, null, null),
            InvoiceDebtorFacts.Individual individual => new(1, 3, null, null, null, null, individual.IndividualId, individual.ObservedRevision, individual.DisplayName),
            _ => throw InvalidHistory(),
        };

        internal InvoiceDebtorFacts ToFacts()
        {
            if (SchemaVersion != 1) throw InvalidHistory();
            return Kind switch
            {
                1 when OrganizationId is { } id && OrganizationDisplayName is { } name && ProgramId is null &&
                    ProgramDisplayName is null && IndividualId is null && ObservedRevision is null && DisplayName is null => new InvoiceDebtorFacts.Organization(id, name),
                2 when OrganizationId is { } organization && OrganizationDisplayName is { } organizationName && ProgramId is { } program &&
                    ProgramDisplayName is { } programName && IndividualId is null && ObservedRevision is null && DisplayName is null => new InvoiceDebtorFacts.Program(organization, organizationName, program, programName),
                3 when IndividualId is { } individual && ObservedRevision is { } revision && DisplayName is { } individualName &&
                    OrganizationId is null && OrganizationDisplayName is null && ProgramId is null && ProgramDisplayName is null => new InvoiceDebtorFacts.Individual(individual, revision, individualName),
                _ => throw InvalidHistory(),
            };
        }
    }

    private static T Deserialize<T>(string json, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(json) > maximumBytes) throw InvalidHistory();
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            RequireUniqueProperties(document.RootElement);
            var value = JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw InvalidHistory();
            if (!JsonElement.DeepEquals(document.RootElement, JsonSerializer.SerializeToElement(value, JsonOptions))) throw InvalidHistory();
            return value;
        }
        catch (JsonException) { throw InvalidHistory(); }
        catch (ArgumentException) { throw InvalidHistory(); }
    }

    private static void RequireUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw InvalidHistory();
                RequireUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RequireUniqueProperties(item);
    }
}
