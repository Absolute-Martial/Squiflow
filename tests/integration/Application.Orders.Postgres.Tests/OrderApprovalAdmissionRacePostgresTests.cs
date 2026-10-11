using Application.Orders;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
    [Fact]
    public async Task ConcurrentApprovalAndRevisionNeverAdmitAMixedSubject()
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var request = (await fixture.Orders.SubmitApprovalAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1, fixture.Approver.AccountId), "submit", default)).Request!;
        var revisedIntent = CreateIntent("Changed approval subject", fixture.Draft.CustomerContext);

        var deciding = fixture.Orders.DecideApprovalAsync(fixture.Approver,
            new(request.RequestId, 1, OrderApprovalDecision.Approve), "approve", default);
        var revising = fixture.NewStore().ReviseAsync(fixture.Requester, new(fixture.Draft.OrderId, 1,
                revisedIntent.Summary, revisedIntent.CurrencyCode,
                revisedIntent.Lines.Select(line => new OrderDraftLineInput(line.Description, line.Quantity, line.UnitCode, line.UnitPrice)).ToArray(),
                revisedIntent.CustomerContext), revisedIntent, "revise", new string('a', 64), default);
        await Task.WhenAll(deciding, revising);

        var decision = await deciding;
        var revision = await revising;
        Assert.Equal(ReviseOrderDraftStatus.Revised, revision.Status);
        Assert.Contains(decision.Status, new[] { OrderApprovalStatus.Applied, OrderApprovalStatus.SubjectStale });
        var retained = (await fixture.NewStore().FindApprovalAsync(fixture.Requester, request.RequestId, default))!;
        Assert.Equal(2, revision.Order!.Revision);
        if (decision.Status == OrderApprovalStatus.Applied)
        {
            Assert.Equal(OrderApprovalState.Approved, retained.State);
            Assert.Equal(1, retained.Subject.Order.Revision);
            Assert.Equal(CommitOrderDraftStatus.ApprovalStale, (await new CommitOrderDraft(fixture.NewStore()).ExecuteAsync(
                fixture.Requester, new(fixture.Draft.OrderId, 2), "mixed-subject-commit", default)).Status);
        }
        else
        {
            Assert.Equal(OrderApprovalState.Pending, retained.State);
            Assert.Equal(1, retained.Subject.Order.Revision);
            Assert.Equal(CommitOrderDraftStatus.ApprovalRequired, (await new CommitOrderDraft(fixture.NewStore()).ExecuteAsync(
                fixture.Requester, new(fixture.Draft.OrderId, 2), "mixed-subject-commit", default)).Status);
        }
    }

    [Fact]
    public async Task CommitApprovalEvidenceInsertFailureRollsBackOrderAndReceiptAndAllowsSameKeyRetry()
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var request = (await fixture.Orders.SubmitApprovalAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1, fixture.Approver.AccountId), "submit", default)).Request!;
        Assert.Equal(OrderApprovalStatus.Applied, (await fixture.Orders.DecideApprovalAsync(fixture.Approver,
            new(request.RequestId, 1, OrderApprovalDecision.Approve), "approve", default)).Status);
        var receiptsBefore = await CountReceiptsAsync(fixture.Draft.TenantId);
        await ExecuteOwnerSqlAsync("""
            CREATE FUNCTION orders.fail_test_approval_commit_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Synthetic approval evidence failure' USING ERRCODE='23514'; END; $$;
            CREATE TRIGGER fail_test_approval_commit_evidence BEFORE INSERT ON orders.approval_commit_evidence
                FOR EACH ROW EXECUTE FUNCTION orders.fail_test_approval_commit_evidence();
            """);

        var command = new CommitOrderDraft(fixture.Orders);
        var failure = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => command.ExecuteAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1), "commit-after-evidence-failure", default));
        Assert.Equal("23514", failure.SqlState);
        var retainedOrder = (await fixture.NewStore().FindAsync(fixture.Requester, fixture.Draft.OrderId, default))!;
        Assert.Equal(OrderDraftState.Draft, retainedOrder.State);
        Assert.Equal(1, retainedOrder.Revision);
        Assert.Equal(OrderApprovalState.Approved,
            (await fixture.NewStore().FindApprovalAsync(fixture.Requester, request.RequestId, default))!.State);
        Assert.Equal(receiptsBefore, await CountReceiptsAsync(fixture.Draft.TenantId));
        Assert.Equal(0, await CountRowsAsync("SELECT count(*) FROM orders.approval_commit_evidence WHERE tenant_id = @tenant_id",
            fixture.Draft.TenantId));

        await ExecuteOwnerSqlAsync("""
            DROP TRIGGER fail_test_approval_commit_evidence ON orders.approval_commit_evidence;
            DROP FUNCTION orders.fail_test_approval_commit_evidence();
            """);
        var retried = await new CommitOrderDraft(fixture.NewStore()).ExecuteAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1), "commit-after-evidence-failure", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, retried.Status);
        Assert.NotNull(retried.Order!.ApprovalEvidence);
        Assert.Equal(receiptsBefore + 1, await CountReceiptsAsync(fixture.Draft.TenantId));
        Assert.Equal(1, await CountRowsAsync("SELECT count(*) FROM orders.approval_commit_evidence WHERE tenant_id = @tenant_id",
            fixture.Draft.TenantId));
    }
}
