using Application.Tenancy;
using Xunit;

namespace Application.Tenancy.Tests;

public sealed class TenantMembershipLifecycleTests
{
    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void FactoriesRejectEmptyAuthorityIdentifiers(
        bool emptyPrincipal,
        bool emptyDevice,
        bool emptyTenant,
        bool emptyAccount)
    {
        if (emptyPrincipal || emptyDevice)
        {
            Assert.Throws<ArgumentException>(() => TenantMembershipAdministrationActor.Create(
                emptyPrincipal ? Guid.Empty : Guid.NewGuid(),
                emptyDevice ? Guid.Empty : Guid.NewGuid()));
            return;
        }

        Assert.Throws<ArgumentException>(() => TenantMembershipLifecycleIntent.Invite(
            emptyTenant ? Guid.Empty : Guid.NewGuid(),
            emptyAccount ? Guid.Empty : Guid.NewGuid(),
            "invite-1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("bad\nkey")]
    [InlineData("bad\uD800key")]
    public void IntentRejectsInvalidIdempotencyKeys(string key)
    {
        Assert.ThrowsAny<ArgumentException>(() => TenantMembershipLifecycleIntent.Invite(
            Guid.NewGuid(),
            Guid.NewGuid(),
            key));
    }

    [Fact]
    public void IntentTrimsKeyAndAcceptsTheMaximumLength()
    {
        var key = new string('k', TenantMembershipLifecycleIntent.IdempotencyKeyLimit);

        var intent = TenantMembershipLifecycleIntent.Invite(Guid.NewGuid(), Guid.NewGuid(), $" {key} ");

        Assert.Equal(key, intent.IdempotencyKey);
    }

    [Fact]
    public void IntentRejectsAnIdempotencyKeyOverTheMaximumLength()
    {
        var key = new string('k', TenantMembershipLifecycleIntent.IdempotencyKeyLimit + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => TenantMembershipLifecycleIntent.Invite(
            Guid.NewGuid(),
            Guid.NewGuid(),
            key));
    }

    [Fact]
    public void TransitionRejectsUndefinedOperations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TenantMembershipLifecycleIntent.Transition(
            (MembershipLifecycleOperation)int.MaxValue,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "transition-1"));
    }

    [Fact]
    public void ExpectedRevisionIsRequiredOnlyForTransitionsAndMustBePositive()
    {
        var invitation = TenantMembershipLifecycleIntent.Invite(Guid.NewGuid(), Guid.NewGuid(), "invite-1");

        Assert.Null(invitation.ExpectedRevision);
        Assert.Throws<ArgumentException>(() => TenantMembershipLifecycleIntent.Transition(
            MembershipLifecycleOperation.Invite,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "transition-1"));
        Assert.Throws<ArgumentOutOfRangeException>(() => TenantMembershipLifecycleIntent.Transition(
            MembershipLifecycleOperation.Activate,
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "transition-1"));

        var transition = TenantMembershipLifecycleIntent.Transition(
            MembershipLifecycleOperation.Activate,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "transition-1");
        Assert.Equal(1, transition.ExpectedRevision);
    }

    [Fact]
    public void FingerprintIsStableForEquivalentIntentAndChangesWithEachIntentField()
    {
        var tenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var baseline = TenantMembershipLifecycleIntent.Transition(
            MembershipLifecycleOperation.Activate,
            tenantId,
            accountId,
            1,
            "first-key");
        var equivalent = TenantMembershipLifecycleIntent.Transition(
            MembershipLifecycleOperation.Activate,
            tenantId,
            accountId,
            1,
            "different-key");

        Assert.Equal(baseline.Fingerprint, equivalent.Fingerprint);
        Assert.Matches("^[0-9A-F]{64}$", baseline.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, TenantMembershipLifecycleIntent.Transition(
            MembershipLifecycleOperation.Suspend, tenantId, accountId, 1, "first-key").Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, TenantMembershipLifecycleIntent.Transition(
            MembershipLifecycleOperation.Activate, Guid.NewGuid(), accountId, 1, "first-key").Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, TenantMembershipLifecycleIntent.Transition(
            MembershipLifecycleOperation.Activate, tenantId, Guid.NewGuid(), 1, "first-key").Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, TenantMembershipLifecycleIntent.Transition(
            MembershipLifecycleOperation.Activate, tenantId, accountId, 2, "first-key").Fingerprint);
    }

    [Fact]
    public void SnapshotTransitionsFromInvitationThroughRemoval()
    {
        var invitedAt = new DateTimeOffset(2026, 10, 3, 1, 0, 0, TimeSpan.Zero);
        var activatedAt = invitedAt.AddMinutes(1);
        var suspendedAt = invitedAt.AddMinutes(2);
        var reactivatedAt = invitedAt.AddMinutes(3);
        var removedAt = invitedAt.AddMinutes(4);
        var snapshot = new TenantMembershipLifecycleSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), MembershipAvailability.Invited, 1,
            invitedAt, null, null, null);

        var activated = Apply(snapshot, MembershipLifecycleOperation.Activate, activatedAt);
        Assert.Equal(MembershipLifecycleStatus.Activated, activated.Status);
        snapshot = Assert.IsType<TenantMembershipLifecycleSnapshot>(activated.Membership);
        Assert.Equal(MembershipAvailability.Active, snapshot.Availability);
        Assert.Equal(2, snapshot.Revision);
        Assert.Equal(activatedAt, snapshot.ActivatedAt);

