using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Purchasing;

public static class PurchasingRules
{
    public const int MaximumFacts = 128;
    public const int MaximumSnapshotBytes = 64 * 1024;
    public const int MaximumCommandBytes = 8 * 1024;
    public const int MaximumNameScalars = 128;
    public const int MaximumTextScalars = 512;
    public const int MaximumReferenceScalars = 128;
    public const decimal MaximumAmount = 99_999_999_999_999_999.99m;
    public const decimal MaximumQuantity = 9_999_999_999_999_999_999.999999999m;

    public static bool CanExecuteSupplier(PurchasingAuthority authority) =>
        authority is not null && authority.AuthorizationRevision > 0 && authority.CanManageSupplier;

    public static bool CanExecute(PurchasingAuthority authority, PurchasingCommandKind command) =>
        authority is not null && authority.AuthorizationRevision > 0 && (command switch
        {
            PurchasingCommandKind.Request or PurchasingCommandKind.Cancel => authority.CanRecordRequest,
            PurchasingCommandKind.Receive => authority.CanRecordReceipt,
            PurchasingCommandKind.Pay or PurchasingCommandKind.Refund => authority.CanRecordPayment,
            PurchasingCommandKind.Correct => authority.CanCorrect && authority.IsCurrentInitialOwner && authority.HasRecentAuthentication,
            _ => false,
        });

    public static bool IsQuantity(decimal value, int precision) => value > 0m && value <= MaximumQuantity &&
        precision is >= 0 and <= 4 && decimal.Round(value, precision, MidpointRounding.ToEven) == value;

    public static bool IsAmount(decimal value) => value > 0m && value <= MaximumAmount &&
        decimal.Round(value, 2, MidpointRounding.ToEven) == value;

    public static string NormalizeIdempotencyKey(string? value) => NormalizeText(value, 1, MaximumReferenceScalars)
        ?? throw new PurchasingValidationException("idempotency_key_invalid");

    public static SupplierMutation CanonicalMutation(SupplierMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        RequireCommandSize(mutation);
        return mutation with
        {
            Name = OptionalText(mutation.Name, MaximumNameScalars),
            Contact = OptionalText(mutation.Contact, MaximumTextScalars),
        };
    }

    public static PurchasingMutation CanonicalMutation(PurchasingMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        RequireCommandSize(mutation);
        return mutation with
        {
            Description = OptionalText(mutation.Description, MaximumTextScalars),
            Reference = OptionalText(mutation.Reference, MaximumReferenceScalars),
            Reason = OptionalText(mutation.Reason, MaximumTextScalars),
            Replacement = mutation.Replacement is not { } replacement ? null : replacement with
            {
                Reference = OptionalText(replacement.Reference, MaximumReferenceScalars),
            },
        };
    }

    public static string Fingerprint(Guid id, SupplierMutation mutation)
    {
        mutation = CanonicalMutation(mutation);
        return Hash(new { schema = 1, id, mutation.Kind, mutation.ExpectedRevision, mutation.Name, mutation.Contact });
    }

    public static string Fingerprint(Guid id, PurchasingMutation mutation)
    {
        mutation = CanonicalMutation(mutation);
        return Hash(new
        {
            schema = 1,
            id,
            mutation.Kind,
            mutation.ExpectedRevision,
            mutation.SupplierId,
            mutation.UnitId,
            mutation.Order,
            mutation.Description,
            quantity = Number(mutation.Quantity),
            amount = Number(mutation.Amount),
            mutation.ReceiptId,
            mutation.PaymentId,
            mutation.Method,
            mutation.Reference,
            mutation.Reason,
            mutation.TargetEventId,
            replacement = mutation.Replacement is not { } replacement ? null : new
            {
                replacement.Kind,
                quantity = Number(replacement.Quantity),
                amount = Number(replacement.Amount),
                replacement.ReceiptId,
                replacement.PaymentId,
                replacement.Method,
                replacement.Reference,
            },
        });
    }

