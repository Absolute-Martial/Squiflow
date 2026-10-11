using Application.Orders;
using Application.Tenancy;
using Npgsql;

namespace Application.Quotations.Postgres;

public sealed partial class PostgresQuotationStore
{
    public async Task<QuotationCommandResult> RespondAsync(TenantContext context, Guid quotationId, QuotationResponseKind kind,
        QuotationResponseRequest request, string key, string fingerprint, CancellationToken ct)
    {
        QuotationRules.RequireIdentity(quotationId, request.ExpectedVersion);
        if (!Enum.IsDefined(kind) || request.IssuedRevisionId == Guid.Empty ||
            QuotationRules.Text(request.Evidence, 2000, "evidence_invalid") != request.Evidence ||
            (kind == QuotationResponseKind.Expired ? request.CustomerClaim is not null :
                QuotationRules.Text(request.CustomerClaim!, 300, "customer_claim_invalid") != request.CustomerClaim))
            throw new QuotationValidationException("response_invalid", "Supported response evidence is required.");
        var operation = kind switch { QuotationResponseKind.Accepted => "accept", QuotationResponseKind.Rejected => "reject", _ => "expire" };
        await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
        var replay = await LockAndReadReceiptAsync(connection, transaction, context, operation, key, fingerprint, ct).ConfigureAwait(false);
        if (replay is not null) return replay;
        await LockHeadAsync(connection, transaction, context, quotationId, ct).ConfigureAwait(false);
        var current = await ReadHeadAsync(connection, transaction, context, quotationId, ct).ConfigureAwait(false);
        if (current is null) return new(QuotationCommandStatus.NotFound, null);
        if (current.Version != request.ExpectedVersion) return new(QuotationCommandStatus.RevisionConflict, null);
        if (current.CurrentIssued is not { } issued) return new(QuotationCommandStatus.NotAccepted, null);
        if (issued.RevisionId != request.IssuedRevisionId) return new(QuotationCommandStatus.Superseded, null);
        if (current.CurrentResponse is not null) return new(QuotationCommandStatus.AlreadyResponded, null);
        var at = Now();
        if (at < issued.IssuedAt || kind == QuotationResponseKind.Accepted && at >= issued.Offer.ValidUntil ||
            kind == QuotationResponseKind.Expired && at < issued.Offer.ValidUntil)
            return new(QuotationCommandStatus.ValidityConflict, null);
        var response = new QuotationResponseFacts(quotationId, issued.RevisionId, context.TenantId, kind, context.AccountId, at, request.Evidence, request.CustomerClaim);
        ValidateResponse(response, issued, context);
        await using (var insert = Command("InsertResponse", connection, transaction, context, ("id", quotationId), ("revision_id", issued.RevisionId),
            ("kind", (short)kind), ("facts", Serialize(response, 32768))))
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await AdvanceVersionAsync(connection, transaction, context, quotationId, current.Version, ct).ConfigureAwait(false);
        var snapshot = current with { Version = checked(current.Version + 1), CurrentResponse = response };
        await InsertSnapshotReceiptAsync(connection, transaction, context, operation, key, fingerprint, snapshot, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return new(kind switch
        {
            QuotationResponseKind.Accepted => QuotationCommandStatus.Accepted,
            QuotationResponseKind.Rejected => QuotationCommandStatus.Rejected,
            _ => QuotationCommandStatus.Expired
        }, snapshot);
    }
    public async Task<QuotationCommandResult> ConvertAsync(TenantContext context, Guid quotationId, QuotationConvertRequest request,
        string key, string fingerprint, CancellationToken ct)
    {
        QuotationRules.RequireIdentity(quotationId, request.ExpectedVersion);
        if (request.IssuedRevisionId == Guid.Empty) throw new QuotationValidationException("response_invalid", "An exact issued revision is required.");
        await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
        var replay = await LockAndReadReceiptAsync(connection, transaction, context, "convert", key, fingerprint, ct).ConfigureAwait(false);
        if (replay is not null) return replay;
        await LockHeadAsync(connection, transaction, context, quotationId, ct).ConfigureAwait(false);
        var current = await ReadHeadAsync(connection, transaction, context, quotationId, ct).ConfigureAwait(false);
        if (current is null) return new(QuotationCommandStatus.NotFound, null);
        if (current.CurrentIssued?.RevisionId != request.IssuedRevisionId) return new(QuotationCommandStatus.Superseded, null);
        if (current.CurrentResponse?.Kind != QuotationResponseKind.Accepted) return new(QuotationCommandStatus.NotAccepted, null);
        if (current.Conversion is not null)
        {
            // Different callers/keys still bind to the original created Order, including after its lifecycle changes.
            await InsertSnapshotReceiptAsync(connection, transaction, context, "convert", key, fingerprint, current, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return new(QuotationCommandStatus.AlreadyLinked, current);
        }
        if (current.Version != request.ExpectedVersion) return new(QuotationCommandStatus.RevisionConflict, null);
        var at = Now();
        if (at < current.CurrentResponse.RecordedAt) return new(QuotationCommandStatus.ValidityConflict, null);
        var writer = orders ?? throw new InvalidOperationException("The owning Order writer is unavailable.");
        var accepted = QuotationRules.OrderFacts(current.CurrentIssued!);
        var order = await writer.CreateAsync(context, accepted, connection, transaction, at, ct).ConfigureAwait(false);
        var conversion = new QuotationConversionFacts(quotationId, request.IssuedRevisionId, context.TenantId, context.AccountId, at, order);
        ValidateConversion(conversion, current.CurrentIssued!, current.CurrentResponse, context);
        await using (var insert = Command("InsertConversion", connection, transaction, context, ("id", quotationId), ("revision_id", request.IssuedRevisionId),
            ("order_id", order.OrderId), ("facts", Serialize(conversion, 16 * 1024 * 1024))))
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await AdvanceVersionAsync(connection, transaction, context, quotationId, current.Version, ct).ConfigureAwait(false);
        var snapshot = current with { Version = checked(current.Version + 1), Conversion = conversion };
        await InsertSnapshotReceiptAsync(connection, transaction, context, "convert", key, fingerprint, snapshot, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return new(QuotationCommandStatus.Converted, snapshot);
    }
    public async Task<QuotationResponseFacts?> FindResponseAsync(TenantContext context, Guid quotationId, Guid issuedRevisionId, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
        await using var command = Command("ReadResponse", connection, transaction, context, ("id", quotationId), ("revision_id", issuedRevisionId));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var response = Deserialize<QuotationResponseFacts>(reader.GetString(0));
        var issued = Deserialize<QuotationIssuedFacts>(reader.GetString(1));
        ValidateIssued(issued, context, quotationId); ValidateResponse(response, issued, context);
        if (issued.RevisionId != issuedRevisionId || reader.GetInt32(2) != 1 || reader.GetInt16(3) != (short)response.Kind ||
            reader.GetInt32(4) != 1 || reader.GetInt64(5) != issued.RevisionNumber)
            throw new InvalidOperationException("Stored quotation response identities or versions are inconsistent.");
        return response;
    }
    public async Task<AcceptedQuotationOrder?> ReadAsync(TenantContext context, Guid orderId, OrderQuotationOrigin origin, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context, ct).ConfigureAwait(false);
        await using var command = Command("ReadAcceptedOrder", connection, transaction, context, ("id", origin.QuotationId),
            ("revision_id", origin.IssuedRevisionId), ("order_id", orderId));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var conversion = Deserialize<QuotationConversionFacts>(reader.GetString(0));
        var issued = Deserialize<QuotationIssuedFacts>(reader.GetString(1));
        var response = Deserialize<QuotationResponseFacts>(reader.GetString(2));
        ValidateIssued(issued, context, origin.QuotationId); ValidateResponse(response, issued, context); ValidateConversion(conversion, issued, response, context);
        if (conversion.OriginalOrder.OrderId != orderId || issued.RevisionId != origin.IssuedRevisionId ||
            reader.GetInt32(3) != 1 || reader.GetInt32(4) != 1 || reader.GetInt32(5) != 1 ||
            reader.GetInt16(6) != (short)response.Kind || reader.GetInt64(7) != issued.RevisionNumber)
            throw new InvalidOperationException("Stored accepted quotation link identities or versions are inconsistent.");
        var accepted = QuotationRules.OrderFacts(issued);
        return accepted.Origin == origin ? accepted : null;
    }
    private static void ValidateResponse(QuotationResponseFacts response, QuotationIssuedFacts issued, TenantContext context)
    {
        if (issued is null || response.TenantId != context.TenantId || response.QuotationId != issued.QuotationId || response.IssuedRevisionId != issued.RevisionId ||
            !Enum.IsDefined(response.Kind) || response.RecordedByAccountId == Guid.Empty || response.RecordedAt.Offset != TimeSpan.Zero ||
            response.RecordedAt.Ticks % 10 != 0 || response.RecordedAt < issued.IssuedAt ||
            response.Kind == QuotationResponseKind.Accepted && response.RecordedAt >= issued.Offer.ValidUntil ||
            response.Kind == QuotationResponseKind.Expired && response.RecordedAt < issued.Offer.ValidUntil ||
            QuotationRules.Text(response.Evidence, 2000, "retained_response_invalid") != response.Evidence ||
            (response.Kind == QuotationResponseKind.Expired ? response.CustomerClaim is not null :
                QuotationRules.Text(response.CustomerClaim!, 300, "retained_response_invalid") != response.CustomerClaim))
            throw new InvalidOperationException("Stored quotation response facts are invalid.");
    }
    private static void ValidateConversion(QuotationConversionFacts conversion, QuotationIssuedFacts issued, QuotationResponseFacts? response, TenantContext context)
    {
        var order = conversion.OriginalOrder;
        if (issued is null || response?.Kind != QuotationResponseKind.Accepted || conversion.TenantId != context.TenantId ||
            conversion.QuotationId != issued.QuotationId || conversion.IssuedRevisionId != issued.RevisionId || conversion.ConvertedByAccountId == Guid.Empty ||
            conversion.ConvertedAt.Offset != TimeSpan.Zero || conversion.ConvertedAt.Ticks % 10 != 0 || conversion.ConvertedAt < response.RecordedAt ||
            order is null || order.OrderId == Guid.Empty || order.TenantId != context.TenantId || order.CreatedByAccountId != conversion.ConvertedByAccountId ||
            order.CreatedAt != conversion.ConvertedAt || order.State != OrderDraftState.Draft || order.Revision != 1 ||
            order.CommittedAt is not null || order.CommittedByAccountId is not null || order.AbandonedAt is not null || order.AbandonedByAccountId is not null ||
            order.ExternalProgramReference is not null ||
            !QuotationRules.OrderFacts(issued).Matches(order))
            throw new InvalidOperationException("Stored quotation conversion facts are invalid.");
        if (order.ProgramPolicy is not null) OrderProgramReference.RequireValidStored(order.ProgramPolicy, null);
    }
    private static QuotationResponseKind ResponseKind(string operation) => operation switch
    { "accept" => QuotationResponseKind.Accepted, "reject" => QuotationResponseKind.Rejected, "expire" => QuotationResponseKind.Expired, _ => throw new InvalidOperationException("Unknown quotation response operation.") };
    private static async Task<QuotationCommandResult?> LockAndReadReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, string operation, string key, string fingerprint, CancellationToken ct)
    {
        key = QuotationRules.Key(key);
        await using (var command = Command("LockReceipt", connection, transaction, context, ("actor", context.AccountId), ("operation", operation), ("key", key)))
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return await ReadReceiptAsync(connection, transaction, context, operation, key, fingerprint, ct).ConfigureAwait(false);
    }
    private static async Task LockHeadAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context, Guid quotationId, CancellationToken ct)
    {
        await using var command = Command("LockHead", connection, transaction, context, ("id", quotationId));
        await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }
    private static async Task AdvanceVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context, Guid quotationId, long expected, CancellationToken ct)
    {
        await using var command = Command("AdvanceVersion", connection, transaction, context, ("id", quotationId), ("expected", expected));
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1) throw new InvalidOperationException("The locked quotation changed unexpectedly.");
    }
    private static async Task InsertSnapshotReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context,
        string operation, string key, string fingerprint, QuotationSnapshot snapshot, CancellationToken ct)
    {
        ValidateSnapshot(snapshot, context);
        var version = snapshot.CurrentResponse is not null || snapshot.Conversion is not null ? 2 : 1;
        await using var command = Command("InsertReceipt", connection, transaction, context, ("actor", context.AccountId), ("operation", operation),
            ("key", QuotationRules.Key(key)), ("fingerprint", fingerprint), ("version", version), ("response", Serialize(snapshot, version == 1 ? 16 * 1024 * 1024 : 40 * 1024 * 1024)));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
