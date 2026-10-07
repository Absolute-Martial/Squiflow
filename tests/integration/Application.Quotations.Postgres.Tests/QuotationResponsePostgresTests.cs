using Application.Orders;
using Application.Orders.Postgres;
using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Application.Quotations.Postgres.Tests;

public sealed partial class QuotationPostgresTests
{
    private static readonly string[] ConversionTables = ["orders.order_drafts", "orders.order_draft_lines", "orders.quotation_origins", "orders.command_receipts", "quotations.conversions"];
    private static readonly string[] CountTables = [.. ConversionTables, "quotations.responses"];
    private static readonly string[] ImmutableMutations = [
        "UPDATE quotations.responses SET kind=kind", "DELETE FROM quotations.responses",
        "UPDATE quotations.conversions SET order_id=order_id", "DELETE FROM quotations.conversions",
        "UPDATE orders.quotation_origins SET number=number", "DELETE FROM orders.quotation_origins",
        "UPDATE orders.order_drafts SET summary='Changed offer'", "DELETE FROM orders.order_drafts",
        "UPDATE orders.order_draft_lines SET description='Changed line'", "DELETE FROM orders.order_draft_lines",
        "INSERT INTO orders.order_draft_lines SELECT tenant_id,order_id,position+1,description,quantity,unit_code,unit_price,line_total,commercial_facts FROM orders.order_draft_lines",
        "UPDATE quotations.heads SET version=version+1,draft=(SELECT facts->'Offer' FROM quotations.issued LIMIT 1)||'{\"Summary\":\"Changed offer\"}'::jsonb"
    ];
    private PostgresQuotationStore ConversionStore => new(_runtime, _clock, new OrderWriter());
    private async Task<QuotationSnapshot> IssuedAsync(string suffix = "", decimal price = 10)
    {
        var context = await Context();
        var draft = (await Store.CreateDraftAsync(context, Offer(price), "create" + suffix, Fingerprint("create" + suffix), default)).Quotation!;
        return (await Store.IssueAsync(context, draft.QuotationId, 1, "issue" + suffix, Fingerprint("issue" + suffix), (_, _) => Task.FromResult(true), default)).Quotation!;
    }
    private async Task<QuotationSnapshot> AcceptedAsync(string suffix = "")
    {
        var issued = await IssuedAsync(suffix);
        var request = new QuotationResponseRequest(issued.CurrentIssued!.RevisionId, issued.Version, "Synthetic response evidence", "Synthetic customer claim");
        return (await Store.RespondAsync(await Context(), issued.QuotationId, QuotationResponseKind.Accepted, request, "accept" + suffix, Fingerprint("accept" + suffix), default)).Quotation!;
    }
    [Theory]
    [InlineData(QuotationResponseKind.Accepted, "before-issue", QuotationCommandStatus.ValidityConflict)]
    [InlineData(QuotationResponseKind.Accepted, "at-issue", QuotationCommandStatus.Accepted)]
    [InlineData(QuotationResponseKind.Accepted, "before-end", QuotationCommandStatus.Accepted)]
    [InlineData(QuotationResponseKind.Accepted, "at-end", QuotationCommandStatus.ValidityConflict)]
    [InlineData(QuotationResponseKind.Expired, "before-end", QuotationCommandStatus.ValidityConflict)]
    [InlineData(QuotationResponseKind.Expired, "at-end", QuotationCommandStatus.Expired)]
    [InlineData(QuotationResponseKind.Rejected, "after-end", QuotationCommandStatus.Rejected)]
    public async Task ResponseCommandsHonorExactServerBoundariesAndRetainOriginalReplay(QuotationResponseKind kind, string moment, QuotationCommandStatus expected)
    {
        var context = await Context(); var issued = await IssuedAsync(); var facts = issued.CurrentIssued!;
        _clock.At = moment switch
        {
            "before-issue" => facts.IssuedAt.AddTicks(-10),
            "at-issue" => facts.IssuedAt,
            "before-end" => facts.Offer.ValidUntil.AddTicks(-10),
            "at-end" => facts.Offer.ValidUntil,
            _ => facts.Offer.ValidUntil.AddDays(1)
        };
        var request = new QuotationResponseRequest(facts.RevisionId, issued.Version, "Synthetic evidence", kind == QuotationResponseKind.Expired ? null : "Synthetic customer");
        var result = await Store.RespondAsync(context, issued.QuotationId, kind, request, "response", Fingerprint("response"), default);
        Assert.Equal(expected, result.Status);
        var operation = kind switch { QuotationResponseKind.Accepted => "accept", QuotationResponseKind.Rejected => "reject", _ => "expire" };
        if (expected == QuotationCommandStatus.ValidityConflict)
        {
            Assert.Equivalent(issued, await Store.FindAsync(context, issued.QuotationId, default));
            Assert.Null(await Store.FindReceiptAsync(context, operation, "response", Fingerprint("response"), default)); return;
        }
        Assert.Equal(_clock.At, result.Quotation!.CurrentResponse!.RecordedAt);
        Assert.Equal(_account, result.Quotation.CurrentResponse.RecordedByAccountId);
        _clock.At = _clock.At.AddYears(1);
        var replay = await Store.RespondAsync(context, issued.QuotationId, kind, request, "response", Fingerprint("response"), default);
        Assert.Equal(QuotationCommandStatus.Replayed, replay.Status); Assert.Equivalent(result.Quotation, replay.Quotation);
        Assert.Equal(QuotationCommandStatus.IdempotencyKeyConflict, (await Store.RespondAsync(context, issued.QuotationId, kind,
            request with { Evidence = "Changed evidence" }, "response", Fingerprint("changed"), default)).Status);
    }
    [Fact]
    public async Task AcceptanceFreezesTheCurrentOfferEvenWithANewerDormantDraft()
    {
        var context = await Context(); var issued = await IssuedAsync();
        var newer = (await Store.ReviseDraftAsync(context, issued.QuotationId, issued.Version, Offer(25), "new-draft", Fingerprint("new-draft"), default)).Quotation!;
        var accepted = (await Store.RespondAsync(context, issued.QuotationId, QuotationResponseKind.Accepted,
            new(issued.CurrentIssued!.RevisionId, newer.Version, "Customer selected the issued offer", "Synthetic customer"), "accept", Fingerprint("accept"), default)).Quotation!;
        Assert.Equal(20, accepted.CurrentIssued!.Offer.Total); Assert.Equal(50, accepted.Draft!.Total);
        Assert.Equal(QuotationCommandStatus.AcceptedFamily, (await Store.ReviseDraftAsync(context, issued.QuotationId, accepted.Version, Offer(30), "replace", Fingerprint("replace"), default)).Status);
        Assert.Equal(QuotationCommandStatus.AcceptedFamily, (await Store.IssueAsync(context, issued.QuotationId, accepted.Version, "replace-issue", Fingerprint("replace-issue"),
            (_, _) => throw new InvalidOperationException("Accepted offers cannot be reselected"), default)).Status);
        var converted = await ConversionStore.ConvertAsync(context, issued.QuotationId, new(issued.CurrentIssued.RevisionId, accepted.Version), "convert", Fingerprint("convert"), default);
        Assert.Equal(20, converted.Quotation!.Conversion!.OriginalOrder.Total);
    }
    [Fact]
    public async Task SupersededOffersCannotRespondAndLaterIssuesPreserveTerminalResponseHistory()
    {
        var context = await Context(); var first = await IssuedAsync();
        var draft = (await Store.ReviseDraftAsync(context, first.QuotationId, first.Version, Offer(15), "revise", Fingerprint("revise"), default)).Quotation!;
        var second = (await Store.IssueAsync(context, first.QuotationId, draft.Version, "issue-2", Fingerprint("issue-2"), (_, _) => Task.FromResult(true), default)).Quotation!;
        Assert.Equal(QuotationCommandStatus.Superseded, (await Store.RespondAsync(context, first.QuotationId, QuotationResponseKind.Accepted,
            new(first.CurrentIssued!.RevisionId, second.Version, "Old offer", "Synthetic customer"), "old", Fingerprint("old"), default)).Status);
        _clock.At = second.CurrentIssued!.Offer.ValidUntil.AddDays(1);
        var rejected = (await Store.RespondAsync(context, second.QuotationId, QuotationResponseKind.Rejected,
            new(second.CurrentIssued.RevisionId, second.Version, "Late rejection", "Synthetic customer"), "reject", Fingerprint("reject"), default)).Quotation!;
        Assert.Equal(QuotationCommandStatus.AlreadyResponded, (await Store.RespondAsync(context, second.QuotationId, QuotationResponseKind.Expired,
            new(second.CurrentIssued.RevisionId, rejected.Version, "Elapsed validity"), "expire", Fingerprint("expire"), default)).Status);
        draft = (await Store.ReviseDraftAsync(context, second.QuotationId, rejected.Version, Offer(17), "later-draft", Fingerprint("later-draft"), default)).Quotation!;
        var next = (await Store.IssueAsync(context, second.QuotationId, draft.Version, "issue-3", Fingerprint("issue-3"), (_, _) => Task.FromResult(true), default)).Quotation!;
        Assert.Null(next.CurrentResponse);
        Assert.Equivalent(rejected.CurrentResponse, await Store.FindResponseAsync(context, second.QuotationId, second.CurrentIssued.RevisionId, default));
        Assert.Null(await Store.FindResponseAsync(await Context(_foreign), second.QuotationId, second.CurrentIssued.RevisionId, default));
        Assert.Equal(3, (await Store.ListIssuedAsync(context, second.QuotationId, 0, 10, default)).Items.Count);
    }
    [Fact]
    public async Task AcceptanceIssueAndTerminalResponseRacesHaveOneRevisionCheckedWinner()
    {
        var context = await Context(); var issued = await IssuedAsync();
        var draft = (await Store.ReviseDraftAsync(context, issued.QuotationId, issued.Version, Offer(20), "revise", Fingerprint("revise"), default)).Quotation!;
        var accept = new QuotationResponseRequest(issued.CurrentIssued!.RevisionId, draft.Version, "Race evidence", "Synthetic customer");
        var race = await Task.WhenAll(Store.RespondAsync(context, draft.QuotationId, QuotationResponseKind.Accepted, accept, "accept", Fingerprint("accept"), default),
            Store.IssueAsync(context, draft.QuotationId, draft.Version, "next-issue", Fingerprint("next-issue"), (_, _) => Task.FromResult(true), default));
        Assert.Single(race, r => r.Status == QuotationCommandStatus.RevisionConflict);
        Assert.Single(race, r => r.Status is QuotationCommandStatus.Accepted or QuotationCommandStatus.Issued);
        var other = await IssuedAsync("-other");
        var response = new QuotationResponseRequest(other.CurrentIssued!.RevisionId, other.Version, "Terminal evidence", "Synthetic customer");
        var terminal = await Task.WhenAll(Store.RespondAsync(context, other.QuotationId, QuotationResponseKind.Accepted, response, "a", Fingerprint("a"), default),
            Store.RespondAsync(context, other.QuotationId, QuotationResponseKind.Rejected, response, "r", Fingerprint("r"), default));
        Assert.Single(terminal, r => r.Status == QuotationCommandStatus.RevisionConflict);
        Assert.Single(terminal, r => r.Status is QuotationCommandStatus.Accepted or QuotationCommandStatus.Rejected);
    }
    [Fact]
    public async Task ConversionIsOneOrderAcrossKeysCallersExpiryAndOrderLifecycle()
    {
        var context = await Context(); var accepted = await AcceptedAsync(); var issued = accepted.CurrentIssued!;
        var direct = new PostgresOrderDraftStore(_runtime, _clock);
        var directIntent = OrderDraftIntent.Create(new("Direct order", "USD", [new("Direct line", 1, "EA", 99)]));
        await direct.CreateAsync(context, directIntent, issued.RevisionId.ToString("D"), default);
        _clock.At = issued.Offer.ValidUntil.AddDays(20);
        var request = new QuotationConvertRequest(issued.RevisionId, accepted.Version);
        var store = ConversionStore;
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => store.ConvertAsync(context, accepted.QuotationId, request, "convert", Fingerprint("convert"), default)));
        Assert.Single(results, r => r.Status == QuotationCommandStatus.Converted); Assert.Equal(5, results.Count(r => r.Status == QuotationCommandStatus.Replayed));
        var original = results[0].Quotation!.Conversion!.OriginalOrder; Assert.Equal(20, original.Total); Assert.NotNull(original.QuotationOrigin);
        var caller = Guid.NewGuid(); await OwnerSqlAsync("INSERT INTO identity_access.accounts(id) VALUES(@account)", ("account", caller));
        var otherContext = await ContextAsync(_tenant, caller);
        var second = await store.ConvertAsync(otherContext, accepted.QuotationId, request, "another-key", Fingerprint("another"), default);
        Assert.Equal(QuotationCommandStatus.AlreadyLinked, second.Status); Assert.Equivalent(original, second.Quotation!.Conversion!.OriginalOrder);
        Assert.Equal(QuotationCommandStatus.IdempotencyKeyConflict, (await store.ConvertAsync(otherContext, accepted.QuotationId,
            request with { ExpectedVersion = request.ExpectedVersion + 1 }, "another-key", Fingerprint("changed"), default)).Status);
        var orderStore = new PostgresOrderDraftStore(_runtime, _clock, new OrderCommercialCommitGuard(null!, null!, null!, store));
        Assert.Equal(ReviseOrderDraftStatus.QuotationBound, (await orderStore.ReviseAsync(context,
            new(original.OrderId, 1, "Replacement", "USD", []), directIntent, "replace", Fingerprint("replace"), default)).Status);
        Assert.Equal(CommitOrderDraftStatus.Committed, (await orderStore.CommitAsync(context, new(original.OrderId, 1), "commit", Fingerprint("commit"), default)).Status);
        var history = await orderStore.ListHistoryAsync(context, new(original.OrderId, 10, null), default);
        Assert.Equal(2, history!.Items.Count); Assert.Equal(OrderDraftChange.Created, history.Items[1].Change);
        Assert.NotNull(history.Items[0].Order.QuotationOrigin);
        var duplicate = await store.ConvertAsync(context, accepted.QuotationId, request, "after-commit", Fingerprint("after-commit"), default);
        Assert.Equivalent(original, duplicate.Quotation!.Conversion!.OriginalOrder); Assert.Equal(OrderDraftState.Draft, duplicate.Quotation.Conversion.OriginalOrder.State);
        Assert.Null(await store.ReadAsync(await Context(_foreign), original.OrderId, original.QuotationOrigin!, default));
        Assert.Equal(2, await CountAsync("orders.order_drafts")); Assert.Equal(1, await CountAsync("quotations.conversions"));
        var abandoned = await AcceptedAsync("-abandoned");
        var abandonedConversion = await store.ConvertAsync(context, abandoned.QuotationId, new(abandoned.CurrentIssued!.RevisionId, abandoned.Version), "abandoned-convert", Fingerprint("abandoned"), default);
        var abandonedOriginal = abandonedConversion.Quotation!.Conversion!.OriginalOrder;
        Assert.Equal(AbandonOrderDraftStatus.Abandoned, (await orderStore.AbandonAsync(context, new(abandonedOriginal.OrderId, 1), "abandon", Fingerprint("abandon"), default)).Status);
        Assert.Equivalent(abandonedOriginal, (await store.ConvertAsync(context, abandoned.QuotationId, new(abandoned.CurrentIssued.RevisionId, abandoned.Version),
            "after-abandon", Fingerprint("after-abandon"), default)).Quotation!.Conversion!.OriginalOrder);
    }
    [Fact]
    public async Task ResponseAndConversionReceiptFailuresRollBackAllOwnedFacts()
    {
        var context = await Context(); var issued = await IssuedAsync();
        await OwnerSqlAsync("""
            CREATE FUNCTION quotations.fail_new_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.operation IN ('accept','convert') THEN RAISE EXCEPTION 'Synthetic receipt failure'; END IF; RETURN NEW; END; $$;
            CREATE TRIGGER fail_new_receipt BEFORE INSERT ON quotations.receipts FOR EACH ROW EXECUTE FUNCTION quotations.fail_new_receipt();
            """);
        await Assert.ThrowsAsync<PostgresException>(() => Store.RespondAsync(context, issued.QuotationId, QuotationResponseKind.Accepted,
            new(issued.CurrentIssued!.RevisionId, issued.Version, "Synthetic evidence", "Synthetic customer"), "accept", Fingerprint("accept"), default));
        Assert.Equivalent(issued, await Store.FindAsync(context, issued.QuotationId, default)); Assert.Equal(0, await CountAsync("quotations.responses"));
        await OwnerSqlAsync("DROP TRIGGER fail_new_receipt ON quotations.receipts");
        var accepted = (await Store.RespondAsync(context, issued.QuotationId, QuotationResponseKind.Accepted,
            new(issued.CurrentIssued!.RevisionId, issued.Version, "Synthetic evidence", "Synthetic customer"), "accept", Fingerprint("accept"), default)).Quotation!;
        await OwnerSqlAsync("CREATE TRIGGER fail_new_receipt BEFORE INSERT ON quotations.receipts FOR EACH ROW EXECUTE FUNCTION quotations.fail_new_receipt()");
        await Assert.ThrowsAsync<PostgresException>(() => ConversionStore.ConvertAsync(context, accepted.QuotationId, new(accepted.CurrentIssued!.RevisionId, accepted.Version),
            "convert", Fingerprint("convert"), default));
        Assert.Equivalent(accepted, await Store.FindAsync(context, accepted.QuotationId, default));
        foreach (var table in ConversionTables)
            Assert.Equal(0, await CountAsync(table));
        Assert.Null(await Store.FindReceiptAsync(context, "convert", "convert", Fingerprint("convert"), default));
        await OwnerSqlAsync("DROP TRIGGER fail_new_receipt ON quotations.receipts");
        Assert.Equal(QuotationCommandStatus.Converted, (await ConversionStore.ConvertAsync(context, accepted.QuotationId, new(accepted.CurrentIssued!.RevisionId, accepted.Version),
            "convert", Fingerprint("convert"), default)).Status);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConversionBackendLossBeforeOrAfterOrderWritesLeavesNoPartialCreation(bool afterWrites)
    {
        var context = await Context(); var accepted = await AcceptedAsync();
        var entered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var store = new PostgresQuotationStore(_runtime, _clock, new OrderWriter(afterWrites, async (connection, ct) =>
        { entered.TrySetResult(connection.ProcessID); await release.Task.WaitAsync(ct); }));
        var converting = store.ConvertAsync(context, accepted.QuotationId, new(accepted.CurrentIssued!.RevisionId, accepted.Version), "convert", Fingerprint("convert"), budget.Token);
        var pid = await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        try { await OwnerSqlAsync("SELECT pg_terminate_backend(@pid)", ("pid", pid)); }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<NpgsqlException>(() => converting);
        Assert.Equivalent(accepted, await Store.FindAsync(context, accepted.QuotationId, default));
        foreach (var table in ConversionTables)
            Assert.Equal(0, await CountAsync(table));
        Assert.Null(await Store.FindReceiptAsync(context, "convert", "convert", Fingerprint("convert"), default));
        Assert.Equal(QuotationCommandStatus.Converted, (await ConversionStore.ConvertAsync(context, accepted.QuotationId, new(accepted.CurrentIssued.RevisionId, accepted.Version),
            "convert", Fingerprint("convert"), default)).Status);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationBeforeOrAfterOrderWritesRollsBackAndAllowsRetry(bool afterWrites)
    {
        var context = await Context(); var accepted = await AcceptedAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var store = new PostgresQuotationStore(_runtime, _clock, new OrderWriter(afterWrites, async (_, ct) =>
        { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); }));
        var request = new QuotationConvertRequest(accepted.CurrentIssued!.RevisionId, accepted.Version);
        var converting = store.ConvertAsync(context, accepted.QuotationId, request, "convert", Fingerprint("convert"), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20)); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => converting);
        Assert.Equivalent(accepted, await Store.FindAsync(context, accepted.QuotationId, default));
        foreach (var table in ConversionTables) Assert.Equal(0, await CountAsync(table));
        Assert.Equal(QuotationCommandStatus.Converted, (await ConversionStore.ConvertAsync(context, accepted.QuotationId, request,
            "convert", Fingerprint("convert"), default)).Status);
    }
    [Fact]
    public async Task ConcurrentDistinctCallersAndKeysShareOneOriginalOrder()
    {
        var context = await Context(); var accepted = await AcceptedAsync(); var caller = Guid.NewGuid();
        await OwnerSqlAsync("INSERT INTO identity_access.accounts(id) VALUES(@account)", ("account", caller));
        var other = await ContextAsync(_tenant, caller); var request = new QuotationConvertRequest(accepted.CurrentIssued!.RevisionId, accepted.Version);
        var store = ConversionStore;
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => store.ConvertAsync(index % 2 == 0 ? context : other,
            accepted.QuotationId, request, "key-" + index, Fingerprint("key-" + index), default)));
        Assert.Single(results, result => result.Status == QuotationCommandStatus.Converted);
        Assert.Equal(5, results.Count(result => result.Status == QuotationCommandStatus.AlreadyLinked));
        Assert.Single(results.Select(result => result.Quotation!.Conversion!.OriginalOrder.OrderId).Distinct());
        foreach (var table in ConversionTables) Assert.Equal(1, await CountAsync(table));
    }
    [Fact]
    public async Task ResponseLinkAndQuotedOrderGuardsSurviveBroadMutationGrantsAndHideForeignFacts()
    {
        var context = await Context(); var accepted = await AcceptedAsync();
        var converted = (await ConversionStore.ConvertAsync(context, accepted.QuotationId, new(accepted.CurrentIssued!.RevisionId, accepted.Version),
            "convert", Fingerprint("convert"), default)).Quotation!;
        // Broad temporary grants prove the database guards themselves, beyond production's least-privilege grants.
        await OwnerSqlAsync("""
            GRANT UPDATE,DELETE ON quotations.responses,quotations.conversions,orders.quotation_origins,orders.order_drafts,orders.order_draft_lines TO quotation_runtime;
            """);
        foreach (var sql in ImmutableMutations)
            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => RuntimeSqlAsync(sql, _tenant))).SqlState);
        foreach (var table in CountTables)
            Assert.Equal(0, await RuntimeSqlAsync($"SELECT count(*) FROM {table}", _foreign));
        Assert.Equivalent(converted, await Store.FindAsync(context, accepted.QuotationId, default));
    }
    [Theory]
    [InlineData("origin")]
    [InlineData("price")]
    [InlineData("version")]
    public async Task QuotedHistoryRejectsContradictoryOriginPriceAndReceiptVersion(string damage)
    {
        var context = await Context(); var accepted = await AcceptedAsync(); var store = ConversionStore;
        var original = (await store.ConvertAsync(context, accepted.QuotationId, new(accepted.CurrentIssued!.RevisionId, accepted.Version),
            "convert", Fingerprint("convert"), default)).Quotation!.Conversion!.OriginalOrder;
        var guarded = new PostgresOrderDraftStore(_runtime, _clock, new OrderCommercialCommitGuard(null!, null!, null!, store));
        Assert.Equivalent(original, await guarded.FindAsync(context, original.OrderId, default));
        var unavailable = new PostgresOrderDraftStore(_runtime, _clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => unavailable.FindAsync(context, original.OrderId, default));
        var path = damage switch { "origin" => "{payload,quotationOrigin,number}", "price" => "{payload,summary}", _ => "{schemaVersion}" };
        object value = damage switch { "origin" => "999", "price" => "\"Forged accepted summary\"", _ => "4" };
        await OwnerSqlAsync("UPDATE orders.command_receipts SET response_json=jsonb_set(response_json,@path::text[],@value::jsonb) WHERE operation='create-quotation-order'",
            ("path", path), ("value", value));
        await Assert.ThrowsAsync<InvalidOperationException>(() => guarded.ListHistoryAsync(context, new(original.OrderId, 10, null), default));
    }
    [Fact]
    public async Task QuotedDetailRejectsDamagedPriceEvenWhenDatabaseGuardsAreBypassed()
    {
        var context = await Context(); var accepted = await AcceptedAsync(); var store = ConversionStore;
        var original = (await store.ConvertAsync(context, accepted.QuotationId, new(accepted.CurrentIssued!.RevisionId, accepted.Version),
            "convert", Fingerprint("convert"), default)).Quotation!.Conversion!.OriginalOrder;
        await OwnerSqlAsync("ALTER TABLE orders.order_drafts DISABLE TRIGGER protect_quoted_header; UPDATE orders.order_drafts SET summary='Forged summary'");
        var guarded = new PostgresOrderDraftStore(_runtime, _clock, new OrderCommercialCommitGuard(null!, null!, null!, store));
        await Assert.ThrowsAsync<InvalidOperationException>(() => guarded.FindAsync(context, original.OrderId, default));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuotedLifecycleReplayRejectsSelfConsistentButForgedAcceptedFacts(bool commit)
    {
        var context = await Context(); var accepted = await AcceptedAsync(); var store = ConversionStore;
        var original = (await store.ConvertAsync(context, accepted.QuotationId, new(accepted.CurrentIssued!.RevisionId, accepted.Version),
            "convert", Fingerprint("convert"), default)).Quotation!.Conversion!.OriginalOrder;
        var guarded = new PostgresOrderDraftStore(_runtime, _clock, new OrderCommercialCommitGuard(null!, null!, null!, store));
        if (commit) await guarded.CommitAsync(context, new(original.OrderId, 1), "lifecycle", Fingerprint("lifecycle"), default);
        else await guarded.AbandonAsync(context, new(original.OrderId, 1), "lifecycle", Fingerprint("lifecycle"), default);
        await OwnerSqlAsync("UPDATE orders.command_receipts SET response_json=jsonb_set(response_json,'{payload,summary}','\"Forged accepted summary\"') WHERE operation<>'create-quotation-order'");
        if (commit) await Assert.ThrowsAsync<InvalidOperationException>(() => guarded.CommitAsync(context, new(original.OrderId, 1), "lifecycle", Fingerprint("lifecycle"), default));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => guarded.AbandonAsync(context, new(original.OrderId, 1), "lifecycle", Fingerprint("lifecycle"), default));
    }
    [Fact]
    public async Task QuotedAbandonmentWithoutTrustedReaderRollsBackInsteadOfReturningUnverifiedPrices()
    {
        var context = await Context(); var accepted = await AcceptedAsync();
        var original = (await ConversionStore.ConvertAsync(context, accepted.QuotationId, new(accepted.CurrentIssued!.RevisionId, accepted.Version),
            "convert", Fingerprint("convert"), default)).Quotation!.Conversion!.OriginalOrder;
        var unavailable = new PostgresOrderDraftStore(_runtime, _clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => unavailable.AbandonAsync(context, new(original.OrderId, 1), "abandon", Fingerprint("abandon"), default));
        Assert.Equal(1, await CountAsync("orders.command_receipts"));
        var guarded = new PostgresOrderDraftStore(_runtime, _clock, new OrderCommercialCommitGuard(null!, null!, null!, ConversionStore));
        Assert.Equivalent(original, await guarded.FindAsync(context, original.OrderId, default));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExtendedResponseReceiptAndPhysicalKindMustAgreeWithRetainedFacts(bool physicalKind)
    {
        var context = await Context(); var accepted = await AcceptedAsync();
        await OwnerSqlAsync(physicalKind ?
            "ALTER TABLE quotations.responses DISABLE TRIGGER immutable_response; UPDATE quotations.responses SET kind=2" :
            "ALTER TABLE quotations.receipts DISABLE TRIGGER immutable_receipt; UPDATE quotations.receipts SET version=1 WHERE operation='accept'");
        if (physicalKind)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Store.FindAsync(context, accepted.QuotationId, default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Store.FindResponseAsync(context, accepted.QuotationId, accepted.CurrentIssued!.RevisionId, default));
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(() => Store.FindReceiptAsync(context, "accept", "accept", Fingerprint("accept"), default));
    }
    [Fact]
    public async Task QuotationOriginAndResponseMigrationsRejectWholeChainDowngradeWithoutDiscardingAcceptedFacts()
    {
        var context = await Context(); var accepted = await AcceptedAsync(); var store = ConversionStore;
        var converted = (await store.ConvertAsync(context, accepted.QuotationId, new(accepted.CurrentIssued!.RevisionId, accepted.Version),
            "convert", Fingerprint("convert"), default)).Quotation!;
        var options = new DbContextOptionsBuilder<OrderDbContext>();
        PostgresOrderOptions.Configure(options, _database.GetConnectionString());
        await using var orders = new OrderDbContext(options.Options);
        await Assert.ThrowsAsync<NotSupportedException>(() => orders.GetService<IMigrator>().MigrateAsync("202610070001_OrderCommercialFacts"));
        await using var quotations = QuotationsPostgresRegistration.CreateContext(_database.GetConnectionString());
        await Assert.ThrowsAsync<NotSupportedException>(() => quotations.GetService<IMigrator>().MigrateAsync("202610070020_QuotationFacts"));
        Assert.Equivalent(converted, await Store.FindAsync(context, accepted.QuotationId, default));
        var guarded = new PostgresOrderDraftStore(_runtime, _clock, new OrderCommercialCommitGuard(null!, null!, null!, store));
        Assert.Equivalent(converted.Conversion!.OriginalOrder, await guarded.FindAsync(context, converted.Conversion.OriginalOrder.OrderId, default));
    }
    private async Task<long> RuntimeSqlAsync(string sql, Guid tenantId)
    {
        await using var connection = await _runtime.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        await using (var tenant = new NpgsqlCommand("SELECT set_config('app.current_tenant',@tenant,true)", connection, transaction))
        { tenant.Parameters.AddWithValue("tenant", tenantId.ToString("D")); await tenant.ExecuteNonQueryAsync(); }
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
    private async Task OwnerSqlAsync(string sql, params (string Name, object Value)[] values)
    {
        await using var connection = new NpgsqlConnection(_database.GetConnectionString()); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection); foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
    private async Task<long> CountAsync(string table)
    {
        Assert.Contains(table, CountTables);
        await using var connection = await _runtime.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        await using var tenant = new NpgsqlCommand("SELECT set_config('app.current_tenant',@tenant,true)", connection, transaction);
        tenant.Parameters.AddWithValue("tenant", _tenant.ToString("D")); await tenant.ExecuteNonQueryAsync();
        await using var count = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection, transaction); return (long)(await count.ExecuteScalarAsync())!;
    }
    private sealed class OrderWriter(bool afterWrites = false, Func<NpgsqlConnection, CancellationToken, Task>? pause = null) : IQuotationOrderWriter
    {
        public async Task<OrderDraftSnapshot> CreateAsync(TenantContext context, AcceptedQuotationOrder accepted, NpgsqlConnection connection,
            NpgsqlTransaction transaction, DateTimeOffset createdAt, CancellationToken ct)
        {
            if (!afterWrites && pause is not null) await pause(connection, ct);
            var order = await PostgresOrderDraftStore.CreateAcceptedQuotationAsync(context, accepted, connection, transaction, createdAt, ct);
            if (afterWrites && pause is not null) await pause(connection, ct); return order;
        }
    }
}