    public static SupplierTransitionResult ApplySupplier(Guid tenantId, SupplierSnapshot? current, SupplierMutation mutation,
        Guid supplierId, Guid eventId, Guid actorAccountId, long authorizationRevision, DateTimeOffset now)
    {
        if (!ValidActor(tenantId, supplierId, eventId, actorAccountId, authorizationRevision, now) || mutation is null ||
            !Enum.IsDefined(mutation.Kind) || mutation.ExpectedRevision is < 0 or >= long.MaxValue - 1)
            return new(PurchasingMutationStatus.InputInvalid);
        try { mutation = CanonicalMutation(mutation); }
        catch (PurchasingValidationException) { return new(PurchasingMutationStatus.TextInvalid); }
        if (current is not null && (!IsValidSupplier(current) || current.SupplierId != supplierId || current.TenantId != tenantId || now < current.UpdatedAt))
            return new(PurchasingMutationStatus.InvalidProjection);
        if (current is null && mutation.Kind != SupplierCommandKind.Create) return new(PurchasingMutationStatus.NotFound);
        if (current is not null && mutation.Kind == SupplierCommandKind.Create) return new(PurchasingMutationStatus.AlreadyExists);
        if ((current?.Revision ?? 0) != mutation.ExpectedRevision) return new(PurchasingMutationStatus.RevisionConflict);
        if (current?.Status == SupplierStatus.Retired && mutation.Kind == SupplierCommandKind.Retire)
            return new(PurchasingMutationStatus.AlreadyRetired);
        if (mutation.Kind == SupplierCommandKind.Retire && (mutation.Name is not null || mutation.Contact is not null))
            return new(PurchasingMutationStatus.InputInvalid);
        if (mutation.Kind != SupplierCommandKind.Retire && mutation.Name is null) return new(PurchasingMutationStatus.TextInvalid);
        var supplier = current is null ? new SupplierSnapshot(supplierId, tenantId, 1, mutation.Name!, mutation.Contact,
            SupplierStatus.Active, actorAccountId, now, actorAccountId, now) : current with
            {
                Revision = current.Revision + 1,
                Name = mutation.Kind == SupplierCommandKind.Retire ? current.Name : mutation.Name!,
                Contact = mutation.Kind == SupplierCommandKind.Retire ? current.Contact : mutation.Contact,
                Status = mutation.Kind == SupplierCommandKind.Retire ? SupplierStatus.Retired : current.Status,
                UpdatedByAccountId = actorAccountId,
                UpdatedAt = now,
            };
        var entry = new SupplierEvent(eventId, supplier.Revision, actorAccountId, now, authorizationRevision,
            (SupplierEventKind)mutation.Kind, supplier);
        return new(PurchasingMutationStatus.Applied, supplier, entry);
    }

