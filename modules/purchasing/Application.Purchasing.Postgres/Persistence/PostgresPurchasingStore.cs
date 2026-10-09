using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Tenancy;
using Npgsql;

namespace Application.Purchasing.Postgres;

public sealed partial class PostgresPurchasingStore(NpgsqlDataSource source, IPurchasingSourceReader sourceReader,
    IPurchasingAuthorityReader authorityReader, IPurchasingTransactionalAuthorityReader transactionalAuthorityReader,
    TimeProvider clock) : IPurchasingStore
{
    private const int MaximumRetainedBytes = 65536;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
    };

    public async Task<SupplierCommandResult> ExecuteSupplierAsync(TenantContext context, Guid supplierId,
        SupplierMutation mutation, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mutation);
        if (supplierId == Guid.Empty || !Enum.IsDefined(mutation.Kind) || mutation.ExpectedRevision is < 0 or long.MaxValue)
            throw new PurchasingValidationException("purchasing_request_invalid");
        if (!PurchasingRules.CanExecuteSupplier(await authorityReader.ResolveAsync(context, cancellationToken).ConfigureAwait(false)))
            return new(PurchasingStoreStatus.Forbidden);
        mutation = PurchasingRules.CanonicalMutation(mutation);
        var key = PurchasingRules.NormalizeIdempotencyKey(idempotencyKey);
        var fingerprint = PurchasingRules.Fingerprint(supplierId, mutation);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        await LockAsync(connection, transaction, context, "supplier", supplierId, (int)mutation.Kind, key, cancellationToken).ConfigureAwait(false);
        var authority = await transactionalAuthorityReader.ResolveAsync(connection, transaction, context, cancellationToken).ConfigureAwait(false);
        if (!PurchasingRules.CanExecuteSupplier(authority)) return new(PurchasingStoreStatus.Forbidden);
        var current = await ReadSupplierAsync(connection, transaction, context.TenantId, supplierId, cancellationToken).ConfigureAwait(false);
        var replay = await ReadSupplierReceiptAsync(connection, transaction, context, supplierId, mutation.Kind, key, fingerprint, cancellationToken).ConfigureAwait(false);
        if (replay is not null) return replay;
        var transition = PurchasingRules.ApplySupplier(context.TenantId, current, mutation, supplierId, Guid.CreateVersion7(),
            context.AccountId, authority.AuthorizationRevision, UtcNow());
        if (transition.Status != PurchasingMutationStatus.Applied)
            return new(PurchasingStoreStatus.TransitionRejected, Rejection: transition.Status);
        var supplier = transition.Snapshot ?? throw InvalidFacts();
        var entry = transition.Event ?? throw InvalidFacts();
        var snapshot = Serialize(supplier);
        var eventFacts = Serialize(entry);
        var receipt = Serialize(new Receipt<SupplierSnapshot>(1, (int)mutation.Kind, supplier));
        if (!await RetainedFactsFitAsync(connection, transaction, snapshot, "null", eventFacts, receipt, cancellationToken).ConfigureAwait(false))
            return new(PurchasingStoreStatus.TransitionRejected, Rejection: PurchasingMutationStatus.ResourceLimit);
        if (current is null)
            await ExecuteSqlAsync(connection, transaction, "InsertSupplier", cancellationToken,
                ("tenant", context.TenantId), ("id", supplierId), ("revision", supplier.Revision), ("snapshot", snapshot),
                ("created", supplier.CreatedAt), ("updated", supplier.UpdatedAt)).ConfigureAwait(false);
        else
            await ExecuteSqlAsync(connection, transaction, "UpdateSupplier", cancellationToken,
                ("tenant", context.TenantId), ("id", supplierId), ("revision", supplier.Revision), ("expected", current.Revision),
                ("snapshot", snapshot), ("updated", supplier.UpdatedAt)).ConfigureAwait(false);
        await ExecuteSqlAsync(connection, transaction, "InsertSupplierEvent", cancellationToken,
            ("tenant", context.TenantId), ("id", supplierId), ("event", entry.EventId), ("revision", entry.Revision),
            ("actor", entry.ActorAccountId), ("authorization", entry.AuthorizationRevision), ("kind", (int)entry.Kind),
            ("facts", eventFacts), ("snapshot", snapshot), ("at", entry.RecordedAt)).ConfigureAwait(false);
        await ExecuteSqlAsync(connection, transaction, "InsertSupplierReceipt", cancellationToken,
            ("tenant", context.TenantId), ("id", supplierId), ("actor", context.AccountId), ("operation", (int)mutation.Kind),
            ("key", key), ("fingerprint", fingerprint), ("revision", supplier.Revision),
            ("response", receipt)).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(PurchasingStoreStatus.Applied, supplier);
    }

    public async Task<PurchasingCommandResult> ExecutePurchaseAsync(TenantContext context, Guid purchaseId,
        PurchasingMutation mutation, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mutation);
        if (purchaseId == Guid.Empty || !Enum.IsDefined(mutation.Kind) || mutation.ExpectedRevision is < 0 or long.MaxValue)
            throw new PurchasingValidationException("purchasing_request_invalid");
        if (!PurchasingRules.CanExecute(await authorityReader.ResolveAsync(context, cancellationToken).ConfigureAwait(false), mutation.Kind))
            return new(PurchasingStoreStatus.Forbidden);
        mutation = PurchasingRules.CanonicalMutation(mutation);
        var key = PurchasingRules.NormalizeIdempotencyKey(idempotencyKey);
        var fingerprint = PurchasingRules.Fingerprint(purchaseId, mutation);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        await LockAsync(connection, transaction, context, "purchase", purchaseId, (int)mutation.Kind, key, cancellationToken).ConfigureAwait(false);
        if (mutation.Kind == PurchasingCommandKind.Request && mutation.SupplierId is { } supplierId)
            await ExecuteSqlAsync(connection, transaction, "LockAggregate", cancellationToken,
                ("tenant", context.TenantId), ("family", "supplier"), ("id", supplierId)).ConfigureAwait(false);
        var authority = await transactionalAuthorityReader.ResolveAsync(connection, transaction, context, cancellationToken).ConfigureAwait(false);
        if (!PurchasingRules.CanExecute(authority, mutation.Kind)) return new(PurchasingStoreStatus.Forbidden);
        var current = await ReadPurchaseAsync(connection, transaction, context.TenantId, purchaseId, cancellationToken).ConfigureAwait(false);
        var replay = await ReadPurchaseReceiptAsync(connection, transaction, context, purchaseId, mutation.Kind, key, fingerprint, cancellationToken).ConfigureAwait(false);
        if (replay is not null) return replay;
        SupplierSnapshot? supplier = null;
        PurchasingSourceFacts? facts = null;
        if (current is null && mutation.Kind == PurchasingCommandKind.Request && mutation.SupplierId is { } requestedSupplier)
        {
            supplier = await ReadSupplierAsync(connection, transaction, context.TenantId, requestedSupplier, cancellationToken).ConfigureAwait(false);
            if (supplier is null) return new(PurchasingStoreStatus.NotFound);
            facts = await sourceReader.ResolveAsync(connection, transaction, context, mutation, cancellationToken).ConfigureAwait(false);
            if (facts is null) return new(PurchasingStoreStatus.NotFound);
        }
        var transition = PurchasingRules.ApplyPurchase(context.TenantId, current, mutation, supplier, facts, purchaseId,
            Guid.CreateVersion7(), context.AccountId, authority.AuthorizationRevision, UtcNow());
        if (transition.Status != PurchasingMutationStatus.Applied)
            return new(PurchasingStoreStatus.TransitionRejected, Rejection: transition.Status);
        var purchase = transition.Snapshot ?? throw InvalidFacts();
        var entry = transition.Event ?? throw InvalidFacts();
        var snapshot = Serialize(purchase);
        var sourceFacts = Serialize(Source(purchase));
        var eventFacts = Serialize(entry);
        var receipt = Serialize(new Receipt<PurchaseSnapshot>(1, (int)mutation.Kind, purchase));
        if (!await RetainedFactsFitAsync(connection, transaction, snapshot, sourceFacts, eventFacts, receipt, cancellationToken).ConfigureAwait(false))
            return new(PurchasingStoreStatus.TransitionRejected, Rejection: PurchasingMutationStatus.ResourceLimit);
        if (current is null)
            await ExecuteSqlAsync(connection, transaction, "InsertPurchase", cancellationToken,
                ("tenant", context.TenantId), ("id", purchaseId), ("supplier", purchase.Supplier.SupplierId),
                ("revision", purchase.Revision), ("source", sourceFacts), ("snapshot", snapshot),
                ("created", purchase.CreatedAt), ("updated", purchase.UpdatedAt)).ConfigureAwait(false);
        else
            await ExecuteSqlAsync(connection, transaction, "UpdatePurchase", cancellationToken,
                ("tenant", context.TenantId), ("id", purchaseId), ("revision", purchase.Revision), ("expected", current.Revision),
                ("snapshot", snapshot), ("updated", purchase.UpdatedAt)).ConfigureAwait(false);
        await ExecuteSqlAsync(connection, transaction, "InsertPurchaseEvent", cancellationToken,
            ("tenant", context.TenantId), ("id", purchaseId), ("event", entry.EventId), ("revision", entry.Revision),
            ("actor", entry.ActorAccountId), ("authorization", entry.AuthorizationRevision), ("kind", (int)entry.Kind),
            ("target", entry.TargetEventId ?? (object)DBNull.Value), ("facts", eventFacts),
            ("snapshot", snapshot), ("at", entry.RecordedAt)).ConfigureAwait(false);
        await ExecuteSqlAsync(connection, transaction, "InsertPurchaseReceipt", cancellationToken,
            ("tenant", context.TenantId), ("id", purchaseId), ("actor", context.AccountId), ("operation", (int)mutation.Kind),
            ("key", key), ("fingerprint", fingerprint), ("revision", purchase.Revision),
            ("response", receipt)).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(PurchasingStoreStatus.Applied, purchase);
    }

    private DateTimeOffset UtcNow()
    {
        var now = clock.GetUtcNow();
        return new(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
    }

    private static async Task LockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context,
        string family, Guid id, int operation, string key, CancellationToken ct)
    {
        await ExecuteSqlAsync(connection, transaction, "LockReceipt", ct, ("tenant", context.TenantId), ("family", family),
            ("actor", context.AccountId), ("operation", operation), ("key", key)).ConfigureAwait(false);
        await ExecuteSqlAsync(connection, transaction, "LockAggregate", ct, ("tenant", context.TenantId), ("family", family), ("id", id)).ConfigureAwait(false);
    }

    private sealed record Receipt<T>(int SchemaVersion, int Operation, T Snapshot);
    private sealed record PurchaseSource(SupplierSnapshot Supplier, PurchasingUnitFacts Unit, PurchasingOrderLink? Order);
    private static PurchaseSource Source(PurchaseSnapshot snapshot) => new(snapshot.Supplier, snapshot.Unit, snapshot.Order);
    private static InvalidOperationException InvalidFacts() => new("Retained purchasing facts are invalid or unsupported.");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static async Task<bool> RetainedFactsFitAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string snapshot, string sourceFacts, string eventFacts, string receipt, CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetByteCount(snapshot) > MaximumRetainedBytes ||
            Encoding.UTF8.GetByteCount(sourceFacts) > MaximumRetainedBytes ||
            Encoding.UTF8.GetByteCount(eventFacts) > MaximumRetainedBytes ||
            Encoding.UTF8.GetByteCount(receipt) > MaximumRetainedBytes) return false;
        await using var command = Command(connection, transaction, "CheckRetainedBytes", ("snapshot", snapshot),
            ("source", sourceFacts), ("event", eventFacts), ("receipt", receipt), ("maximum", MaximumRetainedBytes));
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? throw InvalidFacts());
    }

    private static T Deserialize<T>(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumRetainedBytes) throw InvalidFacts();
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            RequireUniqueProperties(document.RootElement);
            var value = JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw InvalidFacts();
            if (!JsonElement.DeepEquals(document.RootElement, JsonSerializer.SerializeToElement(value, JsonOptions))) throw InvalidFacts();
            return value;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or OverflowException or NullReferenceException)
        {
            throw InvalidFacts();
        }
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

    private static bool Same<T>(T left, T right) => JsonElement.DeepEquals(
        JsonSerializer.SerializeToElement(left, JsonOptions), JsonSerializer.SerializeToElement(right, JsonOptions));

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenant, CancellationToken ct) =>
        await ExecuteSqlAsync(connection, transaction, "SetTenant", ct, ("tenant", tenant.ToString("D"))).ConfigureAwait(false);

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction transaction, string name,
        params (string Name, object Value)[] parameters)
    {
        using var resource = typeof(PostgresPurchasingStore).Assembly.GetManifestResourceStream("Application.Purchasing.Postgres.Sql." + name + ".sql")
            ?? throw new InvalidOperationException("The purchasing SQL resource is missing.");
        using var reader = new StreamReader(resource);
        var command = new NpgsqlCommand(reader.ReadToEnd(), connection, transaction);
        foreach (var (parameter, value) in parameters) command.Parameters.AddWithValue(parameter, value);
        return command;
    }

    private static async Task ExecuteSqlAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string name, CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        await using var command = Command(connection, transaction, name, parameters);
        var count = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (name is not ("SetTenant" or "LockAggregate" or "LockReceipt") && count != 1) throw InvalidFacts();
    }
}
