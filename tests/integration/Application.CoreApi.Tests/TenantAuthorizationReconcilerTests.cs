using Application.CoreApi.Authorization;
using Application.Tenancy;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class TenantAuthorizationReconcilerTests
{
    [Theory]
    [InlineData(0, TenantAuthorizationProposalStatus.Failed)]
    [InlineData(1, TenantAuthorizationProposalStatus.Uncertain)]
    public async Task RevokedDelegatorRejectsOnlyAnUnattemptedProposal(int attempts, TenantAuthorizationProposalStatus expected)
    {
        using var store = new Store { OwnerActive = false };
        store.Proposal = store.Proposal with { AttemptCount = attempts };
        var provider = new Provider();
        var result = await Reconciler(store, provider).ReconcileAsync(store.Proposal.TenantId, store.Proposal.ProposalId, default);
        Assert.Equal(expected, result!.Status);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task DuplicateExecutorWaitsThenReplaysWithoutUndoingASubsequentRevoke()
    {
        using var store = new Store();
        var provider = new Provider { Delay = true };
        var grant = store.Proposal;
        var first = Reconciler(store, provider).ReconcileAsync(grant.TenantId, grant.ProposalId, default);
        await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var duplicate = Reconciler(store, provider).ReconcileAsync(grant.TenantId, grant.ProposalId, default);
        Assert.False(duplicate.IsCompleted);
        provider.Release.TrySetResult();
        Assert.All(await Task.WhenAll(first, duplicate), result => Assert.Equal(TenantAuthorizationProposalStatus.Applied, result!.Status));
        Assert.Equal(1, provider.Calls);
        Assert.Equal(2, store.Revision);
        store.Proposal = grant with
        {
            ProposalId = Guid.NewGuid(),
            Kind = TenantAuthorizationProposalKind.RevokePermission,
            ExpectedAuthorizationRevision = 2
        };
        await Reconciler(store, provider).ReconcileAsync(grant.TenantId, store.Proposal.ProposalId, default);
        Assert.False(provider.Granted);
        var revoke = store.Proposal;
        store.Proposal = store.Completed[grant.ProposalId];
        await Reconciler(store, provider).ReconcileAsync(grant.TenantId, grant.ProposalId, default);
        Assert.False(provider.Granted);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(3, store.Revision);
        Assert.Equal(TenantAuthorizationProposalStatus.Applied, revoke.Status);
    }

    [Fact]
    public async Task ObservedGrantSurvivesLocalFailureAndAuthorityLossThenCompletesWithoutAnotherWrite()
    {
        using var store = new Store { FailCompletionOnce = true };
        var provider = new Provider();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Reconciler(store, provider).ReconcileAsync(
            store.Proposal.TenantId, store.Proposal.ProposalId, default));
        Assert.True(provider.Granted);
        Assert.Equal(TenantAuthorizationExecution.Observed, store.Proposal.FailureCode);
        store.OwnerActive = false;
        var held = await Reconciler(store, provider).ReconcileAsync(store.Proposal.TenantId, store.Proposal.ProposalId, default);
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, held!.Status);
        store.OwnerActive = true;
        var recovered = await Reconciler(store, provider).ReconcileAsync(store.Proposal.TenantId, store.Proposal.ProposalId, default);
        Assert.Equal(TenantAuthorizationProposalStatus.Applied, recovered!.Status);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(2, store.Revision);
    }

    [Fact]
    public async Task UnknownWriteOutcomeCannotDispatchOrCompleteAfterRestartOrAuthorityLoss()
    {
        using var store = new Store();
        var provider = new Provider { Result = new(TenantAuthorizationProviderOutcome.Uncertain, "provider_timeout") };
        var first = await Reconciler(store, provider).ReconcileAsync(store.Proposal.TenantId, store.Proposal.ProposalId, default);
        Assert.True(provider.Granted);
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, first!.Status);
        foreach (var ownerActive in new[] { true, false, true })
        {
            store.OwnerActive = ownerActive;
            var retry = await Reconciler(store, provider).ReconcileAsync(store.Proposal.TenantId, store.Proposal.ProposalId, default);
            Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, retry!.Status);
        }
        Assert.Equal(1, provider.Calls);
        Assert.Equal(1, store.Revision);
    }

    [Theory]
    [InlineData(false, TenantAuthorizationProposalStatus.Failed)]
    [InlineData(true, TenantAuthorizationProposalStatus.Uncertain)]
    public async Task KnownRejectionCannotEraseAnEarlierPossibleEffect(bool earlierEffect, TenantAuthorizationProposalStatus expected)
    {
        using var store = new Store();
        if (earlierEffect) store.Proposal = store.Proposal with
        {
            AttemptCount = 1,
            Status = TenantAuthorizationProposalStatus.Uncertain,
            FailureCode = TenantAuthorizationExecution.FinishedPrefix + "observation_failed"
        };
        var provider = new Provider { Result = new(TenantAuthorizationProviderOutcome.Failed, "authorization_model_rejected", NoEffectProven: true) };
        var result = await Reconciler(store, provider).ReconcileAsync(store.Proposal.TenantId, store.Proposal.ProposalId, default);
        Assert.Equal(expected, result!.Status);
        Assert.Equal(1, store.Revision);
    }

    [Fact]
    public async Task KnownFinishedObservationFailureStopsAtTheAttemptBudgetWithoutReleasingTheProposal()
    {
        using var store = new Store();
        store.Proposal = store.Proposal with
        {
            AttemptCount = TenantAuthorizationExecution.MaximumAttempts - 1,
            Status = TenantAuthorizationProposalStatus.Uncertain,
            FailureCode = TenantAuthorizationExecution.FinishedPrefix + "authorization_model_rejected"
        };
        var provider = new Provider
        {
            Result = new(TenantAuthorizationProviderOutcome.Uncertain, "authorization_model_rejected", MutationCompleted: true)
        };
        var reconciler = Reconciler(store, provider);
        var finalAttempt = await reconciler.ReconcileAsync(store.Proposal.TenantId, store.Proposal.ProposalId, default);
        Assert.Equal(TenantAuthorizationExecution.MaximumAttempts, finalAttempt!.AttemptCount);
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, finalAttempt.Status);
        var retained = await reconciler.ReconcileAsync(store.Proposal.TenantId, store.Proposal.ProposalId, default);
        Assert.Equal(finalAttempt, retained);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(1, store.Revision);
    }

    private static TenantAuthorizationReconciler Reconciler(Store store, Provider provider) => new(store, provider, TimeProvider.System);

    private sealed class Provider : ITenantAuthorizationAdministrationProvider
    {
        internal int Calls { get; private set; }
        internal bool Granted { get; private set; }
        internal bool Delay { get; init; }
        internal TenantAuthorizationProviderResult Result { get; init; } = new(TenantAuthorizationProviderOutcome.Applied);
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<TenantAuthorizationProviderResult> EnsureAsync(TenantAuthorizationProposal proposal,
            IReadOnlyList<TenantRoleAssignment> roleAssignments, CancellationToken cancellationToken)
        {
            Calls++;
            if (Delay) { Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
            Granted = proposal.Kind == TenantAuthorizationProposalKind.GrantPermission;
            return Result;
        }
    }

    // Controlled port tests prove orchestration only; real PostgreSQL/OpenFGA cases prove the session implementation.
    private sealed class Store : ITenantAuthorizationReconciliationStore, IDisposable
    {
        private readonly SemaphoreSlim ownership = new(1);
        internal TenantAuthorizationProposal Proposal { get; set; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "reconcile", new string('A', 64), TenantAuthorizationProposalKind.GrantPermission,
            TenantAuthorizationProposalStatus.Pending, 1, null, Guid.NewGuid(), "invoices.issue", null, null, null,
            [], [], 0, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        internal Dictionary<Guid, TenantAuthorizationProposal> Completed { get; } = [];
        internal bool OwnerActive { get; set; } = true;
        internal bool FailCompletionOnce { get; set; }
        internal int Revision { get; private set; } = 1;
        public async ValueTask<ITenantAuthorizationReconciliationSession> OpenAsync(Guid tenantId, CancellationToken cancellationToken)
        { await ownership.WaitAsync(cancellationToken); return new Session(this); }
        public void Dispose() => ownership.Dispose();

        private sealed class Session(Store store) : ITenantAuthorizationReconciliationSession
        {
            private bool fresh;
            public ValueTask DisposeAsync() { store.ownership.Release(); return ValueTask.CompletedTask; }
            public Task<TenantAuthorizationProposal?> FindAsync(Guid proposalId, CancellationToken cancellationToken) => Task.FromResult<TenantAuthorizationProposal?>(store.Proposal);
            public Task<bool> IsInitialOwnerAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult(store.OwnerActive);
            public Task<IReadOnlyList<TenantRoleAssignment>> ListRoleAssignmentsAsync(Guid roleId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<TenantAuthorizationProposal?> RejectUnattemptedAsync(Guid proposalId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
            {
                store.Proposal = store.Proposal with
                {
                    Status = store.Proposal.AttemptCount == 0 && store.Proposal.Status == TenantAuthorizationProposalStatus.Pending
                    ? TenantAuthorizationProposalStatus.Failed : TenantAuthorizationProposalStatus.Uncertain
                };
                return Task.FromResult<TenantAuthorizationProposal?>(store.Proposal);
            }
            public Task<TenantAuthorizationProposal> BeginProviderCallAsync(Guid proposalId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
            {
                Assert.True(TenantAuthorizationExecution.CanDispatch(store.Proposal));
                fresh = store.Proposal.AttemptCount == 0;
                store.Proposal = store.Proposal with
                {
                    Status = TenantAuthorizationProposalStatus.Uncertain,
                    AttemptCount = store.Proposal.AttemptCount + 1,
                    FailureCode = TenantAuthorizationExecution.InFlight
                };
                return Task.FromResult(store.Proposal);
            }
            public Task<TenantAuthorizationProposal> RecordProviderOutcomeAsync(Guid proposalId, TenantAuthorizationProviderEffect effect,
                string? failureCode, DateTimeOffset occurredAt, CancellationToken cancellationToken)
            {
                store.Proposal = store.Proposal with
                {
                    Status = effect == TenantAuthorizationProviderEffect.RejectedWithoutEffect && fresh ? TenantAuthorizationProposalStatus.Failed : TenantAuthorizationProposalStatus.Uncertain,
                    FailureCode = effect switch
                    {
                        TenantAuthorizationProviderEffect.Observed => TenantAuthorizationExecution.Observed,
                        TenantAuthorizationProviderEffect.FinishedWithoutObservation => TenantAuthorizationExecution.FinishedPrefix + failureCode,
                        _ => TenantAuthorizationExecution.UnresolvedPrefix + failureCode,
                    },
                };
                return Task.FromResult(store.Proposal);
            }
            public Task<TenantAuthorizationProposal?> CompleteAsync(Guid proposalId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
            {
                if (store.FailCompletionOnce) { store.FailCompletionOnce = false; throw new InvalidOperationException("Injected local completion failure"); }
                Assert.Equal(TenantAuthorizationExecution.Observed, store.Proposal.FailureCode);
                store.Proposal = store.Proposal with { Status = TenantAuthorizationProposalStatus.Applied, AppliedAuthorizationRevision = ++store.Revision };
                store.Completed[proposalId] = store.Proposal;
                return Task.FromResult<TenantAuthorizationProposal?>(store.Proposal);
            }
        }
    }
}
