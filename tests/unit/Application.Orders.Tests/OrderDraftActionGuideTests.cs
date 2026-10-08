using Application.Orders;
using Xunit;

namespace Application.Orders.Tests;

public sealed class OrderDraftActionGuideTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DraftActionsFollowPermissions(bool mayRevise, bool mayAbandon)
    {
        var guide = OrderDraftActionGuide.Explain(OrderDraftState.Draft, 7, mayRevise, mayAbandon);

        Assert.Equal(7, guide.ObservedRevision);
        Assert.Collection(
            guide.Actions,
            revise => AssertAvailability(revise, OrderDraftAction.Revise, mayRevise,
                mayRevise ? null : OrderDraftActionUnavailability.PermissionRequired),
            abandon => AssertAvailability(abandon, OrderDraftAction.Abandon, mayAbandon,
                mayAbandon ? null : OrderDraftActionUnavailability.PermissionRequired),
            commit => AssertAvailability(commit, OrderDraftAction.Commit, false,
                OrderDraftActionUnavailability.PermissionRequired));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CommitAvailabilityUsesExplicitPermission(bool mayCommit)
    {
        var guide = OrderDraftActionGuide.Explain(OrderDraftState.Draft, 4, true, true, mayCommit);
        AssertAvailability(guide.Actions[2], OrderDraftAction.Commit, mayCommit,
            mayCommit ? null : OrderDraftActionUnavailability.PermissionRequired);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AbandonedStateTakesPriorityOverPermissions(bool permitted)
    {
        var guide = OrderDraftActionGuide.Explain(OrderDraftState.Abandoned, 3, permitted, permitted);

        AssertAvailability(guide.Actions[0], OrderDraftAction.Revise, false, OrderDraftActionUnavailability.AlreadyAbandoned);
        AssertAvailability(guide.Actions[1], OrderDraftAction.Abandon, false, OrderDraftActionUnavailability.AlreadyAbandoned);
        AssertAvailability(guide.Actions[2], OrderDraftAction.Commit, false, OrderDraftActionUnavailability.AlreadyAbandoned);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CommittedStateBlocksAllDraftActions(bool permitted)
    {
        var guide = OrderDraftActionGuide.Explain(OrderDraftState.Committed, 3, permitted, permitted, permitted);
        Assert.All(guide.Actions, action =>
        {
            Assert.False(action.Available);
            Assert.Equal(OrderDraftActionUnavailability.AlreadyCommitted, action.Unavailability);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveRevisionIsAnInternalError(long revision) =>
        Assert.Throws<InvalidOperationException>(() =>
            OrderDraftActionGuide.Explain(OrderDraftState.Draft, revision, true, true));

    [Fact]
    public void UnsupportedStateFailsClosedThroughLifecycleAssessment() =>
        Assert.Throws<InvalidOperationException>(() =>
            OrderDraftActionGuide.Explain((OrderDraftState)99, 1, true, true, true));

    [Fact]
    public void ActionsAreReadOnly()
    {
        var guide = OrderDraftActionGuide.Explain(OrderDraftState.Draft, 1, true, true, true);
        var actions = Assert.IsAssignableFrom<IList<OrderDraftActionAvailability>>(guide.Actions);

        Assert.True(actions.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => actions.Clear());
    }

    [Theory]
    [InlineData(false, false, OrderDraftActionUnavailability.ProfileUnavailable)]
    [InlineData(true, true, OrderDraftActionUnavailability.ProgramReferenceRequired)]
    public void CommitmentExplainsUnmetPinnedPolicy(bool profileAvailable, bool referenceMissing, OrderDraftActionUnavailability reason)
    {
        var guide = OrderDraftActionGuide.Explain(OrderDraftState.Draft, 2, true, true, true,
            profileAvailable: profileAvailable, programReferenceMissing: referenceMissing);
        AssertAvailability(guide.Actions[2], OrderDraftAction.Commit, false, reason);
    }

    private static void AssertAvailability(
        OrderDraftActionAvailability actual,
        OrderDraftAction action,
        bool available,
        OrderDraftActionUnavailability? unavailability)
    {
        Assert.Equal(action, actual.Action);
        Assert.Equal(available, actual.Available);
        Assert.Equal(unavailability, actual.Unavailability);
    }
}
