namespace Application.Orders;

public enum OrderDraftAction
{
    Revise = 1,
    Abandon = 2,
    Commit = 3,
}

public enum OrderDraftActionUnavailability
{
    PermissionRequired = 1,
    AlreadyAbandoned = 2,
    AlreadyCommitted = 3,
}

public sealed record OrderDraftActionAvailability(
    OrderDraftAction Action,
    bool Available,
    OrderDraftActionUnavailability? Unavailability);

public sealed record OrderDraftActionGuide(
    long ObservedRevision,
    IReadOnlyList<OrderDraftActionAvailability> Actions)
{
    public static OrderDraftActionGuide Explain(
        OrderDraftState state,
        long observedRevision,
        bool mayRevise,
        bool mayAbandon,
        bool mayCommit = false)
    {
        if (observedRevision < 1)
            throw new InvalidOperationException("An order draft revision must be positive.");

        var revise = OrderDraftLifecycle.AssessRevise(state, observedRevision, observedRevision);
        var abandon = OrderDraftLifecycle.AssessAbandon(state, observedRevision, observedRevision);
        var commit = OrderDraftLifecycle.AssessCommit(state, observedRevision, observedRevision);
        return new OrderDraftActionGuide(observedRevision, Array.AsReadOnly(
        [
            Availability(
                OrderDraftAction.Revise,
                revise switch
                {
                    ReviseOrderDraftStatus.AlreadyAbandoned => OrderDraftActionUnavailability.AlreadyAbandoned,
                    ReviseOrderDraftStatus.AlreadyCommitted => OrderDraftActionUnavailability.AlreadyCommitted,
                    _ => null,
                },
                mayRevise),
            Availability(
                OrderDraftAction.Abandon,
                abandon switch
                {
                    AbandonOrderDraftStatus.AlreadyAbandoned => OrderDraftActionUnavailability.AlreadyAbandoned,
                    AbandonOrderDraftStatus.AlreadyCommitted => OrderDraftActionUnavailability.AlreadyCommitted,
                    _ => null,
                },
                mayAbandon),
            Availability(
                OrderDraftAction.Commit,
                commit switch
                {
                    CommitOrderDraftStatus.AlreadyAbandoned => OrderDraftActionUnavailability.AlreadyAbandoned,
                    CommitOrderDraftStatus.AlreadyCommitted => OrderDraftActionUnavailability.AlreadyCommitted,
                    _ => null,
                },
                mayCommit),
        ]));
    }

    private static OrderDraftActionAvailability Availability(
        OrderDraftAction action,
        OrderDraftActionUnavailability? lifecycleUnavailability,
        bool permitted) => new(
            action,
            lifecycleUnavailability is null && permitted,
            lifecycleUnavailability
                ?? (permitted
                    ? null
                    : OrderDraftActionUnavailability.PermissionRequired));
}
