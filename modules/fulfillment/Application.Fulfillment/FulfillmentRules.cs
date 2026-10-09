using System.Buffers;
using System.Globalization;
using System.Text;

namespace Application.Fulfillment;

public static class FulfillmentRules
{
    public const int MaximumIdempotencyKeyLength = 128;
    public const int MaximumRecipientScalars = 128;
    public const int MaximumReasonScalars = 512;

    public static bool IsEligibleSource(FulfillmentSourceFacts source, Guid tenantId, Guid orderId) =>
        source.TenantId == tenantId && source.OrderId == orderId && source.TenantId != Guid.Empty &&
        source.OrderId != Guid.Empty && source.OrderRevision > 0 && source.ProgramId != Guid.Empty &&
        source.LinePosition == 1 && source.ItemId != Guid.Empty && source.UnitId != Guid.Empty &&
        IsValidUnitCode(source.UnitCode) && source.UnitPrecision is >= 0 and <= 4 &&
        IsQuantityAtPrecision(source.OrderedQuantity, source.UnitPrecision) && source.IsCommitted &&
        source.LineCount == 1 && source.IsCatalog && source.IsNonStock && !source.HasConversion && source.IsService;

    public static string NormalizeIdempotencyKey(string? value) => NormalizeText(value, 1, MaximumIdempotencyKeyLength)
        ?? throw new ArgumentException("Idempotency key is invalid.", nameof(value));

    public static bool CanExecute(FulfillmentAuthority authority, FulfillmentCommandKind command) =>
        authority is not null && authority.AuthorizationRevision > 0 && (command switch
        {
            FulfillmentCommandKind.Start or FulfillmentCommandKind.Complete or FulfillmentCommandKind.Block or FulfillmentCommandKind.Resolve =>
                authority.CanRecordWork,
            FulfillmentCommandKind.Handoff => authority.CanRecordHandoff,
            FulfillmentCommandKind.Correct => authority.CanCorrect && authority.IsCurrentInitialOwner && authority.HasRecentAuthentication,
            _ => false,
        });

