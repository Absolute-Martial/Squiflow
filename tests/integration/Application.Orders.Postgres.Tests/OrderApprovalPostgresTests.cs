using System.Collections.Concurrent;
using System.Text.Json;
using Application.Customers;
using Application.Orders;
using Application.Profiles;
using Application.Profiles.Postgres;
using Application.Tenancy;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
    [Fact]
    public async Task ApprovalReceiptsAndCommitEvidenceSurviveDatabaseTimestampPrecision()
    {
        await using var fixture = await CreateApprovalFixtureAsync(clock: new SubMicrosecondClock());
        var stored = (await fixture.Orders.FindAsync(fixture.Requester, fixture.Draft.OrderId, default))!;
        Assert.Equal(fixture.Draft.CreatedAt, stored.CreatedAt);
        var submitted = await fixture.Orders.SubmitApprovalAsync(fixture.Requester,
            new(stored.OrderId, stored.Revision, fixture.Approver.AccountId), "submit", default);
        Assert.Equal(OrderApprovalStatus.Applied, submitted.Status);
        var approved = await fixture.Orders.DecideApprovalAsync(fixture.Approver,
            new(submitted.Request!.RequestId, 1, OrderApprovalDecision.Approve), "approve", default);
        Assert.Equal(OrderApprovalStatus.Applied, approved.Status);
        Assert.Equivalent(approved.Request, await fixture.NewStore().FindApprovalAsync(fixture.Requester, submitted.Request.RequestId, default));
        var committed = await new CommitOrderDraft(fixture.Orders).ExecuteAsync(fixture.Requester,
            new(stored.OrderId, stored.Revision), "commit", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        Assert.Equivalent(committed.Order, await fixture.NewStore().FindAsync(fixture.Requester, stored.OrderId, default));
    }

    [Fact]
    public async Task RequiredApprovalDeniesDirectCommitAndFreezesIndependentExactDecisionWithReplay()
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var orders = fixture.Orders; var draft = fixture.Draft;
        var before = await CountReceiptsAsync(draft.TenantId);
        Assert.Equal(CommitOrderDraftStatus.ApprovalRequired, (await new CommitOrderDraft(orders).ExecuteAsync(
            fixture.Requester, new(draft.OrderId, 1), "without-approval", default)).Status);
        Assert.Equal(before, await CountReceiptsAsync(draft.TenantId));
        var submit = new SubmitOrderApprovalRequest(draft.OrderId, 1, fixture.Approver.AccountId);
        Assert.Equal(OrderApprovalStatus.SelfApprovalForbidden, (await orders.SubmitApprovalAsync(fixture.Requester,
            submit with { AssignedAccountId = fixture.Requester.AccountId }, "self", default)).Status);
        var submitted = (await orders.SubmitApprovalAsync(fixture.Requester, submit, "submit", default)).Request!;
        Assert.Equal(OrderApprovalStatus.Replayed, (await orders.SubmitApprovalAsync(fixture.Requester, submit, "submit", default)).Status);
        var decide = new DecideOrderApprovalRequest(submitted.RequestId, 1, OrderApprovalDecision.Approve);
        Assert.Equal(OrderApprovalStatus.SelfApprovalForbidden, (await orders.DecideApprovalAsync(fixture.Requester, decide, "self-decision", default)).Status);
        var approved = await orders.DecideApprovalAsync(fixture.Approver, decide, "decide", default);
        Assert.Equal(OrderApprovalStatus.Applied, approved.Status);
        Assert.Equal(OrderApprovalState.Approved, approved.Request!.State);
        Assert.Equal(OrderApprovalStatus.Replayed, (await orders.DecideApprovalAsync(fixture.Approver, decide, "decide", default)).Status);
        Assert.Equal(OrderApprovalStatus.IdempotencyKeyConflict, (await orders.DecideApprovalAsync(fixture.Approver,
            decide with { Decision = OrderApprovalDecision.Reject, Reason = "Changed decision" }, "decide", default)).Status);
        fixture.Authority.RevokedDecision.TryAdd(fixture.Approver.AccountId, true);
        Assert.Equal(OrderApprovalStatus.Forbidden, (await orders.DecideApprovalAsync(fixture.Approver, decide, "decide", default)).Status);
        var committed = await new CommitOrderDraft(orders).ExecuteAsync(fixture.Requester, new(draft.OrderId, 1), "commit", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        var evidence = Assert.IsType<OrderApprovalEvidence>(committed.Order!.ApprovalEvidence);
        Assert.Equal(submitted.RequestId, evidence.RequestId);
        Assert.Equal(1, evidence.OrderRevision);
        Assert.Equal(fixture.Approver.AccountId, evidence.ApprovedByAccountId);
        var restarted = fixture.NewStore();
        Assert.Equivalent(committed.Order, await restarted.FindAsync(fixture.Requester, draft.OrderId, default));
        Assert.Equal(CommitOrderDraftStatus.Replayed, (await new CommitOrderDraft(restarted).ExecuteAsync(
            fixture.Requester, new(draft.OrderId, 1), "commit", default)).Status);
        Assert.Equal(before + 1, await CountReceiptsAsync(draft.TenantId));
    }

    [Fact]
    public async Task DraftRevisionInvalidatesApprovalAndStalePendingReplacementIsExplicitAtomicCas()
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var orders = fixture.Orders; var draft = fixture.Draft;
        var pending = (await orders.SubmitApprovalAsync(fixture.Requester,
            new(draft.OrderId, 1, fixture.Approver.AccountId), "submit", default)).Request!;
        var revisedIntent = CreateIntent("Changed exact draft", draft.CustomerContext);
        var revised = await orders.ReviseAsync(fixture.Requester, new(draft.OrderId, 1,
            revisedIntent.Summary, revisedIntent.CurrencyCode,
            revisedIntent.Lines.Select(line => new OrderDraftLineInput(line.Description, line.Quantity, line.UnitCode, line.UnitPrice)).ToArray(),
            revisedIntent.CustomerContext), revisedIntent, "revise", new string('a', 64), default);
        Assert.Equal(ReviseOrderDraftStatus.Revised, revised.Status);
        Assert.Equal(OrderApprovalStatus.SubjectStale, (await orders.DecideApprovalAsync(fixture.Approver,
            new(pending.RequestId, 1, OrderApprovalDecision.Approve), "stale", default)).Status);
        Assert.True(Assert.Single((await orders.ListApprovalsAsync(fixture.Approver, new(), default)).Items).SubjectStale);
        Assert.Equal(OrderApprovalStatus.RevisionConflict, (await orders.SubmitApprovalAsync(fixture.Requester,
            new(draft.OrderId, 2, fixture.Approver.AccountId), "implicit-replace", default)).Status);
        var replacement = await orders.SubmitApprovalAsync(fixture.Alternate,
            new(draft.OrderId, 2, fixture.Approver.AccountId, 1), "replace", default);
        Assert.Equal(OrderApprovalStatus.Applied, replacement.Status);
        Assert.Equal(OrderApprovalState.Superseded, (await orders.FindApprovalAsync(fixture.Requester, pending.RequestId, default))!.State);
        var next = replacement.Request!;
        Assert.Equal(2, next.Subject.Order.Revision);
        Assert.Equal(fixture.Requester.AccountId, next.Subject.RevisionAuthorAccountId);
        Assert.Equal(OrderApprovalStatus.SelfApprovalForbidden, (await orders.ReassignApprovalAsync(fixture.Owner,
            new(next.RequestId, 1, fixture.Requester.AccountId, "Cannot assign the revision author"), "author", default)).Status);
        Assert.Equal(OrderApprovalStatus.Applied, (await orders.DecideApprovalAsync(fixture.Approver,
            new(next.RequestId, 1, OrderApprovalDecision.Approve), "approve-current", default)).Status);
        Assert.Equal(CommitOrderDraftStatus.Committed, (await new CommitOrderDraft(orders).ExecuteAsync(
            fixture.Requester, new(draft.OrderId, 2), "commit-current", default)).Status);
    }

    [Fact]
    public async Task ApprovedOrderApprovalSubjectCannotCommitAfterReferenceEdit()
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var request = (await fixture.Orders.SubmitApprovalAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1, fixture.Approver.AccountId), "submit", default)).Request!;
        Assert.Equal(OrderApprovalStatus.Applied, (await fixture.Orders.DecideApprovalAsync(fixture.Approver,
            new(request.RequestId, 1, OrderApprovalDecision.Approve), "approve", default)).Status);
        var reference = await new SetOrderProgramReference(fixture.Orders).ExecuteAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1, "New reference"), "reference", default);
        Assert.Equal(SetOrderProgramReferenceStatus.Updated, reference.Status);
        var before = await CountReceiptsAsync(fixture.Draft.TenantId);
        Assert.Equal(CommitOrderDraftStatus.ApprovalStale, (await new CommitOrderDraft(fixture.Orders).ExecuteAsync(
            fixture.Requester, new(fixture.Draft.OrderId, 2), "stale-commit", default)).Status);
        Assert.Equal(before, await CountReceiptsAsync(fixture.Draft.TenantId));
    }

    [Fact]
    public async Task ConcurrentApprovalDecisionAndReassignmentHaveOneRequestRevisionWinner()
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var request = (await fixture.Orders.SubmitApprovalAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1, fixture.Approver.AccountId), "submit", default)).Request!;
        var results = await Task.WhenAll(
            fixture.Orders.DecideApprovalAsync(fixture.Approver, new(request.RequestId, 1, OrderApprovalDecision.Approve), "approve", default),
            fixture.NewStore().ReassignApprovalAsync(fixture.Owner, new(request.RequestId, 1, fixture.Alternate.AccountId, "Available alternate"), "reassign", default));
        Assert.Single(results, result => result.Status == OrderApprovalStatus.Applied);
        Assert.Single(results, result => result.Status == OrderApprovalStatus.RevisionConflict);
        var retained = (await fixture.Orders.FindApprovalAsync(fixture.Owner, request.RequestId, default))!;
        Assert.Equal(2, retained.Revision);
        Assert.Equal(2, (await fixture.Orders.ListApprovalHistoryAsync(fixture.Owner, request.RequestId, new(), default))!.Items.Count);
    }

    [Fact]
    public async Task ConcurrentApprovalAndRejectionHaveOneDurableTerminalDecision()
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var request = (await fixture.Orders.SubmitApprovalAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1, fixture.Approver.AccountId), "submit", default)).Request!;
        var results = await Task.WhenAll(
            fixture.Orders.DecideApprovalAsync(fixture.Approver, new(request.RequestId, 1, OrderApprovalDecision.Approve), "approve", default),
            fixture.NewStore().DecideApprovalAsync(fixture.Approver, new(request.RequestId, 1, OrderApprovalDecision.Reject, "Needs work"), "reject", default));
        Assert.Single(results, result => result.Status == OrderApprovalStatus.Applied);
        Assert.Single(results, result => result.Status == OrderApprovalStatus.RevisionConflict);
        Assert.Contains((await fixture.NewStore().FindApprovalAsync(fixture.Requester, request.RequestId, default))!.State,
            new[] { OrderApprovalState.Approved, OrderApprovalState.Rejected });
    }

    [Fact]
    public async Task PendingApprovalWorkSurvivesRestartAndUnavailableAssigneeHasExplicitRecoveryAndConfidentialFilters()
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var pending = (await fixture.Orders.SubmitApprovalAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1, fixture.Approver.AccountId), "submit", default)).Request!;
        fixture.Authority.RevokedDecision.TryAdd(fixture.Approver.AccountId, true);
        var restarted = fixture.NewStore();
        await Assert.ThrowsAsync<OrderDraftValidationException>(() => restarted.ListApprovalHistoryAsync(
            fixture.Requester, pending.RequestId, new(11), default));
        var inbox = await restarted.ListApprovalsAsync(fixture.Approver, new(), default);
        Assert.True(Assert.Single(inbox.Items).AssigneeUnavailable);
        var json = JsonSerializer.Serialize(inbox);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("Items")[0].TryGetProperty("Subject", out _));
        Assert.DoesNotContain("UnitPrice", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Reason", json, StringComparison.Ordinal);
        Assert.Empty((await restarted.ListApprovalsAsync(fixture.Alternate, new(), default)).Items);
        await Assert.ThrowsAsync<OrderApprovalAccessDeniedException>(() => restarted.FindApprovalAsync(fixture.Alternate, pending.RequestId, default));
        fixture.Authority.RecentAuthentication = false;
        Assert.Single((await restarted.ListApprovalsAsync(fixture.Owner, new(Recovery: true), default)).Items);
        Assert.Equal(OrderApprovalStatus.Forbidden, (await restarted.ReassignApprovalAsync(fixture.Owner,
            new(pending.RequestId, 1, fixture.Alternate.AccountId, "Unavailable approver"), "step-up", default)).Status);
        fixture.Authority.RecentAuthentication = true;
        Assert.Equal(OrderApprovalStatus.Applied, (await restarted.ReassignApprovalAsync(fixture.Owner,
            new(pending.RequestId, 1, fixture.Alternate.AccountId, "Unavailable approver"), "recover", default)).Status);
        Assert.Single((await fixture.NewStore().ListApprovalsAsync(fixture.Alternate, new(), default)).Items);
        fixture.Authority.RevokedView.TryAdd(fixture.Requester.AccountId, true);
        Assert.Equal(OrderApprovalStatus.Applied, (await restarted.CancelApprovalAsync(fixture.Requester,
            new(pending.RequestId, 2, "Requester cancels after view revocation"), "cancel", default)).Status);
        Assert.Empty((await restarted.ListApprovalsAsync(fixture.Owner, new(Recovery: true), default)).Items);
        Assert.Equal(OrderDraftState.Draft, (await restarted.FindAsync(fixture.Owner, fixture.Draft.OrderId, default))!.State);
    }

    [Fact]
    public async Task ApprovalReceiptFailureRollsBackDecisionAndRequestRevisionBeforeRestart()
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var request = (await fixture.Orders.SubmitApprovalAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1, fixture.Approver.AccountId), "submit", default)).Request!;
        await ExecuteOwnerSqlAsync("""
            CREATE FUNCTION orders.fail_test_approval_event() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.operation='approve' THEN RAISE EXCEPTION 'Synthetic receipt failure' USING ERRCODE='23514'; END IF; RETURN NEW; END; $$;
            CREATE TRIGGER fail_test_approval_event BEFORE INSERT ON orders.approval_events
                FOR EACH ROW EXECUTE FUNCTION orders.fail_test_approval_event();
            """);
        await Assert.ThrowsAsync<PostgresException>(() => fixture.Orders.DecideApprovalAsync(fixture.Approver,
            new(request.RequestId, 1, OrderApprovalDecision.Approve), "failure", default));
        var retained = (await fixture.NewStore().FindApprovalAsync(fixture.Requester, request.RequestId, default))!;
        Assert.Equal(1, retained.Revision);
        Assert.Equal(OrderApprovalState.Pending, retained.State);
        Assert.Single((await fixture.NewStore().ListApprovalHistoryAsync(fixture.Requester, request.RequestId, new(), default))!.Items);
        await ExecuteOwnerSqlAsync("DROP TRIGGER fail_test_approval_event ON orders.approval_events");
        Assert.Equal(OrderApprovalStatus.Applied, (await fixture.Orders.DecideApprovalAsync(fixture.Approver,
            new(request.RequestId, 1, OrderApprovalDecision.Approve), "failure", default)).Status);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task OptionalAndNonProgramOrdersStillCommitWithoutApproval(bool requiredPolicy, bool programAttributed)
    {
        await using var fixture = await CreateApprovalFixtureAsync(requiredPolicy, programAttributed);
        var committed = await new CommitOrderDraft(fixture.Orders).ExecuteAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1), "commit", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        Assert.Null(committed.Order!.ApprovalEvidence);
    }

    private async Task<ApprovalFixture> CreateApprovalFixtureAsync(bool requireApproval = true, bool programAttributed = true, TimeProvider? clock = null)
    {
        await ApplyOrderSchemaAsync();
        var tenant = Guid.NewGuid(); var requester = Guid.NewGuid(); var approver = Guid.NewGuid();
        var owner = Guid.NewGuid(); var alternate = Guid.NewGuid(); var organization = Guid.NewGuid(); var program = Guid.NewGuid();
        await SeedAuthorityRowsAsync(requester, tenant);
        foreach (var actor in new[] { approver, owner, alternate }) await SeedAdditionalAccountAsync(actor, tenant);
        await SeedCustomerContextAsync(tenant, requester, organization, program);
        var context = await ResolveContextAsync(tenant, requester);
        await using (var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build())
        {
            var profiles = new PostgresProfileStore(profileSource, new FixedTimeProvider());
            var edited = await profiles.EditPolicyAsync(context, new(0, false, requireApproval), 1, "policy-edit", default);
            Assert.Equal(ProfileCommandStatus.Edited, edited.Status);
            var policy = await profiles.PublishPolicyAsync(context, new(edited.PolicyState!.Revision), 1, "policy-publish", default);
            var publication = await profiles.PublishProfileAsync(new(Guid.NewGuid(), Guid.NewGuid()),
                new(tenant, 0, policy.PublishedPolicy!.PolicyRevisionId, 1, false), "profile-publish", default);
            var activated = await profiles.ActivateProfileAsync(new(Guid.NewGuid(), Guid.NewGuid()),
                new(tenant, publication.Authority!.Revision, publication.Profile!.ProfileId, false), "profile-activate", default);
            Assert.Equal(ProfileCommandStatus.Activated, activated.Status);
        }
        var runtime = await CreateRuntimeRoleAsync();
        await ExecuteOwnerSqlAsync("""
            GRANT INSERT(require_approval) ON orders.program_order_metadata TO application_orders_runtime;
            GRANT SELECT,INSERT ON orders.approval_requests,orders.approval_events,orders.approval_commit_evidence TO application_orders_runtime;
            GRANT UPDATE(assigned_id,revision,state,updated_at) ON orders.approval_requests TO application_orders_runtime;
            """);
        var source = new NpgsqlDataSourceBuilder(runtime).Build();
        try
        {
            var authority = new ApprovalTestAuthority(tenant, owner, [requester, approver, owner, alternate]);
            clock ??= new FixedTimeProvider();
            var orders = new PostgresOrderDraftStore(source, clock, profilePolicySource: new ApprovalProfileSource(), approvalAuthority: authority);
            var draft = (await orders.CreateAsync(context, CreateIntent("Approval program work",
                programAttributed ? new CustomerOrderContext(organization, program) : null), "create", default)).Order!;
            return new(source, orders, authority, context, await ResolveContextAsync(tenant, approver),
                await ResolveContextAsync(tenant, owner), await ResolveContextAsync(tenant, alternate), draft, clock, runtime);
        }
        catch
        {
            await source.DisposeAsync();
            throw;
        }
    }

    private sealed record ApprovalFixture(NpgsqlDataSource Source, PostgresOrderDraftStore Orders, ApprovalTestAuthority Authority,
        TenantContext Requester, TenantContext Approver, TenantContext Owner, TenantContext Alternate, OrderDraftSnapshot Draft,
        TimeProvider Clock, string RuntimeConnectionString) : IAsyncDisposable
    {
        internal PostgresOrderDraftStore NewStore() => new(Source, Clock, profilePolicySource: new ApprovalProfileSource(), approvalAuthority: Authority);
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }

    private sealed class SubMicrosecondClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero).AddTicks(7);
    }

    private sealed class ApprovalTestAuthority(Guid tenantId, Guid ownerId, HashSet<Guid> accounts) : IOrderApprovalAuthority
    {
        internal ConcurrentDictionary<Guid, bool> RevokedDecision { get; } = new();
        internal ConcurrentDictionary<Guid, bool> RevokedView { get; } = new();
        internal bool RecentAuthentication { get; set; } = true;
        public Task<OrderApprovalAuthority> ResolveAsync(TenantContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var active = context.TenantId == tenantId && accounts.Contains(context.AccountId);
            return Task.FromResult(new OrderApprovalAuthority(1, active, active && !RevokedView.ContainsKey(context.AccountId),
                active && !RevokedDecision.ContainsKey(context.AccountId), active && context.AccountId == ownerId,
                active && context.AccountId == ownerId, RecentAuthentication, active && !RevokedView.ContainsKey(context.AccountId)));
        }
        public Task<bool> IsEligibleApproverAsync(TenantContext context, Guid accountId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(context.TenantId == tenantId && accounts.Contains(accountId) &&
                !RevokedDecision.ContainsKey(accountId) && !RevokedView.ContainsKey(accountId));
        }
    }

    private sealed class ApprovalProfileSource : IOrderProfilePolicySource
    {
        public async Task<OrderProgramPolicyFacts?> ResolveActiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            Guid tenantId, CancellationToken cancellationToken) => Facts(await PostgresProfileStore.ResolveActiveForOrderAsync(connection, transaction, tenantId, cancellationToken));
        public async Task<OrderProgramPolicyFacts?> ResolveLegacyBaselineAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            Guid tenantId, CancellationToken cancellationToken) => Facts(await PostgresProfileStore.ResolveLegacyBaselineForOrderAsync(connection, transaction, tenantId, cancellationToken));
        public async Task<bool> IsRetainedCompatibleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            Guid tenantId, OrderProgramPolicyFacts policy, CancellationToken cancellationToken) =>
            Facts(await PostgresProfileStore.ResolveRetainedForOrderAsync(connection, transaction, tenantId, policy.ProfileId, cancellationToken)) == policy;
        private static OrderProgramPolicyFacts? Facts(TenantProfileSnapshot? profile) => profile is null ? null :
            new(profile.ProfileId, profile.Policy.PolicyRevisionId, profile.Policy.RequireReferenceForProgramOrders, profile.Policy.RequireApprovalForProgramOrders);
    }
}
