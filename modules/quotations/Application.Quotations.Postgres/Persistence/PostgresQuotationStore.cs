using System.Text.Json;
using Application.Tenancy;
using Npgsql;

namespace Application.Quotations.Postgres;

public sealed partial class PostgresQuotationStore(NpgsqlDataSource source, TimeProvider clock, IQuotationOrderWriter? orders = null)
    : IQuotationStore, IQuotationResponseStore, Application.Orders.IOrderAcceptedQuotationReader
{
    public async Task<QuotationCommandResult?> FindReceiptAsync(TenantContext context, string operation, string key, string fingerprint, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
        return await ReadReceiptAsync(connection, transaction, context, operation, key, fingerprint, ct).ConfigureAwait(false);
    }
    public Task<QuotationCommandResult> CreateDraftAsync(TenantContext context, QuotationDraftFacts draft, string key, string fingerprint, CancellationToken ct) =>
        MutateAsync(context, "create", Guid.CreateVersion7(), 0, draft, key, fingerprint, null, ct);
    public Task<QuotationCommandResult> ReviseDraftAsync(TenantContext context, Guid id, long expectedVersion, QuotationDraftFacts draft,
        string key, string fingerprint, CancellationToken ct) => MutateAsync(context, "revise", id, expectedVersion, draft, key, fingerprint, null, ct);
    public Task<QuotationCommandResult> IssueAsync(TenantContext context, Guid id, long expectedVersion, string key, string fingerprint,
        Func<QuotationDraftFacts, CancellationToken, Task<bool>> validateCurrentFacts, CancellationToken ct) =>
        MutateAsync(context, "issue", id, expectedVersion, null, key, fingerprint, validateCurrentFacts, ct);

    private async Task<QuotationCommandResult> MutateAsync(TenantContext context, string operation, Guid id, long expectedVersion,
        QuotationDraftFacts? draft, string key, string fingerprint,
        Func<QuotationDraftFacts, CancellationToken, Task<bool>>? validateCurrentFacts, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (draft is not null) QuotationRules.Validate(draft, context.TenantId);
        key = QuotationRules.Key(key);
        await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
        await using (var gate = Command("LockReceipt", connection, transaction, context,
            ("actor", context.AccountId), ("operation", operation), ("key", key)))
            await gate.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        var replay = await ReadReceiptAsync(connection, transaction, context, operation, key, fingerprint, ct).ConfigureAwait(false);
        if (replay is not null) return replay;
        QuotationSnapshot? previous = null;
        if (operation != "create")
        {
            await using (var gate = Command("LockHead", connection, transaction, context, ("id", id)))
                await gate.ExecuteScalarAsync(ct).ConfigureAwait(false);
            previous = await ReadHeadAsync(connection, transaction, context, id, ct).ConfigureAwait(false);
            if (previous is null) return new(QuotationCommandStatus.NotFound, null);
            if (previous.Version != expectedVersion) return new(QuotationCommandStatus.RevisionConflict, null);
            if (previous.CurrentResponse?.Kind == QuotationResponseKind.Accepted) return new(QuotationCommandStatus.AcceptedFamily, null);
        }
        if (operation == "issue")
        {
            draft = previous!.Draft;
            if (draft is null) return new(QuotationCommandStatus.NoDraft, null);
            if (draft.Mode == QuotationPriceMode.Catalog)
            {
                // The only pin backend is this effect/receipt transaction; public queries never acquire another pin.
                await using var pin = Command("PinPublication", connection, transaction, context);
                await pin.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            if (!await validateCurrentFacts!(draft, ct).ConfigureAwait(false))
                return new(QuotationCommandStatus.CommercialFactsConflict, null);
        }
        var at = Now();
        if (draft!.ValidUntil <= at) return new(QuotationCommandStatus.ValidityConflict, null);
        // The publication fence protects writes, not passage of time during comparison.
        if (operation == "issue" && draft.Lines.Any(line => line.PublishedPrice is { } price && !price.Validity.Contains(at)))
            return new(QuotationCommandStatus.CommercialFactsConflict, null);
        QuotationSnapshot snapshot;
        QuotationCommandStatus status;
        if (operation == "create")
        {
            await using var insert = Command("InsertHead", connection, transaction, context, ("id", id),
                ("actor", context.AccountId), ("at", at), ("draft", Serialize(draft, 8 * 1024 * 1024)));
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            snapshot = new(id, context.TenantId, context.AccountId, at, 1, null, draft, null);
            status = QuotationCommandStatus.Created;
        }
        else if (operation == "revise")
        {
            await using var update = Command("ReviseHead", connection, transaction, context, ("id", id),
                ("expected", expectedVersion), ("draft", Serialize(draft, 8 * 1024 * 1024)));
            if (await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1) throw new InvalidOperationException("The locked quotation changed unexpectedly.");
            snapshot = previous! with { Version = checked(expectedVersion + 1), Draft = draft };
            status = QuotationCommandStatus.Revised;
        }
        else
        {
            long number;
            if (previous!.Number is { } existing) number = existing;
            else
            {
                await using var next = Command("NextNumber", connection, transaction, context);
                number = (long)(await next.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
            }
            var issued = new QuotationIssuedFacts(id, Guid.CreateVersion7(), context.TenantId, number,
                checked((previous.CurrentIssued?.RevisionNumber ?? 0) + 1), context.AccountId, at, draft);
            await using (var insert = Command("InsertIssued", connection, transaction, context, ("id", id),
                ("revision_id", issued.RevisionId), ("revision", issued.RevisionNumber), ("facts", Serialize(issued, 8 * 1024 * 1024))))
                await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            await using (var update = Command("IssueHead", connection, transaction, context, ("id", id), ("expected", expectedVersion),
                ("number", number), ("revision_id", issued.RevisionId), ("revision", issued.RevisionNumber)))
                if (await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1) throw new InvalidOperationException("The locked quotation changed unexpectedly.");
            snapshot = previous with { Version = checked(expectedVersion + 1), Number = number, Draft = null, CurrentIssued = issued, CurrentResponse = null, Conversion = null };
            status = QuotationCommandStatus.Issued;
        }
        await InsertSnapshotReceiptAsync(connection, transaction, context, operation, key, fingerprint, snapshot, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return new(status, snapshot);
    }
    public async Task<QuotationSnapshot?> FindAsync(TenantContext context, Guid id, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
        return await ReadHeadAsync(connection, transaction, context, id, ct).ConfigureAwait(false);
    }
    public async Task<QuotationIssuedPage> ListIssuedAsync(TenantContext context, Guid id, long afterRevision, int limit, CancellationToken ct)
    {
        if (id == Guid.Empty || afterRevision < 0 || limit is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
        await using var command = Command("ListIssued", connection, transaction, context, ("id", id), ("after", afterRevision), ("limit", limit + 1));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var items = new List<QuotationIssuedFacts>(limit + 1);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var facts = Deserialize<QuotationIssuedFacts>(reader.GetString(0));
            ValidateIssued(facts, context, id);
            if (facts.RevisionId != reader.GetGuid(1) || facts.RevisionNumber != reader.GetInt64(2) || reader.GetInt32(3) != 1)
                throw new InvalidOperationException("Stored quotation history identities or version are inconsistent.");
            items.Add(facts);
        }
        var more = items.Count > limit;
        if (more) items.RemoveAt(items.Count - 1);
        return new(Array.AsReadOnly(items.ToArray()), more ? items[^1].RevisionNumber : null);
    }
    private static async Task<QuotationCommandResult?> ReadReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, string operation, string key, string fingerprint, CancellationToken ct)
    {
        await using var command = Command("ReadReceipt", connection, transaction, context, ("actor", context.AccountId), ("operation", operation), ("key", key));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var version = reader.GetInt32(2);
        if (version is not (1 or 2)) throw new InvalidOperationException("The quotation receipt version is unsupported.");
        if (reader.GetString(0) != fingerprint) return new(QuotationCommandStatus.IdempotencyKeyConflict, null);
        var snapshot = Deserialize<QuotationSnapshot>(reader.GetString(1));
        ValidateSnapshot(snapshot, context);
        if (version == 1 && (snapshot.CurrentResponse is not null || snapshot.Conversion is not null) ||
            operation is "accept" or "reject" or "expire" or "convert" && version != 2)
            throw new InvalidOperationException("The quotation receipt facts do not match their version.");
        if (operation is "accept" or "reject" or "expire" && snapshot.CurrentResponse?.Kind != ResponseKind(operation) ||
            operation == "convert" && snapshot.Conversion is null)
            throw new InvalidOperationException("The quotation receipt facts do not match their operation.");
        return new(QuotationCommandStatus.Replayed, snapshot);
    }
    private static async Task<QuotationSnapshot?> ReadHeadAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context, Guid id, CancellationToken ct)
    {
        await using var command = Command("ReadHead", connection, transaction, context, ("id", id));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var snapshot = new QuotationSnapshot(reader.GetGuid(0), context.TenantId, reader.GetGuid(1), reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetInt64(4),
            reader.IsDBNull(5) ? null : Deserialize<QuotationDraftFacts>(reader.GetString(5)),
            reader.IsDBNull(6) ? null : Deserialize<QuotationIssuedFacts>(reader.GetString(6)),
            reader.IsDBNull(11) ? null : Deserialize<QuotationResponseFacts>(reader.GetString(11)),
            reader.IsDBNull(14) ? null : Deserialize<QuotationConversionFacts>(reader.GetString(14)));
        ValidateSnapshot(snapshot, context);
        if (snapshot.QuotationId != id || (snapshot.CurrentIssued?.RevisionNumber ?? 0) != reader.GetInt64(7) ||
            snapshot.CurrentIssued?.RevisionId != (reader.IsDBNull(8) ? null : reader.GetGuid(8)) || reader.GetInt32(9) != 1 ||
            !reader.IsDBNull(10) && reader.GetInt32(10) != 1)
            throw new InvalidOperationException("Stored quotation revision identities are inconsistent.");
        if (snapshot.CurrentResponse is { } response && (reader.GetInt32(12) != 1 || reader.GetGuid(13) != response.IssuedRevisionId || reader.GetInt16(18) != (short)response.Kind) ||
            snapshot.Conversion is { } conversion && (reader.GetInt32(15) != 1 || reader.GetGuid(16) != conversion.IssuedRevisionId || reader.GetGuid(17) != conversion.OriginalOrder.OrderId))
            throw new InvalidOperationException("Stored quotation response or conversion identities are inconsistent.");
        return snapshot;
    }
    private static void ValidateSnapshot(QuotationSnapshot snapshot, TenantContext context)
    {
        if (snapshot.TenantId != context.TenantId || snapshot.QuotationId == Guid.Empty || snapshot.CreatedByAccountId == Guid.Empty ||
            snapshot.Version < 1 || snapshot.Number is < 1 || snapshot.CreatedAt == default || snapshot.CreatedAt.Offset != TimeSpan.Zero ||
            snapshot.CreatedAt.Ticks % 10 != 0 || snapshot.Number.HasValue != (snapshot.CurrentIssued is not null) ||
            snapshot.CurrentIssued is { } current && snapshot.Version <= current.RevisionNumber ||
            snapshot.Draft is null && snapshot.CurrentIssued is null) throw new InvalidOperationException("Stored quotation facts are invalid.");
        if (snapshot.Draft is not null) QuotationRules.Validate(snapshot.Draft, context.TenantId);
        if (snapshot.CurrentIssued is { } issued)
        {
            ValidateIssued(issued, context, snapshot.QuotationId);
            if (issued.Number != snapshot.Number) throw new InvalidOperationException("Stored quotation numbering is inconsistent.");
        }
        if (snapshot.CurrentResponse is { } response) ValidateResponse(response, snapshot.CurrentIssued!, context);
        if (snapshot.Conversion is { } conversion) ValidateConversion(conversion, snapshot.CurrentIssued!, snapshot.CurrentResponse, context);
    }
    private static void ValidateIssued(QuotationIssuedFacts facts, TenantContext context, Guid id)
    {
        if (facts.TenantId != context.TenantId || facts.QuotationId != id || facts.RevisionId == Guid.Empty || facts.Number < 1 ||
            facts.RevisionNumber < 1 || facts.IssuedByAccountId == Guid.Empty || facts.IssuedAt == default ||
            facts.IssuedAt.Offset != TimeSpan.Zero || facts.IssuedAt.Ticks % 10 != 0 || facts.Offer.ValidUntil <= facts.IssuedAt)
            throw new InvalidOperationException("Stored issued quotation facts are invalid.");
        QuotationRules.Validate(facts.Offer, context.TenantId);
        if (facts.Offer.Lines.Any(line => line.PublishedPrice is { } price && !price.Validity.Contains(facts.IssuedAt)))
            throw new InvalidOperationException("Stored quotation sources were outside validity at issue.");
    }
    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context, CancellationToken ct)
    {
        await using var command = Command("SetTenant", connection, transaction, context, ("tenant_text", context.TenantId.ToString("D")));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    private static NpgsqlCommand Command(string resource, NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, params (string Name, object Value)[] values)
    {
        var command = new NpgsqlCommand(QuotationSql.Load(resource), connection, transaction);
        command.Parameters.AddWithValue("tenant", context.TenantId);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return command;
    }
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();
        return new(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
    }
    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Quotation facts are missing.");
    private static string Serialize<T>(T value, int maximumBytes)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(value);
        if (data.Length > maximumBytes) throw new QuotationValidationException("quotation_too_large", "Retained quotation facts exceed the supported size.");
        return System.Text.Encoding.UTF8.GetString(data);
    }
}