    public static PurchasingTransitionResult ApplyPurchase(Guid tenantId, PurchaseSnapshot? current, PurchasingMutation mutation,
        SupplierSnapshot? supplier, PurchasingSourceFacts? source, Guid purchaseId, Guid eventId, Guid actorAccountId,
        long authorizationRevision, DateTimeOffset now)
    {
        if (!ValidActor(tenantId, purchaseId, eventId, actorAccountId, authorizationRevision, now) || mutation is null ||
            !Enum.IsDefined(mutation.Kind) || mutation.ExpectedRevision is < 0 or >= long.MaxValue - 1)
            return Rejected(PurchasingMutationStatus.InputInvalid);
        try { mutation = CanonicalMutation(mutation); }
        catch (PurchasingValidationException) { return Rejected(PurchasingMutationStatus.TextInvalid); }
        if (!ValidMutationShape(mutation)) return Rejected(PurchasingMutationStatus.InputInvalid);
        if (current is null)
            return CreatePurchase(tenantId, mutation, supplier, source, purchaseId, eventId, actorAccountId, authorizationRevision, now);
        if (!IsValidSnapshot(current) || current.TenantId != tenantId || current.PurchaseId != purchaseId || now < current.UpdatedAt ||
            current.Receipts.Any(x => x.EntryId == eventId || x.EffectiveEventId == eventId) ||
            current.Payments.Any(x => x.EntryId == eventId || x.EffectiveEventId == eventId) ||
            current.Refunds.Any(x => x.EntryId == eventId || x.EffectiveEventId == eventId))
            return Rejected(PurchasingMutationStatus.InvalidProjection);
        if (mutation.Kind == PurchasingCommandKind.Request) return Rejected(PurchasingMutationStatus.AlreadyExists);
        if (current.Revision != mutation.ExpectedRevision) return Rejected(PurchasingMutationStatus.RevisionConflict);
        var revision = current.Revision + 1;
        var changed = current with { Revision = revision, UpdatedAt = now, UpdatedByAccountId = actorAccountId };
        var fact = new PurchasingFact((PurchasingEventKind)mutation.Kind, mutation.Quantity, mutation.Amount,
            mutation.ReceiptId, mutation.PaymentId, mutation.Method, mutation.Reference);
        PurchasingEvent entry;
        switch (mutation.Kind)
        {
            case PurchasingCommandKind.Receive:
                if (current.CancelledQuantity is not null) return Rejected(PurchasingMutationStatus.RequestCancelled);
                if (!ValidFact(fact, current.Unit.UnitPrecision)) return InvalidFactResult(fact, current.Unit.UnitPrecision);
                if (fact.Quantity > current.RemainingQuantity) return Rejected(PurchasingMutationStatus.OverReceipt);
                if (FactCount(current) >= MaximumFacts) return Rejected(PurchasingMutationStatus.ResourceLimit);
                changed = changed with { Receipts = Append(current.Receipts, new PurchasingReceipt(eventId, eventId, fact.Quantity!.Value, fact.Amount!.Value)) };
                break;
            case PurchasingCommandKind.Pay:
                if (!ValidFact(fact, current.Unit.UnitPrecision)) return InvalidFactResult(fact, current.Unit.UnitPrecision);
                var receipt = current.Receipts.SingleOrDefault(x => x.EntryId == fact.ReceiptId && !x.IsVoided);
                if (receipt is null) return Rejected(PurchasingMutationStatus.DependencyInvalid);
                if (fact.Amount > Outstanding(current, receipt.EntryId)) return Rejected(PurchasingMutationStatus.OverPayment);
                if (FactCount(current) >= MaximumFacts) return Rejected(PurchasingMutationStatus.ResourceLimit);
                changed = changed with
                {
                    Payments = Append(current.Payments, new PurchasingPayment(eventId, eventId, receipt.EntryId,
                    fact.Amount!.Value, fact.Method!.Value, fact.Reference))
                };
                break;
            case PurchasingCommandKind.Refund:
                if (!ValidFact(fact, current.Unit.UnitPrecision)) return InvalidFactResult(fact, current.Unit.UnitPrecision);
                var payment = current.Payments.SingleOrDefault(x => x.EntryId == fact.PaymentId && !x.IsVoided);
                if (payment is null) return Rejected(PurchasingMutationStatus.DependencyInvalid);
                if (fact.Amount > payment.Amount - Refunded(current, payment.EntryId)) return Rejected(PurchasingMutationStatus.OverRefund);
                if (FactCount(current) >= MaximumFacts) return Rejected(PurchasingMutationStatus.ResourceLimit);
                changed = changed with
                {
                    Refunds = Append(current.Refunds, new PurchasingRefund(eventId, eventId, payment.EntryId,
                    fact.Amount!.Value, fact.Method!.Value, fact.Reference))
                };
                break;
            case PurchasingCommandKind.Correct:
                return Correct(current, changed, mutation, eventId, actorAccountId, authorizationRevision, now);
            case PurchasingCommandKind.Cancel:
                if (current.CancelledQuantity is not null) return Rejected(PurchasingMutationStatus.RequestCancelled);
                if (current.RemainingQuantity <= 0m) return Rejected(PurchasingMutationStatus.NoRemainingQuantity);
                changed = changed with { CancelledQuantity = current.RemainingQuantity };
                fact = new(PurchasingEventKind.Cancellation, Quantity: current.RemainingQuantity);
                break;
            default:
                return Rejected(PurchasingMutationStatus.InputInvalid);
        }
        entry = new(eventId, revision, actorAccountId, now, authorizationRevision, fact.Kind, fact, mutation.Reason);
        return Finish(changed, entry);
    }