    public static FulfillmentTransitionResult Apply(
        FulfillmentSourceFacts source,
        FulfillmentSnapshot? current,
        FulfillmentMutation mutation,
        FulfillmentCorrectionTarget? correctionTarget,
        Guid workId,
        Guid eventId,
        Guid actorAccountId,
        long authorizationRevision,
        DateTimeOffset now)
    {
        if (source is null || !IsEligibleSource(source, source.TenantId, source.OrderId))
            return Result(FulfillmentMutationStatus.SourceIneligible);
        if (mutation is null || workId == Guid.Empty || eventId == Guid.Empty || actorAccountId == Guid.Empty || authorizationRevision < 1 ||
            now == default || now.Offset != TimeSpan.Zero || !Enum.IsDefined(mutation.Kind) ||
            mutation.ExpectedRevision is < 0 or long.MaxValue)
            return Result(FulfillmentMutationStatus.InputInvalid);
        if (current is null)
        {
            if (mutation.Kind != FulfillmentCommandKind.Start) return Result(FulfillmentMutationStatus.NotStarted);
            if (mutation.ExpectedRevision != 0) return Result(FulfillmentMutationStatus.RevisionConflict);
            if (!NoMutationFacts(mutation)) return Result(FulfillmentMutationStatus.InputInvalid);
            var started = new FulfillmentSnapshot(source, workId, source.TenantId, source.OrderId, 1, true,
                0m, 0m, null, null, now, now);
            var startEntry = NewEvent(eventId, 1, actorAccountId, authorizationRevision, now, FulfillmentEventKind.Started);
            return new(FulfillmentMutationStatus.Applied, started, startEntry);
        }

        if (!IsValidProjection(source, current)) return Result(FulfillmentMutationStatus.InvalidProjection);
        if (current.Revision != mutation.ExpectedRevision) return Result(FulfillmentMutationStatus.RevisionConflict);
        if (!current.Started && mutation.Kind != FulfillmentCommandKind.Start)
            return Result(FulfillmentMutationStatus.NotStarted);
        if (current.Revision >= long.MaxValue - 1) return Result(FulfillmentMutationStatus.InputInvalid);
        if (now < current.UpdatedAt) return Result(FulfillmentMutationStatus.InputInvalid);

        var revision = current.Revision + 1;
        FulfillmentEvent entry;
        var completed = current.CompletedQuantity;
        var handedOff = current.HandedOffQuantity;
        var dispositionId = current.ActiveDispositionId;
        var disposition = current.ActiveDispositionKind;

        switch (mutation.Kind)
        {
            case FulfillmentCommandKind.Start:
                if (current.Started) return Result(FulfillmentMutationStatus.AlreadyStarted);
                if (current.ActiveDispositionKind is not null || current.CompletedQuantity != 0m || current.HandedOffQuantity != 0m)
                    return Result(FulfillmentMutationStatus.InvalidProjection);
                if (!NoMutationFacts(mutation)) return Result(FulfillmentMutationStatus.InputInvalid);
                entry = NewEvent(eventId, revision, actorAccountId, authorizationRevision, now, FulfillmentEventKind.Started);
                break;
            case FulfillmentCommandKind.Complete:
                if (current.ActiveDispositionKind is not null) return Result(FulfillmentMutationStatus.DispositionActive);
                if (!NoMutationFacts(mutation, quantity: true) || !IsAvailableQuantity(mutation.Quantity, source.UnitPrecision,
                    source.OrderedQuantity - completed)) return Result(FulfillmentMutationStatus.QuantityInvalid);
                completed += mutation.Quantity!.Value;
                entry = NewEvent(eventId, revision, actorAccountId, authorizationRevision, now, FulfillmentEventKind.Completion,
                    quantity: mutation.Quantity);
                break;
            case FulfillmentCommandKind.Handoff:
                if (current.ActiveDispositionKind is not null) return Result(FulfillmentMutationStatus.DispositionActive);
                if (!NoMutationFacts(mutation, quantity: true, recipient: true) || !IsAvailableQuantity(mutation.Quantity,
                    source.UnitPrecision, completed - handedOff)) return Result(FulfillmentMutationStatus.QuantityInvalid);
                var recipient = NormalizeText(mutation.Recipient, 1, MaximumRecipientScalars);
                if (recipient is null) return Result(FulfillmentMutationStatus.TextInvalid);
                handedOff += mutation.Quantity!.Value;
                entry = NewEvent(eventId, revision, actorAccountId, authorizationRevision, now, FulfillmentEventKind.Handoff,
                    quantity: mutation.Quantity, recipient: recipient);
                break;
            case FulfillmentCommandKind.Block:
                if (current.ActiveDispositionKind is not null) return Result(FulfillmentMutationStatus.DispositionActive);
                if (!NoMutationFacts(mutation, quantity: mutation.Quantity is not null, reason: true, disposition: true) ||
                    !ValidDisposition(mutation.Disposition)) return Result(FulfillmentMutationStatus.InputInvalid);
                if (mutation.Quantity is { } affected && !IsAvailableQuantity(affected, source.UnitPrecision, source.OrderedQuantity))
                    return Result(FulfillmentMutationStatus.QuantityInvalid);
                var blockReason = NormalizeText(mutation.Reason, 1, MaximumReasonScalars);
                if (blockReason is null) return Result(FulfillmentMutationStatus.TextInvalid);
                dispositionId = eventId;
                disposition = mutation.Disposition;
                entry = NewEvent(eventId, revision, actorAccountId, authorizationRevision, now, FulfillmentEventKind.Disposition,
                    quantity: mutation.Quantity, reason: blockReason, disposition: disposition);
                break;
            case FulfillmentCommandKind.Resolve:
                if (current.ActiveDispositionKind is null || current.ActiveDispositionId is null)
                    return Result(FulfillmentMutationStatus.DispositionMissing);
                if (!NoMutationFacts(mutation, reason: true)) return Result(FulfillmentMutationStatus.InputInvalid);
                var resolutionReason = NormalizeText(mutation.Reason, 1, MaximumReasonScalars);
                if (resolutionReason is null) return Result(FulfillmentMutationStatus.TextInvalid);
                entry = NewEvent(eventId, revision, actorAccountId, authorizationRevision, now, FulfillmentEventKind.Resolution,
                    reason: resolutionReason, targetEventId: current.ActiveDispositionId);
                dispositionId = null;
                disposition = null;
                break;
            case FulfillmentCommandKind.Correct:
                var correction = ApplyCorrection(source, current, mutation, correctionTarget, eventId, revision,
                    actorAccountId, authorizationRevision, now);
                return correction;
            default:
                return Result(FulfillmentMutationStatus.InputInvalid);
        }

        var changed = current with
        {
            Revision = revision,
            Started = mutation.Kind == FulfillmentCommandKind.Start || current.Started,
            CompletedQuantity = completed,
            HandedOffQuantity = handedOff,
            ActiveDispositionId = dispositionId,
            ActiveDispositionKind = disposition,
            UpdatedAt = now,
        };
        return new(FulfillmentMutationStatus.Applied, changed, entry);
    }

