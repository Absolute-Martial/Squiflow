using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Application.Tenancy.Postgres.Tests;

public sealed class TenantAuthorizationAdministrationPostgresTests : PostgresTestDatabase
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 5, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExhaustedReconciliationBudgetLeavesAnAmbiguousProposalUncertainAndHoldingItsSlot()
    {
        var fixture = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantAuthorizationAdministrationStore(source);
        var actor = TenantAuthorizationActor.Create(fixture.OwnerAccountId);
        var created = await store.ProposeAsync(actor, TenantAuthorizationProposalIntent.PermissionChange(
            TenantAuthorizationProposalKind.GrantPermission,
            fixture.TenantId,
            fixture.TargetAccountId,
            "orders.view",
            1,
            "ambiguous-budget"), OccurredAt, CancellationToken.None);
        var proposal = Assert.IsType<TenantAuthorizationProposal>(created.Proposal);

        // Drive the proposal past the reconciliation budget entirely through ambiguous
        // outcomes. A timeout can hide a provider write that succeeded, so the proposal must
        // never be turned into a final failure on this evidence.
        for (var attempt = 0; attempt < 24; attempt++)
        {
            await store.MarkAttemptAsync(
                fixture.TenantId, proposal.ProposalId, OccurredAt.AddSeconds(attempt), CancellationToken.None);
            await store.MarkUncertainAsync(
                fixture.TenantId, proposal.ProposalId, "provider_timeout",
                OccurredAt.AddSeconds(attempt), CancellationToken.None);
        }

        var exhausted = await store.FindProposalAsync(
            fixture.TenantId, proposal.ProposalId, CancellationToken.None);
        Assert.NotNull(exhausted);
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, exhausted!.Status);
        // The exhausted budget must not erase why the outcome is unresolved: an operator
        // looking at a wedged proposal needs the provider's own reason, not only the fact
        // that twenty attempts were spent.
        Assert.Equal("provider_timeout", exhausted.FailureCode);
        Assert.Equal(20, exhausted.AttemptCount);

        // The slot must still be held: a competing change to the same authorization has to be
        // refused, otherwise it would reconcile against provider state that may already
        // reflect this unresolved write.
        var competing = await store.ProposeAsync(actor, TenantAuthorizationProposalIntent.PermissionChange(
            TenantAuthorizationProposalKind.GrantPermission,
            fixture.TenantId,
            fixture.TargetAccountId,
            "orders.view",
            1,
            "competing-after-budget"), OccurredAt.AddSeconds(60), CancellationToken.None);
        Assert.Equal(
            TenantAuthorizationProposalResultStatus.AuthorizationRevisionConflict,
            competing.Status);

        // Still reconcilable: the authorization revision must not have advanced and the
        // grant must not have been completed behind the ambiguous outcome.
        Assert.Equal(1, await store.GetAuthorizationRevisionAsync(fixture.TenantId, CancellationToken.None));
        Assert.False(await GrantIsActiveAsync(fixture.TenantId, fixture.TargetAccountId, "orders.view"));

        // A later reconciliation that does observe the provider can still complete it.
        var completed = await store.CompleteAsync(
            fixture.TenantId, proposal.ProposalId, OccurredAt.AddSeconds(120), CancellationToken.None);
        Assert.Equal(TenantAuthorizationProposalStatus.Applied, completed!.Status);
        Assert.Equal(2, await store.GetAuthorizationRevisionAsync(fixture.TenantId, CancellationToken.None));
    }

    [Fact]
    public async Task GrantProposalIsIdempotentArbitratedAndAdvancesRevisionOnlyOnceAfterCompletion()
    {
        var fixture = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantAuthorizationAdministrationStore(source);
        var actor = TenantAuthorizationActor.Create(fixture.OwnerAccountId);
        var intent = TenantAuthorizationProposalIntent.PermissionChange(
            TenantAuthorizationProposalKind.GrantPermission,
            fixture.TenantId,
            fixture.TargetAccountId,
            "orders.view",
            1,
            "grant-orders-view");

        var created = await store.ProposeAsync(actor, intent, OccurredAt, CancellationToken.None);
        Assert.Equal(TenantAuthorizationProposalResultStatus.Created, created.Status);
        var proposal = Assert.IsType<TenantAuthorizationProposal>(created.Proposal);
        Assert.Equal(TenantAuthorizationProposalStatus.Pending, proposal.Status);
        Assert.Equal(1, await store.GetAuthorizationRevisionAsync(fixture.TenantId, CancellationToken.None));

        var replay = await store.ProposeAsync(actor, intent, OccurredAt.AddSeconds(1), CancellationToken.None);
        Assert.Equal(TenantAuthorizationProposalResultStatus.Replayed, replay.Status);
        Assert.Equal(proposal.ProposalId, replay.Proposal!.ProposalId);

        var changed = TenantAuthorizationProposalIntent.PermissionChange(
            TenantAuthorizationProposalKind.GrantPermission,
            fixture.TenantId,
            fixture.TargetAccountId,
            "workspace.view",
            1,
            "grant-orders-view");
        Assert.Equal(
            TenantAuthorizationProposalResultStatus.IdempotencyKeyConflict,
            (await store.ProposeAsync(actor, changed, OccurredAt.AddSeconds(2), CancellationToken.None)).Status);

        var competing = TenantAuthorizationProposalIntent.PermissionChange(
            TenantAuthorizationProposalKind.GrantPermission,
            fixture.TenantId,
            fixture.TargetAccountId,
            "workspace.view",
            1,
            "competing");
        Assert.Equal(
            TenantAuthorizationProposalResultStatus.AuthorizationRevisionConflict,
            (await store.ProposeAsync(actor, competing, OccurredAt.AddSeconds(3), CancellationToken.None)).Status);

        var attempted = await store.MarkAttemptAsync(
            fixture.TenantId, proposal.ProposalId, OccurredAt.AddSeconds(4), CancellationToken.None);
        Assert.Equal(1, attempted!.AttemptCount);
        var uncertain = await store.MarkUncertainAsync(
            fixture.TenantId, proposal.ProposalId, "provider_timeout", OccurredAt.AddSeconds(5), CancellationToken.None);
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, uncertain!.Status);
        Assert.Equal("provider_timeout", uncertain.FailureCode);

        var retried = await store.MarkAttemptAsync(
            fixture.TenantId, proposal.ProposalId, OccurredAt.AddMilliseconds(5500), CancellationToken.None);
        Assert.Equal(TenantAuthorizationProposalStatus.Uncertain, retried!.Status);
        Assert.Equal(2, retried.AttemptCount);
        Assert.Equal("provider_timeout", retried.FailureCode);

        var completed = await store.CompleteAsync(
            fixture.TenantId, proposal.ProposalId, OccurredAt.AddSeconds(6), CancellationToken.None);
        Assert.Equal(TenantAuthorizationProposalStatus.Applied, completed!.Status);
        Assert.Equal(2, completed.AppliedAuthorizationRevision);
        Assert.Equal(2, await store.GetAuthorizationRevisionAsync(fixture.TenantId, CancellationToken.None));
        Assert.True(await GrantIsActiveAsync(fixture.TenantId, fixture.TargetAccountId, "orders.view"));

        var completionReplay = await store.CompleteAsync(
            fixture.TenantId, proposal.ProposalId, OccurredAt.AddSeconds(7), CancellationToken.None);
        Assert.Equal(2, completionReplay!.AppliedAuthorizationRevision);
        Assert.Equal(2, await store.GetAuthorizationRevisionAsync(fixture.TenantId, CancellationToken.None));

        var duplicateGrant = TenantAuthorizationProposalIntent.PermissionChange(
            TenantAuthorizationProposalKind.GrantPermission,
            fixture.TenantId,
            fixture.TargetAccountId,
            "orders.view",
            2,
            "duplicate-grant");
        Assert.Equal(
            TenantAuthorizationProposalResultStatus.DirectPermissionConflict,
            (await store.ProposeAsync(actor, duplicateGrant, OccurredAt.AddSeconds(8), CancellationToken.None)).Status);
        Assert.Equal(2, await store.GetAuthorizationRevisionAsync(fixture.TenantId, CancellationToken.None));
    }

    [Fact]
    public async Task RevocationAndCustomRoleLifecyclePreserveHistoryAndAdvanceOneRevisionPerObservedChange()
    {
        var fixture = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantAuthorizationAdministrationStore(source);
        var actor = TenantAuthorizationActor.Create(fixture.OwnerAccountId);

        var grant = await CreateAndCompleteAsync(store, actor,
            TenantAuthorizationProposalIntent.PermissionChange(
                TenantAuthorizationProposalKind.GrantPermission,
                fixture.TenantId, fixture.TargetAccountId, "workspace.view", 1, "grant"), 0);
        Assert.Equal(2, grant.AppliedAuthorizationRevision);

        var revoke = await CreateAndCompleteAsync(store, actor,
            TenantAuthorizationProposalIntent.PermissionChange(
                TenantAuthorizationProposalKind.RevokePermission,
                fixture.TenantId, fixture.TargetAccountId, "workspace.view", 2, "revoke"), 10);
        Assert.Equal(3, revoke.AppliedAuthorizationRevision);
        Assert.False(await GrantIsActiveAsync(fixture.TenantId, fixture.TargetAccountId, "workspace.view"));
        Assert.Equal(
            TenantAuthorizationProposalResultStatus.DirectPermissionConflict,
            (await store.ProposeAsync(
                actor,
                TenantAuthorizationProposalIntent.PermissionChange(
                    TenantAuthorizationProposalKind.RevokePermission,
                    fixture.TenantId, fixture.TargetAccountId, "workspace.view", 3, "duplicate-revoke"),
                OccurredAt.AddSeconds(12), CancellationToken.None)).Status);
        Assert.Equal(3, await store.GetAuthorizationRevisionAsync(fixture.TenantId, CancellationToken.None));

        var roleId = Guid.NewGuid();
        var createRole = await CreateAndCompleteAsync(store, actor,
            TenantAuthorizationProposalIntent.CreateRole(
                fixture.TenantId, roleId, "Order reader", ["orders.view"], 3, "role-create"), 20);
        Assert.Equal(4, createRole.AppliedAuthorizationRevision);
        var role = Assert.IsType<TenantCustomRole>(await store.FindRoleAsync(fixture.TenantId, roleId, CancellationToken.None));
        Assert.Equal(TenantRoleAvailability.Active, role.Availability);
        Assert.Equal(1, role.Revision);

        var assignment = await CreateAndCompleteAsync(store, actor,
            TenantAuthorizationProposalIntent.RoleAssignment(
                TenantAuthorizationProposalKind.AssignRole,
                fixture.TenantId, roleId, fixture.TargetAccountId, 4, "role-assign"), 30);
        Assert.Equal(5, assignment.AppliedAuthorizationRevision);
        Assert.Single(
            await store.ListRoleAssignmentsAsync(fixture.TenantId, roleId, CancellationToken.None),
            value => value.Availability == TenantRoleAssignmentAvailability.Active);

        var revised = await CreateAndCompleteAsync(store, actor,
            TenantAuthorizationProposalIntent.ReviseRole(
                fixture.TenantId, roleId, 1, "Order operator", ["orders.create", "orders.view"], 5, "role-revise"), 40);
        Assert.Equal(6, revised.AppliedAuthorizationRevision);
        role = Assert.IsType<TenantCustomRole>(await store.FindRoleAsync(fixture.TenantId, roleId, CancellationToken.None));
        Assert.Equal(2, role.Revision);
        Assert.Equal(["orders.create", "orders.view"], role.PermissionIds);

        var retired = await CreateAndCompleteAsync(store, actor,
            TenantAuthorizationProposalIntent.RetireRole(
                fixture.TenantId, roleId, 2, 6, "role-retire"), 50);
        Assert.Equal(7, retired.AppliedAuthorizationRevision);
        role = Assert.IsType<TenantCustomRole>(await store.FindRoleAsync(fixture.TenantId, roleId, CancellationToken.None));
        Assert.Equal(TenantRoleAvailability.Retired, role.Availability);
        Assert.All(
            await store.ListRoleAssignmentsAsync(fixture.TenantId, roleId, CancellationToken.None),
            value => Assert.Equal(TenantRoleAssignmentAvailability.Removed, value.Availability));
    }

    [Fact]
    public async Task ProposalRejectsStaleRevisionInactiveTargetAndRoleFromAnotherTenant()
    {
        var fixture = await SeedAsync();
        var other = await SeedAsync("Other tenant");
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantAuthorizationAdministrationStore(source);
        var actor = TenantAuthorizationActor.Create(fixture.OwnerAccountId);

        Assert.Equal(
            TenantAuthorizationProposalResultStatus.AuthorizationRevisionConflict,
            (await store.ProposeAsync(actor,
                TenantAuthorizationProposalIntent.PermissionChange(
                    TenantAuthorizationProposalKind.GrantPermission,
                    fixture.TenantId, fixture.TargetAccountId, "orders.view", 2, "stale"),
                OccurredAt, CancellationToken.None)).Status);

        await ExecuteSqlAsync(
            "UPDATE tenancy.memberships SET availability = 2, suspended_at = now() WHERE tenant_id = @tenant AND account_id = @account",
            ("tenant", fixture.TenantId), ("account", fixture.TargetAccountId));
        Assert.Equal(
            TenantAuthorizationProposalResultStatus.TargetMembershipUnavailable,
            (await store.ProposeAsync(actor,
                TenantAuthorizationProposalIntent.PermissionChange(
                    TenantAuthorizationProposalKind.GrantPermission,
                    fixture.TenantId, fixture.TargetAccountId, "orders.view", 1, "inactive"),
                OccurredAt.AddSeconds(1), CancellationToken.None)).Status);

        var otherActor = TenantAuthorizationActor.Create(other.OwnerAccountId);
        var roleId = Guid.NewGuid();
        await CreateAndCompleteAsync(store, otherActor,
            TenantAuthorizationProposalIntent.CreateRole(
                other.TenantId, roleId, "Other role", ["orders.view"], 1, "other-role"), 10);
        Assert.Equal(
            TenantAuthorizationProposalResultStatus.RoleNotFound,
            (await store.ProposeAsync(actor,
                TenantAuthorizationProposalIntent.RoleAssignment(
                    TenantAuthorizationProposalKind.AssignRole,
                    fixture.TenantId, roleId, fixture.OwnerAccountId, 1, "cross-tenant-role"),
                OccurredAt.AddSeconds(20), CancellationToken.None)).Status);
    }

    [Fact]
    public async Task MigrationRollbackRefusesToDiscardAuthorizationEvidence()
    {
        var fixture = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantAuthorizationAdministrationStore(source);
        var actor = TenantAuthorizationActor.Create(fixture.OwnerAccountId);
        _ = await CreateAndCompleteAsync(
            store,
            actor,
            TenantAuthorizationProposalIntent.PermissionChange(
                TenantAuthorizationProposalKind.GrantPermission,
                fixture.TenantId, fixture.TargetAccountId, "orders.view", 1, "retained-grant"),
            0);

        await using var db = CreateContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            db.GetService<IMigrator>().MigrateAsync("202610030005_TenantAndInitialOwnerLifecycle"));
        Assert.Contains("durable evidence exists", exception.MessageText, StringComparison.Ordinal);
        Assert.Contains(
            "202610050001_TenantAuthorizationAdministration",
            await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(2, await store.GetAuthorizationRevisionAsync(fixture.TenantId, CancellationToken.None));
    }

    [Fact]
    public async Task OwnerTransferWaitsForActiveAuthorizationReconciliation()
    {
        var fixture = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantAuthorizationAdministrationStore(source);
        var actor = TenantAuthorizationActor.Create(fixture.OwnerAccountId);
        var proposal = await store.ProposeAsync(
            actor,
            TenantAuthorizationProposalIntent.PermissionChange(
                TenantAuthorizationProposalKind.GrantPermission,
                fixture.TenantId, fixture.TargetAccountId, "orders.view", 1, "pending-grant"),
            OccurredAt,
            CancellationToken.None);
        Assert.Equal(TenantAuthorizationProposalResultStatus.Created, proposal.Status);

        var blocked = await store.TransferInitialOwnerAsync(
            actor,
            TenantOwnerTransferIntent.Create(fixture.TenantId, fixture.TargetAccountId, 1, "owner-during-grant"),
            OccurredAt.AddSeconds(1),
            CancellationToken.None);
        Assert.Equal(TenantOwnerTransferStatus.AuthorizationChangeInProgress, blocked.Status);
        Assert.True(await store.IsInitialOwnerAsync(fixture.TenantId, fixture.OwnerAccountId, CancellationToken.None));
        Assert.Equal(1, blocked.AuthorizationRevision);

        var pending = Assert.IsType<TenantAuthorizationProposal>(proposal.Proposal);
        _ = await store.MarkFailedAsync(
            fixture.TenantId, pending.ProposalId, "review_rejected", OccurredAt.AddSeconds(2), CancellationToken.None);
        var transferred = await store.TransferInitialOwnerAsync(
            actor,
            TenantOwnerTransferIntent.Create(fixture.TenantId, fixture.TargetAccountId, 1, "owner-after-terminal"),
            OccurredAt.AddSeconds(3),
            CancellationToken.None);
        Assert.Equal(TenantOwnerTransferStatus.Transferred, transferred.Status);
    }

    [Fact]
    public async Task OwnerTransferIsAtomicReplayableAndMovesBothTenantAndAuthorizationRevision()
    {
        var fixture = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresTenantAuthorizationAdministrationStore(source);
        var actor = TenantAuthorizationActor.Create(fixture.OwnerAccountId);
        var intent = TenantOwnerTransferIntent.Create(
            fixture.TenantId, fixture.TargetAccountId, 1, "transfer-owner");

        var result = await store.TransferInitialOwnerAsync(actor, intent, OccurredAt, CancellationToken.None);
        Assert.Equal(TenantOwnerTransferStatus.Transferred, result.Status);
        Assert.Equal(fixture.OwnerAccountId, result.PreviousOwnerAccountId);
        Assert.Equal(fixture.TargetAccountId, result.CurrentOwnerAccountId);
        Assert.Equal(2, result.TenantRevision);
        Assert.Equal(2, result.AuthorizationRevision);
        Assert.False(await store.IsInitialOwnerAsync(fixture.TenantId, fixture.OwnerAccountId, CancellationToken.None));
        Assert.True(await store.IsInitialOwnerAsync(fixture.TenantId, fixture.TargetAccountId, CancellationToken.None));

        var replay = await store.TransferInitialOwnerAsync(actor, intent, OccurredAt.AddSeconds(1), CancellationToken.None);
        Assert.Equal(TenantOwnerTransferStatus.Replayed, replay.Status);
        Assert.Equal(2, replay.TenantRevision);
        Assert.Equal(2, replay.AuthorizationRevision);

        var changed = TenantOwnerTransferIntent.Create(
            fixture.TenantId, fixture.OwnerAccountId, 2, "transfer-owner");
        Assert.Equal(
            TenantOwnerTransferStatus.IdempotencyKeyConflict,
            (await store.TransferInitialOwnerAsync(actor, changed, OccurredAt.AddSeconds(2), CancellationToken.None)).Status);

        Assert.Equal(
            TenantOwnerTransferStatus.ActorNotInitialOwner,
            (await store.TransferInitialOwnerAsync(
                actor,
                TenantOwnerTransferIntent.Create(fixture.TenantId, fixture.OwnerAccountId, 2, "old-owner-cannot-transfer"),
                OccurredAt.AddSeconds(3), CancellationToken.None)).Status);
    }

    private static async Task<TenantAuthorizationProposal> CreateAndCompleteAsync(
        PostgresTenantAuthorizationAdministrationStore store,
        TenantAuthorizationActor actor,
        TenantAuthorizationProposalIntent intent,
        int seconds)
    {
        var proposed = await store.ProposeAsync(actor, intent, OccurredAt.AddSeconds(seconds), CancellationToken.None);
        Assert.Equal(TenantAuthorizationProposalResultStatus.Created, proposed.Status);
        var proposal = Assert.IsType<TenantAuthorizationProposal>(proposed.Proposal);
        return Assert.IsType<TenantAuthorizationProposal>(await store.CompleteAsync(
            intent.TenantId, proposal.ProposalId, OccurredAt.AddSeconds(seconds + 1), CancellationToken.None));
    }

    private async Task<(Guid TenantId, Guid OwnerAccountId, Guid TargetAccountId)> SeedAsync(string name = "Authorization tenant")
    {
        await CreateMigrationRunner().ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await ExecuteSqlAsync("""
            INSERT INTO identity_access.accounts(id, availability, created_at)
            VALUES (@owner, 1, now()), (@target, 1, now());
            INSERT INTO tenancy.tenants(id, display_name, availability, created_at, revision)
            VALUES (@tenant, @name, 1, now(), 1);
            INSERT INTO tenancy.memberships(
                tenant_id, account_id, availability, created_at, revision, activated_at, suspended_at, removed_at, is_initial_owner)
            VALUES
                (@tenant, @owner, 1, now(), 1, now(), NULL, NULL, true),
                (@tenant, @target, 1, now(), 1, now(), NULL, NULL, false);
            """,
            ("tenant", tenantId), ("owner", ownerId), ("target", targetId), ("name", name));
        return (tenantId, ownerId, targetId);
    }

    private async Task<bool> GrantIsActiveAsync(Guid tenantId, Guid accountId, string permissionId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT is_active
            FROM tenancy.tenant_permission_grants
            WHERE tenant_id = @tenant AND account_id = @account AND permission_id = @permission;
            """;
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("permission", permissionId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private async Task ExecuteSqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }
}