    private static PurchasingTransitionResult CreatePurchase(Guid tenantId, PurchasingMutation mutation, SupplierSnapshot? supplier,
        PurchasingSourceFacts? source, Guid purchaseId, Guid eventId, Guid actor, long authorizationRevision, DateTimeOffset now)
    {
        if (mutation.Kind != PurchasingCommandKind.Request) return Rejected(PurchasingMutationStatus.NotFound);
        if (mutation.ExpectedRevision != 0) return Rejected(PurchasingMutationStatus.RevisionConflict);
        if (supplier is null || !IsValidSupplier(supplier) || supplier.TenantId != tenantId || supplier.SupplierId != mutation.SupplierId ||
            supplier.UpdatedAt > now)
            return Rejected(PurchasingMutationStatus.SourceIneligible);
        if (supplier.Status != SupplierStatus.Active) return Rejected(PurchasingMutationStatus.SupplierRetired);
        if (!IsEligibleSource(source, tenantId, mutation.UnitId!.Value, mutation.Order))
            return Rejected(PurchasingMutationStatus.SourceIneligible);
        if (mutation.Quantity is not { } quantity || !IsQuantity(quantity, source!.Unit.UnitPrecision))
            return Rejected(PurchasingMutationStatus.QuantityInvalid);
        var snapshot = new PurchaseSnapshot(purchaseId, tenantId, 1, supplier, source!.Unit, mutation.Order, mutation.Description!, quantity,
            Array.Empty<PurchasingReceipt>(), Array.Empty<PurchasingPayment>(), Array.Empty<PurchasingRefund>(), null,
            actor, now, actor, now);
        var entry = new PurchasingEvent(eventId, 1, actor, now, authorizationRevision, PurchasingEventKind.Requested,
            new(PurchasingEventKind.Requested, Quantity: quantity));
        return Finish(snapshot, entry);
    }

    private static PurchasingTransitionResult Correct(PurchaseSnapshot current, PurchaseSnapshot changed, PurchasingMutation mutation,
        Guid eventId, Guid actor, long authorizationRevision, DateTimeOffset now)
    {
        var target = mutation.TargetEventId!.Value;
        var receipt = current.Receipts.SingleOrDefault(x => x.EffectiveEventId == target && !x.IsVoided);
        var payment = current.Payments.SingleOrDefault(x => x.EffectiveEventId == target && !x.IsVoided);
        var refund = current.Refunds.SingleOrDefault(x => x.EffectiveEventId == target && !x.IsVoided);
        var kind = receipt is not null ? PurchasingEventKind.Receipt : payment is not null ? PurchasingEventKind.Payment :
            refund is not null ? PurchasingEventKind.Refund : (PurchasingEventKind?)null;
        if (kind is null) return Rejected(PurchasingMutationStatus.CorrectionTargetInvalid);
        var replacement = mutation.Replacement;
        if (replacement is not null && (replacement.Kind != kind || !ValidFact(replacement, current.Unit.UnitPrecision)))
            return Rejected(PurchasingMutationStatus.InputInvalid);
        if (receipt is not null)
        {
            if (replacement is null && current.Payments.Any(x => x.ReceiptId == receipt.EntryId && !x.IsVoided))
                return Rejected(PurchasingMutationStatus.CorrectionHasDependents);
            changed = changed with
            {
                Receipts = Replace(current.Receipts, receipt, receipt with
                {
                    EffectiveEventId = eventId,
                    Quantity = replacement?.Quantity ?? receipt.Quantity,
                    Cost = replacement?.Amount ?? receipt.Cost,
                    IsVoided = replacement is null,
                })
            };
        }
        else if (payment is not null)
        {
            if (replacement is not null && replacement.ReceiptId != payment.ReceiptId ||
                replacement is null && current.Refunds.Any(x => x.PaymentId == payment.EntryId && !x.IsVoided))
                return Rejected(PurchasingMutationStatus.CorrectionHasDependents);
            changed = changed with
            {
                Payments = Replace(current.Payments, payment, payment with
                {
                    EffectiveEventId = eventId,
                    Amount = replacement?.Amount ?? payment.Amount,
                    Method = replacement?.Method ?? payment.Method,
                    Reference = replacement?.Reference,
                    IsVoided = replacement is null,
                })
            };
        }
        else if (refund is not null)
        {
            if (replacement is not null && replacement.PaymentId != refund.PaymentId)
                return Rejected(PurchasingMutationStatus.CorrectionHasDependents);
            changed = changed with
            {
                Refunds = Replace(current.Refunds, refund, refund with
                {
                    EffectiveEventId = eventId,
                    Amount = replacement?.Amount ?? refund.Amount,
                    Method = replacement?.Method ?? refund.Method,
                    Reference = replacement?.Reference,
                    IsVoided = replacement is null,
                })
            };
        }
        if (!ValidFinancialProjection(changed) || changed.ReceivedQuantity + (changed.CancelledQuantity ?? 0m) > changed.RequestedQuantity)
            return Rejected(PurchasingMutationStatus.CorrectionHasDependents);
        var entry = new PurchasingEvent(eventId, changed.Revision, actor, now, authorizationRevision, PurchasingEventKind.Correction,
            Reason: mutation.Reason, TargetEventId: target, Replacement: replacement);
        return Finish(changed, entry);
    }