        var suspended = Apply(snapshot, MembershipLifecycleOperation.Suspend, suspendedAt);
        Assert.Equal(MembershipLifecycleStatus.Suspended, suspended.Status);
        snapshot = Assert.IsType<TenantMembershipLifecycleSnapshot>(suspended.Membership);
        Assert.Equal(MembershipAvailability.Suspended, snapshot.Availability);
        Assert.Equal(suspendedAt, snapshot.SuspendedAt);

        var reactivated = Apply(snapshot, MembershipLifecycleOperation.Activate, reactivatedAt);
        Assert.Equal(MembershipLifecycleStatus.Activated, reactivated.Status);
        snapshot = Assert.IsType<TenantMembershipLifecycleSnapshot>(reactivated.Membership);
        Assert.Equal(MembershipAvailability.Active, snapshot.Availability);
        Assert.Equal(reactivatedAt, snapshot.ActivatedAt);
        Assert.Null(snapshot.SuspendedAt);

        var removed = Apply(snapshot, MembershipLifecycleOperation.Remove, removedAt);
        Assert.Equal(MembershipLifecycleStatus.Removed, removed.Status);
        snapshot = Assert.IsType<TenantMembershipLifecycleSnapshot>(removed.Membership);
        Assert.Equal(MembershipAvailability.Removed, snapshot.Availability);
        Assert.Equal(5, snapshot.Revision);
        Assert.Equal(removedAt, snapshot.RemovedAt);
    }

    [Fact]
    public void SnapshotRejectsAStaleExpectedRevisionWithoutChangingMembership()
    {
        var snapshot = NewSnapshot(MembershipAvailability.Active, revision: 2);
        var intent = CreateTransition(snapshot, MembershipLifecycleOperation.Suspend, expectedRevision: 1);

        var result = snapshot.Transition(intent, DateTimeOffset.UnixEpoch);

        Assert.Equal(MembershipLifecycleStatus.RevisionConflict, result.Status);
        Assert.Same(snapshot, result.Membership);
    }

    [Fact]
    public void RemovedMembershipCannotBeActivated()
    {
        var snapshot = NewSnapshot(MembershipAvailability.Removed, revision: 2);

        var result = Apply(snapshot, MembershipLifecycleOperation.Activate, DateTimeOffset.UnixEpoch);

        Assert.Equal(MembershipLifecycleStatus.InvalidTransition, result.Status);
        Assert.Same(snapshot, result.Membership);
    }

    [Fact]
    public void InitialOwnerCannotBeSuspendedOrRemovedBeforeRoleAdministrationExists()
    {
        var snapshot = new TenantMembershipLifecycleSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MembershipAvailability.Active,
            1,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            null,
            null,
            IsInitialOwner: true);

        Assert.Equal(
            MembershipLifecycleStatus.InitialOwnerProtected,
            Apply(snapshot, MembershipLifecycleOperation.Suspend, DateTimeOffset.UnixEpoch).Status);
        Assert.Equal(
            MembershipLifecycleStatus.InitialOwnerProtected,
            Apply(snapshot, MembershipLifecycleOperation.Remove, DateTimeOffset.UnixEpoch).Status);
    }

    [Fact]
    public void InitialOwnerBootstrapHasStableIntentWithoutExpectedRevision()
    {
        var tenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var first = TenantMembershipLifecycleIntent.BootstrapOwner(tenantId, accountId, "owner-1");
        var replay = TenantMembershipLifecycleIntent.BootstrapOwner(tenantId, accountId, "owner-2");

        Assert.Null(first.ExpectedRevision);
        Assert.Equal(first.Fingerprint, replay.Fingerprint);
        Assert.Equal(MembershipLifecycleOperation.BootstrapOwner, first.Operation);
    }

    [Fact]
    public void SnapshotReportsRevisionLimitInsteadOfOverflowing()
    {
        var snapshot = NewSnapshot(MembershipAvailability.Active, revision: int.MaxValue);

        var result = Apply(snapshot, MembershipLifecycleOperation.Suspend, DateTimeOffset.UnixEpoch);

        Assert.Equal(MembershipLifecycleStatus.RevisionLimitReached, result.Status);
        Assert.Same(snapshot, result.Membership);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SnapshotRejectsIntentForAnotherMembership(bool differentTenant, bool differentAccount)
    {
        var snapshot = NewSnapshot(MembershipAvailability.Active, revision: 1);
        var intent = TenantMembershipLifecycleIntent.Transition(
            MembershipLifecycleOperation.Suspend,
            differentTenant ? Guid.NewGuid() : snapshot.TenantId,
            differentAccount ? Guid.NewGuid() : snapshot.AccountId,
            snapshot.Revision,
            "transition-1");

        Assert.Throws<ArgumentException>(() => snapshot.Transition(intent, DateTimeOffset.UnixEpoch));
    }

    private static TenantMembershipLifecycleResult Apply(
        TenantMembershipLifecycleSnapshot snapshot,
        MembershipLifecycleOperation operation,
        DateTimeOffset occurredAt) =>
        snapshot.Transition(CreateTransition(snapshot, operation, snapshot.Revision), occurredAt);

    private static TenantMembershipLifecycleIntent CreateTransition(
        TenantMembershipLifecycleSnapshot snapshot,
        MembershipLifecycleOperation operation,
        int expectedRevision) =>
        TenantMembershipLifecycleIntent.Transition(
            operation,
            snapshot.TenantId,
            snapshot.AccountId,
            expectedRevision,
            $"{operation}-{expectedRevision}");

    private static TenantMembershipLifecycleSnapshot NewSnapshot(
        MembershipAvailability availability,
        int revision) =>
        new(
            Guid.NewGuid(), Guid.NewGuid(), availability, revision,
            DateTimeOffset.UnixEpoch, null, null, null);
}
