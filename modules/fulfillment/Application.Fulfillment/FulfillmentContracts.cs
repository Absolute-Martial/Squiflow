using Application.Tenancy;

namespace Application.Fulfillment;

public sealed record FulfillmentSourceFacts(
    Guid TenantId,
    Guid OrderId,
    long OrderRevision,
    Guid ProgramId,
    int LinePosition,
    Guid ItemId,
    Guid UnitId,
    string UnitCode,
    int UnitPrecision,
    decimal OrderedQuantity,
    bool IsCommitted,
    int LineCount,
    bool IsCatalog,
    bool IsNonStock,
    bool HasConversion,
    bool IsService);

public sealed record FulfillmentAuthority(
    long AuthorizationRevision,
    bool CanView,
    bool CanRecordWork,
    bool CanRecordHandoff,
    bool CanCorrect,
    bool IsCurrentInitialOwner,
    bool HasRecentAuthentication);

public interface IFulfillmentAuthorityReader
{
    Task<FulfillmentAuthority> ResolveAsync(TenantContext context, CancellationToken cancellationToken);
}

public enum FulfillmentDispositionKind
{
    Stopped = 1,
    Disputed = 2,
    Defective = 3,
}

public enum FulfillmentCommandKind
{
    Start = 1,
    Complete = 2,
    Handoff = 3,
    Block = 4,
    Resolve = 5,
    Correct = 6,
}

public enum FulfillmentEventKind
{
    Started = 1,
    Completion = 2,
    Handoff = 3,
    Disposition = 4,
    Resolution = 5,
    Correction = 6,
}

public enum FulfillmentStage
{
    NotStarted = 1,
    InProgress = 2,
    Paused = 3,
    ReadyForPickup = 4,
    HandedOff = 5,
}

public enum FulfillmentMutationStatus
{
    Applied = 1,
    SourceIneligible = 2,
    RevisionConflict = 3,
    AlreadyStarted = 4,
    NotStarted = 5,
    DispositionActive = 6,
    DispositionMissing = 7,
    QuantityInvalid = 8,
    TextInvalid = 9,
    CorrectionTargetInvalid = 10,
    CorrectionHasDependents = 11,
    InvalidProjection = 12,
    InputInvalid = 13,
}

public sealed record FulfillmentSnapshot(
    FulfillmentSourceFacts Source,
    Guid WorkId,
    Guid TenantId,
    Guid OrderId,
    long Revision,
    bool Started,
    decimal CompletedQuantity,
    decimal HandedOffQuantity,
    Guid? ActiveDispositionId,
    FulfillmentDispositionKind? ActiveDispositionKind,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public FulfillmentStage Stage => !Started ? FulfillmentStage.NotStarted :
        ActiveDispositionKind is not null ? FulfillmentStage.Paused :
        HandedOffQuantity == CompletedQuantity && CompletedQuantity == Source.OrderedQuantity ? FulfillmentStage.HandedOff :
        CompletedQuantity == Source.OrderedQuantity ? FulfillmentStage.ReadyForPickup : FulfillmentStage.InProgress;
}

public sealed record FulfillmentEventFacts(
    FulfillmentEventKind Kind,
    decimal? Quantity = null,
    string? Recipient = null,
    FulfillmentDispositionKind? Disposition = null);

public sealed record FulfillmentEvent(
    Guid EventId,
    long Revision,
    Guid ActorAccountId,
    DateTimeOffset RecordedAt,
    long AuthorizationRevision,
    FulfillmentEventKind Kind,
    decimal? Quantity = null,
    string? Recipient = null,
    string? Reason = null,
    FulfillmentDispositionKind? Disposition = null,
    Guid? TargetEventId = null,
    FulfillmentEventFacts? Replacement = null);

public sealed record FulfillmentCorrectionTarget(
    FulfillmentEvent Event,
    bool IsActive,
    bool HasActiveDependents,
    FulfillmentEvent? ReferencedDispositionEvent = null);

public sealed record FulfillmentMutation(
    FulfillmentCommandKind Kind,
    long ExpectedRevision,
    decimal? Quantity = null,
    string? Recipient = null,
    string? Reason = null,
    FulfillmentDispositionKind? Disposition = null,
    Guid? TargetEventId = null,
    FulfillmentEventFacts? Replacement = null);

public sealed record FulfillmentTransitionResult(
    FulfillmentMutationStatus Status,
    FulfillmentSnapshot? Snapshot,
    FulfillmentEvent? Event);