    public static bool IsEligibleSource(PurchasingSourceFacts? source, Guid tenantId, Guid unitId, PurchasingOrderLink? link)
    {
        if (source is null || !ValidUnit(source.Unit, tenantId) || !source.Unit.IsActive || source.Unit.UnitId != unitId)
            return false;
        if (link is null) return source.Order is null;
        return ValidOrderLink(link) && source.Order is { } order && order.TenantId == tenantId &&
            order.OrderId == link.OrderId && order.LinePosition == link.LinePosition && order.OrderRevision == link.OrderRevision &&
            order.IsCommitted;
    }

    public static bool IsValidSupplier(SupplierSnapshot? snapshot) => snapshot is not null && snapshot.SchemaVersion == 1 &&
        snapshot.SupplierId != Guid.Empty && snapshot.TenantId != Guid.Empty && snapshot.Revision is > 0 and < long.MaxValue &&
        Enum.IsDefined(snapshot.Status) && IsCanonicalText(snapshot.Name, 1, MaximumNameScalars) &&
        (snapshot.Contact is null || IsCanonicalText(snapshot.Contact, 1, MaximumTextScalars)) &&
        snapshot.CreatedByAccountId != Guid.Empty && snapshot.UpdatedByAccountId != Guid.Empty &&
        ValidTime(snapshot.CreatedAt) && ValidTime(snapshot.UpdatedAt) && snapshot.UpdatedAt >= snapshot.CreatedAt;

    public static bool IsValidSupplierEvent(SupplierEvent? entry) => entry is not null && entry.SchemaVersion == 1 &&
        entry.EventId != Guid.Empty && entry.ActorAccountId != Guid.Empty && entry.AuthorizationRevision > 0 &&
        Enum.IsDefined(entry.Kind) && IsValidSupplier(entry.Supplier) && entry.Revision == entry.Supplier.Revision &&
        entry.ActorAccountId == entry.Supplier.UpdatedByAccountId && entry.RecordedAt == entry.Supplier.UpdatedAt &&
        (entry.Kind == SupplierEventKind.Created ? entry.Revision == 1 && entry.Supplier.Status == SupplierStatus.Active : entry.Revision > 1) &&
        (entry.Kind != SupplierEventKind.Retired || entry.Supplier.Status == SupplierStatus.Retired);

