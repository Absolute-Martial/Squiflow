using System.Text.Json;
using Application.Customers;
using Application.Invoices.Postgres;
using Application.Tenancy;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.Invoices.Postgres.Tests;

public sealed class InvoiceIssuancePostgresTests(InvoiceDatabaseFixture database) : IClassFixture<InvoiceDatabaseFixture>
{
    [Fact]
    public async Task FullOrderIssueFreezesArithmeticDebtorUtcDateAndReceiptAcrossRestart()
    {
        await using var scenario = await Scenario.CreateAsync(database, lines:
        [new(1, "round even down", .5m, "PCS", .0001m, 0m), new(2, "round even up", 1.5m, "PCS", .0001m, .0002m), new(3, "free", 1m, "PCS", 0m, 0m)]);
        var first = await scenario.CommitAsync("one");
        Assert.Equal(InvoiceIssueCommitStatus.Issued, first.Status);
        var invoice = Assert.IsType<InvoiceIssuedFacts>(first.Invoice);
        Assert.Equal(.0002m, invoice.Total);
        Assert.Equal(1, invoice.OrganizationOrdinal);
        Assert.Equal(TimeSpan.Zero, invoice.IssuedAt.Offset);
        Assert.Equal(DateOnly.FromDateTime(invoice.IssuedAt.UtcDateTime), invoice.BusinessDate);
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT debtor_kind,debtor_id,original_amount,invoice_date,recognized_at FROM invoices.receivables WHERE tenant_id=@tenant AND invoice_id=@invoice", connection);
            command.Parameters.AddWithValue("tenant", scenario.Context.TenantId); command.Parameters.AddWithValue("invoice", invoice.InvoiceId);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal((int)InvoiceDebtorKind.Organization, reader.GetInt32(0));
            Assert.Equal(scenario.OrganizationId, reader.GetGuid(1));
            Assert.Equal(invoice.Total, reader.GetDecimal(2));
            Assert.Equal(invoice.BusinessDate, reader.GetFieldValue<DateOnly>(3));
            Assert.True(reader.GetDateTime(4) >= invoice.IssuedAt.UtcDateTime);
            Assert.False(await reader.ReadAsync());
        }
        Assert.Equal(new long[] { 1, 1, 3, 1 }, await database.CountsAsync(scenario.Context.TenantId));
        scenario.Source.Facts = null;
        scenario.Source.DebtorStatus = InvoiceIssueDebtorObservationStatus.Unavailable;
        var replay = await scenario.CommitAsync("one");
        Assert.Equal(InvoiceIssueCommitStatus.Replayed, replay.Status);
        Assert.Equal(JsonSerializer.Serialize(invoice), JsonSerializer.Serialize(replay.Invoice));
        Assert.Equal(InvoiceReadStatus.Found, (await scenario.Store.FindByOrderAsync(scenario.Context, invoice.SourceOrderId, default)).Status);
        Assert.Equal(JsonSerializer.Serialize(invoice), JsonSerializer.Serialize((await scenario.Store.FindByIdAsync(scenario.Context, invoice.InvoiceId, default)).Invoice));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SameCallerKeyRacesRetainOneInvoiceAndUseOnlyTheHeldConnection(int poolSize)
    {
        await using var scenario = await Scenario.CreateAsync(database, poolSize: poolSize);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => scenario.CommitAsync("same-key")));
        Assert.Single(outcomes, outcome => outcome.Status == InvoiceIssueCommitStatus.Issued);
        Assert.Equal(7, outcomes.Count(outcome => outcome.Status == InvoiceIssueCommitStatus.Replayed));
        Assert.Single(outcomes.Select(outcome => outcome.Invoice!.InvoiceId).Distinct());
        Assert.Equal(new long[] { 1, 1, 1, 1 }, await database.CountsAsync(scenario.Context.TenantId));
        Assert.True(scenario.Source.HeldTransactionReads > 0);
    }

    [Fact]
    public async Task ChangedIntentSameKeyConflictsBeforeOrderAllocationAndSeparateKeysCannotInvoiceTwice()
    {
        await using var first = await Scenario.CreateAsync(database);
        await using var other = await Scenario.CreateAsync(database, tenantId: first.Context.TenantId,
            actorId: first.Context.AccountId, organizationId: first.OrganizationId);
        var race = await Task.WhenAll(first.CommitAsync("shared"), other.CommitAsync("shared"));
        Assert.Single(race, result => result.Status == InvoiceIssueCommitStatus.Issued);
        Assert.Single(race, result => result.Status == InvoiceIssueCommitStatus.IdempotencyKeyConflict);
        var winner = race[0].Status == InvoiceIssueCommitStatus.Issued ? first : other;
        var duplicate = await winner.CommitAsync("different-key");
        Assert.Equal(InvoiceIssueCommitStatus.OrderAlreadyInvoiced, duplicate.Status);
        Assert.Null(duplicate.Invoice);
        await using var anotherActor = await Scenario.CreateAsync(database, tenantId: winner.Context.TenantId,
            organizationId: winner.OrganizationId, orderId: winner.Candidate.SourceOrderId);
        Assert.Equal(InvoiceIssueCommitStatus.OrderAlreadyInvoiced, (await anotherActor.CommitAsync("shared")).Status);
        Assert.Equal(new long[] { 1, 1, 1, 1 }, await database.CountsAsync(first.Context.TenantId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentKeysAndActorsRacingTheSameOrderRetainOnlyOneFullInvoice(bool differentActor)
    {
        await using var first = await Scenario.CreateAsync(database);
        await using var other = await Scenario.CreateAsync(database, tenantId: first.Context.TenantId,
            actorId: differentActor ? null : first.Context.AccountId, organizationId: first.OrganizationId, orderId: first.Candidate.SourceOrderId);
        var outcomes = await Task.WhenAll(first.CommitAsync("first-key"), other.CommitAsync("second-key"));
        Assert.Single(outcomes, outcome => outcome.Status == InvoiceIssueCommitStatus.Issued);
        Assert.Null(Assert.Single(outcomes, outcome => outcome.Status == InvoiceIssueCommitStatus.OrderAlreadyInvoiced).Invoice);
        Assert.Equal(new long[] { 1, 1, 1, 1 }, await database.CountsAsync(first.Context.TenantId));
    }

    [Fact]
    public async Task ConcurrentOrdersAllocateIncreasingOrganizationOrdinalsAndDebtorDoesNotChangeAnchor()
    {
        await using var first = await Scenario.CreateAsync(database);
        var others = new List<Scenario>();
        try
        {
            for (var index = 0; index < 7; index++)
                others.Add(await Scenario.CreateAsync(database, tenantId: first.Context.TenantId, organizationId: first.OrganizationId,
                    individual: index == 0));
            var issued = await Task.WhenAll(others.Prepend(first).Select(scenario => scenario.CommitAsync("issue")));
            Assert.All(issued, result => Assert.Equal(InvoiceIssueCommitStatus.Issued, result.Status));
            Assert.Equal(Enumerable.Range(1, 8).Select(value => (long)value), issued.Select(result => result.Invoice!.OrganizationOrdinal).Order());
            await using var secondOrganization = await Scenario.CreateAsync(database, tenantId: first.Context.TenantId);
            Assert.Equal(1, (await secondOrganization.CommitAsync("issue")).Invoice!.OrganizationOrdinal);
        }
        finally { foreach (var scenario in others) await scenario.DisposeAsync(); }
    }

    [Theory]
    [InlineData("source")]
    [InlineData("debtor")]
    [InlineData("unavailable")]
    public async Task ChangedOrUnavailableSourceObservationCannotRetainAnIssue(string variation)
    {
        await using var scenario = await Scenario.CreateAsync(database, individual: true);
        if (variation == "source")
        {
            var original = scenario.Source.Facts!;
            scenario.Source.Facts = new(original.TenantId, original.OrderId, 99, original.IsCommitted,
                original.CurrencyCode, original.CustomerContext, original.Lines, original.Total);
        }
        else scenario.Source.DebtorStatus = variation == "debtor" ? InvoiceIssueDebtorObservationStatus.Invalid : InvoiceIssueDebtorObservationStatus.Unavailable;
        var rejected = await scenario.CommitAsync("source-race");
        Assert.Equal(variation switch
        {
            "source" => InvoiceIssueCommitStatus.InvalidSource,
            "debtor" => InvoiceIssueCommitStatus.InvalidDebtor,
            _ => InvoiceIssueCommitStatus.Unavailable,
        }, rejected.Status);
        Assert.Null(rejected.Invoice);
        Assert.Equal(new long[] { 0, 0, 0, 0 }, await database.CountsAsync(scenario.Context.TenantId));
    }

    [Fact]
    public async Task IssueReplayAndFrozenReadsRequireTheirIndependentCurrentAuthority()
    {
        await using var scenario = await Scenario.CreateAsync(database);
        var invoice = (await scenario.CommitAsync("protected")).Invoice!;
        scenario.Authority.TransactionalIssue = InvoiceIssueAuthorityStatus.Denied;
        Assert.Equal(InvoiceIssueCommitStatus.NotAuthorized, (await scenario.CommitAsync("protected")).Status);
        Assert.Equal(InvoiceIssueReceiptStatus.NotAuthorized, (await scenario.Store.FindReceiptAsync(scenario.Context, "protected", scenario.Fingerprint, default)).Status);
        Assert.Equal(InvoiceReadStatus.Found, (await scenario.Store.FindByIdAsync(scenario.Context, invoice.InvoiceId, default)).Status);
        scenario.Authority.TransactionalView = InvoiceIssueAuthorityStatus.Denied;
        Assert.Equal(InvoiceReadStatus.NotAuthorized, (await scenario.Store.FindByOrderAsync(scenario.Context, invoice.SourceOrderId, default)).Status);
        scenario.Authority.TransactionalIssue = InvoiceIssueAuthorityStatus.Unavailable;
        Assert.Equal(InvoiceIssueCommitStatus.Unavailable, (await scenario.CommitAsync("protected")).Status);
        Assert.Equal(new long[] { 1, 1, 1, 1 }, await database.CountsAsync(scenario.Context.TenantId));
    }

    [Fact]
    public async Task AuthorityRevokedWhileWaitingForOrganizationLockIsRecheckedBeforeAnyWrites()
    {
        var name = Guid.NewGuid().ToString("N");
        await using var scenario = await Scenario.CreateAsync(database, applicationName: name);
        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@tenant::text || ':invoices:organization:' || @organization::text,0))", blocker, transaction))
        {
            command.Parameters.AddWithValue("tenant", scenario.Context.TenantId);
            command.Parameters.AddWithValue("organization", scenario.OrganizationId);
            await command.ExecuteNonQueryAsync();
        }
        var pending = scenario.CommitAsync("waiting");
        await database.WaitForAdvisoryWaitAsync(name);
        scenario.Authority.TransactionalIssue = InvoiceIssueAuthorityStatus.Denied;
        await transaction.RollbackAsync();
        Assert.Equal(InvoiceIssueCommitStatus.NotAuthorized, (await pending).Status);
        Assert.Equal(new long[] { 0, 0, 0, 0 }, await database.CountsAsync(scenario.Context.TenantId));
    }

    [Theory]
    [InlineData("issued_invoices")]
    [InlineData("issued_lines")]
    [InlineData("issue_receipts")]
    [InlineData("receivables")]
    public async Task FailureAtEveryRetainedWriteRollsBackOrdinalInvoiceLinesAndReceipt(string table)
    {
        await using var scenario = await Scenario.CreateAsync(database);
        await database.OwnerSqlAsync($"""
            CREATE FUNCTION invoices.test_fail() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN IF NEW.tenant_id='{scenario.Context.TenantId:D}'::uuid THEN RAISE EXCEPTION 'Synthetic invoice write failure'; END IF; RETURN NEW; END; $body$;
            CREATE TRIGGER test_fail BEFORE INSERT ON invoices.{table} FOR EACH ROW EXECUTE FUNCTION invoices.test_fail();
            """);
        try
        {
            Assert.Equal(InvoiceIssueCommitStatus.Unavailable, (await scenario.CommitAsync("retry-write")).Status);
            Assert.Equal(new long[] { 0, 0, 0, 0 }, await database.CountsAsync(scenario.Context.TenantId));
        }
        finally { await database.OwnerSqlAsync($"DROP TRIGGER test_fail ON invoices.{table}; DROP FUNCTION invoices.test_fail();"); }
        Assert.Equal(InvoiceIssueCommitStatus.Issued, (await scenario.CommitAsync("retry-write")).Status);
        Assert.Equal(InvoiceIssueCommitStatus.Replayed, (await scenario.CommitAsync("retry-write")).Status);
    }

    [Fact]
    public async Task DeferredCompletenessRejectsHeaderWithoutLinesAndReceiptAndRollsBackCounter()
    {
        await using var scenario = await Scenario.CreateAsync(database);
        var invoice = (await scenario.CommitAsync("original")).Invoice!;
        await using var source = database.CreateRuntimeSource();
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, scenario.Context.TenantId);
        await using var command = new NpgsqlCommand("""
            UPDATE invoices.organization_ordinals SET last_ordinal=2 WHERE tenant_id=@tenant AND organization_id=@organization;
            INSERT INTO invoices.issued_invoices SELECT tenant_id,@new, @order,source_order_revision,organization_id,program_id,2,
                currency_code,total,line_count,debtor,issued_by_account_id,issued_at,business_date,schema_version
            FROM invoices.issued_invoices WHERE tenant_id=@tenant AND invoice_id=@original;
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant", scenario.Context.TenantId);
        command.Parameters.AddWithValue("organization", scenario.OrganizationId);
        command.Parameters.AddWithValue("new", Guid.NewGuid());
        command.Parameters.AddWithValue("order", Guid.NewGuid());
        command.Parameters.AddWithValue("original", invoice.InvoiceId);
        await command.ExecuteNonQueryAsync();
        Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync())).SqlState);
        Assert.Equal(new long[] { 1, 1, 1, 1 }, await database.CountsAsync(scenario.Context.TenantId));
        Assert.Equal(1, (await scenario.Store.FindByIdAsync(scenario.Context, invoice.InvoiceId, default)).Invoice!.OrganizationOrdinal);
    }

    [Fact]
    public async Task ForcedRlsTenantScopedKeysAndPooledTenantResetProtectAllFourTables()
    {
        await using var scenario = await Scenario.CreateAsync(database);
        var invoice = (await scenario.CommitAsync("rls")).Invoice!;
        await using var source = database.CreateRuntimeSource(1);
        await using (var connection = await source.OpenConnectionAsync())
        {
            foreach (var table in InvoiceDatabaseFixture.Tables) Assert.Equal(0L, await VisibleCountAsync(connection, table));
            await using var transaction = await connection.BeginTransactionAsync();
            await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, scenario.Context.TenantId);
            foreach (var table in InvoiceDatabaseFixture.Tables) Assert.Equal(1L, await VisibleCountAsync(connection, table, transaction));
            await transaction.RollbackAsync();
        }
        await using (var connection = await source.OpenConnectionAsync())
        {
            foreach (var table in InvoiceDatabaseFixture.Tables) Assert.Equal(0L, await VisibleCountAsync(connection, table));
            await using var transaction = await connection.BeginTransactionAsync();
            await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, Guid.NewGuid());
            await using var command = new NpgsqlCommand("INSERT INTO invoices.issued_lines VALUES(@tenant,@invoice,1,'cross tenant',1,'PCS',1,1)", connection, transaction);
            command.Parameters.AddWithValue("tenant", Guid.NewGuid());
            command.Parameters.AddWithValue("invoice", invoice.InvoiceId);
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
        await using var other = await Scenario.CreateAsync(database);
        Assert.Equal(InvoiceReadStatus.Missing, (await other.Store.FindByIdAsync(other.Context, invoice.InvoiceId, default)).Status);
        await using (var connection = await source.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, other.Context.TenantId);
            await using var command = new NpgsqlCommand("INSERT INTO invoices.issued_lines VALUES(@tenant,@invoice,1,'cross tenant',1,'PCS',1,1)", connection, transaction);
            command.Parameters.AddWithValue("tenant", other.Context.TenantId);
            command.Parameters.AddWithValue("invoice", invoice.InvoiceId);
            Assert.Equal("23503", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
    }

    [Fact]
    public async Task RestrictedRuntimeCannotRewriteTruncateOrCreateSchemaObjects()
    {
        await using var scenario = await Scenario.CreateAsync(database);
        await scenario.CommitAsync("immutable");
        await using var source = database.CreateRuntimeSource();
        await using var connection = await source.OpenConnectionAsync();
        foreach (var table in InvoiceDatabaseFixture.Tables)
        {
            foreach (var sql in new[] { $"DELETE FROM invoices.{table}", $"TRUNCATE invoices.{table}",
                table == "organization_ordinals" ? null : $"UPDATE invoices.{table} SET tenant_id=tenant_id" }.OfType<string>())
            {
                await using var command = new NpgsqlCommand(sql, connection);
                Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
            }
        }
        await using var create = new NpgsqlCommand("CREATE TABLE invoices.runtime_object(id integer)", connection);
        Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => create.ExecuteNonQueryAsync())).SqlState);
        await using var transaction = await connection.BeginTransactionAsync();
        await InvoiceDatabaseFixture.SetTenantAsync(connection, transaction, scenario.Context.TenantId);
        await using var decrement = new NpgsqlCommand("UPDATE invoices.organization_ordinals SET last_ordinal=last_ordinal WHERE tenant_id=@tenant", connection, transaction);
        decrement.Parameters.AddWithValue("tenant", scenario.Context.TenantId);
        Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => decrement.ExecuteNonQueryAsync())).SqlState);
    }

    [Theory]
    [InlineData("receipt")]
    [InlineData("debtor")]
    [InlineData("fingerprint")]
    [InlineData("line")]
    [InlineData("ordinal")]
    [InlineData("oversizeDebtor")]
    [InlineData("oversizeReceipt")]
    [InlineData("numericScale")]
    [InlineData("numericOverflow")]
    [InlineData("siblingProgram")]
    public async Task CorruptHistoricalVersionsAndLinkageFailClosedOnReplayAndView(string variation)
    {
        await using var scenario = await Scenario.CreateAsync(database);
        var invoice = (await scenario.CommitAsync("history")).Invoice!;
        var (table, trigger, constraint, mutation, check) = variation switch
        {
            "receipt" => ("issue_receipts", "immutable_receipts", "issue_receipts_response_check", "response=jsonb_set(response,'{schemaVersion}','99')", "invoices.valid_receipt(response)"),
            "debtor" => ("issued_invoices", "immutable_invoices", "issued_invoices_debtor_check", "debtor=jsonb_set(debtor,'{schemaVersion}','99')", "invoices.valid_debtor(debtor)"),
            "fingerprint" => ("issue_receipts", "immutable_receipts", (string?)null, "fingerprint=repeat('a',64)", (string?)null),
            "line" => ("issued_lines", "immutable_lines", (string?)null, "line_total=0", (string?)null),
            "oversizeDebtor" => ("issued_invoices", "immutable_invoices", "issued_invoices_debtor_check", "debtor=jsonb_set(debtor,'{organizationDisplayName}',to_jsonb(repeat(chr(92),5000)))", "invoices.valid_debtor(debtor)"),
            "oversizeReceipt" => ("issue_receipts", "immutable_receipts", "issue_receipts_response_check", "response=response || jsonb_build_object('oversize',repeat(chr(92),5000))", "invoices.valid_receipt(response)"),
            "numericScale" => ("issued_lines", "immutable_lines", "issued_lines_quantity_check", "quantity=1.00001", "quantity>0 AND quantity<=999999999999999.9999 AND scale(quantity)<=4"),
            "numericOverflow" => ("issued_lines", "immutable_lines", "issued_lines_quantity_check", "quantity=1000000000000000000000000000000000", "quantity>0 AND quantity<=999999999999999.9999 AND scale(quantity)<=4"),
            "siblingProgram" => ("issued_invoices", "immutable_invoices", "debtor_program_matches_source", $"program_id='{Guid.NewGuid():D}'::uuid,debtor=debtor || jsonb_build_object('kind',2,'programId','{Guid.NewGuid():D}','programDisplayName','Sibling program')", "(debtor->>'kind')<>'2' OR (program_id IS NOT NULL AND debtor->>'programId'=program_id::text)"),
            _ => ("issued_invoices", "immutable_invoices", (string?)null, "organization_ordinal=99", (string?)null),
        };
        var drop = constraint is null ? string.Empty : $"ALTER TABLE invoices.{table} DROP CONSTRAINT {constraint};";
        var restore = constraint is null ? string.Empty : $"ALTER TABLE invoices.{table} ADD CONSTRAINT {constraint} CHECK({check}) NOT VALID;";
        await database.OwnerSqlAsync($"""
            BEGIN; ALTER TABLE invoices.{table} DISABLE TRIGGER {trigger}; {drop}
            UPDATE invoices.{table} SET {mutation} WHERE tenant_id=@tenant; {restore}
            ALTER TABLE invoices.{table} ENABLE TRIGGER {trigger}; COMMIT;
            """, ("tenant", scenario.Context.TenantId));
        Assert.Equal(InvoiceReadStatus.HistoricalInvalid, (await scenario.Store.FindByIdAsync(scenario.Context, invoice.InvoiceId, default)).Status);
        Assert.Equal(InvoiceIssueReceiptStatus.HistoricalReceiptInvalid, (await scenario.Store.FindReceiptAsync(scenario.Context, "history", scenario.Fingerprint, default)).Status);
        Assert.Null((await scenario.CommitAsync("history")).Invoice);
    }

    [Fact]
    public async Task OneHundredLegalEscapedLinesAndMaximumUnitCodesSurviveNormalizedPersistence()
    {
        var lines = Enumerable.Range(1, 100).Select(position => new InvoiceSourceLine(position,
            new string('\\', 300), .5m, new string('U', 64), .0001m, 0m)).ToArray();
        await using var scenario = await Scenario.CreateAsync(database, lines: lines, organizationName: new string('\\', 200));
        var issued = await scenario.CommitAsync("maximum-legal");
        Assert.Equal(InvoiceIssueCommitStatus.Issued, issued.Status);
        Assert.Equal(100, issued.Invoice!.Lines.Count);
        Assert.Equal(JsonSerializer.Serialize(issued.Invoice), JsonSerializer.Serialize((await scenario.Store.FindByIdAsync(scenario.Context, issued.Invoice.InvoiceId, default)).Invoice));
        Assert.Equal(new long[] { 1, 1, 100, 1 }, await database.CountsAsync(scenario.Context.TenantId));
    }

    [Fact]
    public async Task ExhaustedOrganizationOrdinalReturnsResourceLimitWithoutRetainedEffects()
    {
        await using var first = await Scenario.CreateAsync(database);
        await first.CommitAsync("first");
        await database.OwnerSqlAsync("UPDATE invoices.organization_ordinals SET last_ordinal=9223372036854775807 WHERE tenant_id=@tenant", ("tenant", first.Context.TenantId));
        await using var next = await Scenario.CreateAsync(database, tenantId: first.Context.TenantId, organizationId: first.OrganizationId);
        Assert.Equal(InvoiceIssueCommitStatus.ResourceLimit, (await next.CommitAsync("exhausted")).Status);
        Assert.Equal(new long[] { 1, 1, 1, 1 }, await database.CountsAsync(first.Context.TenantId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackendTerminationAndCallerCancellationBeforeCommitLeaveNoIssue(bool callerCancellation)
    {
        var name = Guid.NewGuid().ToString("N");
        await using var scenario = await Scenario.CreateAsync(database, applicationName: name);
        await database.OwnerSqlAsync($"""
            CREATE FUNCTION invoices.test_block() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN IF NEW.tenant_id='{scenario.Context.TenantId:D}'::uuid THEN PERFORM pg_advisory_xact_lock(782341); END IF; RETURN NEW; END; $body$;
            CREATE TRIGGER test_block BEFORE INSERT ON invoices.issued_lines FOR EACH ROW EXECUTE FUNCTION invoices.test_block();
            """);
        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(782341)", blocker, transaction)) await command.ExecuteNonQueryAsync();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var pending = scenario.CommitAsync("interrupted", cancellation.Token);
            await database.WaitForAdvisoryWaitAsync(name);
            if (callerCancellation)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                await database.OwnerSqlAsync("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name=@name", ("name", name));
                Assert.Equal(InvoiceIssueCommitStatus.Unavailable, (await pending).Status);
            }
            Assert.Equal(new long[] { 0, 0, 0, 0 }, await database.CountsAsync(scenario.Context.TenantId));
        }
        finally
        {
            await transaction.RollbackAsync();
            await database.OwnerSqlAsync("DROP TRIGGER test_block ON invoices.issued_lines; DROP FUNCTION invoices.test_block();");
        }
        Assert.Equal(InvoiceIssueCommitStatus.Issued, (await scenario.CommitAsync("interrupted")).Status);
    }

    [Fact]
    public async Task ActualCommitResponseLossReportsUnknownAndFreshSameKeyRecoversOneInvoice()
    {
        await using var scenario = await Scenario.CreateAsync(database);
        await using var proxy = new CommitResponseLossProxy(database.ConnectionString);
        await using var lossySource = database.CreateRuntimeSource(1, connectionString: proxy.ConnectionString);
        var pending = scenario.NewStore(lossySource).CommitAsync(scenario.Context, scenario.Candidate, "lost-commit", scenario.Fingerprint, default);
        await proxy.CommitCompleted.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(new long[] { 1, 1, 1, 1 }, await database.CountsAsync(scenario.Context.TenantId));
        proxy.DropResponse();
        var unknown = await pending.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(InvoiceIssueCommitStatus.OutcomeUnknown, unknown.Status);
        Assert.Null(unknown.Invoice);
        var recovered = await scenario.CommitAsync("lost-commit");
        Assert.Equal(InvoiceIssueCommitStatus.Replayed, recovered.Status);
        Assert.Equal(1, recovered.Invoice!.OrganizationOrdinal);
        Assert.Equal(new long[] { 1, 1, 1, 1 }, await database.CountsAsync(scenario.Context.TenantId));
    }

    [Fact]
    public async Task MigrationModelAndRetainedHistoryDestructiveRollbackPolicyAgree()
    {
        await using var context = InvoicesPostgresRegistration.CreateContext(database.ConnectionString);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(["20261010120000_FirstInvoiceIssuance", "20261015120000_CustomerSettlement"], await context.Database.GetAppliedMigrationsAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => context.GetService<IMigrator>().MigrateAsync("0"));
    }

    private static async Task<long> VisibleCountAsync(NpgsqlConnection connection, string table, NpgsqlTransaction? transaction = null)
    {
        Assert.Contains(table, InvoiceDatabaseFixture.Tables);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM invoices.{table}", connection, transaction);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private sealed class Scenario(NpgsqlDataSource dataSource, TenantContext context, InvoiceIssueCandidate candidate, SourceReader source,
        Authority authority) : IAsyncDisposable
    {
        public TenantContext Context { get; } = context;
        public InvoiceIssueCandidate Candidate { get; } = candidate;
        public SourceReader Source { get; } = source;
        public Authority Authority { get; } = authority;
        public Guid OrganizationId => Candidate.SourceCustomerContext!.OrganizationId;
        public string Fingerprint => InvoiceIssueIntent.Fingerprint(new(Candidate.SourceOrderId, Candidate.SourceOrderRevision,
            Candidate.Debtor is InvoiceDebtorFacts.Individual individual ? new InvoiceDebtorSelection.Individual(individual.IndividualId) : null, "fixture"));
        public PostgresInvoiceIssueStore Store => NewStore();
        public PostgresInvoiceIssueStore NewStore(NpgsqlDataSource? replacement = null) => new(replacement ?? dataSource, Authority, Authority, Authority, Source);
        public Task<InvoiceIssueCommitResult> CommitAsync(string key, CancellationToken ct = default) => Store.CommitAsync(Context, Candidate, key, Fingerprint, ct);
        public ValueTask DisposeAsync() => dataSource.DisposeAsync();

        public static async Task<Scenario> CreateAsync(InvoiceDatabaseFixture database, Guid? tenantId = null, Guid? actorId = null,
            Guid? organizationId = null, Guid? orderId = null, bool individual = false, int poolSize = 2,
            string? applicationName = null, InvoiceSourceLine[]? lines = null, string organizationName = "Synthetic organization")
        {
            var account = actorId ?? Guid.NewGuid();
            var context = (await new ResolveTenantContext(new Membership()).ExecuteAsync(account, tenantId ?? Guid.NewGuid(), default))!;
            var organization = organizationId ?? Guid.NewGuid();
            var issuedLines = lines ?? [new(1, "Synthetic service", 1m, "PCS", 12m, 12m)];
            var facts = new InvoiceSourceOrderFacts(context.TenantId, orderId ?? Guid.NewGuid(), 7, true, "NPR", new(organization, null), issuedLines, issuedLines.Sum(line => line.LineTotal));
            var source = new SourceReader { Facts = facts };
            var authority = new Authority();
            var customers = new Customers(context, organization, organizationName);
            var capture = new CandidateCapture();
            var useCase = new IssueInvoice(authority, capture, source, new GetCustomerOrganization(customers),
                new GetCustomerProgram(customers), new GetCustomerIndividual(customers));
            var request = new IssueInvoiceRequest(facts.OrderId, 7, individual ? new InvoiceDebtorSelection.Individual(customers.IndividualId) : null, "capture");
            Assert.Equal(InvoiceIssueStatus.DependencyUnavailable, (await useCase.ExecuteAsync(context, request, default)).Status);
            return new(database.CreateRuntimeSource(poolSize, applicationName), context, capture.Candidate!, source, authority);
        }
    }

    private sealed class SourceReader : IInvoiceSourceOrderReader, IInvoiceIssueTransactionalSourceReader
    {
        public InvoiceSourceOrderFacts? Facts { get; set; }
        public InvoiceIssueDebtorObservationStatus DebtorStatus { get; set; } = InvoiceIssueDebtorObservationStatus.Valid;
        public int HeldTransactionReads { get; private set; }
        public Task<InvoiceSourceOrderReadResult> ReadAsync(TenantContext context, Guid orderId, CancellationToken ct) =>
            Task.FromResult(Facts is null ? new InvoiceSourceOrderReadResult(InvoiceSourceOrderReadStatus.Unavailable)
                : new(InvoiceSourceOrderReadStatus.Found, Facts));
        public Task<InvoiceSourceOrderReadResult> ReadOrderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            TenantContext context, Guid orderId, CancellationToken ct)
        {
            Assert.Same(connection, transaction.Connection);
            HeldTransactionReads++;
            return ReadAsync(context, orderId, ct);
        }
        public Task<InvoiceIssueDebtorObservationStatus> ValidateDebtorAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            TenantContext context, InvoiceIssueCandidate candidate, CancellationToken ct)
        {
            Assert.Same(connection, transaction.Connection);
            return Task.FromResult(DebtorStatus);
        }
    }

    private sealed class Authority : IInvoiceIssueAuthority, IInvoiceViewAuthority, IInvoiceIssueTransactionalAuthority
    {
        public InvoiceIssueAuthorityStatus TransactionalIssue { get; set; } = InvoiceIssueAuthorityStatus.Allowed;
        public InvoiceIssueAuthorityStatus TransactionalView { get; set; } = InvoiceIssueAuthorityStatus.Allowed;
        public Task<InvoiceIssueAuthorityStatus> CheckAsync(TenantContext context, CancellationToken ct) => Task.FromResult(InvoiceIssueAuthorityStatus.Allowed);
        public Task<InvoiceIssueAuthorityStatus> CheckAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context, CancellationToken ct)
        { Assert.Same(connection, transaction.Connection); return Task.FromResult(TransactionalIssue); }
        public Task<InvoiceIssueAuthorityStatus> CheckViewAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context, CancellationToken ct)
        { Assert.Same(connection, transaction.Connection); return Task.FromResult(TransactionalView); }
    }

    private sealed class CandidateCapture : IInvoiceIssueStore
    {
        public InvoiceIssueCandidate? Candidate { get; private set; }
        public Task<InvoiceIssueReceiptResult> FindReceiptAsync(TenantContext context, string key, string fingerprint, CancellationToken ct) =>
            Task.FromResult(new InvoiceIssueReceiptResult(InvoiceIssueReceiptStatus.Missing));
        public Task<InvoiceIssueCommitResult> CommitAsync(TenantContext context, InvoiceIssueCandidate candidate, string key, string fingerprint, CancellationToken ct)
        { Candidate = candidate; return Task.FromResult(new InvoiceIssueCommitResult(InvoiceIssueCommitStatus.Unavailable)); }
    }

    private sealed class Membership : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken ct) => Task.FromResult<IReadOnlyList<TenantMembership>>([]);
        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class Customers(TenantContext context, Guid organizationId, string organizationName) : ICustomerStore, ICustomerIndividualStore
    {
        public Guid IndividualId { get; } = Guid.NewGuid();
        public Task<CustomerOrganizationSnapshot?> FindOrganizationAsync(TenantContext tenant, Guid id, CancellationToken ct) =>
            Task.FromResult<CustomerOrganizationSnapshot?>(new(organizationId, context.TenantId, organizationName, DateTimeOffset.UtcNow));
        public Task<CustomerProgramSnapshot?> FindProgramAsync(TenantContext tenant, Guid id, CancellationToken ct) => Task.FromResult<CustomerProgramSnapshot?>(null);
        public Task<CustomerIndividualSnapshot?> FindIndividualAsync(TenantContext tenant, Guid id, CancellationToken ct) =>
            Task.FromResult<CustomerIndividualSnapshot?>(new(IndividualId, context.TenantId, "Synthetic individual", null, null, CustomerIndividualAvailability.Active, 3,
                context.AccountId, DateTimeOffset.UtcNow, null, null));
        public Task<CustomerOrderContext?> ResolveOrderContextAsync(TenantContext tenant, Guid organization, Guid? program, CancellationToken ct) => throw new NotSupportedException();
        public Task<CreateCustomerOrganizationResult> CreateOrganizationAsync(TenantContext tenant, CustomerOrganizationIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<CreateCustomerProgramResult> CreateProgramAsync(TenantContext tenant, CustomerProgramIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<CustomerOrganizationPage> ListOrganizationsAsync(TenantContext tenant, ListCustomerOrganizationsRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<CustomerProgramPage> ListProgramsAsync(TenantContext tenant, ListCustomerProgramsRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<CreateCustomerIndividualResult> CreateIndividualAsync(TenantContext tenant, CustomerIndividualIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<ChangeCustomerIndividualAvailabilityResult> ChangeIndividualAvailabilityAsync(TenantContext tenant, CustomerIndividualAvailabilityIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
    }
}

public sealed class InvoiceDatabaseFixture : IAsyncLifetime
{
    public static readonly string[] Tables = ["organization_ordinals", "issued_invoices", "issued_lines", "issue_receipts"];
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
        .WithDatabase("invoice_tests").WithUsername("postgres").WithPassword("local-invoice-owner-only").Build();
    public string ConnectionString => database.GetConnectionString();
    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await using (var context = InvoicesPostgresRegistration.CreateContext(ConnectionString)) await context.Database.MigrateAsync();
        await OwnerSqlAsync("""
            CREATE ROLE invoice_runtime LOGIN PASSWORD 'local-invoice-runtime-only';
            GRANT CONNECT ON DATABASE invoice_tests TO invoice_runtime;
            GRANT USAGE ON SCHEMA invoices TO invoice_runtime;
            GRANT SELECT,INSERT ON invoices.organization_ordinals,invoices.issued_invoices,invoices.issued_lines,invoices.issue_receipts TO invoice_runtime;
            GRANT UPDATE(last_ordinal) ON invoices.organization_ordinals TO invoice_runtime;
            GRANT EXECUTE ON FUNCTION invoices.valid_debtor(jsonb),invoices.valid_receipt(jsonb) TO invoice_runtime;
            GRANT SELECT,INSERT ON invoices.receivables,invoices.customer_payments,invoices.customer_payment_events,invoices.customer_financial_receipts TO invoice_runtime;
            GRANT SELECT ON invoices.receivable_balances,invoices.customer_payment_balances TO invoice_runtime;
            """);
    }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();
    public NpgsqlDataSource CreateRuntimeSource(int poolSize = 2, string? applicationName = null, string? connectionString = null) =>
        new NpgsqlDataSourceBuilder(new NpgsqlConnectionStringBuilder(connectionString ?? ConnectionString)
        { Username = "invoice_runtime", Password = "local-invoice-runtime-only", MaxPoolSize = poolSize, ApplicationName = applicationName ?? string.Empty }.ConnectionString).Build();
    public async Task OwnerSqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
    public async Task<long[]> CountsAsync(Guid tenant)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var counts = new List<long>();
        foreach (var table in Tables)
        {
            await using var command = new NpgsqlCommand($"SELECT count(*) FROM invoices.{table} WHERE tenant_id=@tenant", connection);
            command.Parameters.AddWithValue("tenant", tenant);
            counts.Add((long)(await command.ExecuteScalarAsync())!);
        }
        return counts.ToArray();
    }
    public static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenant)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('app.current_tenant',@tenant,true)", connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }
    public async Task WaitForAdvisoryWaitAsync(string name)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid WHERE a.application_name=@name AND l.locktype='advisory' AND NOT l.granted)", connection);
            command.Parameters.AddWithValue("name", name);
            if ((bool)(await command.ExecuteScalarAsync(timeout.Token))!) return;
            await Task.Delay(10, timeout.Token);
        }
    }
}
