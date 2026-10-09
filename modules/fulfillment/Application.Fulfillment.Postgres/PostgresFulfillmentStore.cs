using System.Data;
using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Tenancy;
using Npgsql;

namespace Application.Fulfillment.Postgres;

public sealed partial class PostgresFulfillmentStore(NpgsqlDataSource source, IFulfillmentStartSource startSource,
    IFulfillmentAuthorityReader authorityReader, IFulfillmentTransactionalAuthorityReader transactionalAuthorityReader,
    TimeProvider clock) : IFulfillmentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
    };

    public async Task<FulfillmentCommandResult> ExecuteAsync(TenantContext context, Guid orderId, FulfillmentMutation mutation,
        long? expectedOrderRevision, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mutation);
        if (orderId == Guid.Empty || !Enum.IsDefined(mutation.Kind) || mutation.ExpectedRevision is < 0 or long.MaxValue ||
            mutation.Kind == FulfillmentCommandKind.Start && expectedOrderRevision is (null or < 1 or long.MaxValue) ||
            mutation.Kind != FulfillmentCommandKind.Start && expectedOrderRevision is not null)
            throw new FulfillmentValidationException("fulfillment_request_invalid");
        var authority = await authorityReader.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        if (!FulfillmentRules.CanExecute(authority, mutation.Kind)) return new(FulfillmentStoreStatus.Forbidden);
        string key;
        try { key = FulfillmentRules.NormalizeIdempotencyKey(idempotencyKey); }
        catch (ArgumentException) { throw new FulfillmentValidationException("idempotency_key_invalid"); }
        mutation = CanonicalMutation(mutation);
        var fingerprint = Fingerprint(orderId, mutation, expectedOrderRevision);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "LockReceipt", cancellationToken, ("tenant", context.TenantId), ("actor", context.AccountId),
            ("operation", (int)mutation.Kind), ("key", key)).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "LockWork", cancellationToken, ("tenant", context.TenantId), ("order", orderId)).ConfigureAwait(false);
        authority = await transactionalAuthorityReader.ResolveAsync(connection, transaction, context, cancellationToken).ConfigureAwait(false);
        if (!FulfillmentRules.CanExecute(authority, mutation.Kind)) return new(FulfillmentStoreStatus.Forbidden);
        var replay = await ReadReceiptAsync(connection, transaction, context, orderId, mutation.Kind, key, fingerprint, cancellationToken).ConfigureAwait(false);
        if (replay is not null) return replay;
        var current = await ReadWorkAsync(connection, transaction, context.TenantId, orderId, cancellationToken).ConfigureAwait(false);
        var facts = current?.Source;
        if (mutation.Kind == FulfillmentCommandKind.Start)
        {
            if (current is null) facts = await startSource.ResolveAsync(connection, transaction, context, orderId, cancellationToken).ConfigureAwait(false);
            if (facts is null) return new(FulfillmentStoreStatus.NotFound);
            if (facts.OrderRevision != expectedOrderRevision) return new(FulfillmentStoreStatus.OrderRevisionConflict);
        }
        if (facts is null) return new(FulfillmentStoreStatus.NotFound);
        if (!FulfillmentRules.IsEligibleSource(facts, context.TenantId, orderId))
            return new(FulfillmentStoreStatus.TransitionRejected, Rejection: FulfillmentMutationStatus.SourceIneligible);
        FulfillmentCorrectionTarget? target = null;
        if (mutation.Kind == FulfillmentCommandKind.Correct && mutation.TargetEventId is { } targetId)
            target = await ReadCorrectionTargetAsync(connection, transaction, context.TenantId, orderId, targetId, facts, current!.Revision, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow(); now = new(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
        var transition = FulfillmentRules.Apply(facts, current, mutation, target,
            current?.WorkId ?? Guid.CreateVersion7(), Guid.CreateVersion7(), context.AccountId, authority.AuthorizationRevision, now);
        if (transition.Status != FulfillmentMutationStatus.Applied)
            return new(FulfillmentStoreStatus.TransitionRejected, Rejection: transition.Status);
        var work = transition.Snapshot ?? throw InvalidFacts();
        var entry = transition.Event ?? throw InvalidFacts();
        if (current is null)
            await ExecuteAsync(connection, transaction, "InsertWork", cancellationToken, ("tenant", context.TenantId), ("order", orderId),
                ("work", work.WorkId), ("revision", work.Revision), ("source", Serialize(work.Source)), ("snapshot", Serialize(work)),
                ("created", work.CreatedAt), ("updated", work.UpdatedAt)).ConfigureAwait(false);
        else
            await ExecuteAsync(connection, transaction, "UpdateWork", cancellationToken, ("tenant", context.TenantId), ("order", orderId),
                ("revision", work.Revision), ("expected", current.Revision), ("snapshot", Serialize(work)), ("updated", work.UpdatedAt)).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "InsertEvent", cancellationToken, ("tenant", context.TenantId), ("order", orderId),
            ("event", entry.EventId), ("revision", entry.Revision), ("actor", entry.ActorAccountId), ("authorization", entry.AuthorizationRevision),
            ("kind", (int)entry.Kind), ("target", entry.TargetEventId ?? (object)DBNull.Value), ("facts", Serialize(entry)), ("at", entry.RecordedAt)).ConfigureAwait(false);
        var entryActive = entry.Kind != FulfillmentEventKind.Correction || entry.Replacement is not null;
        await ExecuteAsync(connection, transaction, "InsertActivity", cancellationToken, ("tenant", context.TenantId), ("order", orderId),
            ("event", entry.EventId), ("active", entryActive), ("revision", entry.Revision),
            ("kind", (int)(entry.Replacement?.Kind ?? entry.Kind)),
            ("target", entry.Kind == FulfillmentEventKind.Correction ? DBNull.Value : entry.TargetEventId ?? (object)DBNull.Value)).ConfigureAwait(false);
        if (entry.Kind == FulfillmentEventKind.Correction)
            await ExecuteAsync(connection, transaction, "VoidActivity", cancellationToken, ("tenant", context.TenantId), ("order", orderId),
                ("target", entry.TargetEventId ?? throw InvalidFacts()), ("event", entry.EventId)).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "InsertReceipt", cancellationToken, ("tenant", context.TenantId), ("order", orderId),
            ("actor", context.AccountId), ("operation", (int)mutation.Kind), ("key", key), ("fingerprint", fingerprint),
            ("revision", work.Revision), ("response", Serialize(new Receipt(1, mutation.Kind, work)))).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(FulfillmentStoreStatus.Applied, work);
    }

    private static FulfillmentMutation CanonicalMutation(FulfillmentMutation mutation)
    {
        static string? Text(string? value, int maximum)
        {
            if (value is null) return null;
            if (value.Length > maximum * 2 + 8192) throw new FulfillmentValidationException("fulfillment_text_invalid");
            for (var offset = 0; offset < value.Length;)
            {
                if (System.Text.Rune.DecodeFromUtf16(value.AsSpan(offset), out var rune, out var consumed) != System.Buffers.OperationStatus.Done ||
                    System.Text.Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.Control)
                    throw new FulfillmentValidationException("fulfillment_text_invalid");
                offset += consumed;
            }
            var normalized = value.Trim();
            if (normalized.EnumerateRunes().Count() > maximum) throw new FulfillmentValidationException("fulfillment_text_invalid");
            return normalized;
        }
        return mutation with
        {
            Recipient = Text(mutation.Recipient, FulfillmentRules.MaximumRecipientScalars),
            Reason = Text(mutation.Reason, FulfillmentRules.MaximumReasonScalars),
            Replacement = mutation.Replacement is not { } replacement ? null : replacement with { Recipient = Text(replacement.Recipient, FulfillmentRules.MaximumRecipientScalars) }
        };
    }

    private static string Fingerprint(Guid orderId, FulfillmentMutation mutation, long? orderRevision)
    {
        static string? Quantity(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture);
        var text = JsonSerializer.Serialize(new
        {
            schema = 1,
            orderId,
            orderRevision,
            mutation.Kind,
            mutation.ExpectedRevision,
            quantity = Quantity(mutation.Quantity),
            recipient = mutation.Recipient,
            reason = mutation.Reason,
            mutation.Disposition,
            mutation.TargetEventId,
            replacement = mutation.Replacement is not { } replacement ? null : new
            {
                replacement.Kind,
                quantity = Quantity(replacement.Quantity),
                replacement.Recipient,
                replacement.Disposition
            }
        }, JsonOptions);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static async Task<FulfillmentCommandResult?> ReadReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, Guid orderId, FulfillmentCommandKind operation, string key, string fingerprint, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "ReadReceipt", ("tenant", context.TenantId), ("actor", context.AccountId),
            ("operation", (int)operation), ("key", key));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        if (reader.GetInt16(6) != 1 || reader.GetInt16(11) != 1 || reader.GetInt16(12) != 1) throw InvalidFacts();
        var fingerprintMatches = reader.GetString(8) == fingerprint;
        var receipt = Deserialize<Receipt>(reader.GetString(9));
        var work = receipt.Work ?? throw InvalidFacts();
        var pinnedSource = Deserialize<FulfillmentSourceFacts>(reader.GetString(13));
        var entry = ReadEvent(reader, pinnedSource, 6, relationalKindIndex: 7);
        if (receipt.SchemaVersion != 1 || receipt.Operation != operation || work.TenantId != context.TenantId ||
            work.OrderId != reader.GetGuid(16) || work.Revision != reader.GetInt64(10) || work.WorkId != reader.GetGuid(14) ||
            work.CreatedAt != reader.GetFieldValue<DateTimeOffset>(15) || work.Revision != reader.GetInt64(2) ||
            entry.ActorAccountId != context.AccountId || entry.Kind != (FulfillmentEventKind)reader.GetInt32(7) ||
            entry.Kind != (FulfillmentEventKind)operation || entry.RecordedAt != work.UpdatedAt ||
            !SameSource(work.Source, pinnedSource)) throw InvalidFacts();
        RequireValidWork(receipt.Work);
        if (fingerprintMatches && work.OrderId != orderId) throw InvalidFacts();
        return fingerprintMatches ? new(FulfillmentStoreStatus.Replayed, receipt.Work) : new(FulfillmentStoreStatus.IdempotencyConflict);
    }

    private static async Task<FulfillmentSnapshot?> ReadWorkAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenant, Guid order, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "ReadWork", ("tenant", tenant), ("order", order));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        if (reader.GetInt16(6) != 1) throw InvalidFacts();
        var work = Deserialize<FulfillmentSnapshot>(reader.GetString(0));
        RequireValidWork(work);
        if (work.TenantId != tenant || work.OrderId != order || work.Revision != reader.GetInt64(1) || work.WorkId != reader.GetGuid(2) ||
            work.CreatedAt != reader.GetFieldValue<DateTimeOffset>(3) || work.UpdatedAt != reader.GetFieldValue<DateTimeOffset>(4) ||
            !SameSource(work.Source, Deserialize<FulfillmentSourceFacts>(reader.GetString(5)))) throw InvalidFacts();
        return work;
    }

    private static async Task<FulfillmentCorrectionTarget?> ReadCorrectionTargetAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenant, Guid order, Guid target, FulfillmentSourceFacts sourceFacts, long currentRevision, CancellationToken ct)
    {
        FulfillmentEvent entry; bool active; bool dependents;
        await using (var command = Command(connection, transaction, "ReadTarget", ("tenant", tenant), ("order", order), ("target", target)))
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
            entry = ReadEvent(reader, sourceFacts, 8, 9, 10); active = reader.GetBoolean(6); dependents = reader.GetBoolean(7);
        }
        if (entry.Revision > currentRevision) throw InvalidFacts();
        if (entry.Kind == FulfillmentEventKind.Correction)
        {
            if (entry.Replacement is not { } replacement) return null;
            entry = entry with
            {
                Kind = replacement.Kind,
                Quantity = replacement.Quantity,
                Recipient = replacement.Recipient,
                Disposition = replacement.Disposition,
                TargetEventId = null,
                Replacement = null
            };
        }
        FulfillmentEvent? referenced = null;
        if (entry.Kind == FulfillmentEventKind.Resolution && entry.TargetEventId is { } dispositionId)
        {
            await using var command = Command(connection, transaction, "ReadTarget", ("tenant", tenant), ("order", order), ("target", dispositionId));
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (!reader.GetBoolean(6)) throw InvalidFacts();
                referenced = ReadEvent(reader, sourceFacts, 8, 9, 10);
                if (referenced.Revision >= entry.Revision) throw InvalidFacts();
                if (referenced.Kind == FulfillmentEventKind.Correction && referenced.Replacement is { } replacement)
                    referenced = referenced with
                    {
                        Kind = replacement.Kind,
                        Quantity = replacement.Quantity,
                        Recipient = replacement.Recipient,
                        Disposition = replacement.Disposition,
                        TargetEventId = null,
                        Replacement = null
                    };
            }
        }
        return new(entry, active, dependents, referenced);
    }

    private static FulfillmentEvent ReadEvent(NpgsqlDataReader reader, FulfillmentSourceFacts sourceFacts,
        int eventSchemaVersionIndex, int? activitySchemaVersionIndex = null, int? relationalKindIndex = null)
    {
        var entry = Deserialize<FulfillmentEvent>(reader.GetString(0));
        if (reader.GetInt16(eventSchemaVersionIndex) != 1 || activitySchemaVersionIndex is { } activityIndex && reader.GetInt16(activityIndex) != 1)
            throw InvalidFacts();
        if (entry.EventId == Guid.Empty || entry.EventId != reader.GetGuid(1) || entry.Revision != reader.GetInt64(2) ||
            entry.ActorAccountId != reader.GetGuid(3) || entry.AuthorizationRevision != reader.GetInt64(4) ||
            entry.RecordedAt != reader.GetFieldValue<DateTimeOffset>(5) || !Enum.IsDefined(entry.Kind) ||
            relationalKindIndex is { } kindIndex && entry.Kind != (FulfillmentEventKind)reader.GetInt32(kindIndex)) throw InvalidFacts();
        RequireValidEvent(entry, sourceFacts);
        return entry;
    }

    private static void RequireValidEvent(FulfillmentEvent entry, FulfillmentSourceFacts source)
    {
        static bool ValidQuantity(decimal? quantity, FulfillmentSourceFacts facts) => quantity is { } value && value > 0m &&
            value <= facts.OrderedQuantity && decimal.Round(value, facts.UnitPrecision) == value;
        static bool ValidText(string? value, int maximum)
        {
            if (value is null || value.Length == 0 || !string.Equals(value, value.Trim(), StringComparison.Ordinal)) return false;
            var count = 0;
            for (var offset = 0; offset < value.Length;)
            {
                if (Rune.DecodeFromUtf16(value.AsSpan(offset), out var rune, out var consumed) != OperationStatus.Done ||
                    Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.Control || ++count > maximum) return false;
                offset += consumed;
            }
            return count > 0;
        }
        static bool ValidReplacement(FulfillmentEventFacts? facts, FulfillmentSourceFacts sourceFacts)
        {
            if (facts is null) return true;
            return facts.Kind switch
            {
                FulfillmentEventKind.Completion => ValidQuantity(facts.Quantity, sourceFacts) && facts.Recipient is null && facts.Disposition is null,
                FulfillmentEventKind.Handoff => ValidQuantity(facts.Quantity, sourceFacts) && ValidText(facts.Recipient, FulfillmentRules.MaximumRecipientScalars) && facts.Disposition is null,
                FulfillmentEventKind.Disposition => (facts.Quantity is null || ValidQuantity(facts.Quantity, sourceFacts)) &&
                    facts.Recipient is null && facts.Disposition is { } replacementDisposition && Enum.IsDefined(replacementDisposition),
                _ => false,
            };
        }

        if (entry.EventId == Guid.Empty || entry.Revision < 1 || entry.ActorAccountId == Guid.Empty || entry.AuthorizationRevision < 1 ||
            entry.RecordedAt == default || entry.RecordedAt.Offset != TimeSpan.Zero) throw InvalidFacts();
        var noSharedFacts = entry.Quantity is null && entry.Recipient is null && entry.Reason is null && entry.Disposition is null &&
            entry.TargetEventId is null && entry.Replacement is null;
        var valid = entry.Kind switch
        {
            FulfillmentEventKind.Started => noSharedFacts,
            FulfillmentEventKind.Completion => ValidQuantity(entry.Quantity, source) && entry.Recipient is null && entry.Reason is null &&
                entry.Disposition is null && entry.TargetEventId is null && entry.Replacement is null,
            FulfillmentEventKind.Handoff => ValidQuantity(entry.Quantity, source) && ValidText(entry.Recipient, FulfillmentRules.MaximumRecipientScalars) &&
                entry.Reason is null && entry.Disposition is null && entry.TargetEventId is null && entry.Replacement is null,
            FulfillmentEventKind.Disposition => (entry.Quantity is null || ValidQuantity(entry.Quantity, source)) &&
                ValidText(entry.Reason, FulfillmentRules.MaximumReasonScalars) && entry.Disposition is { } eventDisposition && Enum.IsDefined(eventDisposition) &&
                entry.Recipient is null && entry.TargetEventId is null && entry.Replacement is null,
            FulfillmentEventKind.Resolution => ValidText(entry.Reason, FulfillmentRules.MaximumReasonScalars) && entry.TargetEventId is { } resolutionTarget &&
                resolutionTarget != Guid.Empty && entry.Quantity is null && entry.Recipient is null && entry.Disposition is null && entry.Replacement is null,
            FulfillmentEventKind.Correction => ValidText(entry.Reason, FulfillmentRules.MaximumReasonScalars) && entry.TargetEventId is { } correctionTarget &&
                correctionTarget != Guid.Empty && entry.Quantity is null && entry.Recipient is null && entry.Disposition is null && ValidReplacement(entry.Replacement, source),
            _ => false,
        };
        if (!valid) throw InvalidFacts();
    }

    private static bool SameSource(FulfillmentSourceFacts left, FulfillmentSourceFacts right) =>
        JsonElement.DeepEquals(JsonSerializer.SerializeToElement(left, JsonOptions), JsonSerializer.SerializeToElement(right, JsonOptions));

    private static void RequireValidWork(FulfillmentSnapshot work)
    {
        if (!FulfillmentRules.IsEligibleSource(work.Source, work.TenantId, work.OrderId) || work.WorkId == Guid.Empty ||
            work.Revision < 1 || work.CreatedAt == default || work.CreatedAt.Offset != TimeSpan.Zero || work.UpdatedAt < work.CreatedAt ||
            work.UpdatedAt.Offset != TimeSpan.Zero || work.CompletedQuantity < 0 || work.CompletedQuantity > work.Source.OrderedQuantity ||
            work.HandedOffQuantity < 0 || work.HandedOffQuantity > work.CompletedQuantity ||
            decimal.Round(work.CompletedQuantity, work.Source.UnitPrecision) != work.CompletedQuantity ||
            decimal.Round(work.HandedOffQuantity, work.Source.UnitPrecision) != work.HandedOffQuantity ||
            (work.ActiveDispositionId is null) != (work.ActiveDispositionKind is null) || work.ActiveDispositionId == Guid.Empty ||
            work.ActiveDispositionKind is { } kind && !Enum.IsDefined(kind) || !work.Started && (work.CompletedQuantity != 0 || work.HandedOffQuantity != 0 || work.ActiveDispositionId is not null)) throw InvalidFacts();
    }

    private static T Deserialize<T>(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > 65536) throw InvalidFacts();
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var element = document.RootElement;
            RequireUniqueProperties(element);
            if (typeof(T) == typeof(FulfillmentSnapshot)) RequireWorkShape(element);
            else if (typeof(T) == typeof(FulfillmentSourceFacts)) RequireSourceShape(element);
            else if (typeof(T) == typeof(FulfillmentEvent))
            {
                RequireProperties(element, ["eventId", "revision", "actorAccountId", "recordedAt", "authorizationRevision", "kind",
                    "quantity", "recipient", "reason", "disposition", "targetEventId", "replacement"]);
                if (element.GetProperty("replacement").ValueKind != JsonValueKind.Null)
                    RequireProperties(element.GetProperty("replacement"), ["kind", "quantity", "recipient", "disposition"]);
            }
            else if (typeof(T) == typeof(Receipt))
            {
                RequireProperties(element, ["schemaVersion", "operation", "work"]);
                RequireWorkShape(element.GetProperty("work"));
            }
            var value = JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw InvalidFacts();
            if (value is FulfillmentSnapshot work && (!element.GetProperty("stage").TryGetInt32(out var stage) || stage != (int)work.Stage)) throw InvalidFacts();
            return value;
        }
        catch (JsonException) { throw InvalidFacts(); }
    }

    private static void RequireWorkShape(JsonElement element)
    {
        RequireProperties(element, ["source", "workId", "tenantId", "orderId", "revision", "started", "completedQuantity",
            "handedOffQuantity", "activeDispositionId", "activeDispositionKind", "createdAt", "updatedAt", "stage"]);
        RequireSourceShape(element.GetProperty("source"));
        if (element.GetProperty("started").ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw InvalidFacts();
    }

    private static void RequireSourceShape(JsonElement element)
    {
        RequireProperties(element, ["tenantId", "orderId", "orderRevision", "programId", "linePosition", "itemId", "unitId", "unitCode",
            "unitPrecision", "orderedQuantity", "isCommitted", "lineCount", "isCatalog", "isNonStock", "hasConversion", "isService"]);
        foreach (var name in new[] { "isCommitted", "isCatalog", "isNonStock", "hasConversion", "isService" })
            if (element.GetProperty(name).ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw InvalidFacts();
    }

    private static void RequireProperties(JsonElement element, string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object || names.Any(name => !element.TryGetProperty(name, out _))) throw InvalidFacts();
    }

    private static void RequireUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw InvalidFacts();
                RequireUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RequireUniqueProperties(item);
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static InvalidOperationException InvalidFacts() => new("Retained fulfillment facts are invalid or unsupported.");
    private sealed record Receipt(int SchemaVersion, FulfillmentCommandKind Operation, FulfillmentSnapshot Work);

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenant, CancellationToken ct)
    {
        await ExecuteAsync(connection, transaction, "SetTenant", ct, ("tenant", tenant.ToString("D"))).ConfigureAwait(false);
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction transaction, string name, params (string Name, object Value)[] parameters)
    {
        using var resource = typeof(PostgresFulfillmentStore).Assembly.GetManifestResourceStream("Application.Fulfillment.Postgres.Sql." + name + ".sql")
            ?? throw new InvalidOperationException("The fulfillment SQL resource is missing.");
        using var reader = new StreamReader(resource);
        var command = new NpgsqlCommand(reader.ReadToEnd(), connection, transaction);
        foreach (var (parameter, value) in parameters) command.Parameters.AddWithValue(parameter, value);
        return command;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string name, CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        await using var command = Command(connection, transaction, name, parameters);
        var count = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (name is not ("SetTenant" or "LockWork" or "LockReceipt") && count != 1) throw InvalidFacts();
    }
}