    public static bool IsValidSnapshot(PurchaseSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.SchemaVersion != 1 || snapshot.PurchaseId == Guid.Empty || snapshot.TenantId == Guid.Empty ||
            snapshot.Revision is < 1 or >= long.MaxValue || !IsValidSupplier(snapshot.Supplier) || snapshot.Supplier.TenantId != snapshot.TenantId ||
            snapshot.Supplier.Status != SupplierStatus.Active || snapshot.Supplier.UpdatedAt > snapshot.CreatedAt ||
            !ValidUnit(snapshot.Unit, snapshot.TenantId) || !snapshot.Unit.IsActive ||
            snapshot.Order is { } order && !ValidOrderLink(order) || !IsCanonicalText(snapshot.Description, 1, MaximumTextScalars) ||
            !IsQuantity(snapshot.RequestedQuantity, snapshot.Unit.UnitPrecision) || snapshot.CreatedByAccountId == Guid.Empty ||
            snapshot.UpdatedByAccountId == Guid.Empty || !ValidTime(snapshot.CreatedAt) || !ValidTime(snapshot.UpdatedAt) ||
            snapshot.UpdatedAt < snapshot.CreatedAt || snapshot.Receipts is null || snapshot.Payments is null || snapshot.Refunds is null ||
            snapshot.Receipts.Count > MaximumFacts || snapshot.Payments.Count > MaximumFacts || snapshot.Refunds.Count > MaximumFacts ||
            FactCount(snapshot) > MaximumFacts || FactCount(snapshot) >= snapshot.Revision ||
            snapshot.CancelledQuantity is { } cancelled && (!IsQuantity(cancelled, snapshot.Unit.UnitPrecision) || snapshot.Revision < 2))
            return false;
        var ids = new HashSet<Guid>();
        var effectiveIds = new HashSet<Guid>();
        foreach (var receipt in snapshot.Receipts)
            if (receipt is null || !ValidIds(receipt.EntryId, receipt.EffectiveEventId, ids, effectiveIds) ||
                !IsQuantity(receipt.Quantity, snapshot.Unit.UnitPrecision) || !IsAmount(receipt.Cost)) return false;
        foreach (var payment in snapshot.Payments)
            if (payment is null || !ValidIds(payment.EntryId, payment.EffectiveEventId, ids, effectiveIds) ||
                !ValidFact(new(PurchasingEventKind.Payment, Amount: payment.Amount, ReceiptId: payment.ReceiptId,
                    Method: payment.Method, Reference: payment.Reference), snapshot.Unit.UnitPrecision)) return false;
        foreach (var refund in snapshot.Refunds)
            if (refund is null || !ValidIds(refund.EntryId, refund.EffectiveEventId, ids, effectiveIds) ||
                !ValidFact(new(PurchasingEventKind.Refund, Amount: refund.Amount, PaymentId: refund.PaymentId,
                    Method: refund.Method, Reference: refund.Reference), snapshot.Unit.UnitPrecision)) return false;
        if (snapshot.Receipts.Any(x => x.EffectiveEventId != x.EntryId && ids.Contains(x.EffectiveEventId)) ||
            snapshot.Payments.Any(x => x.EffectiveEventId != x.EntryId && ids.Contains(x.EffectiveEventId)) ||
            snapshot.Refunds.Any(x => x.EffectiveEventId != x.EntryId && ids.Contains(x.EffectiveEventId))) return false;
        return snapshot.ReceivedQuantity + (snapshot.CancelledQuantity ?? 0m) <= snapshot.RequestedQuantity &&
            ValidFinancialProjection(snapshot) && JsonSerializer.SerializeToUtf8Bytes(snapshot).Length <= MaximumSnapshotBytes;
    }

    public static bool IsValidEvent(PurchasingEvent? entry, PurchaseSnapshot snapshot)
    {
        if (entry is null || entry.SchemaVersion != 1 || entry.EventId == Guid.Empty || entry.ActorAccountId == Guid.Empty ||
            entry.AuthorizationRevision < 1 || entry.Revision < 1 || entry.Revision > snapshot.Revision || !ValidTime(entry.RecordedAt) ||
            entry.RecordedAt < snapshot.CreatedAt || entry.RecordedAt > snapshot.UpdatedAt || !Enum.IsDefined(entry.Kind) ||
            entry.Kind != PurchasingEventKind.Requested && entry.Revision == 1) return false;
        return entry.Kind switch
        {
            PurchasingEventKind.Requested => entry.Revision == 1 && entry.Facts == new PurchasingFact(PurchasingEventKind.Requested,
                Quantity: snapshot.RequestedQuantity) && entry.Reason is null && entry.TargetEventId is null && entry.Replacement is null,
            PurchasingEventKind.Receipt or PurchasingEventKind.Payment or PurchasingEventKind.Refund =>
                entry.Facts is { } facts && facts.Kind == entry.Kind && ValidFact(facts, snapshot.Unit.UnitPrecision) &&
                (facts.Quantity is null || facts.Quantity <= snapshot.RequestedQuantity) &&
                entry.Reason is null && entry.TargetEventId is null && entry.Replacement is null,
            PurchasingEventKind.Cancellation => entry.Facts is { } cancellation && cancellation.Kind == PurchasingEventKind.Cancellation &&
                cancellation.Quantity is { } quantity && IsQuantity(quantity, snapshot.Unit.UnitPrecision) && quantity == snapshot.CancelledQuantity &&
                cancellation == new PurchasingFact(PurchasingEventKind.Cancellation, Quantity: quantity) &&
                IsCanonicalText(entry.Reason, 1, MaximumTextScalars) && entry.TargetEventId is null && entry.Replacement is null,
            PurchasingEventKind.Correction => entry.Facts is null && IsCanonicalText(entry.Reason, 1, MaximumTextScalars) &&
                entry.TargetEventId is { } target && target != Guid.Empty && target != entry.EventId &&
                (entry.Replacement is null || ValidFact(entry.Replacement, snapshot.Unit.UnitPrecision) &&
                    (entry.Replacement.Quantity is null || entry.Replacement.Quantity <= snapshot.RequestedQuantity)),
            _ => false,
        };
    }

    public static decimal Outstanding(PurchaseSnapshot snapshot, Guid receiptId)
    {
        var receipt = snapshot.Receipts.Single(x => x.EntryId == receiptId);
        var payments = snapshot.Payments.Where(x => x.ReceiptId == receiptId && !x.IsVoided).ToArray();
        return (receipt.IsVoided ? 0m : receipt.Cost) - payments.Sum(x => x.Amount) + payments.Sum(x => Refunded(snapshot, x.EntryId));
    }

    private static decimal Refunded(PurchaseSnapshot snapshot, Guid paymentId) =>
        snapshot.Refunds.Where(x => x.PaymentId == paymentId && !x.IsVoided).Sum(x => x.Amount);

    private static bool ValidFinancialProjection(PurchaseSnapshot snapshot)
    {
        foreach (var payment in snapshot.Payments)
        {
            var receipt = snapshot.Receipts.SingleOrDefault(x => x.EntryId == payment.ReceiptId);
            if (receipt is null || !payment.IsVoided && receipt.IsVoided ||
                Refunded(snapshot, payment.EntryId) > (payment.IsVoided ? 0m : payment.Amount)) return false;
        }
        foreach (var refund in snapshot.Refunds)
        {
            var payment = snapshot.Payments.SingleOrDefault(x => x.EntryId == refund.PaymentId);
            if (payment is null || !refund.IsVoided && payment.IsVoided) return false;
        }
        return snapshot.Receipts.All(x => Outstanding(snapshot, x.EntryId) >= 0m);
    }

    private static bool ValidMutationShape(PurchasingMutation mutation) => mutation.Kind switch
    {
        PurchasingCommandKind.Request => mutation.SupplierId is { } supplier && supplier != Guid.Empty &&
            mutation.UnitId is { } unit && unit != Guid.Empty && mutation.Description is not null && mutation.Quantity is not null &&
            mutation == new PurchasingMutation(mutation.Kind, mutation.ExpectedRevision, supplier, unit, mutation.Order, mutation.Description, mutation.Quantity),
        PurchasingCommandKind.Receive => mutation.Quantity is not null && mutation.Amount is not null &&
            mutation == new PurchasingMutation(mutation.Kind, mutation.ExpectedRevision, Quantity: mutation.Quantity, Amount: mutation.Amount),
        PurchasingCommandKind.Pay => mutation == new PurchasingMutation(mutation.Kind, mutation.ExpectedRevision, Amount: mutation.Amount,
            ReceiptId: mutation.ReceiptId, Method: mutation.Method, Reference: mutation.Reference),
        PurchasingCommandKind.Refund => mutation == new PurchasingMutation(mutation.Kind, mutation.ExpectedRevision, Amount: mutation.Amount,
            PaymentId: mutation.PaymentId, Method: mutation.Method, Reference: mutation.Reference),
        PurchasingCommandKind.Correct => mutation.TargetEventId is { } target && target != Guid.Empty && mutation.Reason is not null &&
            mutation == new PurchasingMutation(mutation.Kind, mutation.ExpectedRevision, Reason: mutation.Reason, TargetEventId: target, Replacement: mutation.Replacement),
        PurchasingCommandKind.Cancel => mutation.Reason is not null &&
            mutation == new PurchasingMutation(mutation.Kind, mutation.ExpectedRevision, Reason: mutation.Reason),
        _ => false,
    };

    private static bool ValidFact(PurchasingFact facts, int precision)
    {
        if (facts.Reference is not null && !IsCanonicalText(facts.Reference, 1, MaximumReferenceScalars)) return false;
        return facts.Kind switch
        {
            PurchasingEventKind.Receipt => facts.Quantity is { } quantity && IsQuantity(quantity, precision) &&
                facts.Amount is { } cost && IsAmount(cost) && facts == new PurchasingFact(facts.Kind, quantity, cost),
            PurchasingEventKind.Payment => facts.Amount is { } amount && IsAmount(amount) && facts.ReceiptId is { } receipt && receipt != Guid.Empty &&
                facts.Method is { } method && Enum.IsDefined(method) &&
                facts == new PurchasingFact(facts.Kind, Amount: amount, ReceiptId: receipt, Method: method, Reference: facts.Reference),
            PurchasingEventKind.Refund => facts.Amount is { } amount && IsAmount(amount) && facts.PaymentId is { } payment && payment != Guid.Empty &&
                facts.Method is { } method && Enum.IsDefined(method) &&
                facts == new PurchasingFact(facts.Kind, Amount: amount, PaymentId: payment, Method: method, Reference: facts.Reference),
            _ => false,
        };
    }

    private static PurchasingTransitionResult InvalidFactResult(PurchasingFact facts, int precision) =>
        Rejected(facts.Quantity is { } quantity && !IsQuantity(quantity, precision) ? PurchasingMutationStatus.QuantityInvalid :
            facts.Amount is not { } amount || !IsAmount(amount) ? PurchasingMutationStatus.AmountInvalid : PurchasingMutationStatus.InputInvalid);

    private static bool ValidUnit(PurchasingUnitFacts? unit, Guid tenantId) => unit is not null && unit.TenantId == tenantId &&
        unit.TenantId != Guid.Empty && unit.UnitId != Guid.Empty && unit.UnitRevision is > 0 and < long.MaxValue && unit.UnitPrecision is >= 0 and <= 4 &&
        IsCanonicalText(unit.UnitCode, 1, MaximumNameScalars);
    private static bool ValidOrderLink(PurchasingOrderLink link) => link.OrderId != Guid.Empty && link.LinePosition is >= 1 and <= 100 &&
        link.OrderRevision is > 0 and < long.MaxValue;
    private static bool ValidIds(Guid id, Guid effectiveId, HashSet<Guid> ids, HashSet<Guid> effectiveIds) =>
        id != Guid.Empty && effectiveId != Guid.Empty && ids.Add(id) && effectiveIds.Add(effectiveId);
    private static bool ValidActor(Guid tenant, Guid aggregate, Guid entry, Guid actor, long revision, DateTimeOffset now) =>
        tenant != Guid.Empty && aggregate != Guid.Empty && entry != Guid.Empty && actor != Guid.Empty && revision > 0 && ValidTime(now);
    private static bool ValidTime(DateTimeOffset time) => time != default && time.Offset == TimeSpan.Zero;
    private static int FactCount(PurchaseSnapshot snapshot) => snapshot.Receipts.Count + snapshot.Payments.Count + snapshot.Refunds.Count;
    private static System.Collections.ObjectModel.ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> facts, T value) =>
        Array.AsReadOnly(facts.Append(value).ToArray());
    private static System.Collections.ObjectModel.ReadOnlyCollection<T> Replace<T>(IReadOnlyList<T> facts, T previous, T replacement) =>
        Array.AsReadOnly(facts.Select(x => EqualityComparer<T>.Default.Equals(x, previous) ? replacement : x).ToArray());

    private static PurchasingTransitionResult Finish(PurchaseSnapshot snapshot, PurchasingEvent entry) =>
        JsonSerializer.SerializeToUtf8Bytes(snapshot).Length > MaximumSnapshotBytes ? Rejected(PurchasingMutationStatus.ResourceLimit) :
            new(PurchasingMutationStatus.Applied, snapshot, entry);
    private static PurchasingTransitionResult Rejected(PurchasingMutationStatus status) => new(status);
    private static string? Number(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture);
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static void RequireCommandSize<T>(T value)
    {
        if (JsonSerializer.SerializeToUtf8Bytes(value).Length > MaximumCommandBytes)
            throw new PurchasingValidationException("purchasing_request_too_large");
    }
    private static string? OptionalText(string? value, int maximum)
    {
        if (value is null) return null;
        var text = NormalizeText(value, 0, maximum);
        if (text is null) throw new PurchasingValidationException("purchasing_text_invalid");
        return text.Length == 0 ? null : text;
    }
    private static bool IsCanonicalText(string? value, int minimum, int maximum) => value is not null &&
        NormalizeText(value, minimum, maximum) is { } normalized && string.Equals(normalized, value, StringComparison.Ordinal);
    private static string? NormalizeText(string? value, int minimum, int maximum)
    {
        if (value is null || value.Length > MaximumCommandBytes) return null;
        for (var offset = 0; offset < value.Length;)
        {
            if (Rune.DecodeFromUtf16(value.AsSpan(offset), out var rune, out var consumed) != OperationStatus.Done ||
                Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control) return null;
            offset += consumed;
        }
        var normalized = value.Trim();
        var count = normalized.EnumerateRunes().Count();
        return count >= minimum && count <= maximum ? normalized : null;
    }
}
