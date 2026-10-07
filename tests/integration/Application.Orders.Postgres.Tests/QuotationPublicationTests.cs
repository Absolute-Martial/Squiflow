using Application.Catalog;
using Application.Catalog.Postgres;
using Application.Customers;
using Application.Customers.Postgres;
using Application.Pricing;
using Application.Pricing.Postgres;
using Application.Quotations;
using Application.Quotations.Postgres;
using Application.Tenancy;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
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
            var compatible = await pricing.IsCompatibleAsync(context, offer, ct);
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
            var result = await pricing.IsCompatibleAsync(context, offer, ct); Assert.True(result);
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
            Assert.True(await pricing.IsCompatibleAsync(context, offer, ct));
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

    private (QuotationApplication Application, QuotationPricing Pricing) QuoteApplication(NpgsqlDataSource source)
    {
        var catalog = new PostgresCatalogStore(source, new FixedTimeProvider());
        var prices = new PostgresPricingStore(source, new FixedTimeProvider());
        var customers = new PostgresCustomerStore(source);
        var pricing = new QuotationPricing(new(catalog), new(prices, new(prices), prices,
            new CommercialReferences(catalog, ResolveContextAsync), new FixedTimeProvider()),
            new ResolveCustomerOrderContext(customers), new QuoteAuthority(), customers);
        return (new(new PostgresQuotationStore(source, new FixedTimeProvider()), pricing), pricing);
    }
    private sealed class QuoteAuthority : IQuotationAuthority
    { public Task<bool> CheckAsync(TenantContext context, QuotationCapability capability, CancellationToken ct) => Task.FromResult(true); }
}
