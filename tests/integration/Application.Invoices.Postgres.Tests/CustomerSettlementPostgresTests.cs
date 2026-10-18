using System.Globalization;
using System.Text.Json;
using Application.Invoices.Postgres;
using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Application.Invoices.Postgres.Tests;

public sealed class CustomerSettlementPostgresTests(InvoiceDatabaseFixture database) : IClassFixture<InvoiceDatabaseFixture>
{
    private static readonly string[] FinancialTables = ["receivables", "customer_payments", "customer_payment_events", "customer_financial_receipts"];

    [Theory]
    [InlineData(InvoiceDebtorKind.Organization)]
    [InlineData(InvoiceDebtorKind.Program)]
    [InlineData(InvoiceDebtorKind.Individual)]
    public async Task RecognitionFreezesExactDebtorAmountDateAndZeroSettlesWithoutPayment(InvoiceDebtorKind kind)
    {
        await using var s = await Scenario.CreateAsync(database);
        var debtor = new FinancialDebtor(kind, Guid.NewGuid());
        var invoice = await s.InvoiceAsync(debtor, 12.3456m);
        var zero = await s.InvoiceAsync(debtor, 0m);
        s.Ports.DebtorExists = false; // Historical reads do not depend on current customer availability.
        var retained = Found(await s.Store.ReadReceivableAsync(s.Actor, invoice, default));
        Assert.Equal(debtor, retained.Debtor);
        Assert.Equal(12.3456m, retained.OutstandingAmount);
        Assert.Equal("NPR", retained.CurrencyCode);
        Assert.Equal(new DateOnly(2025, 1, 2), retained.InvoiceDate);
        Assert.True(retained.RecognizedAt > new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero));
        Assert.True(Found(await s.Store.ReadReceivableAsync(s.Actor, zero, default)).IsSettled);
        Assert.Equal(new long[] { 2, 0, 0, 0 }, await s.CountsAsync());
    }

    [Theory]
    [InlineData(InvoiceDebtorKind.Organization)]
    [InlineData(InvoiceDebtorKind.Program)]
    [InlineData(InvoiceDebtorKind.Individual)]
    public async Task RecognizedFrozenDebtorCanReceiveFundsAfterCustomersLookupBecomesUnavailable(InvoiceDebtorKind kind)
    {
        await using var s = await Scenario.CreateAsync(database);
        var debtor = new FinancialDebtor(kind, Guid.NewGuid());
        await s.InvoiceAsync(debtor, 1m);
        s.Ports.DebtorExists = false;
        var payment = await s.RecordAsync(1m, debtor);
        Assert.Equal(debtor, (await s.PaymentAsync(payment)).Debtor);
        var missing = await s.Store.RecordAsync(s.Actor, Guid.NewGuid(), new(new(kind, Guid.NewGuid()), "NPR", 1m,
            CustomerPaymentChannel.Cash, "unrecognized debtor"), "missing", default);
        Assert.Equal(FinancialStatus.DebtorNotFound, missing.Status);
    }

    [Fact]
    public async Task HistoricalMigrationBackfillsAllKindsAndZeroExactlyOnce()
    {
        await using var isolated = await MigrationDatabase.CreateAsync(database);
        var tenant = Guid.NewGuid();
        var cases = new List<(Guid Invoice, FinancialDebtor Debtor, decimal Amount)>();
        foreach (var kind in Enum.GetValues<InvoiceDebtorKind>())
        {
            var debtor = new FinancialDebtor(kind, Guid.NewGuid());
            var invoice = await SeedInvoiceAsync(isolated.ConnectionString, tenant, Guid.NewGuid(), debtor, 9.8765m, false);
            cases.Add((invoice, debtor, 9.8765m));
        }
        cases.Add((await SeedInvoiceAsync(isolated.ConnectionString, tenant, Guid.NewGuid(), cases[0].Debtor, 0m, false), cases[0].Debtor, 0m));
        await isolated.MigrateAsync();
        await isolated.MigrateAsync();
        await using var connection = new NpgsqlConnection(isolated.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT invoice_id,debtor_kind,debtor_id,original_amount,invoice_date,recognized_at FROM invoices.receivables", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var seen = new HashSet<Guid>();
        while (await reader.ReadAsync())
        {
            var item = Assert.Single(cases, x => x.Invoice == reader.GetGuid(0));
            Assert.True(seen.Add(item.Invoice));
            Assert.Equal((int)item.Debtor.Kind, reader.GetInt32(1));
            Assert.Equal(item.Debtor.Id, reader.GetGuid(2));
            Assert.Equal(item.Amount, reader.GetDecimal(3));
            Assert.Equal(new DateOnly(2025, 1, 2), reader.GetFieldValue<DateOnly>(4));
            Assert.True(reader.GetDateTime(5) > new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        }
        Assert.Equal(4, seen.Count);
    }

    [Theory]
    [InlineData("arithmetic")]
    [InlineData("fingerprint")]
    [InlineData("debtor")]
    [InlineData("ordinal")]
    public async Task InvalidRetainedHistoryAbortsWholeMigrationWithoutRecognizingValidSibling(string corruption)
    {
        await using var isolated = await MigrationDatabase.CreateAsync(database);
        var tenant = Guid.NewGuid();
        var debtor = new FinancialDebtor(InvoiceDebtorKind.Organization, Guid.NewGuid());
        await SeedInvoiceAsync(isolated.ConnectionString, tenant, Guid.NewGuid(), debtor, 1m, false);
        var invalid = await SeedInvoiceAsync(isolated.ConnectionString, tenant, Guid.NewGuid(), debtor, 2m, false);
        var sql = corruption switch
        {
            "arithmetic" => "ALTER TABLE invoices.issued_lines DISABLE TRIGGER USER; UPDATE invoices.issued_lines SET unit_price=3 WHERE invoice_id=@invoice; ALTER TABLE invoices.issued_lines ENABLE TRIGGER USER;",
            "fingerprint" => "ALTER TABLE invoices.issue_receipts DISABLE TRIGGER USER; UPDATE invoices.issue_receipts SET fingerprint=repeat('A',64) WHERE invoice_id=@invoice; ALTER TABLE invoices.issue_receipts ENABLE TRIGGER USER;",
            "debtor" => "ALTER TABLE invoices.issued_invoices DISABLE TRIGGER USER; UPDATE invoices.issued_invoices SET debtor=jsonb_set(debtor,'{organizationDisplayName}','\" \"') WHERE invoice_id=@invoice; ALTER TABLE invoices.issued_invoices ENABLE TRIGGER USER;",
            _ => "ALTER TABLE invoices.organization_ordinals DISABLE TRIGGER USER; UPDATE invoices.organization_ordinals SET last_ordinal=1; ALTER TABLE invoices.organization_ordinals ENABLE TRIGGER USER;",
        };
        await SqlAsync(isolated.ConnectionString, sql, ("invoice", invalid));
        var error = await Assert.ThrowsAsync<PostgresException>(() => isolated.MigrateAsync());
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(true, await ScalarAsync(isolated.ConnectionString, "SELECT to_regclass('invoices.receivables') IS NULL"));
        Assert.Equal(2L, await ScalarAsync(isolated.ConnectionString, "SELECT count(*) FROM invoices.issued_invoices"));
        await using var context = InvoicesPostgresRegistration.CreateContext(isolated.ConnectionString);
        Assert.Equal(["20261010120000_FirstInvoiceIssuance"], await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task PendingSelfConfirmationIndependentReconciliationDisputeNewEvidenceAndTerminalRejection()
    {
        await using var s = await Scenario.CreateAsync(database);
        var invoice = await s.InvoiceAsync(s.Debtor, 10m);
        var payment = await s.RecordAsync(20.1234m);
        Assert.Equal(0m, (await s.PaymentAsync(payment)).AvailableAmount);
        Assert.Equal(FinancialStatus.InvalidTransition, (await s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 1, invoice: invoice, amount: 1m)).Status);
        Assert.Equal(FinancialStatus.SelfConfirmation, (await s.MutateAsync(payment, CustomerPaymentOperation.Confirm, 1, "received", actor: s.Actor)).Status);
        Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Confirm, 1, "received", actor: s.Reconciler));
        Assert.Equal(20.1234m, (await s.PaymentAsync(payment)).AvailableAmount);
        Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Dispute, 2, "bank query"));
        Assert.Equal(0m, (await s.PaymentAsync(payment)).AvailableAmount);
        Assert.Equal(FinancialStatus.InvalidTransition, (await s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 3, invoice: invoice, amount: 1m)).Status);
        Assert.Equal(FinancialStatus.InvalidTransition, (await s.MutateAsync(payment, CustomerPaymentOperation.Confirm, 3, "received", actor: s.Reconciler)).Status);
        Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Confirm, 3, "new received evidence", actor: s.Reconciler));
        var rejected = await s.RecordAsync(1m);
        Applied(await s.MutateAsync(rejected, CustomerPaymentOperation.Reject, 1, "not received"));
        Assert.Equal(FinancialStatus.InvalidTransition, (await s.MutateAsync(rejected, CustomerPaymentOperation.Confirm, 2, "later", actor: s.Reconciler)).Status);
        var pendingDispute = await s.RecordAsync(1m);
        Applied(await s.MutateAsync(pendingDispute, CustomerPaymentOperation.Dispute, 1, "pending disputed"));
        Applied(await s.MutateAsync(pendingDispute, CustomerPaymentOperation.Reverse, 2, "cancel evidence"));
        Assert.Equal(FinancialStatus.InvalidTransition, (await s.MutateAsync(pendingDispute, CustomerPaymentOperation.Dispute, 3, "terminal")).Status);
        var history = Found(await s.Store.ReadHistoryAsync(s.Actor, payment, null, default)).Items;
        Assert.Equal(new[] { CustomerPaymentOperation.Record, CustomerPaymentOperation.Confirm, CustomerPaymentOperation.Dispute, CustomerPaymentOperation.Confirm }, history.Select(x => x.Operation));
        Assert.Equal(Enumerable.Range(1, 4).Select(x => (long)x), history.Select(x => x.Revision));
        Assert.Equal(s.Reconciler.AccountId, history[1].ActorAccountId);
        Assert.All(history, x => { Assert.Equal(47, x.AuthorizationRevision); Assert.Equal(TimeSpan.Zero, x.RecordedAt.Offset); });
        Assert.Equal("Synthetic receipt", (await s.PaymentAsync(payment)).Evidence);
    }

    [Fact]
    public async Task PartialMultiInvoiceAllocationUnappliedFundsDisputeFullReleaseReallocateAndReverse()
    {
        await using var s = await Scenario.CreateAsync(database);
        var a = await s.InvoiceAsync(s.Debtor, 10.1234m);
        var b = await s.InvoiceAsync(s.Debtor, 20m);
        var payment = await s.ConfirmedAsync(40m);
        var first = Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 2, invoice: a, amount: 10.1234m));
        var second = Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 3, invoice: b, amount: 5m));
        Assert.True(Found(await s.Store.ReadReceivableAsync(s.Actor, a, default)).IsSettled);
        Assert.Equal(15m, Found(await s.Store.ReadReceivableAsync(s.Actor, b, default)).OutstandingAmount);
        Assert.Equal(24.8766m, (await s.PaymentAsync(payment)).AvailableAmount);
        Assert.Equal(FinancialStatus.ActiveAllocations, (await s.MutateAsync(payment, CustomerPaymentOperation.Reverse, 4, "wrong receipt")).Status);
        Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Dispute, 4, "query"));
        Assert.Equal(10.1234m, Found(await s.Store.ReadReceivableAsync(s.Actor, a, default)).AllocatedAmount);
        Assert.Equal(FinancialStatus.ActiveAllocations, (await s.MutateAsync(payment, CustomerPaymentOperation.Reverse, 5, "still allocated")).Status);
        Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Release, 5, "release first", allocation: first.EventId));
        Assert.Equal(FinancialStatus.InvalidTransition, (await s.MutateAsync(payment, CustomerPaymentOperation.Release, 6, "repeat release", allocation: first.EventId)).Status);
        Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Confirm, 6, "new confirmation", actor: s.Reconciler));
        var reallocated = Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 7, invoice: a, amount: 1.0001m));
        Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Release, 8, "release second", allocation: second.EventId));
        Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Release, 9, "release replacement", allocation: reallocated.EventId));
        Assert.Equal(40m, (await s.PaymentAsync(payment)).AvailableAmount);
        Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Reverse, 10, "full evidence correction"));
        Assert.Equal(CustomerPaymentState.Reversed, (await s.PaymentAsync(payment)).State);
        Assert.Equal(0m, (await s.PaymentAsync(payment)).AvailableAmount);
        var balance = Found(await s.Store.ReadBalanceAsync(s.Actor, s.Debtor, default));
        Assert.Equal(30.1234m, balance.OutstandingAmount);
        Assert.Equal(0m, balance.ConfirmedUnappliedAmount);
        var events = Found(await s.Store.ReadHistoryAsync(s.Actor, payment, null, default)).Items;
        Assert.Equal(11, events.Count);
        Assert.Equal(first.EventId, events[5].AllocationId);
        Assert.Equal(10.1234m, events[5].Amount);
        Assert.Equal(a, events[5].ReceivableId);
    }

    [Theory]
    [InlineData(InvoiceDebtorKind.Organization)]
    [InlineData(InvoiceDebtorKind.Program)]
    [InlineData(InvoiceDebtorKind.Individual)]
    public async Task FrozenDebtorsStayIndependentEvenUnderOneOrdinalOrganization(InvoiceDebtorKind kind)
    {
        await using var s = await Scenario.CreateAsync(database);
        var target = new FinancialDebtor(kind, Guid.NewGuid());
        var invoice = await s.InvoiceAsync(target, 4.4444m);
        var wrong = await s.ConfirmedAsync(5m);
        Assert.Equal(FinancialStatus.DebtorMismatch, (await s.MutateAsync(wrong, CustomerPaymentOperation.Allocate, 2, invoice: invoice, amount: 1m)).Status);
        var matching = await s.ConfirmedAsync(5m, target);
        Applied(await s.MutateAsync(matching, CustomerPaymentOperation.Allocate, 2, invoice: invoice, amount: 4.4444m));
        Assert.Equal(.5556m, Found(await s.Store.ReadBalanceAsync(s.Actor, target, default)).ConfirmedUnappliedAmount);
        Assert.Equal(5m, Found(await s.Store.ReadBalanceAsync(s.Actor, s.Debtor, default)).ConfirmedUnappliedAmount);
    }

    [Theory]
    [InlineData("NPR", "0")]
    [InlineData("NPR", "-1")]
    [InlineData("NPR", "1.00001")]
    [InlineData("NPR", "1000000000000000")]
    [InlineData("USD", "1")]
    [InlineData("npr", "1")]
    public async Task InvalidCurrencyScaleRangeAndNonpositiveAmountsLeaveNoFinancialWrites(string currency, string amountText)
    {
        await using var s = await Scenario.CreateAsync(database);
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);
        await Assert.ThrowsAsync<FinancialValidationException>(() => s.Store.RecordAsync(s.Actor, Guid.NewGuid(), new(s.Debtor, currency, amount, CustomerPaymentChannel.Cash, "evidence"), "invalid", default));
        var invoice = await s.InvoiceAsync(s.Debtor, 2m);
        var payment = await s.ConfirmedAsync(2m);
        if (currency == "NPR") await Assert.ThrowsAsync<FinancialValidationException>(() => s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 2, invoice: invoice, amount: amount));
        Assert.Equal(new long[] { 1, 1, 2, 2 }, await s.CountsAsync());
    }

    [Fact]
    public async Task TrimmedBankReferenceRacesAcrossDebtorsPreserveCaseAndTenantScope()
    {
        await using var s = await Scenario.CreateAsync(database);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var outcomes = await Task.WhenAll(
            s.Store.RecordAsync(s.Actor, a, new(s.Debtor, "NPR", 1m, CustomerPaymentChannel.Bank, " receipt ", " Ref-42 "), "a", default),
            s.Store.RecordAsync(s.Actor, b, new(new(InvoiceDebtorKind.Individual, Guid.NewGuid()), "NPR", 2m, CustomerPaymentChannel.Bank, "other", "Ref-42"), "b", default));
        Assert.Single(outcomes, x => x.Status == FinancialStatus.Applied);
        Assert.Single(outcomes, x => x.Status == FinancialStatus.DuplicateReference);
        var winner = outcomes[0].Status == FinancialStatus.Applied ? a : b;
        Assert.Equal("Ref-42", (await s.PaymentAsync(winner)).ExternalReference);
        Applied(await s.Store.RecordAsync(s.Actor, Guid.NewGuid(), new(s.Debtor, "NPR", 1m, CustomerPaymentChannel.Bank, "case", "ref-42"), "case", default));
        Applied(await s.Store.RecordAsync(s.Actor, Guid.NewGuid(), new(s.Debtor, "NPR", 1m, CustomerPaymentChannel.Cash, "cash", "Ref-42"), "cash", default));
        await using var other = await Scenario.CreateAsync(database);
        Applied(await other.Store.RecordAsync(other.Actor, Guid.NewGuid(), new(other.Debtor, "NPR", 1m, CustomerPaymentChannel.Bank, "other tenant", "Ref-42"), "a", default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualConcurrentAllocationsCannotOverspendPaymentOrReceivable(bool samePayment)
    {
        await using var s = await Scenario.CreateAsync(database);
        var a = await s.InvoiceAsync(s.Debtor, 10m);
        var b = samePayment ? await s.InvoiceAsync(s.Debtor, 10m) : a;
        var p = await s.ConfirmedAsync(10m);
        var q = samePayment ? p : await s.ConfirmedAsync(10m);
        var outcomes = await Task.WhenAll(
            s.MutateAsync(p, CustomerPaymentOperation.Allocate, 2, invoice: a, amount: 7m, key: "race-a"),
            s.MutateAsync(q, CustomerPaymentOperation.Allocate, 2, invoice: b, amount: 7m, key: "race-b"));
        Assert.Single(outcomes, x => x.Status == FinancialStatus.Applied);
        Assert.Single(outcomes, x => x.Status == (samePayment ? FinancialStatus.RevisionConflict : FinancialStatus.OverAllocation));
        Assert.Equal(7m, (await s.PaymentAsync(p)).AllocatedAmount + (samePayment ? 0m : (await s.PaymentAsync(q)).AllocatedAmount));
        Assert.Equal(samePayment ? 13m : 3m, Found(await s.Store.ReadBalanceAsync(s.Actor, s.Debtor, default)).OutstandingAmount);
    }

    [Theory]
    [InlineData("customer_payments", false)]
    [InlineData("customer_payment_events", false)]
    [InlineData("customer_financial_receipts", false)]
    [InlineData("customer_payment_events", true)]
    [InlineData("customer_financial_receipts", true)]
    public async Task FailureAtEachPaymentWriteRollsBackFactsProjectionAndReceiptAndExactKeyCanRetry(string table, bool allocation)
    {
        await using var s = await Scenario.CreateAsync(database);
        var invoice = await s.InvoiceAsync(s.Debtor, 20m);
        var payment = allocation ? await s.ConfirmedAsync(10m) : Guid.NewGuid();
        var before = await s.CountsAsync();
        var trigger = "fail_" + Guid.NewGuid().ToString("N");
        await database.OwnerSqlAsync($"""
            CREATE FUNCTION invoices.{trigger}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN IF NEW.tenant_id='{s.Actor.TenantId:D}'::uuid THEN RAISE EXCEPTION 'Synthetic financial write failure'; END IF; RETURN NEW; END; $body$;
            CREATE TRIGGER {trigger} BEFORE INSERT ON invoices.{table} FOR EACH ROW EXECUTE FUNCTION invoices.{trigger}();
            """);
        Task<FinancialResult<FinancialCommandReceipt>> Attempt() => allocation
            ? s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 2, invoice: invoice, amount: 3m, key: "retry")
            : s.Store.RecordAsync(s.Actor, payment, new(s.Debtor, "NPR", 10m, CustomerPaymentChannel.Cash, "evidence"), "retry", default);
        try
        {
            Assert.Equal(FinancialStatus.Unavailable, (await Attempt()).Status);
            Assert.Equal(before, await s.CountsAsync());
            Assert.Equal(20m, Found(await s.Store.ReadReceivableAsync(s.Actor, invoice, default)).OutstandingAmount);
        }
        finally { await database.OwnerSqlAsync($"DROP TRIGGER {trigger} ON invoices.{table}; DROP FUNCTION invoices.{trigger}();"); }
        var result = Applied(await Attempt());
        Assert.Equal(result, Assert.IsType<FinancialCommandReceipt>((await Attempt()).Value));
        Assert.Equal(FinancialStatus.Replayed, (await Attempt()).Status);
    }

    [Fact]
    public async Task RecognitionFailureRollsBackInvoiceOrdinalLinesAndIssueReceiptTogether()
    {
        await using var s = await Scenario.CreateAsync(database);
        var trigger = "fail_" + Guid.NewGuid().ToString("N");
        await database.OwnerSqlAsync($"""
            CREATE FUNCTION invoices.{trigger}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN IF NEW.tenant_id='{s.Actor.TenantId:D}'::uuid THEN RAISE EXCEPTION 'Synthetic recognition failure'; END IF; RETURN NEW; END; $body$;
            CREATE TRIGGER {trigger} BEFORE INSERT ON invoices.receivables FOR EACH ROW EXECUTE FUNCTION invoices.{trigger}();
            """);
        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => s.InvoiceAsync(s.Debtor, 1m));
            Assert.Equal(new long[] { 0, 0, 0, 0 }, await database.CountsAsync(s.Actor.TenantId));
            Assert.Equal(new long[] { 0, 0, 0, 0 }, await s.CountsAsync());
        }
        finally { await database.OwnerSqlAsync($"DROP TRIGGER {trigger} ON invoices.receivables; DROP FUNCTION invoices.{trigger}();"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualCommitResponseLossReturnsUnknownAndFreshSourceRecoversSameImmutableResult(bool allocation)
    {
        await using var s = await Scenario.CreateAsync(database);
        var invoice = await s.InvoiceAsync(s.Debtor, 20m);
        var payment = allocation ? await s.ConfirmedAsync(10m) : Guid.NewGuid();
        var record = new CustomerPaymentRecord(s.Debtor, "NPR", 10m, CustomerPaymentChannel.Cash, "receipt");
        var mutation = new CustomerPaymentMutation(CustomerPaymentOperation.Allocate, 2, ReceivableId: invoice, Amount: 3.1234m);
        await using var proxy = new CommitResponseLossProxy(database.ConnectionString);
        await using var lossy = database.CreateRuntimeSource(1, connectionString: proxy.ConnectionString);
        var store = s.NewStore(lossy);
        var pending = allocation ? store.ExecuteAsync(s.Actor, payment, mutation, "lost", default) : store.RecordAsync(s.Actor, payment, record, "lost", default);
        await proxy.CommitCompleted.WaitAsync(TimeSpan.FromSeconds(30));
        var committed = await s.CountsAsync();
        Assert.Equal(new long[] { 1, 1, allocation ? 3 : 1, allocation ? 3 : 1 }, committed);
        proxy.DropResponse();
        var unknown = await pending.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(FinancialStatus.OutcomeUnknown, unknown.Status);
        Assert.Null(unknown.Value);
        await using var fresh = database.CreateRuntimeSource(1);
        var restarted = s.NewStore(fresh);
        var recovered = allocation ? await restarted.ExecuteAsync(s.Actor, payment, mutation, "lost", default) : await restarted.RecordAsync(s.Actor, payment, record, "lost", default);
        Assert.Equal(FinancialStatus.Replayed, recovered.Status);
        var history = Found(await restarted.ReadHistoryAsync(s.Actor, payment, null, default)).Items;
        Assert.Equal(history[^1].EventId, recovered.Value!.EventId);
        Assert.Equal(committed, await s.CountsAsync());
        var changed = allocation ? await restarted.ExecuteAsync(s.Actor, payment, mutation with { Amount = 2m }, "lost", default)
            : await restarted.RecordAsync(s.Actor, payment, record with { Amount = 2m }, "lost", default);
        Assert.Equal(FinancialStatus.IdempotencyConflict, changed.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevocationWhilePaymentAdvisoryLockWaitsPreventsWritesAndReplayDisclosure(bool replay)
    {
        var name = Guid.NewGuid().ToString("N");
        await using var s = await Scenario.CreateAsync(database, name);
        var payment = Guid.NewGuid();
        var record = new CustomerPaymentRecord(s.Debtor, "NPR", 1m, CustomerPaymentChannel.Cash, "evidence");
        if (replay) Applied(await s.Store.RecordAsync(s.Actor, payment, record, "locked", default));
        var before = await s.CountsAsync();
        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended('customer-payment:'||@tenant::text||':'||@payment::text,0))", blocker, transaction))
        {
            command.Parameters.AddWithValue("tenant", s.Actor.TenantId);
            command.Parameters.AddWithValue("payment", payment);
            await command.ExecuteNonQueryAsync();
        }
        var pending = s.Store.RecordAsync(s.Actor, payment, record, "locked", default);
        try
        {
            await database.WaitForAdvisoryWaitAsync(name);
            s.Ports.Transactional = FinancialAccess.Denied;
        }
        finally { await transaction.RollbackAsync(); }
        var denied = await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(FinancialStatus.Denied, denied.Status);
        Assert.Null(denied.Value);
        Assert.Equal(before, await s.CountsAsync());
    }

    [Fact]
    public async Task RevocationWhileReceivableAdvisoryLockWaitsPreventsAllocation()
    {
        var name = Guid.NewGuid().ToString("N");
        await using var s = await Scenario.CreateAsync(database, name);
        var invoice = await s.InvoiceAsync(s.Debtor, 10m);
        var payment = await s.ConfirmedAsync(10m);
        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended('customer-receivable:'||@tenant::text||':'||@invoice::text,0))", blocker, transaction))
        {
            command.Parameters.AddWithValue("tenant", s.Actor.TenantId);
            command.Parameters.AddWithValue("invoice", invoice);
            await command.ExecuteNonQueryAsync();
        }
        var pending = s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 2, invoice: invoice, amount: 1m);
        try
        {
            await database.WaitForAdvisoryWaitAsync(name);
            s.Ports.Transactional = FinancialAccess.Denied;
        }
        finally { await transaction.RollbackAsync(); }
        Assert.Equal(FinancialStatus.Denied, (await pending.WaitAsync(TimeSpan.FromSeconds(20))).Status);
        Assert.Equal(new long[] { 1, 1, 2, 2 }, await s.CountsAsync());
    }

    [Theory]
    [InlineData(FinancialAccess.Denied)]
    [InlineData(FinancialAccess.Unavailable)]
    public async Task EveryReadAndReplayRequiresFreshIndependentCurrentAuthorityEvenWhenEmpty(FinancialAccess access)
    {
        await using var s = await Scenario.CreateAsync(database);
        var invoice = await s.InvoiceAsync(s.Debtor, 2m);
        var payment = Guid.NewGuid();
        var record = new CustomerPaymentRecord(s.Debtor, "NPR", 1m, CustomerPaymentChannel.Cash, "receipt");
        Applied(await s.Store.RecordAsync(s.Actor, payment, record, "retained", default));
        s.Ports.BlockedCapability = FinancialCapability.Record;
        s.Ports.BlockedAccess = access;
        var expected = access == FinancialAccess.Denied ? FinancialStatus.Denied : FinancialStatus.Unavailable;
        Assert.Equal(expected, (await s.Store.RecordAsync(s.Actor, payment, record, "retained", default)).Status);
        Assert.Equal(FinancialStatus.Found, (await s.Store.ReadPaymentAsync(s.Actor, payment, default)).Status);
        s.Ports.BlockedCapability = FinancialCapability.View;
        Assert.Equal(expected, (await s.Store.ReadPaymentAsync(s.Actor, payment, default)).Status);
        Assert.Equal(expected, (await s.Store.ReadReceivableAsync(s.Actor, invoice, default)).Status);
        Assert.Equal(expected, (await s.Store.BrowsePaymentsAsync(s.Actor, new(InvoiceDebtorKind.Program, Guid.NewGuid()), null, default)).Status);
        Assert.Equal(expected, (await s.Store.BrowseReceivablesAsync(s.Actor, null, null, default)).Status);
        Assert.Equal(expected, (await s.Store.ReadHistoryAsync(s.Actor, Guid.NewGuid(), null, default)).Status);
        Assert.Equal(expected, (await s.Store.ReadBalanceAsync(s.Actor, new(InvoiceDebtorKind.Individual, Guid.NewGuid()), default)).Status);
        s.Ports.BlockedCapability = null;
        s.Ports.Transactional = access;
        Assert.Equal(expected, (await s.Store.RecordAsync(s.Actor, payment, record, "retained", default)).Status);
        Assert.Equal(expected, (await s.Store.ReadBalanceAsync(s.Actor, s.Debtor, default)).Status);
        Assert.Equal(new long[] { 1, 1, 1, 1 }, await s.CountsAsync());
    }

    [Fact]
    public async Task PaymentReceivableAndHistoryPagesAreFiftyStableTypedTenantFilterAndResourceBound()
    {
        await using var s = await Scenario.CreateAsync(database);
        var payments = new HashSet<Guid>(); var invoices = new HashSet<Guid>();
        for (var i = 0; i < 53; i++)
        {
            payments.Add(await s.RecordAsync(1m));
            invoices.Add(await s.InvoiceAsync(s.Debtor, 1m));
        }
        var otherDebtor = new FinancialDebtor(InvoiceDebtorKind.Program, Guid.NewGuid());
        await s.RecordAsync(1m, otherDebtor);
        await s.InvoiceAsync(otherDebtor, 1m);
        var firstP = Found(await s.Store.BrowsePaymentsAsync(s.Actor, s.Debtor, null, default));
        var firstR = Found(await s.Store.BrowseReceivablesAsync(s.Actor, s.Debtor, null, default));
        Assert.Equal(50, firstP.Items.Count); Assert.Equal(50, firstR.Items.Count);
        Assert.NotNull(firstP.NextCursor); Assert.NotNull(firstR.NextCursor);
        var lastP = Found(await s.Store.BrowsePaymentsAsync(s.Actor, s.Debtor, firstP.NextCursor, default));
        var lastR = Found(await s.Store.BrowseReceivablesAsync(s.Actor, s.Debtor, firstR.NextCursor, default));
        Assert.Equal(3, lastP.Items.Count); Assert.Equal(3, lastR.Items.Count);
        Assert.Null(lastP.NextCursor); Assert.Null(lastR.NextCursor);
        Assert.True(payments.SetEquals(firstP.Items.Concat(lastP.Items).Select(x => x.PaymentId)));
        Assert.True(invoices.SetEquals(firstR.Items.Concat(lastR.Items).Select(x => x.InvoiceId)));
        Assert.Equal(53, firstP.Items.Concat(lastP.Items).Select(x => x.PaymentId).Distinct().Count());
        Assert.Equal(firstP.Items.Select(x => x.PaymentId), Found(await s.Store.BrowsePaymentsAsync(s.Actor, s.Debtor, null, default)).Items.Select(x => x.PaymentId));
        await Assert.ThrowsAsync<FinancialValidationException>(() => s.Store.BrowseReceivablesAsync(s.Actor, s.Debtor, firstP.NextCursor, default));
        await Assert.ThrowsAsync<FinancialValidationException>(() => s.Store.BrowsePaymentsAsync(s.Actor, null, firstP.NextCursor, default));
        await Assert.ThrowsAsync<FinancialValidationException>(() => s.Store.BrowsePaymentsAsync(s.Actor, otherDebtor, firstP.NextCursor, default));
        await using var other = await Scenario.CreateAsync(database);
        await Assert.ThrowsAsync<FinancialValidationException>(() => other.Store.BrowsePaymentsAsync(other.Actor, s.Debtor, firstP.NextCursor, default));
        Assert.Empty(Found(await other.Store.BrowsePaymentsAsync(other.Actor, null, null, default)).Items);
        Assert.Empty(Found(await other.Store.BrowseReceivablesAsync(other.Actor, null, null, default)).Items);
        Assert.Equal(FinancialStatus.NotFound, (await other.Store.ReadPaymentAsync(other.Actor, payments.First(), default)).Status);
        Assert.Equal(FinancialStatus.NotFound, (await other.Store.ReadReceivableAsync(other.Actor, invoices.First(), default)).Status);
        Assert.Equal(FinancialStatus.NotFound, (await other.Store.ReadHistoryAsync(other.Actor, payments.First(), null, default)).Status);
        Assert.Equal(0m, Found(await other.Store.ReadBalanceAsync(other.Actor, s.Debtor, default)).RecognizedAmount);
        var longHistory = payments.First();
        Applied(await s.MutateAsync(longHistory, CustomerPaymentOperation.Confirm, 1, "first", actor: s.Reconciler));
        for (var revision = 2; revision <= 52; revision++)
            Applied(await s.MutateAsync(longHistory, revision % 2 == 0 ? CustomerPaymentOperation.Dispute : CustomerPaymentOperation.Confirm,
                revision, "evidence " + revision.ToString(CultureInfo.InvariantCulture), actor: s.Reconciler));
        var h = Found(await s.Store.ReadHistoryAsync(s.Actor, longHistory, null, default));
        Assert.Equal(50, h.Items.Count); Assert.NotNull(h.NextCursor);
        var tail = Found(await s.Store.ReadHistoryAsync(s.Actor, longHistory, h.NextCursor, default));
        Assert.Equal(3, tail.Items.Count); Assert.Null(tail.NextCursor);
        Assert.Equal(Enumerable.Range(1, 53).Select(x => (long)x), h.Items.Concat(tail.Items).Select(x => x.Revision));
        await Assert.ThrowsAsync<FinancialValidationException>(() => s.Store.ReadHistoryAsync(s.Actor, payments.Last(), h.NextCursor, default));
        await Assert.ThrowsAsync<FinancialValidationException>(() => s.Store.ReadHistoryAsync(s.Actor, longHistory, firstP.NextCursor, default));
        await Assert.ThrowsAsync<FinancialValidationException>(() => other.Store.ReadHistoryAsync(other.Actor, longHistory, h.NextCursor, default));
        var counts = await s.CountsAsync();
        await s.Store.ReadBalanceAsync(s.Actor, s.Debtor, default);
        await s.Store.ReadHistoryAsync(s.Actor, longHistory, null, default);
        Assert.Equal(counts, await s.CountsAsync());
    }

    [Fact]
    public async Task DebtorBalanceUsesOneSnapshotWhileAllocationAndFullReleaseCommitConcurrently()
    {
        await using var s = await Scenario.CreateAsync(database);
        var invoice = await s.InvoiceAsync(s.Debtor, 10m);
        var payment = await s.ConfirmedAsync(10m);
        var allocated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writing = Task.Run(async () =>
        {
            long revision = 2;
            for (var i = 0; i < 20; i++)
            {
                var allocation = Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Allocate, revision++, invoice: invoice, amount: 7.1234m));
                if (i == 0)
                {
                    allocated.SetResult();
                    await observed.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Release, revision++, "release", allocation: allocation.EventId));
            }
        });
        try
        {
            await allocated.Task.WaitAsync(TimeSpan.FromSeconds(30));
            for (var i = 0; i < 40; i++)
            {
                var balance = Found(await s.Store.ReadBalanceAsync(s.Actor, s.Debtor, default));
                Assert.Equal(10m, balance.RecognizedAmount);
                Assert.Equal(balance.OutstandingAmount, balance.ConfirmedUnappliedAmount);
                Assert.True(balance.OutstandingAmount is 10m or 2.8766m);
                if (i == 0)
                {
                    Assert.Equal(2.8766m, balance.OutstandingAmount);
                    observed.SetResult();
                }
            }
        }
        finally
        {
            observed.TrySetResult();
            await writing.WaitAsync(TimeSpan.FromSeconds(30));
        }
        Assert.Equal(10m, Found(await s.Store.ReadBalanceAsync(s.Actor, s.Debtor, default)).OutstandingAmount);
    }

    [Fact]
    public async Task FinancialTablesForceRlsResetPooledTenantAndDenyRuntimeMutationAndDdl()
    {
        await using var s = await Scenario.CreateAsync(database);
        await s.InvoiceAsync(s.Debtor, 1m);
        await s.RecordAsync(1m);
        await using var source = database.CreateRuntimeSource(1);
        await using (var connection = await source.OpenConnectionAsync())
        {
            foreach (var table in FinancialTables)
                Assert.Equal(0L, await CountAsync(connection, table));
            await using var transaction = await connection.BeginTransactionAsync();
            await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, s.Actor.TenantId);
            foreach (var table in FinancialTables) Assert.Equal(1L, await CountAsync(connection, table, transaction));
            await transaction.RollbackAsync();
        }
        await using (var connection = await source.OpenConnectionAsync())
        {
            foreach (var table in FinancialTables)
            {
                Assert.Equal(0L, await CountAsync(connection, table));
                foreach (var sql in new[] { $"UPDATE invoices.{table} SET tenant_id=tenant_id", $"DELETE FROM invoices.{table}", $"TRUNCATE invoices.{table}" })
                {
                    await using var command = new NpgsqlCommand(sql, connection);
                    Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
                }
                Assert.Equal(true, await ScalarAsync(database.ConnectionString,
                    "SELECT relrowsecurity AND relforcerowsecurity FROM pg_class WHERE oid=@table::regclass", ("table", "invoices." + table)));
                await Assert.ThrowsAsync<PostgresException>(() => database.OwnerSqlAsync($"UPDATE invoices.{table} SET tenant_id=tenant_id WHERE tenant_id=@tenant", ("tenant", s.Actor.TenantId)));
            }
            await using var ddl = new NpgsqlCommand("CREATE TABLE invoices.illegal_financial_object(id integer)", connection);
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => ddl.ExecuteNonQueryAsync())).SqlState);
            await using var transaction = await connection.BeginTransactionAsync();
            await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, Guid.NewGuid());
            await using var cross = new NpgsqlCommand("""
                INSERT INTO invoices.customer_payments VALUES(@tenant,@payment,1,@debtor,'NPR',1,1,'evidence',NULL,@actor,clock_timestamp(),1)
                """, connection, transaction);
            cross.Parameters.AddWithValue("tenant", s.Actor.TenantId); cross.Parameters.AddWithValue("payment", Guid.NewGuid());
            cross.Parameters.AddWithValue("debtor", s.Debtor.Id); cross.Parameters.AddWithValue("actor", s.Actor.AccountId);
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => cross.ExecuteNonQueryAsync())).SqlState);
        }
    }

    [Fact]
    public async Task DeferredCompletenessRejectsPaymentWithoutEventAndReceipt()
    {
        await using var s = await Scenario.CreateAsync(database);
        await using var connection = await s.Source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, s.Actor.TenantId);
        await using var command = new NpgsqlCommand("""
            INSERT INTO invoices.customer_payments VALUES(@tenant,@payment,1,@debtor,'NPR',1,1,'evidence',NULL,@actor,clock_timestamp(),1)
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant", s.Actor.TenantId); command.Parameters.AddWithValue("payment", Guid.NewGuid());
        command.Parameters.AddWithValue("debtor", s.Debtor.Id); command.Parameters.AddWithValue("actor", s.Actor.AccountId);
        await command.ExecuteNonQueryAsync();
        Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync())).SqlState);
        Assert.Equal(new long[] { 0, 0, 0, 0 }, await s.CountsAsync());
    }

    [Fact]
    public async Task DirectDatabaseRejectsPartialReleaseAndMissingReceiptWithoutChangingBalances()
    {
        await using var s = await Scenario.CreateAsync(database);
        var invoice = await s.InvoiceAsync(s.Debtor, 10m);
        var payment = await s.ConfirmedAsync(10m);
        var allocation = Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 2, invoice: invoice, amount: 5m));
        await Assert.ThrowsAsync<FinancialValidationException>(() => s.MutateAsync(payment, CustomerPaymentOperation.Release, 3, "partial", amount: 1m, allocation: allocation.EventId));
        foreach (var partial in new[] { true, false })
        {
            await using var connection = await s.Source.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, s.Actor.TenantId);
            await using var command = new NpgsqlCommand("""
                INSERT INTO invoices.customer_payment_events VALUES(@tenant,@event,@payment,4,7,2,@actor,clock_timestamp(),47,'release',@invoice,@amount,@allocation,1)
                """, connection, transaction);
            command.Parameters.AddWithValue("tenant", s.Actor.TenantId); command.Parameters.AddWithValue("payment", payment);
            command.Parameters.AddWithValue("event", Guid.NewGuid()); command.Parameters.AddWithValue("actor", s.Actor.AccountId);
            command.Parameters.AddWithValue("invoice", invoice); command.Parameters.AddWithValue("amount", partial ? 1m : 5m);
            command.Parameters.AddWithValue("allocation", allocation.EventId);
            if (partial) Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
            else
            {
                await command.ExecuteNonQueryAsync();
                Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync())).SqlState);
            }
        }
        Assert.Equal(5m, (await s.PaymentAsync(payment)).AllocatedAmount);
        Assert.Equal(new long[] { 1, 1, 3, 3 }, await s.CountsAsync());
    }

    [Theory]
    [InlineData("customer_payments")]
    [InlineData("customer_payment_events")]
    [InlineData("customer_financial_receipts")]
    public async Task UnsupportedFinancialHistoryFailsClosedForDetailHistoryReplayAndBalance(string table)
    {
        await using var s = await Scenario.CreateAsync(database);
        var payment = Guid.NewGuid();
        var record = new CustomerPaymentRecord(s.Debtor, "NPR", 1m, CustomerPaymentChannel.Cash, "receipt");
        Applied(await s.Store.RecordAsync(s.Actor, payment, record, "history", default));
        var constraint = table + "_schema_version_check";
        await database.OwnerSqlAsync($"""
            ALTER TABLE invoices.{table} DROP CONSTRAINT {constraint};
            ALTER TABLE invoices.{table} DISABLE TRIGGER immutable_financial;
            UPDATE invoices.{table} SET schema_version=99 WHERE tenant_id=@tenant;
            ALTER TABLE invoices.{table} ENABLE TRIGGER immutable_financial;
            """, ("tenant", s.Actor.TenantId));
        try
        {
            Assert.Equal(FinancialStatus.HistoryInvalid, (await s.Store.ReadPaymentAsync(s.Actor, payment, default)).Status);
            Assert.Equal(FinancialStatus.HistoryInvalid, (await s.Store.ReadHistoryAsync(s.Actor, payment, null, default)).Status);
            Assert.Equal(FinancialStatus.HistoryInvalid, (await s.Store.RecordAsync(s.Actor, payment, record, "history", default)).Status);
            Assert.Equal(FinancialStatus.HistoryInvalid, (await s.Store.ReadBalanceAsync(s.Actor, s.Debtor, default)).Status);
        }
        finally
        {
            await database.OwnerSqlAsync($"""
                ALTER TABLE invoices.{table} DISABLE TRIGGER immutable_financial;
                UPDATE invoices.{table} SET schema_version=1 WHERE tenant_id=@tenant;
                ALTER TABLE invoices.{table} ENABLE TRIGGER immutable_financial;
                ALTER TABLE invoices.{table} ADD CONSTRAINT {constraint} CHECK(schema_version=1);
                """, ("tenant", s.Actor.TenantId));
        }
    }

    [Theory]
    [InlineData(CustomerPaymentOperation.Confirm, "customer_payment_events")]
    [InlineData(CustomerPaymentOperation.Confirm, "customer_financial_receipts")]
    [InlineData(CustomerPaymentOperation.Reject, "customer_payment_events")]
    [InlineData(CustomerPaymentOperation.Reject, "customer_financial_receipts")]
    [InlineData(CustomerPaymentOperation.Dispute, "customer_payment_events")]
    [InlineData(CustomerPaymentOperation.Dispute, "customer_financial_receipts")]
    [InlineData(CustomerPaymentOperation.Release, "customer_payment_events")]
    [InlineData(CustomerPaymentOperation.Release, "customer_financial_receipts")]
    [InlineData(CustomerPaymentOperation.Reverse, "customer_payment_events")]
    [InlineData(CustomerPaymentOperation.Reverse, "customer_financial_receipts")]
    public async Task FailureAtEachTransitionWritePreservesPriorFactsAndRetryAppliesOnce(CustomerPaymentOperation operation, string table)
    {
        await using var s = await Scenario.CreateAsync(database);
        var invoice = await s.InvoiceAsync(s.Debtor, 20m);
        var payment = operation is CustomerPaymentOperation.Release or CustomerPaymentOperation.Reverse
            ? await s.ConfirmedAsync(10m) : await s.RecordAsync(10m);
        Guid? allocation = null;
        long revision = operation is CustomerPaymentOperation.Release or CustomerPaymentOperation.Reverse ? 2 : 1;
        if (operation == CustomerPaymentOperation.Release)
        {
            allocation = Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 2, invoice: invoice, amount: 3m)).EventId;
            revision = 3;
        }
        var before = await s.CountsAsync();
        var originalPayment = await s.PaymentAsync(payment);
        var originalReceivable = Found(await s.Store.ReadReceivableAsync(s.Actor, invoice, default));
        var trigger = "fail_" + Guid.NewGuid().ToString("N");
        await database.OwnerSqlAsync($"""
            CREATE FUNCTION invoices.{trigger}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN IF NEW.tenant_id='{s.Actor.TenantId:D}'::uuid THEN RAISE EXCEPTION 'Synthetic transition failure'; END IF; RETURN NEW; END; $body$;
            CREATE TRIGGER {trigger} BEFORE INSERT ON invoices.{table} FOR EACH ROW EXECUTE FUNCTION invoices.{trigger}();
            """);
        Task<FinancialResult<FinancialCommandReceipt>> Attempt() => s.MutateAsync(payment, operation, revision, "transition evidence",
            allocation: allocation, actor: operation == CustomerPaymentOperation.Confirm ? s.Reconciler : s.Actor, key: "retry-transition");
        try
        {
            Assert.Equal(FinancialStatus.Unavailable, (await Attempt()).Status);
            Assert.Equal(before, await s.CountsAsync());
            Assert.Equal(originalPayment, await s.PaymentAsync(payment));
            Assert.Equal(originalReceivable, Found(await s.Store.ReadReceivableAsync(s.Actor, invoice, default)));
        }
        finally { await database.OwnerSqlAsync($"DROP TRIGGER {trigger} ON invoices.{table}; DROP FUNCTION invoices.{trigger}();"); }
        var applied = Applied(await Attempt());
        var replay = await Attempt();
        Assert.Equal(FinancialStatus.Replayed, replay.Status);
        Assert.Equal(applied, replay.Value);
    }

    [Fact]
    public async Task MinimumAndMaximumExactAmountsSurviveAndDirectDatabaseRejectsExcessScaleCurrencyAndRange()
    {
        await using var s = await Scenario.CreateAsync(database);
        foreach (var amount in new[] { .0001m, CustomerSettlementRules.MaximumAmount })
        {
            var payment = await s.RecordAsync(amount);
            Assert.Equal(amount, (await s.PaymentAsync(payment)).Amount);
        }
        foreach (var (currency, amount) in new[] { ("USD", 1m), ("NPR", 1.00001m), ("NPR", 0m), ("NPR", 1000000000000000m) })
        {
            await using var connection = await s.Source.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, s.Actor.TenantId);
            await using var command = new NpgsqlCommand("""
                INSERT INTO invoices.customer_payments VALUES(@tenant,@payment,1,@debtor,@currency,@amount,1,'evidence',NULL,@actor,clock_timestamp(),1)
                """, connection, transaction);
            command.Parameters.AddWithValue("tenant", s.Actor.TenantId); command.Parameters.AddWithValue("payment", Guid.NewGuid());
            command.Parameters.AddWithValue("debtor", s.Debtor.Id); command.Parameters.AddWithValue("actor", s.Actor.AccountId);
            command.Parameters.AddWithValue("currency", currency); command.Parameters.AddWithValue("amount", amount);
            Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
        Assert.Equal(new long[] { 0, 2, 2, 2 }, await s.CountsAsync());
    }

    [Theory]
    [InlineData(CustomerPaymentOperation.Confirm, FinancialCapability.Reconcile)]
    [InlineData(CustomerPaymentOperation.Reject, FinancialCapability.Reconcile)]
    [InlineData(CustomerPaymentOperation.Dispute, FinancialCapability.Reconcile)]
    [InlineData(CustomerPaymentOperation.Allocate, FinancialCapability.Allocate)]
    [InlineData(CustomerPaymentOperation.Release, FinancialCapability.Correct)]
    [InlineData(CustomerPaymentOperation.Reverse, FinancialCapability.Correct)]
    public async Task EachMutationRequiresItsOwnCapabilityOnFirstExecutionAndRetainedReplay(CustomerPaymentOperation operation, FinancialCapability capability)
    {
        await using var s = await Scenario.CreateAsync(database);
        var invoice = await s.InvoiceAsync(s.Debtor, 10m);
        var needsConfirmed = operation is CustomerPaymentOperation.Allocate or CustomerPaymentOperation.Release or CustomerPaymentOperation.Reverse;
        var payment = needsConfirmed ? await s.ConfirmedAsync(10m) : await s.RecordAsync(10m);
        long revision = needsConfirmed ? 2 : 1;
        Guid? allocation = null;
        if (operation == CustomerPaymentOperation.Release)
        {
            allocation = Applied(await s.MutateAsync(payment, CustomerPaymentOperation.Allocate, 2, invoice: invoice, amount: 2m)).EventId;
            revision = 3;
        }
        Task<FinancialResult<FinancialCommandReceipt>> Attempt() => s.MutateAsync(payment, operation, revision,
            operation == CustomerPaymentOperation.Allocate ? null : "independent evidence",
            operation == CustomerPaymentOperation.Allocate ? invoice : null, operation == CustomerPaymentOperation.Allocate ? 1m : null,
            allocation, operation == CustomerPaymentOperation.Confirm ? s.Reconciler : s.Actor, "protected-transition");
        var before = await s.CountsAsync();
        s.Ports.BlockedCapability = capability;
        Assert.Equal(FinancialStatus.Denied, (await Attempt()).Status);
        Assert.Equal(before, await s.CountsAsync());
        s.Ports.BlockedCapability = null;
        Applied(await Attempt());
        s.Ports.BlockedCapability = capability;
        var denied = await Attempt();
        Assert.Equal(FinancialStatus.Denied, denied.Status); Assert.Null(denied.Value);
    }

    [Fact]
    public async Task ConcurrentSameCallerKeyRetainsOnePaymentWhileOtherCallerHasIndependentReceiptScope()
    {
        await using var s = await Scenario.CreateAsync(database);
        var payment = Guid.NewGuid();
        var record = new CustomerPaymentRecord(s.Debtor, "NPR", 1.2345m, CustomerPaymentChannel.Cash, " receipt ");
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => s.Store.RecordAsync(s.Actor, payment, record, "same", default)));
        Assert.Single(outcomes, x => x.Status == FinancialStatus.Applied);
        Assert.Equal(5, outcomes.Count(x => x.Status == FinancialStatus.Replayed));
        Assert.Single(outcomes.Select(x => x.Value).Distinct());
        Assert.Equal(FinancialStatus.IdempotencyConflict, (await s.Store.RecordAsync(s.Actor, payment, record with { Evidence = "changed" }, "same", default)).Status);
        Applied(await s.Store.RecordAsync(s.Reconciler, Guid.NewGuid(), record, "same", default));
        Assert.Equal(new long[] { 0, 2, 2, 2 }, await s.CountsAsync());
    }

    [Fact]
    public async Task CorruptRecognizedDebtorCannotFallBackToCustomersOrAcceptNewMoney()
    {
        await using var s = await Scenario.CreateAsync(database);
        var invoice = await s.InvoiceAsync(s.Debtor, 1m);
        await database.OwnerSqlAsync("""
            ALTER TABLE invoices.receivables DISABLE TRIGGER immutable_financial;
            UPDATE invoices.receivables SET original_amount=2 WHERE tenant_id=@tenant;
            ALTER TABLE invoices.receivables ENABLE TRIGGER immutable_financial;
            """, ("tenant", s.Actor.TenantId));
        try
        {
            Assert.Equal(FinancialStatus.HistoryInvalid, (await s.Store.ReadReceivableAsync(s.Actor, invoice, default)).Status);
            Assert.Equal(FinancialStatus.HistoryInvalid, (await s.Store.ReadBalanceAsync(s.Actor, s.Debtor, default)).Status);
            var record = new CustomerPaymentRecord(s.Debtor, "NPR", 1m, CustomerPaymentChannel.Cash, "new money");
            var invalid = await s.Store.RecordAsync(s.Actor, Guid.NewGuid(), record, "corrupt-debtor", default);
            Assert.Equal(FinancialStatus.HistoryInvalid, invalid.Status);
            Assert.Null(invalid.Value);
            Assert.Equal(new long[] { 1, 0, 0, 0 }, await s.CountsAsync());
        }
        finally
        {
            await database.OwnerSqlAsync("""
                ALTER TABLE invoices.receivables DISABLE TRIGGER immutable_financial;
                UPDATE invoices.receivables SET original_amount=1 WHERE tenant_id=@tenant;
                ALTER TABLE invoices.receivables ENABLE TRIGGER immutable_financial;
                """, ("tenant", s.Actor.TenantId));
        }
    }

    [Theory]
    [InlineData(FinancialAccess.Denied)]
    [InlineData(FinancialAccess.Unavailable)]
    public async Task AuthorityRevokedDuringBankReferenceIndexWaitHidesConflictAfterTransactionDisposal(FinancialAccess access)
    {
        var name = Guid.NewGuid().ToString("N");
        await using var s = await Scenario.CreateAsync(database, name);
        var firstPayment = Guid.NewGuid(); var firstEvent = Guid.NewGuid();
        var record = new CustomerPaymentRecord(s.Debtor, "NPR", 1m, CustomerPaymentChannel.Bank, "held evidence", "held-reference");
        await using var blocker = await s.Source.OpenConnectionAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await InvoiceDatabaseFixture.SetTenantAsync(blocker, transaction, s.Actor.TenantId);
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO invoices.customer_payments VALUES(@tenant,@payment,1,@debtor,'NPR',1,2,'held evidence','held-reference',@actor,clock_timestamp(),1);
            INSERT INTO invoices.customer_payment_events
                SELECT @tenant,@event,@payment,1,1,1,@actor,recorded_at,47,NULL,NULL,NULL,NULL,1
                FROM invoices.customer_payments WHERE tenant_id=@tenant AND payment_id=@payment;
            INSERT INTO invoices.customer_financial_receipts VALUES(@tenant,@actor,'held',@fingerprint,@payment,@event,1);
            """, blocker, transaction))
        {
            insert.Parameters.AddWithValue("tenant", s.Actor.TenantId); insert.Parameters.AddWithValue("payment", firstPayment);
            insert.Parameters.AddWithValue("debtor", s.Debtor.Id); insert.Parameters.AddWithValue("actor", s.Actor.AccountId);
            insert.Parameters.AddWithValue("event", firstEvent);
            insert.Parameters.AddWithValue("fingerprint", CustomerSettlementRules.Fingerprint(firstPayment, record));
            await insert.ExecuteNonQueryAsync();
        }
        var pending = s.Store.RecordAsync(s.Actor, Guid.NewGuid(), record with { Amount = 2m }, "waiter", default);
        try
        {
            await WaitForIndexWaitAsync(name);
            s.Ports.BlockedCapability = FinancialCapability.Record;
            s.Ports.BlockedAccess = access;
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(access == FinancialAccess.Denied ? FinancialStatus.Denied : FinancialStatus.Unavailable, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(new long[] { 0, 1, 1, 1 }, await s.CountsAsync());
    }

    private async Task WaitForIndexWaitAsync(string name)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid
                    WHERE a.application_name=@name AND l.locktype='transactionid' AND NOT l.granted)
                """, connection);
            command.Parameters.AddWithValue("name", name);
            if ((bool)(await command.ExecuteScalarAsync(timeout.Token))!) return;
        }
    }

    private static FinancialCommandReceipt Applied(FinancialResult<FinancialCommandReceipt> result)
    {
        Assert.Equal(FinancialStatus.Applied, result.Status);
        return Assert.IsType<FinancialCommandReceipt>(result.Value);
    }

    private static T Found<T>(FinancialResult<T> result) where T : class
    {
        Assert.Equal(FinancialStatus.Found, result.Status);
        return Assert.IsType<T>(result.Value);
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string table, NpgsqlTransaction? transaction = null)
    {
        Assert.Contains(table, FinancialTables);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM invoices.{table}", connection, transaction);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<object?> ScalarAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync();
        return result is DBNull ? null : result;
    }

    private static async Task SqlAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    // Retained SQL seed honors issue/recognition completeness, arithmetic and semantic fingerprints.
    private static async Task<Guid> SeedInvoiceAsync(string connectionString, Guid tenant, Guid actor, FinancialDebtor debtor,
        decimal amount, bool recognize, Guid? organization = null)
    {
        var invoice = Guid.NewGuid(); var order = Guid.NewGuid();
        var anchor = debtor.Kind == InvoiceDebtorKind.Organization ? debtor.Id : organization ?? Guid.NewGuid();
        var debtorJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            kind = (int)debtor.Kind,
            organizationId = debtor.Kind == InvoiceDebtorKind.Individual ? (Guid?)null : anchor,
            organizationDisplayName = debtor.Kind == InvoiceDebtorKind.Individual ? null : "Frozen organization",
            programId = debtor.Kind == InvoiceDebtorKind.Program ? (Guid?)debtor.Id : null,
            programDisplayName = debtor.Kind == InvoiceDebtorKind.Program ? "Frozen independent program" : null,
            individualId = debtor.Kind == InvoiceDebtorKind.Individual ? (Guid?)debtor.Id : null,
            observedRevision = debtor.Kind == InvoiceDebtorKind.Individual ? (long?)7 : null,
            displayName = debtor.Kind == InvoiceDebtorKind.Individual ? "Frozen inactive individual" : null,
        });
        var fingerprint = InvoiceIssueIntent.Fingerprint(new(order, 1, debtor.Kind switch
        {
            InvoiceDebtorKind.Program => new InvoiceDebtorSelection.AttributedProgram(),
            InvoiceDebtorKind.Individual => new InvoiceDebtorSelection.Individual(debtor.Id),
            _ => new InvoiceDebtorSelection.DefaultOrganization(),
        }, "seed"));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, tenant);
        await using var command = new NpgsqlCommand("""
            INSERT INTO invoices.organization_ordinals VALUES(@tenant,@anchor,1)
                ON CONFLICT(tenant_id,organization_id) DO UPDATE SET last_ordinal=invoices.organization_ordinals.last_ordinal+1;
            INSERT INTO invoices.issued_invoices
                SELECT @tenant,@invoice,@order,1,@anchor,@program,last_ordinal,'NPR',@amount,1,@debtor::jsonb,@actor,
                    '2025-01-02T03:04:05Z'::timestamptz,'2025-01-02'::date,1
                FROM invoices.organization_ordinals WHERE tenant_id=@tenant AND organization_id=@anchor;
            INSERT INTO invoices.issued_lines VALUES(@tenant,@invoice,1,'Synthetic retained service',1,'PCS',@amount,@amount);
            INSERT INTO invoices.issue_receipts VALUES(@tenant,@actor,@key,@fingerprint,@invoice,
                jsonb_build_object('schemaVersion',1,'invoiceId',@invoice::text));
            """ + (recognize ? """
            INSERT INTO invoices.receivables VALUES(@tenant,@invoice,@kind,@debtorId,'NPR',@amount,'2025-01-02'::date,clock_timestamp(),1);
            """ : ""), connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("anchor", anchor);
        command.Parameters.AddWithValue("invoice", invoice); command.Parameters.AddWithValue("order", order);
        command.Parameters.AddWithValue("program", NpgsqlTypes.NpgsqlDbType.Uuid, debtor.Kind == InvoiceDebtorKind.Program ? debtor.Id : DBNull.Value);
        command.Parameters.AddWithValue("amount", amount); command.Parameters.AddWithValue("debtor", debtorJson);
        command.Parameters.AddWithValue("actor", actor); command.Parameters.AddWithValue("key", invoice.ToString("N"));
        command.Parameters.AddWithValue("fingerprint", fingerprint); command.Parameters.AddWithValue("kind", (int)debtor.Kind);
        command.Parameters.AddWithValue("debtorId", debtor.Id);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return invoice;
    }

    private sealed class Scenario(InvoiceDatabaseFixture database, NpgsqlDataSource source, TenantContext actor, TenantContext reconciler) : IAsyncDisposable
    {
        public NpgsqlDataSource Source { get; } = source;
        public TenantContext Actor { get; } = actor;
        public TenantContext Reconciler { get; } = reconciler;
        public FinancialDebtor Debtor { get; } = new(InvoiceDebtorKind.Organization, Guid.NewGuid());
        public ControlledPorts Ports { get; } = new();
        public PostgresCustomerSettlementStore Store => NewStore(Source);
        public PostgresCustomerSettlementStore NewStore(NpgsqlDataSource replacement) => new(replacement, Ports, Ports, Ports);
        public Task<Guid> InvoiceAsync(FinancialDebtor debtor, decimal amount) => SeedInvoiceAsync(database.ConnectionString, Actor.TenantId, Actor.AccountId, debtor, amount, true, Debtor.Id);
        public async Task<Guid> RecordAsync(decimal amount, FinancialDebtor? debtor = null)
        {
            var payment = Guid.NewGuid();
            Applied(await Store.RecordAsync(Actor, payment, new(debtor ?? Debtor, "NPR", amount, CustomerPaymentChannel.Cash, "Synthetic receipt"), payment.ToString("N"), default));
            return payment;
        }
        public async Task<Guid> ConfirmedAsync(decimal amount, FinancialDebtor? debtor = null)
        {
            var payment = await RecordAsync(amount, debtor);
            Applied(await MutateAsync(payment, CustomerPaymentOperation.Confirm, 1, "received funds", actor: Reconciler));
            return payment;
        }
        public Task<FinancialResult<FinancialCommandReceipt>> MutateAsync(Guid payment, CustomerPaymentOperation operation, long revision,
            string? evidence = null, Guid? invoice = null, decimal? amount = null, Guid? allocation = null, TenantContext? actor = null, string? key = null) =>
            Store.ExecuteAsync(actor ?? Actor, payment, new(operation, revision, evidence, invoice, amount, allocation), key ?? Guid.NewGuid().ToString("N"), default);
        public async Task<CustomerPaymentSnapshot> PaymentAsync(Guid payment) => Found(await Store.ReadPaymentAsync(Actor, payment, default));
        public async Task<long[]> CountsAsync()
        {
            var counts = new List<long>();
            foreach (var table in FinancialTables)
                counts.Add((long)(await ScalarAsync(database.ConnectionString, $"SELECT count(*) FROM invoices.{table} WHERE tenant_id=@tenant", ("tenant", Actor.TenantId)))!);
            return counts.ToArray();
        }
        public static async Task<Scenario> CreateAsync(InvoiceDatabaseFixture database, string? applicationName = null)
        {
            var tenant = Guid.NewGuid();
            var resolver = new ResolveTenantContext(new Membership());
            var actor = (await resolver.ExecuteAsync(Guid.NewGuid(), tenant, default))!;
            var reconciler = (await resolver.ExecuteAsync(Guid.NewGuid(), tenant, default))!;
            return new(database, database.CreateRuntimeSource(4, applicationName), actor, reconciler);
        }
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }

    // These ports control authority only; all financial state, locks and transactions use PostgreSQL.
    private sealed class ControlledPorts : IFinancialAuthorityReader, IFinancialTransactionalAuthorityReader, IFinancialDebtorReader
    {
        public FinancialAccess Transactional { get; set; } = FinancialAccess.Allowed;
        public FinancialCapability? BlockedCapability { get; set; }
        public FinancialAccess BlockedAccess { get; set; } = FinancialAccess.Denied;
        public bool DebtorExists { get; set; } = true;
        public Task<FinancialAuthority> CheckAsync(TenantContext context, FinancialCapability permission, CancellationToken cancellationToken) =>
            Task.FromResult(new FinancialAuthority(permission == BlockedCapability ? BlockedAccess : FinancialAccess.Allowed, 47));
        public async Task<FinancialAuthority> CheckAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context,
            FinancialCapability permission, CancellationToken cancellationToken)
        {
            await AssertTenantAsync(connection, transaction, context, cancellationToken);
            return new(permission == BlockedCapability ? BlockedAccess : Transactional, 47);
        }
        public async Task<bool> ExistsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context, FinancialDebtor debtor, CancellationToken cancellationToken)
        {
            await AssertTenantAsync(connection, transaction, context, cancellationToken);
            return DebtorExists;
        }
        private static async Task AssertTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context, CancellationToken ct)
        {
            Assert.Same(connection, transaction.Connection);
            await using var command = new NpgsqlCommand("SELECT current_setting('app.current_tenant'),current_user", connection, transaction);
            await using var reader = await command.ExecuteReaderAsync(ct);
            Assert.True(await reader.ReadAsync(ct));
            Assert.Equal(context.TenantId.ToString("D"), reader.GetString(0));
            Assert.Equal("invoice_runtime", reader.GetString(1));
        }
    }

    private sealed class Membership : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TenantMembership>>([]);
        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class MigrationDatabase(string connectionString, string ownerConnectionString, string name) : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;
        public static async Task<MigrationDatabase> CreateAsync(InvoiceDatabaseFixture fixture)
        {
            var name = "settlement_" + Guid.NewGuid().ToString("N");
            await SqlAsync(fixture.ConnectionString, $"CREATE DATABASE {name}");
            var isolated = new MigrationDatabase(new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name, Pooling = false }.ConnectionString, fixture.ConnectionString, name);
            await using var context = InvoicesPostgresRegistration.CreateContext(isolated.ConnectionString);
            await context.GetService<IMigrator>().MigrateAsync("20261010120000_FirstInvoiceIssuance");
            return isolated;
        }
        public async Task MigrateAsync()
        {
            await using var context = InvoicesPostgresRegistration.CreateContext(ConnectionString);
            await context.Database.MigrateAsync();
        }
        public async ValueTask DisposeAsync() => await SqlAsync(ownerConnectionString, $"DROP DATABASE {name} WITH(FORCE)");
    }
}
