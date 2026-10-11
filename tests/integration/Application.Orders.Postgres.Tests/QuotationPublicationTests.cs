using Application.Catalog;
using Application.Catalog.Postgres;
using Application.Customers;
using Application.Customers.Postgres;
using Application.Pricing;
using Application.Pricing.Postgres;
using Application.Orders;
using Application.Quotations;
using Application.Quotations.Postgres;
using Application.Tenancy;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
    [Fact]
    public async Task QuoteAcceptanceConversionAndCommitKeepFrozenPriceAfterActualSourcesAndAuthorityChange()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid(); await SeedAuthorityRowsAsync(account, tenant);
        await using var source = CommercialSource(await CreateCommercialRuntimeAsync(), "quote-accepted-price", 5);
        var context = await ResolveContextAsync(tenant, account); var fixture = await CreateCommercialFixtureAsync(source, context);
        var (initial, _) = QuoteApplication(source);
        var draft = (await initial.CreateAsync(context, new(QuotationPriceMode.Catalog, "Accepted override offer", "USD", new FixedTimeProvider().GetUtcNow().AddDays(1),
            [new(2, ItemId: fixture.Item.ItemId, UnitId: fixture.Unit.UnitId, ConversionRevision: 1, OverridePrice: 150, OverrideReason: "Synthetic owner exception")]), "quote-create", default)).Quotation!;
        var issued = (await initial.IssueAsync(context, draft.QuotationId, draft.Version, "quote-issue", default)).Quotation!;
        Assert.True(issued.CurrentIssued!.Offer.Lines[0].PriceSelection!.Explanation.Override!.BeyondPolicy);
        var catalog = new PostgresCatalogStore(source, new FixedTimeProvider()); var prices = new PostgresPricingStore(source, new FixedTimeProvider());
        Assert.Equal(RetirePriceStatus.Retired, (await prices.RetireAsync(new(tenant, account), new(fixture.Published.RevisionId), "retire-quoted-price", default)).Status);
        Assert.Equal(PublishPricingPolicyStatus.Published, (await prices.PublishPolicyAsync(new(tenant, account), new(1, 0, 1), "narrow-policy", default)).Status);
        Assert.Equal(RetireCatalogItemStatus.Retired, (await new RetireCatalogItem(catalog).ExecuteAsync(context, new(fixture.Item.ItemId, 2), "retire-quoted-item", default)).Status);
        var clock = new QuoteIssueClock(); var quotes = new PostgresQuotationStore(source, clock, new QuoteConversionWriter());
        var customers = new PostgresCustomerStore(source);
        var pricing = new PricingApplication(prices, new(prices), prices, new CommercialReferences(catalog, ResolveContextAsync), new FixedTimeProvider());
        var limited = new QuotationApplication(quotes, new(new(catalog), pricing, new(customers), new ResponseOnlyQuoteAuthority(), customers));
        var accepted = (await limited.AcceptAsync(context, issued.QuotationId, new(issued.CurrentIssued.RevisionId, issued.Version, "Synthetic customer response", "Synthetic customer"), "accept", default)).Quotation!;
        clock.At = issued.CurrentIssued.Offer.ValidUntil.AddDays(10);
        var converted = (await limited.ConvertAsync(context, issued.QuotationId, new(issued.CurrentIssued.RevisionId, accepted.Version), "convert", default)).Quotation!;
        var original = converted.Conversion!.OriginalOrder; Assert.Equal(300, original.Total); Assert.Equal(150, original.Lines[0].UnitPrice);
        var guard = new OrderCommercialCommitGuard(new(catalog), pricing, new NoOverrideAuthority(), quotes);
        var orderStore = new PostgresOrderDraftStore(source, clock, guard);
        var stored = (await orderStore.FindAsync(context, original.OrderId, default))!;
        Assert.True(await guard.IsCompatibleAsync(context, stored, default));
        Assert.False(await guard.IsCompatibleAsync(context, stored with { Summary = "Changed summary" }, default));
        var line = stored.Lines[0]; var facts = line.CommercialFacts!;
        Assert.False(await guard.IsCompatibleAsync(context, stored with
        {
            Lines = [line with { CommercialFacts=facts with {
            Catalog=facts.Catalog with { ItemRevision=facts.Catalog.ItemRevision+1 } } }]
        }, default));
        Assert.False(await new OrderCommercialCommitGuard(new(catalog), pricing, new NoOverrideAuthority()).IsCompatibleAsync(context, stored, default));
        var committed = await orderStore.CommitAsync(context, new(original.OrderId, 1), "commit-quoted", QuotationRules.Fingerprint("commit-quoted"), default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status); Assert.Equal(300, committed.Order!.Total);
        Assert.Equivalent(original.Lines, committed.Order.Lines);
        var history = await orderStore.ListHistoryAsync(context, new(original.OrderId), default);
        Assert.Equal(2, history!.Items.Count); Assert.NotNull(history.Items[1].Order.QuotationOrigin);
        Assert.Equivalent(original, (await limited.ConvertAsync(context, issued.QuotationId, new(issued.CurrentIssued.RevisionId, accepted.Version), "repeat", default)).Quotation!.Conversion!.OriginalOrder);
    }
    // Retained quotation facts must remain readable and committable after the envelope or quantity
    // predicate SEMANTICS change. The retained snapshot pins the semantics version whose evaluator
    // produced its decision, so this proves the pinned evaluator is reached rather than the current
    // one, and that facts stored before the version existed read back as version 1.
    [Fact]
    public async Task IssuedQuotationStaysReadableAndCommittableAfterAPredicateSemanticsChange()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid(); await SeedAuthorityRowsAsync(account, tenant);
        await using var source = CommercialSource(await CreateCommercialRuntimeAsync(), "quote-semantics-version", 6);
        var context = await ResolveContextAsync(tenant, account); var fixture = await CreateCommercialFixtureAsync(source, context);
        var (application, _) = QuoteApplication(source);
        var draft = (await application.CreateAsync(context, new(QuotationPriceMode.Catalog, "Semantics-pinned offer", "USD",
            new FixedTimeProvider().GetUtcNow().AddDays(1), [new(2, ItemId: fixture.Item.ItemId,
            UnitId: fixture.Unit.UnitId, ConversionRevision: 1)]), "quote-create", default)).Quotation!;
        var issued = (await application.IssueAsync(context, draft.QuotationId, draft.Version, "quote-issue", default)).Quotation!;
        var offer = issued.CurrentIssued!.Offer; var line = offer.Lines[0]; var retained = line.PriceSelection!;
        Assert.Equal(QuantityArithmetic.Version1, line.Catalog!.QuantityArithmeticVersion);
        Assert.Equal(QuantityArithmetic.Version1Rounding, line.Catalog.QuantityRounding);
        Assert.Equal(PricingOverridePolicySemantics.Version1, retained.Explanation.Policy!.EnvelopeSemanticsVersion);

        // Rewrite the immutable stored facts into the shape written before these version fields
        // existed. Every later read must treat them as version 1, not as current semantics.
        await using (var owner = new NpgsqlConnection(ConnectionString))
        {
            await owner.OpenAsync();
            await using var legacy = new NpgsqlCommand("""
                ALTER TABLE quotations.issued DISABLE TRIGGER immutable_issued;
                UPDATE quotations.issued SET facts = jsonb_set(
                    jsonb_set(
                        jsonb_set(facts, '{Offer,Lines,0,Catalog}',
                            (facts#>'{Offer,Lines,0,Catalog}') - 'QuantityArithmeticVersion' - 'QuantityRounding'),
                        '{Offer,Lines,0,PriceSelection,Explanation,Policy}',
                        (facts#>'{Offer,Lines,0,PriceSelection,Explanation,Policy}') - 'EnvelopeSemanticsVersion'),
                    '{TenantId}', to_jsonb(@tenant::text));
                ALTER TABLE quotations.issued ENABLE TRIGGER immutable_issued;
                """, owner);
            legacy.Parameters.AddWithValue("tenant", tenant); await legacy.ExecuteNonQueryAsync();
        }

        // The detail read, issued history, response read and convert replay all revalidate retained facts.
        Assert.Equal(issued.CurrentIssued.Offer.Total, (await application.FindAsync(context, issued.QuotationId, default))!.CurrentIssued!.Offer.Total);
        Assert.Single((await application.ListIssuedAsync(context, issued.QuotationId, 0, 10, default)).Items);
        var quotes = new PostgresQuotationStore(source, new QuoteIssueClock(), new QuoteConversionWriter());
        var customers = new PostgresCustomerStore(source);
        var catalog = new PostgresCatalogStore(source, new FixedTimeProvider());
        var prices = new PostgresPricingStore(source, new FixedTimeProvider());
        var pricing = new PricingApplication(prices, new(prices), prices, new CommercialReferences(catalog, ResolveContextAsync), new FixedTimeProvider());
        var clock = new QuoteIssueClock();
        var limited = new QuotationApplication(quotes, new(new(catalog), pricing, new(customers), new ResponseOnlyQuoteAuthority(), customers));
        var reloaded = await application.FindAsync(context, issued.QuotationId, default);
        Assert.Equal(QuantityArithmetic.Version1, reloaded!.CurrentIssued!.Offer.Lines[0].Catalog!.QuantityArithmeticVersion);
        Assert.Equal(PricingOverridePolicySemantics.Version1, reloaded.CurrentIssued.Offer.Lines[0].PriceSelection!.Explanation.Policy!.EnvelopeSemanticsVersion);
        var accepted = (await limited.AcceptAsync(context, issued.QuotationId,
            new(issued.CurrentIssued.RevisionId, reloaded.Version, "Synthetic customer response", "Synthetic customer"), "accept", default)).Quotation!;
        clock.At = issued.CurrentIssued.Offer.ValidUntil.AddDays(10);
        var converted = (await limited.ConvertAsync(context, issued.QuotationId,
            new(issued.CurrentIssued.RevisionId, accepted.Version), "convert", default)).Quotation!;
        var original = converted.Conversion!.OriginalOrder;

        // The whole historical Order commit path revalidates the same retained facts.
        var orderStore = new PostgresOrderDraftStore(source, clock,
            new OrderCommercialCommitGuard(new(catalog), pricing, new NoOverrideAuthority(), quotes));
        var committed = await orderStore.CommitAsync(context, new(original.OrderId, 1),
            "commit-semantics", QuotationRules.Fingerprint("commit-semantics"), default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        Assert.Equivalent(original.Lines, committed.Order!.Lines);
        Assert.Equivalent(original, (await limited.ConvertAsync(context, issued.QuotationId,
            new(issued.CurrentIssued.RevisionId, accepted.Version), "repeat", default)).Quotation!.Conversion!.OriginalOrder);
    }

    [Fact]
    public async Task QuoteIssueUsesActualCurrentSourcesAndNeverRepricesAStaleDraft()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid(); await SeedAuthorityRowsAsync(account, tenant);
        await using var source = CommercialSource(await CreateCommercialRuntimeAsync(), "quote-revalidation", 2);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var prices = new PostgresPricingStore(source, new FixedTimeProvider());
        var (application, _) = QuoteApplication(source);
        var request = new QuotationDraftRequest(QuotationPriceMode.Catalog, "Catalog offer", "USD", new FixedTimeProvider().GetUtcNow().AddDays(1),
            [new(2, ItemId: fixture.Item.ItemId, UnitId: fixture.Unit.UnitId, ConversionRevision: 1)]);
        var draft = (await application.CreateAsync(context, request, "quote-create", default)).Quotation!;
        Assert.Equal(24, draft.Draft!.Total);
        await prices.PublishPolicyAsync(new(tenant, account), new(1, 0, 200), "new-policy", default);
        var rejected = await application.IssueAsync(context, draft.QuotationId, 1, "quote-issue", default);
        Assert.Equal(QuotationCommandStatus.CommercialFactsConflict, rejected.Status);
        Assert.Equivalent(draft, await application.FindAsync(context, draft.QuotationId, default));
        Assert.Empty((await application.ListIssuedAsync(context, draft.QuotationId, 0, 10, default)).Items);
    }
    [Fact]
    public async Task QuoteIssuePinsCurrentPricingThroughTheActualEffectAndReceiptTransaction()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid(); await SeedAuthorityRowsAsync(account, tenant);
        await using var source = CommercialSource(await CreateCommercialRuntimeAsync(), "quote-effect-pin", 3);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var (application, pricing) = QuoteApplication(source);
        var request = new QuotationDraftRequest(QuotationPriceMode.Catalog, "Pinned offer", "USD", new FixedTimeProvider().GetUtcNow().AddDays(1),
            [new(2, ItemId: fixture.Item.ItemId, UnitId: fixture.Unit.UnitId, ConversionRevision: 1)]);
        var draft = (await application.CreateAsync(context, request, "quote-create", default)).Quotation!;
        var compared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new PostgresQuotationStore(source, new FixedTimeProvider());
        var issuing = store.IssueAsync(context, draft.QuotationId, 1, "quote-issue", QuotationRules.Fingerprint("issue"), async (offer, ct) =>
        {
            var compatible = await pricing.IsCompatibleAsync(context, offer,
            await pricing.ResolveFrozenAuthorityAsync(context, offer, ct), ct);
            Assert.True(compatible); compared.TrySetResult();
            await proceed.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            return compatible;
        }, default);
        await compared.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            await AssertEffectTransactionOwnsPublicationPinAsync("quote-effect-pin", "quotations.heads");
            var retiring = new PostgresPricingStore(source, new FixedTimeProvider()).RetireAsync(new(tenant, account),
                new(fixture.Published.RevisionId), "retire-during-issue", default);
            await WaitForPublicationLockAsync("quote-effect-pin", granted: false, "ExclusiveLock");
            Assert.False(retiring.IsCompleted);
            proceed.TrySetResult();
            var result = await issuing.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(QuotationCommandStatus.Issued, result.Status);
            await retiring.WaitAsync(TimeSpan.FromSeconds(20));
            var replay = await store.IssueAsync(context, draft.QuotationId, 1, "quote-issue", QuotationRules.Fingerprint("issue"),
                (_, _) => throw new InvalidOperationException("Replay must retain the issued selection"), default);
            Assert.Equivalent(result.Quotation, replay.Quotation);
            Assert.Equal(24, replay.Quotation!.CurrentIssued!.Offer.Total);
        }
        finally { proceed.TrySetResult(); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuotePinBackendLossBeforeOrAfterHeaderWriteRollsBackIssueAndUnblocksPublisher(bool afterHeaderWrite)
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid(); await SeedAuthorityRowsAsync(account, tenant);
        await using var source = CommercialSource(await CreateCommercialRuntimeAsync(), "quote-lost-backend", 3);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var (application, pricing) = QuoteApplication(source);
        var request = new QuotationDraftRequest(QuotationPriceMode.Catalog, "Failure-tested offer", "USD", new FixedTimeProvider().GetUtcNow().AddDays(1),
            [new(2, ItemId: fixture.Item.ItemId, UnitId: fixture.Unit.UnitId, ConversionRevision: 1)]);
        var draft = (await application.CreateAsync(context, request, "quote-create", default)).Quotation!;
        var compared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new PostgresQuotationStore(source, new FixedTimeProvider());
        await using var admin = new NpgsqlConnection(ConnectionString); await admin.OpenAsync();
        await using var receiptBlock = await admin.BeginTransactionAsync();
        var issuing = store.IssueAsync(context, draft.QuotationId, 1, "quote-issue", QuotationRules.Fingerprint("issue"), async (offer, ct) =>
        {
            var result = await pricing.IsCompatibleAsync(context, offer,
                await pricing.ResolveFrozenAuthorityAsync(context, offer, ct), ct); Assert.True(result);
            compared.TrySetResult(); await proceed.Task.WaitAsync(TimeSpan.FromSeconds(20), ct); return result;
        }, default);
        await compared.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            var retirement = new PostgresPricingStore(source, new FixedTimeProvider()).RetireAsync(new(tenant, account), new(fixture.Published.RevisionId), "retire", default);
            await WaitForPublicationLockAsync("quote-lost-backend", granted: false, "ExclusiveLock");
            if (afterHeaderWrite)
            {
                await using var hold = new NpgsqlCommand("LOCK TABLE quotations.receipts IN SHARE MODE", admin, receiptBlock);
                await hold.ExecuteNonQueryAsync(); proceed.TrySetResult();
                await WaitForRelationLockAsync("quote-lost-backend", "quotations.receipts", "RowExclusiveLock", granted: false);
            }
            var pid = await AssertEffectTransactionOwnsPublicationPinAsync("quote-lost-backend", "quotations.heads");
            await TerminateCommercialBackendAsync(pid);
            Assert.Equal(RetirePriceStatus.Retired, (await retirement.WaitAsync(TimeSpan.FromSeconds(10))).Status);
            proceed.TrySetResult();
            await Assert.ThrowsAnyAsync<NpgsqlException>(() => issuing);
        }
        finally { proceed.TrySetResult(); await receiptBlock.RollbackAsync(); }
        Assert.Equivalent(draft, await application.FindAsync(context, draft.QuotationId, default));
        Assert.Empty((await application.ListIssuedAsync(context, draft.QuotationId, 0, 10, default)).Items);
        Assert.Null(await store.FindReceiptAsync(context, "issue", "quote-issue", QuotationRules.Fingerprint("issue"), default));
        Assert.Equal(QuotationCommandStatus.CommercialFactsConflict, (await application.IssueAsync(context, draft.QuotationId, 1, "quote-issue", default)).Status);
        await AssertCommercialPoolIsCleanAsync(source);
    }

    [Fact]
    public async Task SlowQuotationAuthorityIsResolvedBeforeTheSharedPublicationPinIsTaken()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid(); await SeedAuthorityRowsAsync(account, tenant);
        var runtime = await CreateCommercialRuntimeAsync();
        await using var source = CommercialSource(runtime, "quote-slow-authority", 4);
        await using var publisher = CommercialSource(runtime, "slow-authority-publisher", 2);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var (application, _) = QuoteApplication(source, new QuoteAuthority());
        var request = new QuotationDraftRequest(QuotationPriceMode.Catalog, "Slow-authority offer", "USD", new FixedTimeProvider().GetUtcNow().AddDays(1),
            [new(2, ItemId: fixture.Item.ItemId, UnitId: fixture.Unit.UnitId, ConversionRevision: 1)]);
        var draft = (await application.CreateAsync(context, request, "quote-create", default)).Quotation!;
        var outstanding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (issuing, _) = QuoteApplication(source, new SlowQuoteAuthority(outstanding, answered));
        var issue = issuing.IssueAsync(context, draft.QuotationId, draft.Version, "quote-issue", default);
        // The outbound authorization round trip is still outstanding here.
        await outstanding.Task.WaitAsync(TimeSpan.FromSeconds(20));
        try
        {
            await AssertNoPublicationPinHeldAsync("quote-slow-authority");
            // Tenant-wide publication must still progress while authority is outstanding. A new unit
            // takes the exclusive pin without changing any fact this quotation compares against.
            var catalog = new PostgresCatalogStore(publisher, new FixedTimeProvider());
            var publication = new CreateCatalogUnit(catalog).ExecuteAsync(context, new("PUB", "Publisher", 4), "publisher-unit", default);
            await WaitForPublicationLockAsync("slow-authority-publisher", granted: true, "ExclusiveLock");
            Assert.Equal(CreateCatalogUnitStatus.Created, (await publication.WaitAsync(TimeSpan.FromSeconds(20))).Status);
            Assert.False(issue.IsCompleted);
            await AssertNoPublicationPinHeldAsync("quote-slow-authority");
        }
        finally { answered.TrySetResult(); }
        // The database comparison and the effect still run inside the pinned transaction.
        Assert.Equal(QuotationCommandStatus.Issued, (await issue.WaitAsync(TimeSpan.FromSeconds(20))).Status);
        Assert.Equal(24, (await issuing.FindAsync(context, draft.QuotationId, default))!.CurrentIssued!.Offer.Total);
    }

    private async Task AssertNoPublicationPinHeldAsync(string applicationName)
    {
        await using var monitor = new NpgsqlConnection(ConnectionString);
        await monitor.OpenAsync();
        // Granted holders and queued waiters both appear in pg_locks, so this proves the shared pin is
        // neither held by nor queued behind this backend while its authority call is outstanding.
        await using var probe = new NpgsqlCommand("""
            SELECT count(*) FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid
            WHERE a.application_name=@name AND l.locktype='advisory' AND l.mode IN ('ShareLock','ExclusiveLock')
            """, monitor);
        probe.Parameters.AddWithValue("name", applicationName);
        Assert.Equal(0L, await probe.ExecuteScalarAsync());
    }

    [Fact]
    public async Task QuoteIssueRejectsSourceExpiryBetweenComparisonAndAuthoritativeIssueTime()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid(); await SeedAuthorityRowsAsync(account, tenant);
        await using var source = CommercialSource(await CreateCommercialRuntimeAsync(), "quote-source-expiry", 3);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var clock = new QuoteIssueClock();
        var prices = new PostgresPricingStore(source, new FixedTimeProvider());
        var expiring = (await prices.CreateDraftAsync(new(tenant, account), new(fixture.Item.ItemId, fixture.Unit.Code, "USD",
            fixture.Published.Key.Scope, 12, new(clock.GetUtcNow().AddDays(-1), clock.GetUtcNow().AddSeconds(1)),
            fixture.Unit.UnitId, fixture.Published.PriceId, 1), "expiring-price", default)).Price!;
        Assert.Equal(PublishPriceStatus.Published, (await prices.PublishAsync(new(tenant, account), new(expiring.RevisionId, fixture.Published.RevisionId), "publish-expiring", default)).Status);
        var (application, pricing) = QuoteApplication(source);
        var draft = (await application.CreateAsync(context, new(QuotationPriceMode.Catalog, "Expiry-tested offer", "USD", clock.GetUtcNow().AddDays(1),
            [new(2, ItemId: fixture.Item.ItemId, UnitId: fixture.Unit.UnitId, ConversionRevision: 1)]), "quote-create", default)).Quotation!;
        Assert.Equal(expiring.RevisionId, draft.Draft!.Lines[0].PublishedPrice!.RevisionId);
        var store = new PostgresQuotationStore(source, clock);
        var result = await store.IssueAsync(context, draft.QuotationId, 1, "quote-issue", QuotationRules.Fingerprint("issue"), async (offer, ct) =>
        {
            Assert.True(await pricing.IsCompatibleAsync(context, offer,
                await pricing.ResolveFrozenAuthorityAsync(context, offer, ct), ct));
            clock.At = expiring.Validity.ValidTo!.Value;
            return true;
        }, default);
        Assert.Equal(QuotationCommandStatus.CommercialFactsConflict, result.Status);
        Assert.Equivalent(draft, await store.FindAsync(context, draft.QuotationId, default));
        Assert.Null(await store.FindReceiptAsync(context, "issue", "quote-issue", QuotationRules.Fingerprint("issue"), default));
        Assert.Empty((await store.ListIssuedAsync(context, draft.QuotationId, 0, 10, default)).Items);
    }
    private sealed class QuoteIssueClock : TimeProvider
    {
        internal DateTimeOffset At { get; set; } = new FixedTimeProvider().GetUtcNow();
        public override DateTimeOffset GetUtcNow() => At;
    }

    private (QuotationApplication Application, QuotationPricing Pricing) QuoteApplication(NpgsqlDataSource source,
        IQuotationAuthority? authority = null)
    {
        var catalog = new PostgresCatalogStore(source, new FixedTimeProvider());
        var prices = new PostgresPricingStore(source, new FixedTimeProvider());
        var customers = new PostgresCustomerStore(source);
        var pricing = new QuotationPricing(new(catalog), new(prices, new(prices), prices,
            new CommercialReferences(catalog, ResolveContextAsync), new FixedTimeProvider()),
            new ResolveCustomerOrderContext(customers), authority ?? new QuoteAuthority(), customers);
        return (new(new PostgresQuotationStore(source, new FixedTimeProvider()), pricing), pricing);
    }
    private sealed class QuoteAuthority : IQuotationAuthority
    { public Task<bool> CheckAsync(TenantContext context, QuotationCapability capability, CancellationToken ct) => Task.FromResult(true); }
    // Stands in for a degraded but reachable OpenFGA: the call is slow, never an error.
    private sealed class SlowQuoteAuthority(TaskCompletionSource outstanding, TaskCompletionSource answered) : IQuotationAuthority
    {
        public async Task<bool> CheckAsync(TenantContext context, QuotationCapability capability, CancellationToken ct)
        {
            if (capability is QuotationCapability.CatalogView or QuotationCapability.PricingView)
            {
                outstanding.TrySetResult();
                await answered.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
            return true;
        }
    }
    private sealed class ResponseOnlyQuoteAuthority : IQuotationAuthority
    {
        public Task<bool> CheckAsync(TenantContext context, QuotationCapability permission, CancellationToken ct) =>
        Task.FromResult(permission is QuotationCapability.Respond or QuotationCapability.Convert or QuotationCapability.OrderCreate or QuotationCapability.View);
    }
    private sealed class QuoteConversionWriter : IQuotationOrderWriter
    {
        public Task<OrderDraftSnapshot> CreateAsync(TenantContext context, AcceptedQuotationOrder accepted, NpgsqlConnection connection,
            NpgsqlTransaction transaction, DateTimeOffset createdAt, CancellationToken ct) =>
            PostgresOrderDraftStore.CreateAcceptedQuotationAsync(context, accepted, connection, transaction, createdAt, ct);
    }
}
