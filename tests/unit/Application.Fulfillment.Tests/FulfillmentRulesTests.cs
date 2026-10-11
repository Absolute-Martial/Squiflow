using Application.Fulfillment;
using Xunit;

namespace Application.Fulfillment.Tests;

public sealed class FulfillmentRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly FulfillmentSourceFacts Source = new(Id(1), Id(2), 3, Id(3), 1, Id(4), Id(5), "each", 2,
        3m, true, 1, true, true, false, true);
    private static readonly Guid WorkId = Id(6);
    private static readonly Guid Actor = Id(7);

    [Fact]
    public void StartRequiresExactCommittedProgramServiceSource()
    {
        Assert.True(FulfillmentRules.IsEligibleSource(Source, Source.TenantId, Source.OrderId));
        var ineligible = new[]
        {
            Source with { TenantId = Guid.Empty },
            Source with { OrderId = Guid.Empty },
            Source with { OrderRevision = 0 },
            Source with { ProgramId = Guid.Empty },
            Source with { LinePosition = 0 },
            Source with { LinePosition = 2 },
            Source with { ItemId = Guid.Empty },
            Source with { UnitId = Guid.Empty },
            Source with { UnitCode = " each" },
            Source with { UnitCode = "ea\n" },
            Source with { UnitPrecision = -1 },
            Source with { UnitPrecision = 5 },
            Source with { UnitPrecision = 2, OrderedQuantity = 3.001m },
            Source with { OrderedQuantity = 0m },
            Source with { OrderedQuantity = -1m },
            Source with { IsCommitted = false },
            Source with { LineCount = 0 },
            Source with { LineCount = 2 },
            Source with { IsCatalog = false },
            Source with { IsNonStock = false },
            Source with { HasConversion = true },
            Source with { IsService = false },
        };
        Assert.All(ineligible, source => Assert.False(
            FulfillmentRules.IsEligibleSource(source, Source.TenantId, Source.OrderId), source.ToString()));
        Assert.False(FulfillmentRules.IsEligibleSource(Source, Id(20), Source.OrderId));
        Assert.False(FulfillmentRules.IsEligibleSource(Source, Source.TenantId, Id(20)));
        Assert.True(FulfillmentRules.IsEligibleSource(Source with { UnitPrecision = 0 }, Source.TenantId, Source.OrderId));
        Assert.True(FulfillmentRules.IsEligibleSource(Source with { UnitPrecision = 4 }, Source.TenantId, Source.OrderId));

        var started = Apply(null, new(FulfillmentCommandKind.Start, 0));
        Assert.Equal(FulfillmentMutationStatus.Applied, started.Status);
        Assert.Equal(FulfillmentStage.InProgress, started.Snapshot!.Stage);
        Assert.Equal(FulfillmentEventKind.Started, started.Event!.Kind);
    }

    [Fact]
    public void CompletionAndPickupTrackSeparatePartialQuantitiesWithoutOverfulfillment()
    {
        var work = Start();
        work = Apply(work, new(FulfillmentCommandKind.Complete, 1, Quantity: 1.25m)).Snapshot!;
        Assert.Equal(1.25m, work.CompletedQuantity);
        Assert.Equal(0m, work.HandedOffQuantity);
        var firstPickup = Apply(work, new(FulfillmentCommandKind.Handoff, 2, Quantity: 0.25m, Recipient: "  Alex  "));
        Assert.Equal("Alex", firstPickup.Event!.Recipient);
        work = firstPickup.Snapshot!;
        Assert.Equal(0.25m, work.HandedOffQuantity);

        Assert.Equal(FulfillmentMutationStatus.QuantityInvalid,
            Apply(work, new(FulfillmentCommandKind.Handoff, 3, Quantity: 1.01m, Recipient: "Alex")).Status);
        Assert.Equal(FulfillmentMutationStatus.QuantityInvalid,
            Apply(work, new(FulfillmentCommandKind.Complete, 3, Quantity: 1.001m)).Status);
        Assert.Equal(FulfillmentMutationStatus.QuantityInvalid,
            Apply(work, new(FulfillmentCommandKind.Complete, 3, Quantity: 1.76m)).Status);

        work = Apply(work, new(FulfillmentCommandKind.Complete, 3, Quantity: 1.75m)).Snapshot!;
        Assert.Equal(FulfillmentStage.ReadyForPickup, work.Stage);
        work = Apply(work, new(FulfillmentCommandKind.Handoff, 4, Quantity: 2.75m, Recipient: "Taylor")).Snapshot!;
        Assert.Equal(3m, work.CompletedQuantity);
        Assert.Equal(3m, work.HandedOffQuantity);
        Assert.Equal(FulfillmentStage.HandedOff, work.Stage);
    }

    [Fact]
    public void BlockingDispositionStopsQuantityEffectsAndResolutionDoesNotManufactureCompletion()
    {
        var work = Start();
        var completion = Apply(work, new(FulfillmentCommandKind.Complete, 1, Quantity: 1m));
        work = completion.Snapshot!;
        var blocked = Apply(work, new(FulfillmentCommandKind.Block, 2, Quantity: 1m,
            Reason: "Customer clarification required", Disposition: FulfillmentDispositionKind.Disputed));
        Assert.Equal(FulfillmentMutationStatus.Applied, blocked.Status);
        work = blocked.Snapshot!;
        Assert.Equal(FulfillmentStage.Paused, work.Stage);
        Assert.Equal(FulfillmentMutationStatus.DispositionActive,
            Apply(work, new(FulfillmentCommandKind.Complete, 3, Quantity: 1m)).Status);
        Assert.Equal(FulfillmentMutationStatus.DispositionActive,
            Apply(work, new(FulfillmentCommandKind.Handoff, 3, Quantity: 1m, Recipient: "Alex")).Status);
        Assert.Equal(FulfillmentMutationStatus.DispositionActive,
            Apply(work, new(FulfillmentCommandKind.Correct, 3, Reason: "Completion was entered incorrectly",
                TargetEventId: completion.Event!.EventId), new(completion.Event, true, false)).Status);

        var resolved = Apply(work, new(FulfillmentCommandKind.Resolve, 3, Reason: "Scope confirmed"));
        Assert.Equal(FulfillmentMutationStatus.Applied, resolved.Status);
        Assert.Equal(1m, resolved.Snapshot!.CompletedQuantity);
        Assert.Null(resolved.Snapshot.ActiveDispositionId);
        Assert.Equal(work.ActiveDispositionId, resolved.Event!.TargetEventId);
    }

    [Fact]
    public void CorrectionsRequireActiveIndependentFactsAndPreserveNetQuantityInvariants()
    {
        var work = Start();
        var completion = Apply(work, new(FulfillmentCommandKind.Complete, 1, Quantity: 2m));
        work = completion.Snapshot!;
        var handoff = Apply(work, new(FulfillmentCommandKind.Handoff, 2, Quantity: 1m, Recipient: "Alex"));
        work = handoff.Snapshot!;
        var completionTarget = new FulfillmentCorrectionTarget(completion.Event!, true, false);

        Assert.Equal(FulfillmentMutationStatus.CorrectionHasDependents,
            Apply(work, new(FulfillmentCommandKind.Correct, 3, Reason: "Bad completion entry", TargetEventId: completion.Event!.EventId),
                completionTarget with { HasActiveDependents = true }).Status);
        Assert.Equal(FulfillmentMutationStatus.CorrectionHasDependents,
            Apply(work, new(FulfillmentCommandKind.Correct, 3, Reason: "Bad completion entry", TargetEventId: completion.Event.EventId),
                completionTarget).Status);

        var handoffTarget = new FulfillmentCorrectionTarget(handoff.Event!, true, false);
        var corrected = Apply(work, new(FulfillmentCommandKind.Correct, 3, Reason: "Quantity was entered incorrectly",
            TargetEventId: handoff.Event!.EventId, Replacement: new(FulfillmentEventKind.Handoff, 0.5m, "  Alex  ")), handoffTarget);
        Assert.Equal(FulfillmentMutationStatus.Applied, corrected.Status);
        Assert.Equal(0.5m, corrected.Snapshot!.HandedOffQuantity);
        Assert.Equal(handoff.Event.EventId, corrected.Event!.TargetEventId);
        Assert.Equal(FulfillmentEventKind.Handoff, corrected.Event.Replacement!.Kind);
        Assert.Equal("Alex", corrected.Event.Replacement.Recipient);
        Assert.Equal(1m, handoff.Snapshot!.HandedOffQuantity);

        var replacementTargetEvent = corrected.Event with
        {
            Kind = corrected.Event.Replacement.Kind,
            Quantity = corrected.Event.Replacement.Quantity,
            Recipient = corrected.Event.Replacement.Recipient,
            Disposition = corrected.Event.Replacement.Disposition,
            TargetEventId = null,
            Replacement = null,
        };
        var replacementCorrection = Apply(corrected.Snapshot!, new(FulfillmentCommandKind.Correct, 4,
            Reason: "Replacement pickup amount was also entered incorrectly", TargetEventId: replacementTargetEvent.EventId,
            Replacement: new(FulfillmentEventKind.Handoff, 0.25m, "Taylor")),
            new(replacementTargetEvent, true, false));
        Assert.Equal(FulfillmentMutationStatus.Applied, replacementCorrection.Status);
        Assert.Equal(0.25m, replacementCorrection.Snapshot!.HandedOffQuantity);
    }

    [Fact]
    public void StartEntryCanBeVoidedAndRestartedWithoutReplacingPinnedSourceOrCreationTime()
    {
        var started = Apply(null, new(FulfillmentCommandKind.Start, 0));
        var original = started.Snapshot!;
        var correction = Apply(original, new(FulfillmentCommandKind.Correct, 1, Reason: "Work was opened in error",
            TargetEventId: started.Event!.EventId), new(started.Event, true, false));

        Assert.Equal(FulfillmentMutationStatus.Applied, correction.Status);
        Assert.False(correction.Snapshot!.Started);
        Assert.Equal(FulfillmentStage.NotStarted, correction.Snapshot.Stage);
        Assert.Same(Source, correction.Snapshot.Source);
        Assert.Equal(original.CreatedAt, correction.Snapshot.CreatedAt);

        Assert.Equal(FulfillmentMutationStatus.NotStarted,
            Apply(correction.Snapshot, new(FulfillmentCommandKind.Complete, 2, Quantity: 1m)).Status);
        Assert.Equal(FulfillmentMutationStatus.NotStarted,
            Apply(correction.Snapshot, new(FulfillmentCommandKind.Handoff, 2, Quantity: 1m, Recipient: "Alex")).Status);
        Assert.Equal(FulfillmentMutationStatus.NotStarted,
            Apply(correction.Snapshot, new(FulfillmentCommandKind.Block, 2, Reason: "Stop", Disposition: FulfillmentDispositionKind.Stopped)).Status);
        Assert.Equal(FulfillmentMutationStatus.NotStarted,
            Apply(correction.Snapshot, new(FulfillmentCommandKind.Resolve, 2, Reason: "Resolved")).Status);

        var restarted = Apply(correction.Snapshot, new(FulfillmentCommandKind.Start, 2));
        Assert.Equal(FulfillmentMutationStatus.Applied, restarted.Status);
        Assert.True(restarted.Snapshot!.Started);
        Assert.Equal(3, restarted.Snapshot.Revision);
        Assert.Equal(original.CreatedAt, restarted.Snapshot.CreatedAt);
        Assert.Same(Source, restarted.Snapshot.Source);
        var completion = Apply(restarted.Snapshot, new(FulfillmentCommandKind.Complete, 3, Quantity: 0.5m));
        Assert.Equal(FulfillmentMutationStatus.Applied, completion.Status);
        Assert.Equal(0.5m, completion.Snapshot!.CompletedQuantity);
    }

    [Fact]
    public void ResolutionCorrectionRestoresOriginalDispositionOnlyWithoutDependentFacts()
    {
        var work = Start();
        var blocked = Apply(work, new(FulfillmentCommandKind.Block, 1, Reason: "Disputed scope",
            Disposition: FulfillmentDispositionKind.Disputed));
        var dispositionEvent = blocked.Event!;
        var resolved = Apply(blocked.Snapshot!, new(FulfillmentCommandKind.Resolve, 2, Reason: "Scope confirmed"));
        var resolutionEvent = resolved.Event!;
        var target = new FulfillmentCorrectionTarget(resolutionEvent, true, false, dispositionEvent);
        var mutation = new FulfillmentMutation(FulfillmentCommandKind.Correct, 3, Reason: "Resolution was recorded in error",
            TargetEventId: resolutionEvent.EventId);

        Assert.Equal(FulfillmentMutationStatus.CorrectionHasDependents,
            Apply(resolved.Snapshot!, mutation, target with { HasActiveDependents = true }).Status);
        var corrected = Apply(resolved.Snapshot!, mutation, target);

        Assert.Equal(FulfillmentMutationStatus.Applied, corrected.Status);
        Assert.Equal(dispositionEvent.EventId, corrected.Snapshot!.ActiveDispositionId);
        Assert.Equal(dispositionEvent.Disposition, corrected.Snapshot.ActiveDispositionKind);
        Assert.Equal(0m, corrected.Snapshot.CompletedQuantity);
        Assert.Equal(0m, corrected.Snapshot.HandedOffQuantity);
    }

    [Fact]
    public void RecipientReasonsAndIdempotencyKeysAreBoundedUnicodeScalarText()
    {
        var work = Apply(Start(), new(FulfillmentCommandKind.Complete, 1, Quantity: 1m)).Snapshot!;
        Assert.Equal(FulfillmentMutationStatus.TextInvalid,
            Apply(work, new(FulfillmentCommandKind.Handoff, 2, Quantity: 1m, Recipient: " ")).Status);
        Assert.Equal(FulfillmentMutationStatus.TextInvalid,
            Apply(work, new(FulfillmentCommandKind.Handoff, 2, Quantity: 1m, Recipient: "bad\nname")).Status);
        Assert.Equal(FulfillmentMutationStatus.TextInvalid,
            Apply(work, new(FulfillmentCommandKind.Handoff, 2, Quantity: 1m, Recipient: new string('\uD800', 1))).Status);
        Assert.Equal(FulfillmentMutationStatus.TextInvalid,
            Apply(work, new(FulfillmentCommandKind.Handoff, 2, Quantity: 1m, Recipient: string.Concat(Enumerable.Repeat("😀", 129)))).Status);
        var scalarBoundRecipient = string.Concat(Enumerable.Repeat("😀", 128));
        Assert.Equal(FulfillmentMutationStatus.Applied,
            Apply(work, new(FulfillmentCommandKind.Handoff, 2, Quantity: 1m, Recipient: scalarBoundRecipient)).Status);
        Assert.Equal(FulfillmentMutationStatus.TextInvalid,
            Apply(work, new(FulfillmentCommandKind.Block, 2, Reason: new string('x', 513), Disposition: FulfillmentDispositionKind.Stopped)).Status);
        Assert.Equal(FulfillmentMutationStatus.TextInvalid,
            Apply(work, new(FulfillmentCommandKind.Block, 2, Reason: "bad\0reason", Disposition: FulfillmentDispositionKind.Stopped)).Status);
        Assert.Equal(FulfillmentMutationStatus.TextInvalid,
            Apply(work, new(FulfillmentCommandKind.Block, 2, Reason: new string('\uD800', 1), Disposition: FulfillmentDispositionKind.Stopped)).Status);
        Assert.Equal("key", FulfillmentRules.NormalizeIdempotencyKey(" key "));
        var multibyteKey = string.Concat(Enumerable.Repeat("😀", 128));
        Assert.Equal(multibyteKey, FulfillmentRules.NormalizeIdempotencyKey(multibyteKey));
        Assert.Throws<ArgumentException>(() => FulfillmentRules.NormalizeIdempotencyKey(new string('x', 129)));
        Assert.Throws<ArgumentException>(() => FulfillmentRules.NormalizeIdempotencyKey("bad\tkey"));
        Assert.Throws<ArgumentException>(() => FulfillmentRules.NormalizeIdempotencyKey(new string('\uD800', 1)));
    }

    [Fact]
    public void RevisionAndCorrectionAuthorityAreExplicit()
    {
        var work = Start();
        Assert.Equal(FulfillmentMutationStatus.RevisionConflict,
            Apply(work, new(FulfillmentCommandKind.Complete, 0, Quantity: 1m)).Status);
        Assert.False(FulfillmentRules.CanExecute(new(1, true, true, true, true, false, true), FulfillmentCommandKind.Correct));
        Assert.False(FulfillmentRules.CanExecute(new(1, true, true, true, true, true, false), FulfillmentCommandKind.Correct));
        Assert.True(FulfillmentRules.CanExecute(new(1, true, true, true, true, true, true), FulfillmentCommandKind.Correct));
        Assert.False(FulfillmentRules.CanExecute(new(1, true, true, false, false, true, true), FulfillmentCommandKind.Handoff));
    }

    [Fact]
    public void CapabilityRemainsHostProviderAuthorizationAndContainerNeutral()
    {
        var references = typeof(FulfillmentRules).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("OpenFga", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.StartsWith("Autofac", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name =>
            name.StartsWith("Application.", StringComparison.Ordinal) && name != "Application.Tenancy");
    }

    private static FulfillmentSnapshot Start() => Apply(null, new(FulfillmentCommandKind.Start, 0)).Snapshot!;

    private static FulfillmentTransitionResult Apply(FulfillmentSnapshot? current, FulfillmentMutation mutation,
        FulfillmentCorrectionTarget? target = null) => FulfillmentRules.Apply(Source, current, mutation, target,
        WorkId, Id(checked(100 + (int)(current?.Revision ?? 0))), Actor, 1, Now.AddSeconds(current?.Revision ?? 0));

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
