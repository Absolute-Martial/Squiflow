using Application.CoreApi.Authorization;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed partial class TenantAuthorizationReconciliationNativeTests
{
    [Theory]
    [InlineData(400, false)]
    [InlineData(404, false)]
    [InlineData(400, true)]
    [InlineData(404, true)]
    public async Task CompletedWriteWithRejectedReadbackRetainsFinishedEvidenceAndRecoversAfterRestart(int readbackStatus, bool revoke)
    {
        var scenario = await fixture.SeedAsync();
        await using var proxy = await AuthorizationWriteProxy.StartAsync(fixture.ApiUri);
        var configuration = fixture.Configuration(proxy.Uri);
        using var client = new OpenFgaTenantAuthorizationAdministrationClient(configuration);
        var provider = new OpenFgaTenantAuthorizationAdministrationProvider(client, configuration);
        var proposal = scenario.Proposal;
        var priorWrites = 0;
        await using (var source = fixture.Source(1))
        {
            var store = new PostgresTenantAuthorizationAdministrationStore(source);
            var reconciler = new TenantAuthorizationReconciler(store, provider, TimeProvider.System);
            if (revoke)
            {
                Assert.Equal(TenantAuthorizationProposalStatus.Applied,
                    (await reconciler.ReconcileAsync(scenario.TenantId, proposal.ProposalId, default))!.Status);
                var result = await store.ProposeAsync(TenantAuthorizationActor.Create(scenario.OwnerId),
                    TenantAuthorizationProposalIntent.PermissionChange(TenantAuthorizationProposalKind.RevokePermission,
                        scenario.TenantId, scenario.TargetId, "invoices.issue", 2, "readback-revoke"), DateTimeOffset.UtcNow, default);
                Assert.Equal(TenantAuthorizationProposalResultStatus.Created, result.Status);
                proposal = result.Proposal!;
                priorWrites = 1;
            }

            proxy.ReadbackRejection = readbackStatus;
            var retained = await reconciler.ReconcileAsync(scenario.TenantId, proposal.ProposalId, default);
            Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, retained!.Status);
            Assert.Equal(TenantAuthorizationExecution.FinishedPrefix + "authorization_model_rejected", retained.FailureCode);
            Assert.Equal(1, retained.AttemptCount);
            Assert.True(TenantAuthorizationExecution.CanDispatch(retained));
            Assert.Equal(!revoke, await fixture.AllowedAsync(scenario));
            Assert.Equal(priorWrites + 1, proxy.Writes);
            Assert.Equal(1, proxy.RejectedReadbacks);
            Assert.Equal(proposal.ExpectedAuthorizationRevision, await store.GetAuthorizationRevisionAsync(scenario.TenantId, default));
            Assert.Equal(TenantAuthorizationProposalStatus.Uncertain,
                (await store.CompleteAsync(scenario.TenantId, proposal.ProposalId, DateTimeOffset.UtcNow, default))!.Status);
            var blocked = await store.ProposeAsync(TenantAuthorizationActor.Create(scenario.OwnerId),
                TenantAuthorizationProposalIntent.PermissionChange(TenantAuthorizationProposalKind.GrantPermission,
                    scenario.TenantId, scenario.TargetId, "invoices.view", proposal.ExpectedAuthorizationRevision, "readback-slot"),
                DateTimeOffset.UtcNow, default);
            Assert.Equal(TenantAuthorizationProposalResultStatus.AuthorizationRevisionConflict, blocked.Status);
        }

        proxy.ReadbackRejection = 0;
        await using var restartedSource = fixture.Source(1);
        var restartedStore = new PostgresTenantAuthorizationAdministrationStore(restartedSource);
        var restarted = new TenantAuthorizationReconciler(restartedStore, provider, TimeProvider.System);
        var recovered = await restarted.ReconcileAsync(scenario.TenantId, proposal.ProposalId, default);
        Assert.Equal(TenantAuthorizationProposalStatus.Applied, recovered!.Status);
        Assert.Equal(2, recovered.AttemptCount);
        Assert.Equal(proposal.ExpectedAuthorizationRevision + 1, await restartedStore.GetAuthorizationRevisionAsync(scenario.TenantId, default));
        Assert.Equal(!revoke, await fixture.AllowedAsync(scenario));
        Assert.Equal(priorWrites + 2, proxy.Writes);
        Assert.Equal(1, proxy.RejectedReadbacks);
        Assert.Equal(TenantAuthorizationProposalStatus.Applied,
            (await restarted.ReconcileAsync(scenario.TenantId, proposal.ProposalId, default))!.Status);
        Assert.Equal(priorWrites + 2, proxy.Writes);
        Assert.Equal(priorWrites + 1, await fixture.CountAsync(
            "SELECT count(*) FROM tenancy.tenant_authorization_events WHERE tenant_id=@tenant AND reason='provider_state_observed'", scenario.TenantId));
    }
}
