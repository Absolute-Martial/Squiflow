using Npgsql;

namespace Application.Purchasing.Postgres;

public sealed partial class PostgresPurchasingStore
{
    private static async Task<SupplierSnapshot?> ReadSupplierAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenant, Guid supplierId, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "ReadSupplier", ("tenant", tenant), ("id", supplierId));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? ReadSupplier(reader, tenant, supplierId) : null;
    }

    private static SupplierSnapshot ReadSupplier(NpgsqlDataReader reader, Guid tenant, Guid? supplierId = null)
    {
        var snapshot = Deserialize<SupplierSnapshot>(reader.GetString(0));
        if (!PurchasingRules.IsValidSupplier(snapshot) || reader.GetInt16(1) != 1 || snapshot.TenantId != tenant ||
            snapshot.SupplierId != reader.GetGuid(3) || supplierId is { } id && snapshot.SupplierId != id ||
            snapshot.Revision != reader.GetInt64(2) || snapshot.CreatedAt != reader.GetFieldValue<DateTimeOffset>(4) ||
            snapshot.UpdatedAt != reader.GetFieldValue<DateTimeOffset>(5) || !reader.GetBoolean(6)) throw InvalidFacts();
        var entry = Deserialize<SupplierEvent>(reader.GetString(7));
        RequireSupplierEvent(entry, snapshot);
        var receipt = Deserialize<Receipt<SupplierSnapshot>>(reader.GetString(8));
        if (receipt.SchemaVersion != 1 || receipt.Operation != (int)entry.Kind || !Same(receipt.Snapshot, snapshot)) throw InvalidFacts();
        return snapshot;
    }

    private static async Task<PurchaseSnapshot?> ReadPurchaseAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenant, Guid purchaseId, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "ReadPurchase", ("tenant", tenant), ("id", purchaseId));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? ReadPurchase(reader, tenant, purchaseId) : null;
    }

    private static PurchaseSnapshot ReadPurchase(NpgsqlDataReader reader, Guid tenant, Guid? purchaseId = null)
    {
        var snapshot = Deserialize<PurchaseSnapshot>(reader.GetString(0));
        if (!PurchasingRules.IsValidSnapshot(snapshot) || reader.GetInt16(1) != 1 || snapshot.TenantId != tenant ||
            snapshot.PurchaseId != reader.GetGuid(3) || purchaseId is { } id && snapshot.PurchaseId != id ||
            snapshot.Revision != reader.GetInt64(2) || snapshot.CreatedAt != reader.GetFieldValue<DateTimeOffset>(4) ||
            snapshot.UpdatedAt != reader.GetFieldValue<DateTimeOffset>(5) || snapshot.Supplier.SupplierId != reader.GetGuid(6) ||
            !Same(Source(snapshot), Deserialize<PurchaseSource>(reader.GetString(7))) || !reader.GetBoolean(8)) throw InvalidFacts();
        var entry = Deserialize<PurchasingEvent>(reader.GetString(9));
        Guid? target = reader.IsDBNull(11) ? null : reader.GetGuid(11);
        if (entry.TargetEventId != target) throw InvalidFacts();
        RequirePurchaseEvent(entry, snapshot);
        var receipt = Deserialize<Receipt<PurchaseSnapshot>>(reader.GetString(10));
        if (receipt.SchemaVersion != 1 || receipt.Operation != (int)entry.Kind || !Same(receipt.Snapshot, snapshot)) throw InvalidFacts();
        return snapshot;
    }

    private static async Task<SupplierCommandResult?> ReadSupplierReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Application.Tenancy.TenantContext context, Guid supplierId, SupplierCommandKind operation, string key, string fingerprint, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "ReadSupplierReceipt", ("tenant", context.TenantId),
            ("actor", context.AccountId), ("operation", (int)operation), ("key", key));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var receipt = Deserialize<Receipt<SupplierSnapshot>>(reader.GetString(13));
        var snapshot = receipt.Snapshot ?? throw InvalidFacts();
        var entry = ReadSupplierEvent(reader, context.TenantId, reader.GetGuid(11));
        if (reader.GetInt16(9) != 1 || reader.GetInt16(14) != 1 || receipt.SchemaVersion != 1 || receipt.Operation != (int)operation ||
            snapshot.CreatedAt != reader.GetFieldValue<DateTimeOffset>(15) || snapshot.Revision != reader.GetInt64(12) ||
            entry.ActorAccountId != context.AccountId || (int)entry.Kind != (int)operation || !Same(snapshot, entry.Supplier)) throw InvalidFacts();
        var matches = reader.GetString(10) == fingerprint;
        if (matches && snapshot.SupplierId != supplierId) throw InvalidFacts();
        return matches ? new(PurchasingStoreStatus.Replayed, snapshot) : new(PurchasingStoreStatus.IdempotencyConflict);
    }

    private static async Task<PurchasingCommandResult?> ReadPurchaseReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Application.Tenancy.TenantContext context, Guid purchaseId, PurchasingCommandKind operation, string key, string fingerprint, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "ReadPurchaseReceipt", ("tenant", context.TenantId),
            ("actor", context.AccountId), ("operation", (int)operation), ("key", key));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var receipt = Deserialize<Receipt<PurchaseSnapshot>>(reader.GetString(13));
        var snapshot = receipt.Snapshot ?? throw InvalidFacts();
        var entry = ReadPurchaseEvent(reader, context.TenantId, reader.GetGuid(11));
        if (reader.GetInt16(9) != 1 || reader.GetInt16(14) != 1 || receipt.SchemaVersion != 1 || receipt.Operation != (int)operation ||
            snapshot.CreatedAt != reader.GetFieldValue<DateTimeOffset>(15) || snapshot.Revision != reader.GetInt64(12) ||
            entry.ActorAccountId != context.AccountId || (int)entry.Kind != (int)operation ||
            snapshot.Supplier.SupplierId != reader.GetGuid(16) || !Same(Source(snapshot), Deserialize<PurchaseSource>(reader.GetString(17))) ||
            !Same(snapshot, Deserialize<PurchaseSnapshot>(reader.GetString(7)))) throw InvalidFacts();
        var matches = reader.GetString(10) == fingerprint;
        if (matches && snapshot.PurchaseId != purchaseId) throw InvalidFacts();
        return matches ? new(PurchasingStoreStatus.Replayed, snapshot) : new(PurchasingStoreStatus.IdempotencyConflict);
    }

    private static SupplierEvent ReadSupplierEvent(NpgsqlDataReader reader, Guid tenant, Guid supplierId)
    {
        var entry = Deserialize<SupplierEvent>(reader.GetString(6));
        var snapshot = Deserialize<SupplierSnapshot>(reader.GetString(7));
        if (!PurchasingRules.IsValidSupplier(snapshot) || snapshot.TenantId != tenant || snapshot.SupplierId != supplierId ||
            reader.GetInt16(0) != 1 || entry.EventId != reader.GetGuid(1) || entry.Revision != reader.GetInt64(2) ||
            entry.ActorAccountId != reader.GetGuid(3) || entry.AuthorizationRevision != reader.GetInt64(4) ||
            (int)entry.Kind != reader.GetInt32(5) || entry.RecordedAt != reader.GetFieldValue<DateTimeOffset>(8)) throw InvalidFacts();
        RequireSupplierEvent(entry, snapshot);
        return entry;
    }

    private static PurchasingEvent ReadPurchaseEvent(NpgsqlDataReader reader, Guid tenant, Guid purchaseId)
    {
        var entry = Deserialize<PurchasingEvent>(reader.GetString(6));
        var snapshot = Deserialize<PurchaseSnapshot>(reader.GetString(7));
        if (!PurchasingRules.IsValidSnapshot(snapshot) || snapshot.TenantId != tenant || snapshot.PurchaseId != purchaseId ||
            reader.GetInt16(0) != 1 || entry.EventId != reader.GetGuid(1) || entry.Revision != reader.GetInt64(2) ||
            entry.ActorAccountId != reader.GetGuid(3) || entry.AuthorizationRevision != reader.GetInt64(4) ||
            (int)entry.Kind != reader.GetInt32(5) || entry.RecordedAt != reader.GetFieldValue<DateTimeOffset>(8)) throw InvalidFacts();
        var targetColumn = reader.GetOrdinal("target_event_id");
        Guid? target = reader.IsDBNull(targetColumn) ? null : reader.GetGuid(targetColumn);
        if (entry.TargetEventId != target) throw InvalidFacts();
        RequirePurchaseEvent(entry, snapshot);
        return entry;
    }

    private static void RequireSupplierEvent(SupplierEvent entry, SupplierSnapshot snapshot)
    {
        if (!PurchasingRules.IsValidSupplierEvent(entry) ||
            entry.Revision != snapshot.Revision || entry.ActorAccountId != snapshot.UpdatedByAccountId ||
            entry.RecordedAt != snapshot.UpdatedAt || !Same(entry.Supplier, snapshot) ||
            (entry.Kind == SupplierEventKind.Created) != (entry.Revision == 1)) throw InvalidFacts();
    }

    private static void RequirePurchaseEvent(PurchasingEvent entry, PurchaseSnapshot snapshot)
    {
        if (!PurchasingRules.IsValidEvent(entry, snapshot) || entry.Revision != snapshot.Revision ||
            entry.ActorAccountId != snapshot.UpdatedByAccountId || entry.RecordedAt != snapshot.UpdatedAt) throw InvalidFacts();
        var effectMatches = entry.Kind switch
        {
            PurchasingEventKind.Requested => snapshot.Receipts.Count + snapshot.Payments.Count + snapshot.Refunds.Count == 0 && snapshot.CancelledQuantity is null,
            PurchasingEventKind.Receipt => snapshot.Receipts.Any(x => x.EntryId == entry.EventId && x.EffectiveEventId == entry.EventId &&
                !x.IsVoided && x.Quantity == entry.Facts!.Quantity && x.Cost == entry.Facts.Amount),
            PurchasingEventKind.Payment => snapshot.Payments.Any(x => x.EntryId == entry.EventId && x.EffectiveEventId == entry.EventId &&
                !x.IsVoided && x.Amount == entry.Facts!.Amount && x.ReceiptId == entry.Facts.ReceiptId && x.Method == entry.Facts.Method && x.Reference == entry.Facts.Reference),
            PurchasingEventKind.Refund => snapshot.Refunds.Any(x => x.EntryId == entry.EventId && x.EffectiveEventId == entry.EventId &&
                !x.IsVoided && x.Amount == entry.Facts!.Amount && x.PaymentId == entry.Facts.PaymentId && x.Method == entry.Facts.Method && x.Reference == entry.Facts.Reference),
            PurchasingEventKind.Cancellation => snapshot.CancelledQuantity == entry.Facts!.Quantity,
            PurchasingEventKind.Correction => CorrectionEffectMatches(entry, snapshot),
            _ => false,
        };
        if (!effectMatches) throw InvalidFacts();
    }

    private static bool CorrectionEffectMatches(PurchasingEvent entry, PurchaseSnapshot snapshot)
    {
        var receipt = snapshot.Receipts.SingleOrDefault(x => x.EffectiveEventId == entry.EventId);
        var payment = snapshot.Payments.SingleOrDefault(x => x.EffectiveEventId == entry.EventId);
        var refund = snapshot.Refunds.SingleOrDefault(x => x.EffectiveEventId == entry.EventId);
        return receipt is not null ? receipt.IsVoided == (entry.Replacement is null) &&
                (entry.Replacement is null || entry.Replacement == new PurchasingFact(PurchasingEventKind.Receipt, receipt.Quantity, receipt.Cost)) :
            payment is not null ? payment.IsVoided == (entry.Replacement is null) &&
                (entry.Replacement is null || entry.Replacement == new PurchasingFact(PurchasingEventKind.Payment, Amount: payment.Amount,
                    ReceiptId: payment.ReceiptId, Method: payment.Method, Reference: payment.Reference)) :
            refund is not null && refund.IsVoided == (entry.Replacement is null) &&
                (entry.Replacement is null || entry.Replacement == new PurchasingFact(PurchasingEventKind.Refund, Amount: refund.Amount,
                    PaymentId: refund.PaymentId, Method: refund.Method, Reference: refund.Reference));
    }
}
