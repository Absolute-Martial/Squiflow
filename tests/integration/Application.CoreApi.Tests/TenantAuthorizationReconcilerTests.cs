using Application.CoreApi.Authorization;
using Application.Tenancy;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class TenantAuthorizationReconcilerTests
{
    [Fact]
    public async Task RevokedCurrentOwnerAuthorityStopsBeforeProviderMutation()
    {
        var proposal = new TenantAuthorizationProposal(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "reconcile-after-suspension",
            new string('A', 64),
            TenantAuthorizationProposalKind.GrantPermission,
            TenantAuthorizationProposalStatus.Pending,
            1,
            null,
            Guid.NewGuid(),
            "profiles.policy.view",
            null,
            null,
            null,
            [],
            [],
            0,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var store = new ReconciliationStore(proposal);
        var provider = new RecordingProvider();
        var reconciler = new TenantAuthorizationReconciler(store, provider, TimeProvider.System);

        var result = await reconciler.ReconcileAsync(proposal.TenantId, proposal.ProposalId, CancellationToken.None);

        Assert.Equal(TenantAuthorizationProposalStatus.Failed, result!.Status);
        Assert.Equal("delegator_authority_revoked", result.FailureCode);
        Assert.Equal(0, provider.Calls);
    }

    private sealed class RecordingProvider : ITenantAuthorizationAdministrationProvider
    {
        internal int Calls { get; private set; }

        public Task<TenantAuthorizationProviderResult> EnsureAsync(
            TenantAuthorizationProposal proposal,
            IReadOnlyList<TenantRoleAssignment> roleAssignments,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new TenantAuthorizationProviderResult(TenantAuthorizationProviderOutcome.Applied));
        }
    }

    private sealed class ReconciliationStore(TenantAuthorizationProposal proposal) : ITenantAuthorizationAdministrationStore
    {
        public Task<bool> IsInitialOwnerAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<TenantAuthorizationProposal?> MarkAttemptAsync(
            Guid tenantId, Guid proposalId, DateTimeOffset occurredAt, CancellationToken cancellationToken) =>
            Task.FromResult<TenantAuthorizationProposal?>(proposal);

        public Task<TenantAuthorizationProposal?> MarkFailedAsync(
            Guid tenantId, Guid proposalId, string failureCode, DateTimeOffset occurredAt, CancellationToken cancellationToken) =>
            Task.FromResult<TenantAuthorizationProposal?>(proposal with
            {
                Status = TenantAuthorizationProposalStatus.Failed,
                FailureCode = failureCode,
            });

        public Task<int?> GetAuthorizationRevisionAsync(Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantAuthorizationProposalResult> ProposeAsync(
            TenantAuthorizationActor actor, TenantAuthorizationProposalIntent intent,
            DateTimeOffset requestedAt, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TenantAuthorizationProposal?> FindProposalAsync(
            Guid tenantId, Guid proposalId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantAuthorizationProposal?>(proposal);

        public Task<TenantAuthorizationProposal?> MarkUncertainAsync(
            Guid tenantId, Guid proposalId, string failureCode, DateTimeOffset occurredAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TenantAuthorizationProposal?> CompleteAsync(
            Guid tenantId, Guid proposalId, DateTimeOffset occurredAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<TenantCustomRole>> ListRolesAsync(Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantCustomRole?> FindRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TenantRoleAssignment>> ListRoleAssignmentsAsync(
            Guid tenantId, Guid roleId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantOwnerTransferResult> TransferInitialOwnerAsync(
            TenantAuthorizationActor actor, TenantOwnerTransferIntent intent,
            DateTimeOffset occurredAt, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