    private static FulfillmentTransitionResult ApplyCorrection(FulfillmentSourceFacts source, FulfillmentSnapshot current,
        FulfillmentMutation mutation, FulfillmentCorrectionTarget? target, Guid eventId, long revision,
        Guid actorAccountId, long authorizationRevision, DateTimeOffset now)
    {
        if (mutation.Quantity is not null || mutation.Recipient is not null || mutation.Disposition is not null ||
            mutation.TargetEventId is null || mutation.TargetEventId == Guid.Empty || target is null ||
            target.Event.EventId != mutation.TargetEventId || target.Event.Kind is not (FulfillmentEventKind.Started or
                FulfillmentEventKind.Completion or FulfillmentEventKind.Handoff or FulfillmentEventKind.Disposition or
                FulfillmentEventKind.Resolution) || !target.IsActive)
            return Result(FulfillmentMutationStatus.CorrectionTargetInvalid);
        if (current.ActiveDispositionKind is not null &&
            target.Event.Kind is (FulfillmentEventKind.Completion or FulfillmentEventKind.Handoff))
            return Result(FulfillmentMutationStatus.DispositionActive);
        if (target.HasActiveDependents) return Result(FulfillmentMutationStatus.CorrectionHasDependents);
        var reason = NormalizeText(mutation.Reason, 1, MaximumReasonScalars);
        if (reason is null) return Result(FulfillmentMutationStatus.TextInvalid);
        var replacement = mutation.Replacement;
        if (replacement is not null && replacement.Kind != target.Event.Kind)
            return Result(FulfillmentMutationStatus.InputInvalid);
        if (replacement is not null && !ValidReplacement(replacement, source))
            return Result(replacement.Kind == FulfillmentEventKind.Handoff && NormalizeText(replacement.Recipient, 1, MaximumRecipientScalars) is null
                ? FulfillmentMutationStatus.TextInvalid : FulfillmentMutationStatus.QuantityInvalid);
        if (replacement?.Recipient is { } replacementRecipient)
            replacement = replacement with { Recipient = NormalizeText(replacementRecipient, 1, MaximumRecipientScalars) };

        var completed = current.CompletedQuantity;
        var handedOff = current.HandedOffQuantity;
        var dispositionId = current.ActiveDispositionId;
        var disposition = current.ActiveDispositionKind;
        var started = current.Started;
        switch (target.Event.Kind)
        {
            case FulfillmentEventKind.Started:
                if (!current.Started || current.CompletedQuantity != 0m || current.HandedOffQuantity != 0m ||
                    current.ActiveDispositionKind is not null || replacement is not null)
                    return Result(FulfillmentMutationStatus.CorrectionHasDependents);
                started = false;
                break;
            case FulfillmentEventKind.Completion:
                if (target.Event.Quantity is not { } oldCompleted) return Result(FulfillmentMutationStatus.CorrectionTargetInvalid);
                completed = completed - oldCompleted + (replacement?.Quantity ?? 0m);
                break;
            case FulfillmentEventKind.Handoff:
                if (target.Event.Quantity is not { } oldHandoff) return Result(FulfillmentMutationStatus.CorrectionTargetInvalid);
                handedOff = handedOff - oldHandoff + (replacement?.Quantity ?? 0m);
                break;
            case FulfillmentEventKind.Disposition:
                if (current.ActiveDispositionId != target.Event.EventId || target.Event.Disposition is null ||
                    target.Event.Disposition != current.ActiveDispositionKind)
                    return Result(FulfillmentMutationStatus.CorrectionTargetInvalid);
                dispositionId = replacement is null ? null : eventId;
                disposition = replacement?.Disposition;
                break;
            case FulfillmentEventKind.Resolution:
                var referencedDisposition = target.ReferencedDispositionEvent;
                if (current.ActiveDispositionId is not null || current.ActiveDispositionKind is not null || replacement is not null ||
                    target.Event.TargetEventId is not { } resolvedDispositionId || referencedDisposition is null ||
                    referencedDisposition.EventId != resolvedDispositionId || referencedDisposition.Kind != FulfillmentEventKind.Disposition ||
                    referencedDisposition.Disposition is not { } restoredKind || !Enum.IsDefined(restoredKind))
                    return Result(FulfillmentMutationStatus.CorrectionTargetInvalid);
                dispositionId = referencedDisposition.EventId;
                disposition = restoredKind;
                break;
        }
        if (completed < 0m || completed > source.OrderedQuantity || handedOff < 0m || handedOff > completed)
            return Result(FulfillmentMutationStatus.CorrectionHasDependents);
        var changed = current with
        {
            Revision = revision,
            Started = started,
            CompletedQuantity = completed,
            HandedOffQuantity = handedOff,
            ActiveDispositionId = dispositionId,
            ActiveDispositionKind = disposition,
            UpdatedAt = now,
        };
        var entry = NewEvent(eventId, revision, actorAccountId, authorizationRevision, now, FulfillmentEventKind.Correction,
            reason: reason, targetEventId: target.Event.EventId, replacement: replacement);
        return new(FulfillmentMutationStatus.Applied, changed, entry);
    }

