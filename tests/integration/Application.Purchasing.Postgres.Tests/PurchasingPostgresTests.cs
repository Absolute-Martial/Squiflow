using System.Text;
using System.Text.Json;
using Application.Purchasing.Postgres;
using Application.Tenancy;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.Purchasing.Postgres.Tests;

public sealed class PurchasingPostgresTests : IAsyncLifetime
{
    private const string RuntimeRole = "purchasing_runtime";
    private const string RuntimePassword = "local-purchasing-runtime-only";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
        .WithDatabase("purchasing_tests").WithUsername("postgres").WithPassword("local-purchasing-owner-only").Build();

    private string ConnectionString => _database.GetConnectionString();

    [Fact]
    public async Task InformalRequestPartialReceiptsPaymentsRefundCorrectionAndCancellationRetainIndependentPayables()
    {
        await using var fixture = await CreateFixtureAsync();
        var request = await RequestAsync(fixture);
        Assert.Null(request.Order);
        var received = await ApplyAsync(fixture, new(PurchasingCommandKind.Receive, request.Revision, Quantity: 1.25m, Amount: 250m));
        var receipt = Assert.Single(received.Receipts);
        var paid = await ApplyAsync(fixture, new(PurchasingCommandKind.Pay, received.Revision, Amount: 200m,
            ReceiptId: receipt.EntryId, Method: SupplierPaymentMethod.Bank, Reference: "bank slip"));
        var payment = Assert.Single(paid.Payments);
        var denied = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            new(PurchasingCommandKind.Correct, paid.Revision, TargetEventId: receipt.EffectiveEventId, Reason: "actual cost corrected",
                Replacement: new(PurchasingEventKind.Receipt, Quantity: 1m, Amount: 150m)), "unrefunded-correction", default);
        Assert.Equal(PurchasingMutationStatus.CorrectionHasDependents, denied.Rejection);
        var refunded = await ApplyAsync(fixture, new(PurchasingCommandKind.Refund, paid.Revision, Amount: 50m,
            PaymentId: payment.EntryId, Method: SupplierPaymentMethod.Cash, Reference: "actual refund"));
        var corrected = await ApplyAsync(fixture, new(PurchasingCommandKind.Correct, refunded.Revision,
            TargetEventId: receipt.EffectiveEventId, Reason: "actual cost corrected",
            Replacement: new(PurchasingEventKind.Receipt, Quantity: 1m, Amount: 150m)));
        var cancelled = await ApplyAsync(fixture, new(PurchasingCommandKind.Cancel, corrected.Revision, Reason: "remaining service cancelled"));
        Assert.Equal(2m, cancelled.CancelledQuantity);
        Assert.Equal(0m, cancelled.RemainingQuantity);
        var late = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            new(PurchasingCommandKind.Receive, cancelled.Revision, Quantity: 1m, Amount: 1m), "late", default);
        Assert.Equal(PurchasingMutationStatus.RequestCancelled, late.Rejection);
        var balance = await fixture.Store.SupplierBalanceAsync(fixture.Actor, fixture.SupplierId, default);
        Assert.Equal(new SupplierBalance(fixture.SupplierId, "NPR", 150m, 200m, 50m, 0m), balance);
        var history = (await fixture.Store.HistoryAsync(fixture.Actor, fixture.PurchaseId, new(), default))!;
        Assert.Equal(6, history.Items.Count);
        Assert.Contains(history.Items, item => item.Kind == PurchasingEventKind.Receipt && item.Facts!.Amount == 250m);
        Assert.Contains(history.Items, item => item.Kind == PurchasingEventKind.Correction && item.TargetEventId == receipt.EntryId);
        var json = JsonSerializer.Serialize(new { cancelled, history, balance }, JsonOptions);
        Assert.DoesNotContain("sellingPrice", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customerBalance", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinancialCorrectionsAndLinkedVoidsPreserveOriginalFactsAndValidateTheirCurrentDependents()
    {
        await using var fixture = await CreateFixtureAsync();
        await RequestAsync(fixture);
        var received = await ApplyAsync(fixture, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m));
        var paid = await ApplyAsync(fixture, new(PurchasingCommandKind.Pay, 2, Amount: 10m,
            ReceiptId: received.Receipts[0].EntryId, Method: SupplierPaymentMethod.Cash, Reference: "original payment"));
        var refunded = await ApplyAsync(fixture, new(PurchasingCommandKind.Refund, 3, Amount: 2m,
            PaymentId: paid.Payments[0].EntryId, Method: SupplierPaymentMethod.Cash, Reference: "original refund"));
        var refundCorrected = await ApplyAsync(fixture, new(PurchasingCommandKind.Correct, 4,
            TargetEventId: refunded.Refunds[0].EffectiveEventId, Reason: "correct refund amount and method",
            Replacement: new(PurchasingEventKind.Refund, Amount: 3m, PaymentId: paid.Payments[0].EntryId, Method: SupplierPaymentMethod.Bank, Reference: "refund correction")));
        var paymentCorrected = await ApplyAsync(fixture, new(PurchasingCommandKind.Correct, 5,
            TargetEventId: paid.Payments[0].EffectiveEventId, Reason: "correct payment amount and method",
            Replacement: new(PurchasingEventKind.Payment, Amount: 8m, ReceiptId: received.Receipts[0].EntryId, Method: SupplierPaymentMethod.Bank, Reference: "payment correction")));
        Assert.Equal(5m, paymentCorrected.OutstandingPayable);
        var cannotVoidPayment = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            new(PurchasingCommandKind.Correct, 6, TargetEventId: paymentCorrected.Payments[0].EffectiveEventId, Reason: "void mistaken payment"), "dependent-void", default);
        Assert.Equal(PurchasingMutationStatus.CorrectionHasDependents, cannotVoidPayment.Rejection);
        var refundVoided = await ApplyAsync(fixture, new(PurchasingCommandKind.Correct, 6,
            TargetEventId: refundCorrected.Refunds[0].EffectiveEventId, Reason: "refund was not actually received"));
        var paymentVoided = await ApplyAsync(fixture, new(PurchasingCommandKind.Correct, 7,
            TargetEventId: paymentCorrected.Payments[0].EffectiveEventId, Reason: "payment was not actually sent"));
        var receiptVoided = await ApplyAsync(fixture, new(PurchasingCommandKind.Correct, 8,
            TargetEventId: received.Receipts[0].EffectiveEventId, Reason: "receipt was a duplicate entry"));
        Assert.True(Assert.Single(refundVoided.Refunds).IsVoided);
        Assert.True(Assert.Single(paymentVoided.Payments).IsVoided);
        Assert.True(Assert.Single(receiptVoided.Receipts).IsVoided);
        Assert.Equal(0m, receiptVoided.TotalCost);
        Assert.Equal(0m, receiptVoided.OutstandingPayable);
        Assert.Equal(new SupplierBalance(fixture.SupplierId, "NPR", 0m, 0m, 0m, 0m), await fixture.Store.SupplierBalanceAsync(fixture.Actor, fixture.SupplierId, default));
        var history = (await fixture.NewStore().HistoryAsync(fixture.Actor, fixture.PurchaseId, new(), default))!;
        Assert.Equal(9, history.Items.Count);
        Assert.Contains(history.Items, x => x.EventId == refunded.Refunds[0].EntryId && x.Facts!.Amount == 2m && x.Facts.Method == SupplierPaymentMethod.Cash);
        Assert.Contains(history.Items, x => x.EventId == paid.Payments[0].EntryId && x.Facts!.Amount == 10m && x.Facts.Reference == "original payment");
    }

    [Fact]
    public async Task OriginalResultReplaysAfterLaterFactsAndSourceRetirementWithoutResolvingNewSource()
    {
        await using var fixture = await CreateFixtureAsync();
        var linked = new PurchasingOrderLink(Guid.NewGuid(), 2, 9);
        fixture.Source.Facts = fixture.Source.Facts! with { Order = new(fixture.TenantId, linked.OrderId, 2, 9, true) };
        var mutation = RequestMutation(fixture) with { Order = linked, Description = "  Phone printing  ", Quantity = 3.00m };
        var initial = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, mutation, "original", default);
        Assert.Equal(PurchasingStoreStatus.Applied, initial.Status);
        await ApplyAsync(fixture, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m));
        var retired = await fixture.Store.ExecuteSupplierAsync(fixture.Actor, fixture.SupplierId, new(SupplierCommandKind.Retire, 1), "retire", default);
        Assert.Equal(PurchasingStoreStatus.Applied, retired.Status);
        fixture.Source.Facts = null;
        await using var restartedSource = CreateRuntimeSource();
        var restarted = fixture.NewStore(restartedSource);
        var replay = await restarted.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            mutation with { Description = "Phone printing", Quantity = 3.0m }, "original", default);
        Assert.Equal(PurchasingStoreStatus.Replayed, replay.Status);
        Assert.Equal(JsonSerializer.Serialize(initial.Purchase), JsonSerializer.Serialize(replay.Purchase));
        Assert.Equal(1, replay.Purchase!.Revision);
        Assert.Equal(linked, replay.Purchase.Order);
        Assert.Equal(2, (await restarted.FindPurchaseAsync(fixture.Actor, fixture.PurchaseId, default))!.Revision);
        var conflict = await restarted.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            mutation with { Quantity = 2m }, "original", default);
        Assert.Equal(PurchasingStoreStatus.IdempotencyConflict, conflict.Status);
        fixture.Source.Facts = new(new(fixture.TenantId, fixture.UnitId, "PCS", 2, 5, true));
        var newRequest = await restarted.ExecutePurchaseAsync(fixture.Actor, Guid.NewGuid(), RequestMutation(fixture), "retired-request", default);
        Assert.Equal(PurchasingMutationStatus.SupplierRetired, newRequest.Rejection);
        await ApplyAsync(fixture, new(PurchasingCommandKind.Receive, 2, Quantity: 1m, Amount: 10m));
    }

    [Fact]
    public async Task ConcurrentReceiptsAndPaymentsHaveOneRevisionWinnerAndNeverOverAllocate()
    {
        await using var fixture = await CreateFixtureAsync();
        var request = await RequestAsync(fixture);
        var receipts = await Task.WhenAll(
            fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, new(PurchasingCommandKind.Receive, request.Revision, Quantity: 2m, Amount: 100m), "receive-a", default),
            fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, new(PurchasingCommandKind.Receive, request.Revision, Quantity: 2m, Amount: 100m), "receive-b", default));
        Assert.Single(receipts, x => x.Status == PurchasingStoreStatus.Applied);
        Assert.Single(receipts, x => x.Rejection == PurchasingMutationStatus.RevisionConflict);
        var current = (await fixture.Store.FindPurchaseAsync(fixture.Actor, fixture.PurchaseId, default))!;
        var receipt = Assert.Single(current.Receipts);
        var payments = await Task.WhenAll(
            fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, new(PurchasingCommandKind.Pay, current.Revision, Amount: 75m, ReceiptId: receipt.EntryId, Method: SupplierPaymentMethod.Cash), "pay-a", default),
            fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, new(PurchasingCommandKind.Pay, current.Revision, Amount: 75m, ReceiptId: receipt.EntryId, Method: SupplierPaymentMethod.Bank), "pay-b", default));
        Assert.Single(payments, x => x.Status == PurchasingStoreStatus.Applied);
        Assert.Single(payments, x => x.Rejection == PurchasingMutationStatus.RevisionConflict);
        var final = (await fixture.Store.FindPurchaseAsync(fixture.Actor, fixture.PurchaseId, default))!;
        var overpaid = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            new(PurchasingCommandKind.Pay, final.Revision, Amount: 26m, ReceiptId: receipt.EntryId, Method: SupplierPaymentMethod.Cash), "overpay", default);
        Assert.Equal(PurchasingMutationStatus.OverPayment, overpaid.Rejection);
        Assert.Equal(75m, final.PaidAmount);
        Assert.Equal(3, await CountAsync("purchase_events", fixture.TenantId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameActorOperationAndKeyAcrossAggregatesHasOneWinner(bool supplier)
    {
        await using var fixture = await CreateFixtureAsync();
        if (supplier)
        {
            var results = await Task.WhenAll(
                fixture.Store.ExecuteSupplierAsync(fixture.Actor, Guid.NewGuid(), new(SupplierCommandKind.Create, 0, "First"), "cross-supplier", default),
                fixture.NewStore().ExecuteSupplierAsync(fixture.Actor, Guid.NewGuid(), new(SupplierCommandKind.Create, 0, "Second"), "cross-supplier", default));
            Assert.Single(results, x => x.Status == PurchasingStoreStatus.Applied);
            Assert.Single(results, x => x.Status == PurchasingStoreStatus.IdempotencyConflict);
            Assert.Equal(2, await CountAsync("suppliers", fixture.TenantId));
        }
        else
        {
            var results = await Task.WhenAll(
                fixture.Store.ExecutePurchaseAsync(fixture.Actor, Guid.NewGuid(), RequestMutation(fixture), "cross-purchase", default),
                fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, Guid.NewGuid(), RequestMutation(fixture), "cross-purchase", default));
            Assert.Single(results, x => x.Status == PurchasingStoreStatus.Applied);
            Assert.Single(results, x => x.Status == PurchasingStoreStatus.IdempotencyConflict);
            Assert.Equal(1, await CountAsync("purchases", fixture.TenantId));
        }
    }

    [Fact]
    public async Task RevokedAuthorityBlocksReadsAndReplayAndFreshAdmissionBeforeEffect()
    {
        await using var fixture = await CreateFixtureAsync();
        await RequestAsync(fixture, "accepted");
        fixture.Authority.Current = NoAuthority;
        var replay = await fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, RequestMutation(fixture), "accepted", default);
        Assert.Equal(PurchasingStoreStatus.Forbidden, replay.Status);
        await Assert.ThrowsAsync<PurchasingAccessDeniedException>(() => fixture.Store.FindPurchaseAsync(fixture.Actor, fixture.PurchaseId, default));
        fixture.Authority.Current = FullAuthority;
        fixture.Authority.AfterFirstRead = NoAuthority;
        var revokedAfterLock = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m), "revoked-under-lock", default);
        Assert.Equal(PurchasingStoreStatus.Forbidden, revokedAfterLock.Status);
        Assert.Equal(1, await CountAsync("purchase_events", fixture.TenantId));
        Assert.Equal(1, await CountAsync("purchase_command_receipts", fixture.TenantId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CorrectionRequiresIndependentRightCurrentOwnerAndRecentAuthentication(int missing)
    {
        await using var fixture = await CreateFixtureAsync();
        await RequestAsync(fixture);
        var received = await ApplyAsync(fixture, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m));
        fixture.Authority.Current = missing switch
        {
            0 => FullAuthority with { CanCorrect = false },
            1 => FullAuthority with { IsCurrentInitialOwner = false },
            _ => FullAuthority with { HasRecentAuthentication = false },
        };
        var result = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            new(PurchasingCommandKind.Correct, received.Revision, TargetEventId: received.Receipts[0].EntryId, Reason: "void mistaken receipt"), "correct", default);
        Assert.Equal(PurchasingStoreStatus.Forbidden, result.Status);
        Assert.Equal(2, await CountAsync("purchase_events", fixture.TenantId));
    }

    [Fact]
    public async Task ExactQuantityAndMoneyBoundsAndDependenciesRejectWithoutPartialEffects()
    {
        await using var fixture = await CreateFixtureAsync();
        await RequestAsync(fixture);
        foreach (var (quantity, cost, rejection) in new[]
        {
            (0m, 1m, PurchasingMutationStatus.QuantityInvalid), (1.001m, 1m, PurchasingMutationStatus.QuantityInvalid),
            (4m, 1m, PurchasingMutationStatus.OverReceipt), (1m, 0m, PurchasingMutationStatus.AmountInvalid),
            (1m, 1.001m, PurchasingMutationStatus.AmountInvalid), (1m, PurchasingRules.MaximumAmount + 0.01m, PurchasingMutationStatus.AmountInvalid),
        })
        {
            var result = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
                new(PurchasingCommandKind.Receive, 1, Quantity: quantity, Amount: cost), Guid.NewGuid().ToString("N"), default);
            Assert.Equal(rejection, result.Rejection);
        }
        var received = await ApplyAsync(fixture, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m));
        var paid = await ApplyAsync(fixture, new(PurchasingCommandKind.Pay, 2, Amount: 10m, ReceiptId: received.Receipts[0].EntryId, Method: SupplierPaymentMethod.Cash));
        var refund = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            new(PurchasingCommandKind.Refund, 3, Amount: 10.01m, PaymentId: paid.Payments[0].EntryId, Method: SupplierPaymentMethod.Cash), "overrefund", default);
        Assert.Equal(PurchasingMutationStatus.OverRefund, refund.Rejection);
        var missing = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            new(PurchasingCommandKind.Pay, 3, Amount: 1m, ReceiptId: Guid.NewGuid(), Method: SupplierPaymentMethod.Cash), "missing-payable", default);
        Assert.Equal(PurchasingMutationStatus.DependencyInvalid, missing.Rejection);
        var voidPaidReceipt = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            new(PurchasingCommandKind.Correct, 3, TargetEventId: received.Receipts[0].EntryId, Reason: "mistaken entry"), "void-dependent", default);
        Assert.Equal(PurchasingMutationStatus.CorrectionHasDependents, voidPaidReceipt.Rejection);
        Assert.Equal(3, await CountAsync("purchase_events", fixture.TenantId));
        Assert.Equal(3, await CountAsync("purchase_command_receipts", fixture.TenantId));
    }

    [Theory]
    [InlineData("supplier_events", true)]
    [InlineData("supplier_command_receipts", true)]
    [InlineData("purchase_events", false)]
    [InlineData("purchase_command_receipts", false)]
    public async Task EvidenceFailureRollsBackWholeCommandAndSameKeyRetryCreatesOneTransition(string table, bool supplier)
    {
        await using var fixture = await CreateFixtureAsync();
        if (!supplier) await RequestAsync(fixture);
        var target = supplier ? fixture.SupplierId : fixture.PurchaseId;
        var counts = await CountsAsync(fixture.TenantId);
        await ExecuteOwnerSqlAsync($"""
            CREATE FUNCTION purchasing.reject_test_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Synthetic evidence failure' USING ERRCODE='23514'; END; $$;
            CREATE TRIGGER reject_test_evidence BEFORE INSERT ON purchasing.{table} FOR EACH ROW EXECUTE FUNCTION purchasing.reject_test_evidence();
            """);
        try
        {
            var error = supplier
                ? await Assert.ThrowsAsync<PostgresException>(() => fixture.Store.ExecuteSupplierAsync(fixture.Actor, target, new(SupplierCommandKind.Revise, 1, "Revised supplier"), "retry-evidence", default))
                : await Assert.ThrowsAsync<PostgresException>(() => fixture.Store.ExecutePurchaseAsync(fixture.Actor, target, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m), "retry-evidence", default));
            Assert.Equal("23514", error.SqlState);
            Assert.Equal(counts, await CountsAsync(fixture.TenantId));
            Assert.Equal(1, supplier ? (await fixture.Store.FindSupplierAsync(fixture.Actor, target, default))!.Revision : (await fixture.Store.FindPurchaseAsync(fixture.Actor, target, default))!.Revision);
        }
        finally { await ExecuteOwnerSqlAsync($"DROP TRIGGER reject_test_evidence ON purchasing.{table}; DROP FUNCTION purchasing.reject_test_evidence();"); }
        if (supplier)
        {
            Assert.Equal(PurchasingStoreStatus.Applied, (await fixture.NewStore().ExecuteSupplierAsync(fixture.Actor, target, new(SupplierCommandKind.Revise, 1, "Revised supplier"), "retry-evidence", default)).Status);
            Assert.Equal(PurchasingStoreStatus.Replayed, (await fixture.NewStore().ExecuteSupplierAsync(fixture.Actor, target, new(SupplierCommandKind.Revise, 1, "Revised supplier"), "retry-evidence", default)).Status);
        }
        else
        {
            Assert.Equal(PurchasingStoreStatus.Applied, (await fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, target, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m), "retry-evidence", default)).Status);
            Assert.Equal(PurchasingStoreStatus.Replayed, (await fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, target, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m), "retry-evidence", default)).Status);
        }
        Assert.Equal(2, await CountAsync(table, fixture.TenantId));
    }

    [Theory]
    [InlineData("suppliers")]
    [InlineData("purchases")]
    public async Task AllowedProjectionColumnsCannotCommitWithoutTheirJournalAndCommandReceipt(string table)
    {
        await using var fixture = await CreateFixtureAsync();
        await RequestAsync(fixture);
        var before = await CountsAsync(fixture.TenantId);
        await using var runtime = CreateRuntimeSource();
        await using var connection = await runtime.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantAsync(connection, transaction, fixture.TenantId);
        await using (var command = new NpgsqlCommand($"UPDATE purchasing.{table} SET revision=revision+1,snapshot=jsonb_set(snapshot,'{{revision}}',to_jsonb(revision+1)) WHERE tenant_id=@tenant", connection, transaction))
        {
            command.Parameters.AddWithValue("tenant", fixture.TenantId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
        Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync())).SqlState);
        Assert.Equal(before, await CountsAsync(fixture.TenantId));
        Assert.Equal(1, (await fixture.NewStore().FindSupplierAsync(fixture.Actor, fixture.SupplierId, default))!.Revision);
        Assert.Equal(1, (await fixture.NewStore().FindPurchaseAsync(fixture.Actor, fixture.PurchaseId, default))!.Revision);
    }

    [Theory]
    [InlineData("supplier_events", "UPDATE", "facts=facts")]
    [InlineData("supplier_events", "DELETE", "")]
    [InlineData("supplier_command_receipts", "UPDATE", "response=response")]
    [InlineData("supplier_command_receipts", "DELETE", "")]
    [InlineData("purchase_events", "UPDATE", "facts=facts")]
    [InlineData("purchase_events", "DELETE", "")]
    [InlineData("purchase_command_receipts", "UPDATE", "response=response")]
    [InlineData("purchase_command_receipts", "DELETE", "")]
    public async Task RuntimeCannotRewriteOrDeleteRetainedEvidence(string table, string operation, string set)
    {
        await using var fixture = await CreateFixtureAsync();
        await RequestAsync(fixture);
        var counts = await CountsAsync(fixture.TenantId);
        await using var source = CreateRuntimeSource();
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantAsync(connection, transaction, fixture.TenantId);
        var sql = operation == "UPDATE" ? $"UPDATE purchasing.{table} SET {set} WHERE tenant_id=@tenant" : $"DELETE FROM purchasing.{table} WHERE tenant_id=@tenant";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("tenant", fixture.TenantId);
        Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        await transaction.RollbackAsync();
        Assert.Equal(counts, await CountsAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ForcedTenantIsolationAndScopedForeignKeysProtectAllSixTablesAndPooledConnections()
    {
        await using var fixture = await CreateFixtureAsync();
        await RequestAsync(fixture);
        await using var other = await CreateFixtureAsync();
        await RequestAsync(other);
        Assert.Null(await fixture.Store.FindPurchaseAsync(fixture.Actor, other.PurchaseId, default));
        Assert.Null(await fixture.Store.FindSupplierAsync(fixture.Actor, other.SupplierId, default));
        await using var source = CreateRuntimeSource(maxPoolSize: 1);
        await using (var connection = await source.OpenConnectionAsync())
        {
            foreach (var table in Tables) Assert.Equal(0L, await VisibleCountAsync(connection, table));
            await using var transaction = await connection.BeginTransactionAsync();
            await SetTenantAsync(connection, transaction, fixture.TenantId);
            foreach (var table in Tables) Assert.Equal(1L, await VisibleCountAsync(connection, table, transaction));
            await transaction.CommitAsync();
        }
        await using (var connection = await source.OpenConnectionAsync())
            foreach (var table in Tables) Assert.Equal(0L, await VisibleCountAsync(connection, table));
        var failure = await Assert.ThrowsAsync<PostgresException>(() => ExecuteOwnerSqlAsync("""
            ALTER TABLE purchasing.purchases DISABLE TRIGGER USER;
            UPDATE purchasing.purchases SET supplier_id=@foreign_supplier WHERE tenant_id=@tenant AND purchase_id=@purchase;
            """, ("foreign_supplier", other.SupplierId), ("tenant", fixture.TenantId), ("purchase", fixture.PurchaseId)));
        Assert.Equal("23503", failure.SqlState);
        await ExecuteOwnerSqlAsync("ALTER TABLE purchasing.purchases ENABLE TRIGGER USER;");
        await using var owner = new NpgsqlConnection(ConnectionString);
        await owner.OpenAsync();
        await using var forced = new NpgsqlCommand("SELECT bool_and(c.relrowsecurity AND c.relforcerowsecurity) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='purchasing' AND c.relkind='r' AND c.relname<>'__EFMigrationsHistory'", owner);
        Assert.True((bool)(await forced.ExecuteScalarAsync())!);
    }

    [Theory]
    [InlineData("purchases", "snapshot=jsonb_set(snapshot,'{schemaVersion}','2')")]
    [InlineData("purchases", "snapshot=snapshot-'description'")]
    [InlineData("purchases", "source=jsonb_set(source,'{unit,unitCode}','\"OTHER\"')")]
    [InlineData("purchase_events", "facts=jsonb_set(facts,'{authorizationRevision}','999')")]
    [InlineData("purchase_events", "target_event_id=event_id")]
    [InlineData("purchase_command_receipts", "response=jsonb_set(response,'{snapshot,description}','\"corrupted private text\"')")]
    [InlineData("purchase_command_receipts", "response=response||'{\"unknown\":true}'::jsonb")]
    [InlineData("purchase_command_receipts", "response=response-'schemaVersion'")]
    public async Task MalformedUnsupportedOrInconsistentRetainedFactsFailClosed(string table, string change)
    {
        await using var fixture = await CreateFixtureAsync();
        await RequestAsync(fixture, "retained");
        await ExecuteOwnerSqlAsync($"ALTER TABLE purchasing.{table} DISABLE TRIGGER USER; UPDATE purchasing.{table} SET {change} WHERE tenant_id=@tenant; ALTER TABLE purchasing.{table} ENABLE TRIGGER USER;", ("tenant", fixture.TenantId));
        var read = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.NewStore().FindPurchaseAsync(fixture.Actor, fixture.PurchaseId, default));
        Assert.DoesNotContain("corrupted private text", read.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, RequestMutation(fixture), "retained", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.NewStore().SupplierBalanceAsync(fixture.Actor, fixture.SupplierId, default));
    }

    [Fact]
    public async Task HistoryAndBrowseCursorsStayStableAndQueriesLeaveNoDurableEffects()
    {
        await using var fixture = await CreateFixtureAsync();
        await RequestAsync(fixture);
        await ApplyAsync(fixture, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m));
        var paid = await ApplyAsync(fixture, new(PurchasingCommandKind.Pay, 2, Amount: 5m,
            ReceiptId: (await fixture.Store.FindPurchaseAsync(fixture.Actor, fixture.PurchaseId, default))!.Receipts[0].EntryId, Method: SupplierPaymentMethod.Cash));
        var first = (await fixture.Store.HistoryAsync(fixture.Actor, fixture.PurchaseId, new(1), default))!;
        Assert.Equal(3, Assert.Single(first.Items).Revision);
        await ApplyAsync(fixture, new(PurchasingCommandKind.Refund, paid.Revision, Amount: 1m, PaymentId: paid.Payments[0].EntryId, Method: SupplierPaymentMethod.Cash));
        var second = (await fixture.Store.HistoryAsync(fixture.Actor, fixture.PurchaseId, new(1, first.NextRevision), default))!;
        Assert.Equal(2, Assert.Single(second.Items).Revision);
        var third = (await fixture.Store.HistoryAsync(fixture.Actor, fixture.PurchaseId, new(1, second.NextRevision), default))!;
        Assert.Equal(1, Assert.Single(third.Items).Revision);
        Assert.Null(third.NextRevision);
        var secondId = Guid.NewGuid();
        Assert.Equal(PurchasingStoreStatus.Applied, (await fixture.Store.ExecutePurchaseAsync(fixture.Actor, secondId, RequestMutation(fixture), "second", default)).Status);
        var page = await fixture.Store.ListPurchasesAsync(fixture.Actor, new(1), default);
        var next = await fixture.Store.ListPurchasesAsync(fixture.Actor, new(1, page.NextCursor), default);
        Assert.NotEqual(Assert.Single(page.Items).PurchaseId, Assert.Single(next.Items).PurchaseId);
        Assert.Null(next.NextCursor);
        var before = await CountsAsync(fixture.TenantId);
        await fixture.Store.ListSuppliersAsync(fixture.Actor, new(), default);
        await fixture.Store.SupplierHistoryAsync(fixture.Actor, fixture.SupplierId, new(), default);
        await fixture.Store.SupplierBalanceAsync(fixture.Actor, fixture.SupplierId, default);
        await fixture.Store.FindSupplierAsync(fixture.Actor, fixture.SupplierId, default);
        Assert.Equal(before, await CountsAsync(fixture.TenantId));
        await Assert.ThrowsAsync<PurchasingValidationException>(() => fixture.Store.ListPurchasesAsync(fixture.Actor, new(51), default));
        await Assert.ThrowsAsync<PurchasingValidationException>(() => fixture.Store.HistoryAsync(fixture.Actor, fixture.PurchaseId, new(11), default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackendTerminationAndCallerCancellationRollBackPendingFinancialTransition(bool callerCancellation)
    {
        await using var fixture = await CreateFixtureAsync();
        await RequestAsync(fixture);
        var before = await CountsAsync(fixture.TenantId);
        const long evidenceLock = 74355807123;
        var applicationName = "purchasing-pending-" + Guid.NewGuid().ToString("N");
        await ExecuteOwnerSqlAsync($"""
            CREATE FUNCTION purchasing.hold_test_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock({evidenceLock}); RETURN NEW; END; $$;
            CREATE TRIGGER hold_test_evidence BEFORE INSERT ON purchasing.purchase_events FOR EACH ROW EXECUTE FUNCTION purchasing.hold_test_evidence();
            """);
        await using var blocker = new NpgsqlConnection(ConnectionString);
        await blocker.OpenAsync();
        await using var blockingTransaction = await blocker.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", blocker, blockingTransaction))
        {
            hold.Parameters.AddWithValue("key", evidenceLock);
            await hold.ExecuteNonQueryAsync();
        }
        await using var runtime = CreateRuntimeSource(applicationName: applicationName);
        using var cancellation = new CancellationTokenSource();
        var mutation = new PurchasingMutation(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m);
        var pending = fixture.NewStore(runtime).ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, mutation, "interrupted-receipt", cancellation.Token);
        try
        {
            await WaitForAdvisoryWaitAsync(applicationName);
            if (callerCancellation)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name=@name AND backend_type='client backend'", blocker, blockingTransaction);
                terminate.Parameters.AddWithValue("name", applicationName);
                Assert.True((bool)(await terminate.ExecuteScalarAsync())!);
                await Assert.ThrowsAnyAsync<NpgsqlException>(() => pending);
            }
            Assert.Equal(before, await CountsAsync(fixture.TenantId));
        }
        finally
        {
            cancellation.Cancel();
            await blockingTransaction.RollbackAsync();
            await ExecuteOwnerSqlAsync("DROP TRIGGER hold_test_evidence ON purchasing.purchase_events; DROP FUNCTION purchasing.hold_test_evidence();");
        }
        var recovered = await fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, mutation, "interrupted-receipt", default);
        Assert.Equal(PurchasingStoreStatus.Applied, recovered.Status);
        Assert.Equal(2, recovered.Purchase!.Revision);
        Assert.Equal(2, await CountAsync("purchase_events", fixture.TenantId));
        Assert.Equal(2, await CountAsync("purchase_command_receipts", fixture.TenantId));
    }

    [Fact]
    public async Task RetainedFactLimitRejectsAnAdditionalReceiptWithoutAHeadJournalOrReceiptChange()
    {
        await using var fixture = await CreateFixtureAsync();
        var current = await RequestAsync(fixture);
        for (var index = 0; index < PurchasingRules.MaximumFacts; index++)
            current = await ApplyAsync(fixture, new(PurchasingCommandKind.Receive, current.Revision, Quantity: 0.01m, Amount: 0.01m));
        var before = await CountsAsync(fixture.TenantId);
        var rejected = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            new(PurchasingCommandKind.Receive, current.Revision, Quantity: 0.01m, Amount: 0.01m), "resource-limit", default);
        Assert.Equal(PurchasingStoreStatus.TransitionRejected, rejected.Status);
        Assert.Equal(PurchasingMutationStatus.ResourceLimit, rejected.Rejection);
        Assert.Equal(before, await CountsAsync(fixture.TenantId));
        var retained = (await fixture.NewStore().FindPurchaseAsync(fixture.Actor, fixture.PurchaseId, default))!;
        Assert.Equal(PurchasingRules.MaximumFacts, retained.Receipts.Count);
        Assert.Equal(current.Revision, retained.Revision);
    }

    [Fact]
    public async Task PostgreSqlStoredByteLimitRejectsCompactAdmissibleFactsBeforeAnyDurableWrite()
    {
        await using var fixture = await CreateFixtureAsync();
        var supplier = await fixture.Store.ExecuteSupplierAsync(fixture.Actor, fixture.SupplierId,
            new(SupplierCommandKind.Revise, 1, new string('N', 128), new string('C', 512)), "large-supplier", default);
        Assert.Equal(PurchasingStoreStatus.Applied, supplier.Status);
        var requested = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            RequestMutation(fixture) with { Description = new string('D', 512) }, "large-request", default);
        Assert.Equal(PurchasingStoreStatus.Applied, requested.Status);
        var current = await ApplyAsync(fixture, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: PurchasingRules.MaximumAmount));
        const decimal paymentAmount = 100_000_000_000_000.01m;
        var largeReference = new string('\\', 128);
        for (var index = 0; index < 125; index++)
            current = await ApplyAsync(fixture, new(PurchasingCommandKind.Pay, current.Revision, Amount: paymentAmount,
                ReceiptId: current.Receipts[0].EntryId, Method: SupplierPaymentMethod.Cash, Reference: largeReference));
        var mutation = new PurchasingMutation(PurchasingCommandKind.Pay, current.Revision, Amount: paymentAmount,
            ReceiptId: current.Receipts[0].EntryId, Method: SupplierPaymentMethod.Cash, Reference: largeReference);
        var candidate = PurchasingRules.ApplyPurchase(fixture.TenantId, current, mutation, null, null,
            fixture.PurchaseId, Guid.NewGuid(), fixture.Actor.AccountId, FullAuthority.AuthorizationRevision, new FixedClock().GetUtcNow());
        Assert.Equal(PurchasingMutationStatus.Applied, candidate.Status);
        var snapshot = JsonSerializer.Serialize(candidate.Snapshot, JsonOptions);
        var receipt = JsonSerializer.Serialize(new { schemaVersion = 1, operation = (int)mutation.Kind, snapshot = candidate.Snapshot }, JsonOptions);
        Assert.True(Encoding.UTF8.GetByteCount(snapshot) <= PurchasingRules.MaximumSnapshotBytes);
        Assert.True(Encoding.UTF8.GetByteCount(receipt) <= PurchasingRules.MaximumSnapshotBytes);
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT octet_length(@snapshot::jsonb::text),octet_length(@receipt::jsonb::text)", connection);
            command.Parameters.AddWithValue("snapshot", snapshot);
            command.Parameters.AddWithValue("receipt", receipt);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetInt32(0) > PurchasingRules.MaximumSnapshotBytes);
            Assert.True(reader.GetInt32(1) > PurchasingRules.MaximumSnapshotBytes);
        }
        var before = await CountsAsync(fixture.TenantId);
        foreach (var store in new[] { fixture.Store, fixture.NewStore() })
        {
            var rejected = await store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, mutation, "stored-byte-limit", default);
            Assert.Equal(PurchasingStoreStatus.TransitionRejected, rejected.Status);
            Assert.Equal(PurchasingMutationStatus.ResourceLimit, rejected.Rejection);
            Assert.Null(rejected.Purchase);
            Assert.Equal(before, await CountsAsync(fixture.TenantId));
            Assert.Equal(JsonSerializer.Serialize(current, JsonOptions), JsonSerializer.Serialize(await store.FindPurchaseAsync(fixture.Actor, fixture.PurchaseId, default), JsonOptions));
        }
        var corrected = await ApplyAsync(fixture, new(PurchasingCommandKind.Correct, current.Revision,
            TargetEventId: current.Payments[0].EffectiveEventId, Reason: "remove mistakenly repeated reference",
            Replacement: new(PurchasingEventKind.Payment, Amount: paymentAmount, ReceiptId: current.Receipts[0].EntryId, Method: SupplierPaymentMethod.Cash)));
        var retryMutation = mutation with { ExpectedRevision = corrected.Revision, Reference = null };
        var retry = await fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, retryMutation, "stored-byte-limit", default);
        Assert.Equal(PurchasingStoreStatus.Applied, retry.Status);
        Assert.Equal(126, retry.Purchase!.Payments.Count);
        var replay = await fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, retryMutation, "stored-byte-limit", default);
        Assert.Equal(PurchasingStoreStatus.Replayed, replay.Status);
        var changed = await fixture.NewStore().ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId,
            retryMutation with { Reference = largeReference }, "stored-byte-limit", default);
        Assert.Equal(PurchasingStoreStatus.IdempotencyConflict, changed.Status);
    }

    [Fact]
    public async Task MigrationAndModelAgreeAndDestructiveRollbackIsRefused()
    {
        await using var context = PurchasingPostgresRegistration.CreateContext(ConnectionString);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal("20261010100000_FirstSupplierServicePurchasing", Assert.Single(await context.Database.GetAppliedMigrationsAsync()));
        await Assert.ThrowsAsync<NotSupportedException>(() => context.GetService<IMigrator>().MigrateAsync("0"));
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await using (var context = PurchasingPostgresRegistration.CreateContext(ConnectionString)) await context.Database.MigrateAsync();
        await ExecuteOwnerSqlAsync($"""
            CREATE ROLE {RuntimeRole} LOGIN PASSWORD '{RuntimePassword}';
            GRANT CONNECT ON DATABASE purchasing_tests TO {RuntimeRole};
            GRANT USAGE ON SCHEMA purchasing TO {RuntimeRole};
            GRANT SELECT,INSERT ON purchasing.suppliers,purchasing.supplier_events,purchasing.supplier_command_receipts,
                purchasing.purchases,purchasing.purchase_events,purchasing.purchase_command_receipts TO {RuntimeRole};
            GRANT UPDATE(revision,snapshot,updated_at) ON purchasing.suppliers,purchasing.purchases TO {RuntimeRole};
            """);
    }

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    private static readonly string[] Tables = ["suppliers", "supplier_events", "supplier_command_receipts", "purchases", "purchase_events", "purchase_command_receipts"];
    private static readonly PurchasingAuthority FullAuthority = new(7, true, true, true, true, true, true, true, true);
    private static readonly PurchasingAuthority NoAuthority = new(8, false, false, false, false, false, false, false, false);

    private async Task<Scenario> CreateFixtureAsync()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var context = await new ResolveTenantContext(new TestMembershipDirectory(actor)).ExecuteAsync(actor, tenant, default)
            ?? throw new InvalidOperationException("Fixture membership missing.");
        var unit = Guid.NewGuid();
        var sourceReader = new TestSourceReader { Facts = new(new(tenant, unit, "PCS", 2, 5, true)) };
        var authority = new TestAuthorityReader();
        var source = CreateRuntimeSource();
        var fixture = new Scenario(source, sourceReader, authority, context, unit);
        var created = await fixture.Store.ExecuteSupplierAsync(context, fixture.SupplierId, new(SupplierCommandKind.Create, 0, "Phone printer", "Synthetic contact"), "create-supplier", default);
        Assert.Equal(PurchasingStoreStatus.Applied, created.Status);
        return fixture;
    }

    private NpgsqlDataSource CreateRuntimeSource(int maxPoolSize = 8, string? applicationName = null)
    {
        var settings = new NpgsqlConnectionStringBuilder(ConnectionString) { Username = RuntimeRole, Password = RuntimePassword, MaxPoolSize = maxPoolSize, ApplicationName = applicationName ?? string.Empty };
        return new NpgsqlDataSourceBuilder(settings.ConnectionString).Build();
    }

    private static PurchasingMutation RequestMutation(Scenario fixture) => new(PurchasingCommandKind.Request, 0,
        SupplierId: fixture.SupplierId, UnitId: fixture.UnitId, Description: "Phone printing", Quantity: 3m);

    private static async Task<PurchaseSnapshot> RequestAsync(Scenario fixture, string key = "request")
    {
        var result = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, RequestMutation(fixture), key, default);
        Assert.Equal(PurchasingStoreStatus.Applied, result.Status);
        return result.Purchase!;
    }

    private static async Task<PurchaseSnapshot> ApplyAsync(Scenario fixture, PurchasingMutation mutation)
    {
        var result = await fixture.Store.ExecutePurchaseAsync(fixture.Actor, fixture.PurchaseId, mutation, Guid.NewGuid().ToString("N"), default);
        Assert.Equal(PurchasingStoreStatus.Applied, result.Status);
        return result.Purchase!;
    }

    private async Task<long[]> CountsAsync(Guid tenant) => await Task.WhenAll(Tables.Select(table => CountAsync(table, tenant)));

    private async Task<long> CountAsync(string table, Guid tenant)
    {
        Assert.Contains(table, Tables);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM purchasing.{table} WHERE tenant_id=@tenant", connection);
        command.Parameters.AddWithValue("tenant", tenant);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> VisibleCountAsync(NpgsqlConnection connection, string table, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM purchasing.{table}", connection, transaction);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenant)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('app.current_tenant',@tenant,true)", connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }

    private async Task WaitForAdvisoryWaitAsync(string applicationName)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid
                    WHERE a.application_name=@name AND l.locktype='advisory' AND NOT l.granted)
                """, connection);
            command.Parameters.AddWithValue("name", applicationName);
            if ((bool)(await command.ExecuteScalarAsync(timeout.Token))!) return;
            await Task.Delay(10, timeout.Token);
        }
    }

    private async Task ExecuteOwnerSqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class Scenario(NpgsqlDataSource dataSource, TestSourceReader source, TestAuthorityReader authority,
        TenantContext actor, Guid unitId) : IAsyncDisposable
    {
        private readonly TimeProvider _clock = new FixedClock();
        public Guid SupplierId { get; } = Guid.NewGuid();
        public Guid PurchaseId { get; } = Guid.NewGuid();
        public Guid TenantId => Actor.TenantId;
        public Guid UnitId { get; } = unitId;
        public TenantContext Actor { get; } = actor;
        public TestSourceReader Source { get; } = source;
        public TestAuthorityReader Authority { get; } = authority;
        public PostgresPurchasingStore Store => NewStore();
        public PostgresPurchasingStore NewStore(NpgsqlDataSource? replacement = null) => new(replacement ?? dataSource, Source, Authority, Authority, _clock);
        public ValueTask DisposeAsync() => dataSource.DisposeAsync();
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestSourceReader : IPurchasingSourceReader
    {
        public PurchasingSourceFacts? Facts { get; set; }
        public Task<PurchasingSourceFacts?> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            TenantContext context, PurchasingMutation mutation, CancellationToken cancellationToken) => Task.FromResult(Facts);
    }

    private sealed class TestAuthorityReader : IPurchasingAuthorityReader, IPurchasingTransactionalAuthorityReader
    {
        public PurchasingAuthority Current { get; set; } = FullAuthority;
        public PurchasingAuthority? AfterFirstRead { get; set; }
        public Task<PurchasingAuthority> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            TenantContext context, CancellationToken cancellationToken)
        {
            Assert.Same(connection, transaction.Connection);
            return ResolveAsync(context, cancellationToken);
        }

        public Task<PurchasingAuthority> ResolveAsync(TenantContext context, CancellationToken cancellationToken)
        {
            var result = Current;
            if (AfterFirstRead is { } next) { Current = next; AfterFirstRead = null; }
            return Task.FromResult(result);
        }
    }

    private sealed class TestMembershipDirectory(Guid account) : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>(Array.Empty<TenantMembership>());
        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(accountId == account);
    }
}
