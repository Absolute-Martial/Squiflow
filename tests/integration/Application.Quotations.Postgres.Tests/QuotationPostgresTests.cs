using Application.Tenancy;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.Quotations.Postgres.Tests;

public sealed partial class QuotationPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
        .WithDatabase("quotation_tests").WithUsername("postgres").WithPassword("local-quotation-integration-only").Build();
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _foreign = Guid.NewGuid();
    private readonly Guid _account = Guid.NewGuid();
    private readonly Clock _clock = new();
    private NpgsqlDataSource _runtime = null!;
    private PostgresQuotationStore Store => new(_runtime, _clock);
    private QuotationDraftFacts Offer(decimal price = 10) => new(QuotationPriceMode.Manual, "Synthetic offer", "USD", _clock.GetUtcNow().AddDays(2),
        [new(1, "Synthetic item", 2, "EA", price, price * 2)], price * 2, null, null, null, false);
    private Task<TenantContext> Context(Guid? tenant = null) => ContextAsync(tenant ?? _tenant, _account);
    private static async Task<TenantContext> ContextAsync(Guid tenant, Guid account) =>
        (await new ResolveTenantContext(new Membership()).ExecuteAsync(account, tenant, default))!;
    [Fact]
    public async Task IssuedHistoryNumberingAndReceiptsRemainFrozenAfterLaterDrafts()
    {
        var context = await Context();
        var created = await Store.CreateDraftAsync(context, Offer(), "create", Fingerprint("create"), default);
        var id = created.Quotation!.QuotationId;
        var issued = await Store.IssueAsync(context, id, 1, "issue", Fingerprint("issue"), (_, _) => Task.FromResult(true), default);
        Assert.Equal(1, issued.Quotation!.Number);
        Assert.Equal(2, issued.Quotation.Version);
        Assert.Null(issued.Quotation.Draft);
        var revised = await Store.ReviseDraftAsync(context, id, 2, Offer(15), "revise", Fingerprint("revise"), default);
        Assert.Equivalent(issued.Quotation.CurrentIssued, revised.Quotation!.CurrentIssued);
        var next = await Store.IssueAsync(context, id, 3, "issue-2", Fingerprint("issue-2"), (_, _) => Task.FromResult(true), default);
        Assert.Equal(1, next.Quotation!.Number); Assert.Equal(2, next.Quotation.CurrentIssued!.RevisionNumber);
        var history = await Store.ListIssuedAsync(context, id, 0, 1, default);
        Assert.Equal(1, history.NextAfterRevision); Assert.Equal(20m, history.Items[0].Offer.Total);
        Assert.Equal(30m, (await Store.ListIssuedAsync(context, id, 1, 1, default)).Items[0].Offer.Total);
        _clock.At = _clock.At.AddDays(10);
        var replay = await Store.IssueAsync(context, id, 1, "issue", Fingerprint("issue"), (_, _) => throw new InvalidOperationException("No revalidation on replay"), default);
        Assert.Equal(QuotationCommandStatus.Replayed, replay.Status); Assert.Equivalent(issued.Quotation, replay.Quotation);
        var createReplay = await Store.FindReceiptAsync(context, "create", "create", Fingerprint("create"), default);
        Assert.Equivalent(created.Quotation, createReplay!.Quotation);
        Assert.Null(await Store.FindAsync(await Context(_foreign), id, default));
    }
    [Fact]
    public async Task ConcurrentIssueHasOneVersionAndOneNumberAndDistinctCommandsHaveAClearWinner()
    {
        var context = await Context();
        var draft = (await Store.CreateDraftAsync(context, Offer(), "create", Fingerprint("create"), default)).Quotation!;
        var same = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Store.IssueAsync(context, draft.QuotationId, 1,
            "issue", Fingerprint("issue"), (_, _) => Task.FromResult(true), default)));
        Assert.Single(same, result => result.Status == QuotationCommandStatus.Issued);
        Assert.Equal(5, same.Count(result => result.Status == QuotationCommandStatus.Replayed));
        Assert.Single(same.Select(result => result.Quotation!.CurrentIssued!.RevisionId).Distinct());
        var nextDraft = (await Store.ReviseDraftAsync(context, draft.QuotationId, 2, Offer(), "revise", Fingerprint("revise"), default)).Quotation!;
        var race = await Task.WhenAll(Store.IssueAsync(context, draft.QuotationId, nextDraft.Version, "issue-next", Fingerprint("next"), (_, _) => Task.FromResult(true), default),
            Store.ReviseDraftAsync(context, draft.QuotationId, nextDraft.Version, Offer(20), "race-revise", Fingerprint("race"), default));
        Assert.Single(race, result => result.Status == QuotationCommandStatus.RevisionConflict);
        Assert.Single(race, result => result.Status is QuotationCommandStatus.Issued or QuotationCommandStatus.Revised);
    }
    [Fact]
    public async Task FailedComparisonCancellationAndExpiryLeaveNoNumberOrIssueReceipt()
    {
        var context = await Context();
        var draft = (await Store.CreateDraftAsync(context, Offer(), "create", Fingerprint("create"), default)).Quotation!;
        var conflict = await Store.IssueAsync(context, draft.QuotationId, 1, "issue", Fingerprint("issue"), (_, _) => Task.FromResult(false), default);
        Assert.Equal(QuotationCommandStatus.CommercialFactsConflict, conflict.Status);
        Assert.Equivalent(draft, await Store.FindAsync(context, draft.QuotationId, default));
        Assert.Null(await Store.FindReceiptAsync(context, "issue", "issue", Fingerprint("issue"), default));
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.IssueAsync(context, draft.QuotationId, 1, "cancel", Fingerprint("cancel"),
            (_, _) => { cancel.Cancel(); cancel.Token.ThrowIfCancellationRequested(); return Task.FromResult(true); }, cancel.Token));
        Assert.Equivalent(draft, await Store.FindAsync(context, draft.QuotationId, default));
        _clock.At = draft.Draft!.ValidUntil;
        Assert.Equal(QuotationCommandStatus.ValidityConflict, (await Store.IssueAsync(context, draft.QuotationId, 1, "expiry", Fingerprint("expiry"), (_, _) => Task.FromResult(true), default)).Status);
        _clock.At = _clock.At.AddDays(-1);
        var recovered = await Store.IssueAsync(context, draft.QuotationId, 1, "issue", Fingerprint("issue"), (_, _) => Task.FromResult(true), default);
        Assert.Equal(1, recovered.Quotation!.Number);
    }
    [Fact]
    public async Task ForcedRlsAndAppendOnlyFactsRejectRuntimeMutationAndForeignInserts()
    {
        var context = await Context();
        var draft = (await Store.CreateDraftAsync(context, Offer(), "create", Fingerprint("create"), default)).Quotation!;
        var issued = (await Store.IssueAsync(context, draft.QuotationId, 1, "issue", Fingerprint("issue"), (_, _) => Task.FromResult(true), default)).Quotation!;
        await using var connection = await _runtime.OpenConnectionAsync();
        await using var tenant = new NpgsqlCommand("SELECT set_config('app.current_tenant',@tenant,false)", connection);
        tenant.Parameters.AddWithValue("tenant", _tenant.ToString("D")); await tenant.ExecuteNonQueryAsync();
        await using var update = new NpgsqlCommand("UPDATE quotations.issued SET facts=facts WHERE tenant_id=@tenant", connection);
        update.Parameters.AddWithValue("tenant", _tenant);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync())).SqlState);
        await using var foreign = new NpgsqlCommand("INSERT INTO quotations.numbers(tenant_id,value) VALUES(@tenant,1)", connection);
        foreign.Parameters.AddWithValue("tenant", _foreign);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => foreign.ExecuteNonQueryAsync())).SqlState);
        await using var owner = new NpgsqlConnection(_database.GetConnectionString()); await owner.OpenAsync();
        await using var ownerMutation = new NpgsqlCommand("UPDATE quotations.issued SET facts=facts WHERE id=@id", owner);
        ownerMutation.Parameters.AddWithValue("id", issued.CurrentIssued!.RevisionId);
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => ownerMutation.ExecuteNonQueryAsync())).SqlState);
    }
    [Fact]
    public async Task ReceiptFailureRollsBackIssuedFactsHeaderAndNumberAllocation()
    {
        var context = await Context();
        var draft = (await Store.CreateDraftAsync(context, Offer(), "create", Fingerprint("create"), default)).Quotation!;
        await using var owner = new NpgsqlConnection(_database.GetConnectionString()); await owner.OpenAsync();
        await using (var inject = new NpgsqlCommand("""
            CREATE FUNCTION quotations.test_receipt_failure() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.operation='issue' THEN RAISE EXCEPTION 'Synthetic receipt failure'; END IF; RETURN NEW; END; $$;
            CREATE TRIGGER test_receipt_failure BEFORE INSERT ON quotations.receipts FOR EACH ROW EXECUTE FUNCTION quotations.test_receipt_failure();
            """, owner)) await inject.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<PostgresException>(() => Store.IssueAsync(context, draft.QuotationId, 1, "issue", Fingerprint("issue"), (_, _) => Task.FromResult(true), default));
        Assert.Equivalent(draft, await Store.FindAsync(context, draft.QuotationId, default));
        Assert.Null(await Store.FindReceiptAsync(context, "issue", "issue", Fingerprint("issue"), default));
        Assert.Empty((await Store.ListIssuedAsync(context, draft.QuotationId, 0, 10, default)).Items);
        await using (var remove = new NpgsqlCommand("DROP TRIGGER test_receipt_failure ON quotations.receipts; DROP FUNCTION quotations.test_receipt_failure();", owner))
            await remove.ExecuteNonQueryAsync();
        var recovered = await Store.IssueAsync(context, draft.QuotationId, 1, "issue", Fingerprint("issue"), (_, _) => Task.FromResult(true), default);
        Assert.Equal(1, recovered.Quotation!.Number);
    }

    [Fact]
    public async Task DistinctFamiliesReceiveUniqueTenantWideNumbersUnderConcurrentIssue()
    {
        var context = await Context();
        var first = (await Store.CreateDraftAsync(context, Offer(), "create-a", Fingerprint("a"), default)).Quotation!;
        var second = (await Store.CreateDraftAsync(context, Offer(), "create-b", Fingerprint("b"), default)).Quotation!;
        var result = await Task.WhenAll(Store.IssueAsync(context, first.QuotationId, 1, "issue-a", Fingerprint("a"), (_, _) => Task.FromResult(true), default),
            Store.IssueAsync(context, second.QuotationId, 1, "issue-b", Fingerprint("b"), (_, _) => Task.FromResult(true), default));
        Assert.Equal(new long?[] { 1, 2 }, result.Select(item => item.Quotation!.Number).Order().ToArray());
    }

    [Fact]
    public async Task UnsupportedReceiptVersionAndCorruptRevisionIdentityFailClosedOnHistoricalReads()
    {
        var context = await Context();
        var draft = (await Store.CreateDraftAsync(context, Offer(), "create", Fingerprint("create"), default)).Quotation!;
        await Store.IssueAsync(context, draft.QuotationId, 1, "issue", Fingerprint("issue"), (_, _) => Task.FromResult(true), default);
        await using var owner = new NpgsqlConnection(_database.GetConnectionString()); await owner.OpenAsync();
        // Deliberately bypass migration guards to represent untrusted historical storage.
        await using var corrupt = new NpgsqlCommand("""
            ALTER TABLE quotations.receipts DROP CONSTRAINT receipts_version_check;
            ALTER TABLE quotations.receipts DISABLE TRIGGER immutable_receipt;
            UPDATE quotations.receipts SET version=99 WHERE operation='create';
            ALTER TABLE quotations.issued DISABLE TRIGGER immutable_issued;
            UPDATE quotations.issued SET facts=jsonb_set(facts,'{RevisionId}',to_jsonb(@wrong::text));
            """, owner);
        corrupt.Parameters.AddWithValue("wrong", Guid.NewGuid()); await corrupt.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.FindReceiptAsync(context, "create", "create", Fingerprint("create"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.FindAsync(context, draft.QuotationId, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.ListIssuedAsync(context, draft.QuotationId, 0, 10, default));
    }

    public async Task InitializeAsync()
    {
        try
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await _database.StartAsync(budget.Token);
            await using var owner = new NpgsqlConnection(_database.GetConnectionString()); await owner.OpenAsync(budget.Token);
            await using var prerequisites = new NpgsqlCommand("""
                CREATE SCHEMA tenancy; CREATE TABLE tenancy.tenants(id uuid PRIMARY KEY);
                CREATE SCHEMA identity_access; CREATE TABLE identity_access.accounts(id uuid PRIMARY KEY);
                INSERT INTO tenancy.tenants(id) VALUES(@tenant),(@foreign); INSERT INTO identity_access.accounts(id) VALUES(@actor);
                """, owner);
            prerequisites.Parameters.AddWithValue("tenant", _tenant); prerequisites.Parameters.AddWithValue("foreign", _foreign);
            prerequisites.Parameters.AddWithValue("actor", _account); await prerequisites.ExecuteNonQueryAsync(budget.Token);
            await using var customers = Application.Customers.Postgres.CustomersPostgresMigrations.CreateContext(_database.GetConnectionString());
            await customers.Database.MigrateAsync(budget.Token);
            var orderOptions = new DbContextOptionsBuilder<Application.Orders.Postgres.OrderDbContext>();
            Application.Orders.Postgres.PostgresOrderOptions.Configure(orderOptions, _database.GetConnectionString());
            await using var orders = new Application.Orders.Postgres.OrderDbContext(orderOptions.Options);
            await orders.Database.MigrateAsync(budget.Token);
            Assert.False(orders.Database.HasPendingModelChanges());
            await using var context = QuotationsPostgresRegistration.CreateContext(_database.GetConnectionString());
            await context.Database.MigrateAsync(budget.Token);
            Assert.False(context.Database.HasPendingModelChanges());
            await using var grant = new NpgsqlCommand("""
                CREATE ROLE quotation_runtime LOGIN PASSWORD 'local-runtime-only';
                GRANT USAGE ON SCHEMA quotations TO quotation_runtime;
                GRANT SELECT,INSERT ON quotations.heads,quotations.issued,quotations.numbers,quotations.receipts,quotations.responses,quotations.conversions TO quotation_runtime;
                GRANT UPDATE(version,number,draft,current_issued_id,last_issued_revision) ON quotations.heads TO quotation_runtime;
                GRANT UPDATE(value) ON quotations.numbers TO quotation_runtime;
                GRANT USAGE ON SCHEMA orders TO quotation_runtime;
                GRANT SELECT,INSERT ON orders.order_drafts,orders.order_draft_lines,orders.command_receipts,orders.quotation_origins TO quotation_runtime;
                GRANT UPDATE(state,revision,abandoned_at,abandoned_by_account_id,committed_at,committed_by_account_id) ON orders.order_drafts TO quotation_runtime;
                """, owner);
            await grant.ExecuteNonQueryAsync(budget.Token);
            var settings = new NpgsqlConnectionStringBuilder(_database.GetConnectionString()) { Username = "quotation_runtime", Password = "local-runtime-only", MaxPoolSize = 8 };
            _runtime = new NpgsqlDataSourceBuilder(settings.ConnectionString).Build();
        }
        catch { await _database.DisposeAsync(); throw; }
    }
    public async Task DisposeAsync() { if (_runtime is not null) await _runtime.DisposeAsync(); await _database.DisposeAsync(); }
    private static string Fingerprint(string value) => QuotationRules.Fingerprint(value);
    private sealed class Clock : TimeProvider
    { internal DateTimeOffset At { get; set; } = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => At; }
    private sealed class Membership : ITenantMembershipDirectory
    {
        public Task<bool> IsActiveAsync(Guid account, Guid tenant, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid account, CancellationToken ct) => throw new NotSupportedException();
    }
}
