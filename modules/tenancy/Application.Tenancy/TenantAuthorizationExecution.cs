namespace Application.Tenancy;

public enum TenantAuthorizationProviderEffect
{
    Observed = 1,
    RejectedWithoutEffect = 2,
    FinishedWithoutObservation = 3,
    Unresolved = 4,
}

public static class TenantAuthorizationExecution
{
    // These retained outcome codes distinguish remote-call lifetime from local completion.
    public const string InFlight = "execution_in_flight";
    public const string Observed = "execution_observed";
    public const string FinishedPrefix = "execution_finished:";
    public const string UnresolvedPrefix = "execution_unresolved:";
    public const int MaximumAttempts = 20;

    public static bool IsProtocolCode(string code) => code.StartsWith("execution_", StringComparison.Ordinal);

    public static bool CanDispatch(TenantAuthorizationProposal proposal) =>
        (proposal.Status is TenantAuthorizationProposalStatus.Pending or TenantAuthorizationProposalStatus.Uncertain) &&
        proposal.AttemptCount < MaximumAttempts &&
        (proposal.Status == TenantAuthorizationProposalStatus.Pending && proposal.AttemptCount == 0 ||
         proposal.FailureCode?.StartsWith(FinishedPrefix, StringComparison.Ordinal) is true);
}

public interface ITenantAuthorizationReconciliationStore
{
    ValueTask<ITenantAuthorizationReconciliationSession> OpenAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface ITenantAuthorizationReconciliationSession : IAsyncDisposable
{
    Task<TenantAuthorizationProposal?> FindAsync(Guid proposalId, CancellationToken cancellationToken);
    Task<bool> IsInitialOwnerAsync(Guid accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantRoleAssignment>> ListRoleAssignmentsAsync(Guid roleId, CancellationToken cancellationToken);
    Task<TenantAuthorizationProposal?> RejectUnattemptedAsync(Guid proposalId, DateTimeOffset occurredAt, CancellationToken cancellationToken);
    Task<TenantAuthorizationProposal> BeginProviderCallAsync(Guid proposalId, DateTimeOffset occurredAt, CancellationToken cancellationToken);
    Task<TenantAuthorizationProposal> RecordProviderOutcomeAsync(Guid proposalId, TenantAuthorizationProviderEffect effect,
        string? failureCode, DateTimeOffset occurredAt, CancellationToken cancellationToken);
    Task<TenantAuthorizationProposal?> CompleteAsync(Guid proposalId, DateTimeOffset occurredAt, CancellationToken cancellationToken);
}