    private static bool ValidReplacement(FulfillmentEventFacts facts, FulfillmentSourceFacts source)
    {
        if (facts.Recipient is not null && NormalizeText(facts.Recipient, 1, MaximumRecipientScalars) is null) return false;
        if (facts.Quantity is { } quantity && !IsAvailableQuantity(quantity, source.UnitPrecision, source.OrderedQuantity)) return false;
        return facts.Kind switch
        {
            FulfillmentEventKind.Completion => facts.Quantity is not null && facts.Recipient is null && facts.Disposition is null,
            FulfillmentEventKind.Handoff => facts.Quantity is not null && facts.Recipient is not null && facts.Disposition is null,
            FulfillmentEventKind.Disposition => ValidDisposition(facts.Disposition) && facts.Recipient is null,
            _ => false,
        };
    }

    private static bool IsValidProjection(FulfillmentSourceFacts source, FulfillmentSnapshot current) =>
        current.WorkId != Guid.Empty && current.TenantId == source.TenantId && current.OrderId == source.OrderId &&
        current.Source == source && current.Revision > 0 &&
        current.CreatedAt != default && current.CreatedAt.Offset == TimeSpan.Zero &&
        current.UpdatedAt >= current.CreatedAt && current.UpdatedAt.Offset == TimeSpan.Zero &&
        current.CompletedQuantity >= 0m && FitsPrecision(current.CompletedQuantity, source.UnitPrecision) &&
        current.HandedOffQuantity >= 0m && FitsPrecision(current.HandedOffQuantity, source.UnitPrecision) && current.CompletedQuantity <= source.OrderedQuantity &&
        current.HandedOffQuantity <= current.CompletedQuantity &&
        (current.Started || current.CompletedQuantity == 0m && current.HandedOffQuantity == 0m && current.ActiveDispositionId is null) &&
        ((current.ActiveDispositionId is null) == (current.ActiveDispositionKind is null)) &&
        (current.ActiveDispositionId is null || current.ActiveDispositionId != Guid.Empty &&
            current.ActiveDispositionKind is { } disposition && Enum.IsDefined(disposition));

    private static bool NoMutationFacts(FulfillmentMutation mutation, bool quantity = false, bool recipient = false,
        bool reason = false, bool disposition = false) =>
        (quantity || mutation.Quantity is null) && (recipient || mutation.Recipient is null) &&
        (reason || mutation.Reason is null) && (disposition || mutation.Disposition is null) &&
        mutation.TargetEventId is null && mutation.Replacement is null;

    private static bool IsAvailableQuantity(decimal? quantity, int precision, decimal remaining) =>
        quantity is { } amount && amount > 0m && IsQuantityAtPrecision(amount, precision) && amount <= remaining;

    private static bool IsQuantityAtPrecision(decimal quantity, int precision) => quantity > 0m && FitsPrecision(quantity, precision);

    private static bool FitsPrecision(decimal quantity, int precision) =>
        precision is >= 0 and <= 4 && decimal.Round(quantity, precision, MidpointRounding.ToEven) == quantity;

    private static bool ValidDisposition(FulfillmentDispositionKind? disposition) =>
        disposition is { } value && Enum.IsDefined(value);

    private static bool IsValidUnitCode(string? value) => NormalizeText(value, 1, 128) is { } normalized &&
        string.Equals(value, normalized, StringComparison.Ordinal);

    private static string? NormalizeText(string? value, int minimum, int maximum)
    {
        if (value is null) return null;
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

    private static FulfillmentEvent NewEvent(Guid eventId, long revision, Guid actorAccountId, long authorizationRevision,
        DateTimeOffset now, FulfillmentEventKind kind, decimal? quantity = null, string? recipient = null,
        string? reason = null, FulfillmentDispositionKind? disposition = null, Guid? targetEventId = null,
        FulfillmentEventFacts? replacement = null) =>
        new(eventId, revision, actorAccountId, now, authorizationRevision, kind, quantity, recipient, reason,
            disposition, targetEventId, replacement);

    private static FulfillmentTransitionResult Result(FulfillmentMutationStatus status) => new(status, null, null);
}
