using Application.Orders;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
    [Fact]
    public async Task ApprovalRequestsEventsAndCommitEvidenceRemainTenantIsolatedAfterCommit()
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var submitted = await fixture.Orders.SubmitApprovalAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1, fixture.Approver.AccountId), "isolation-submit", default);
        Assert.Equal(OrderApprovalStatus.Applied, submitted.Status);
        var approved = await fixture.Orders.DecideApprovalAsync(fixture.Approver,
            new(submitted.Request!.RequestId, 1, OrderApprovalDecision.Approve), "isolation-approve", default);
        Assert.Equal(OrderApprovalStatus.Applied, approved.Status);
        var committed = await new CommitOrderDraft(fixture.Orders).ExecuteAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1), "isolation-commit", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);

        var absentTenantTables = new[]
        {
            "orders.approval_requests", "orders.approval_events", "orders.approval_commit_evidence",
        };
        var wrongTenant = Guid.NewGuid();
        await using var runtime = await fixture.Source.OpenConnectionAsync();
        await ResetTenantContextAsync(runtime);
        foreach (var table in absentTenantTables)
            Assert.Equal(0L, await CountApprovalRowsAsync(runtime, table));

        await SetTenantContextAsync(runtime, wrongTenant);
        foreach (var table in absentTenantTables)
            Assert.Equal(0L, await CountApprovalRowsAsync(runtime, table));

        await SetTenantContextAsync(runtime, fixture.Draft.TenantId);
        Assert.True(await CountApprovalRowsAsync(runtime, "orders.approval_requests") > 0);
        Assert.True(await CountApprovalRowsAsync(runtime, "orders.approval_events") > 0);
        Assert.True(await CountApprovalRowsAsync(runtime, "orders.approval_commit_evidence") > 0);

        var crossTenantCopies = new[]
        {
            """
            INSERT INTO orders.approval_requests
                (tenant_id, request_id, order_id, requester_id, assigned_id, revision_author_id,
                 order_revision, subject, subject_fingerprint, revision, state, created_at, updated_at)
            SELECT @wrong_tenant, @new_request, order_id, requester_id, assigned_id, revision_author_id,
                   order_revision, subject, subject_fingerprint, 1, 'pending', created_at, updated_at
            FROM orders.approval_requests WHERE tenant_id = @tenant LIMIT 1
            """,
            """
            INSERT INTO orders.approval_events
                (tenant_id, request_id, request_revision, actor_id, authorization_revision, operation,
                 idempotency_key, fingerprint, response_json, recorded_at)
            SELECT @wrong_tenant, request_id, request_revision, actor_id, authorization_revision, operation,
                   'cross-tenant-copy', fingerprint, response_json, recorded_at
            FROM orders.approval_events WHERE tenant_id = @tenant LIMIT 1
            """,
            """
            INSERT INTO orders.approval_commit_evidence
                (tenant_id, order_id, request_id, request_revision, order_revision,
                 subject_fingerprint, response_json)
            SELECT @wrong_tenant, order_id, request_id, request_revision, order_revision,
                   subject_fingerprint, response_json
            FROM orders.approval_commit_evidence WHERE tenant_id = @tenant LIMIT 1
            """,
        };

        foreach (var sql in crossTenantCopies)
        {
            await using var copy = new NpgsqlCommand(sql, runtime);
            copy.Parameters.AddWithValue("tenant", fixture.Draft.TenantId);
            copy.Parameters.AddWithValue("wrong_tenant", wrongTenant);
            copy.Parameters.AddWithValue("new_request", Guid.NewGuid());
            await Assert.ThrowsAsync<PostgresException>(() => copy.ExecuteNonQueryAsync());
        }

        await SetTenantContextAsync(runtime, wrongTenant);
        foreach (var table in absentTenantTables)
            Assert.Equal(0L, await CountApprovalRowsAsync(runtime, table));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApprovalSubmissionWaitingOnOrderLockRollsBackAndRetriesAfterCallerCancelOrBackendLoss(
        bool terminateBlockedBackend)
    {
        await using var fixture = await CreateApprovalFixtureAsync();
        var applicationName = $"approval-submit-{(terminateBlockedBackend ? "terminated" : "canceled")}-{Guid.NewGuid():N}";
        await using var waitingSource = CommercialSource(fixture.RuntimeConnectionString, applicationName, 1);
        var waitingStore = new PostgresOrderDraftStore(waitingSource, fixture.Clock,
            profilePolicySource: new ApprovalProfileSource(), approvalAuthority: fixture.Authority);
        var key = "blocked-approval-submit";

        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var heldOrder = await admin.BeginTransactionAsync();
        await using (var lockOrder = new NpgsqlCommand("""
            SELECT id FROM orders.order_drafts WHERE tenant_id = @tenant AND id = @order FOR UPDATE
            """, admin, heldOrder))
        {
            lockOrder.Parameters.AddWithValue("tenant", fixture.Draft.TenantId);
            lockOrder.Parameters.AddWithValue("order", fixture.Draft.OrderId);
            Assert.Equal(fixture.Draft.OrderId, (Guid)(await lockOrder.ExecuteScalarAsync())!);
        }

        using var cancel = new CancellationTokenSource();
        var submission = waitingStore.SubmitApprovalAsync(fixture.Requester,
            new(fixture.Draft.OrderId, 1, fixture.Approver.AccountId), key, cancel.Token);
        try
        {
            await WaitForRelationLockAsync(applicationName, "orders.order_drafts", "RowShareLock", granted: true);
            await WaitForApprovalOrderRowWaitAsync(applicationName);
            Assert.False(submission.IsCompleted);

            if (terminateBlockedBackend)
            {
                var terminated = await TerminateBlockedApprovalBackendAsync(applicationName);
                Assert.True(terminated);
                await Assert.ThrowsAnyAsync<NpgsqlException>(() => submission);
            }
            else
            {
                cancel.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => submission);
            }
        }
        finally
        {
            cancel.Cancel();
            await heldOrder.RollbackAsync();
            try { await submission.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception exception) when (exception is OperationCanceledException or NpgsqlException) { }
        }

        Assert.Equal(0, await CountRowsAsync(
            "SELECT count(*) FROM orders.approval_requests WHERE tenant_id = @tenant_id", fixture.Draft.TenantId));
        Assert.Equal(0, await CountRowsAsync(
            "SELECT count(*) FROM orders.approval_events WHERE tenant_id = @tenant_id", fixture.Draft.TenantId));

        var retry = await new PostgresOrderDraftStore(fixture.Source, fixture.Clock,
                profilePolicySource: new ApprovalProfileSource(), approvalAuthority: fixture.Authority)
            .SubmitApprovalAsync(fixture.Requester,
                new(fixture.Draft.OrderId, 1, fixture.Approver.AccountId), key, default);
        Assert.Equal(OrderApprovalStatus.Applied, retry.Status);
        Assert.Equal(1, await CountRowsAsync(
            "SELECT count(*) FROM orders.approval_requests WHERE tenant_id = @tenant_id", fixture.Draft.TenantId));
        Assert.Equal(1, await CountRowsAsync(
            "SELECT count(*) FROM orders.approval_events WHERE tenant_id = @tenant_id", fixture.Draft.TenantId));
    }

    private static async Task<long> CountApprovalRowsAsync(NpgsqlConnection connection, string table)
    {
        await using var count = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection);
        return (long)(await count.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Approval row count returned no value."));
    }

    private async Task WaitForApprovalOrderRowWaitAsync(string applicationName)
    {
        await using var monitor = new NpgsqlConnection(ConnectionString);
        await monitor.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var probe = new NpgsqlCommand("""
                SELECT EXISTS(
                    SELECT 1 FROM pg_locks l
                    JOIN pg_stat_activity activity ON activity.pid = l.pid
                    WHERE activity.application_name = @name
                      AND activity.wait_event_type = 'Lock'
                      AND l.locktype = 'transactionid'
                      AND NOT l.granted)
                """, monitor);
            probe.Parameters.AddWithValue("name", applicationName);
            if ((bool)(await probe.ExecuteScalarAsync(timeout.Token))!) return;
            await Task.Delay(10, timeout.Token);
        }
    }

    private async Task<bool> TerminateBlockedApprovalBackendAsync(string applicationName)
    {
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var terminate = new NpgsqlCommand("""
            SELECT pg_terminate_backend(activity.pid)
            FROM pg_stat_activity activity
            WHERE activity.application_name = @name
              AND activity.wait_event_type = 'Lock'
              AND activity.backend_type = 'client backend'
            """, admin);
        terminate.Parameters.AddWithValue("name", applicationName);
        return (bool)(await terminate.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Named approval backend was not found."));
    }
}
