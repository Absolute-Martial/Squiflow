using Application.Catalog;
using Application.Catalog.Postgres;
using Application.Pricing;
using Application.Pricing.Postgres;
using Application.Tenancy;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
    [Theory]
    [InlineData("create-unit")]
    [InlineData("create-item")]
    [InlineData("rename-unit")]
    [InlineData("rename-item")]
    [InlineData("retire-unit")]
    [InlineData("retire-item")]
    [InlineData("conversion")]
    [InlineData("availability")]
    [InlineData("publish")]
    [InlineData("retire")]
    [InlineData("policy")]
    public async Task CommercialPublicationPinExcludesEveryOwningMutationBeforeRowLocksAcrossRuntimeInstances(string mutation)
    {
        await ApplyOrderSchemaAsync(); // The actual ordered production migrator, not a synthetic schema.
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        var runtime = await CreateCommercialRuntimeAsync();
        await using var readerSource = CommercialSource(runtime, "publication-reader", 2);
        await using var writerSource = CommercialSource(runtime, "publication-writer", 1);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(readerSource, context);
        var gate = new ObservedCommercialGuard(CommercialGuard(readerSource), pauseBeforeComparison: true);
        var orders = new PostgresOrderDraftStore(readerSource, new FixedTimeProvider(), gate);
        var original = (await orders.CreateAsync(context, await RealCommercialIntentAsync(readerSource, context, fixture), "create", default)).Order!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var commit = new CommitOrderDraft(orders).ExecuteAsync(context, new(original.OrderId, 1), "commit", deadline.Token);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var write = ExecuteCommercialMutationAsync(writerSource, context, fixture, mutation, deadline.Token);
        try
        {
            await AssertEffectTransactionOwnsPublicationPinAsync("publication-reader");
            await WaitForPublicationLockAsync("publication-writer", granted: false, "ExclusiveLock");
            Assert.False(write.IsCompleted);
            // Reads remain usable while an exclusive writer waits. In particular,
            // Catalog selection's FOR SHARE must never ask for an exclusive pin.
            var catalog = new PostgresCatalogStore(readerSource, new FixedTimeProvider());
            var selection = await catalog.SelectLineFactsAsync(context, new(fixture.Item.ItemId, fixture.Unit.UnitId, 2m, 1), deadline.Token);
            Assert.Equal(CatalogLineFactsStatus.Available, selection.Status);
            Assert.NotNull(await catalog.FindItemAsync(context, fixture.Item.ItemId, deadline.Token));
            var pricing = new PostgresPricingStore(readerSource, new FixedTimeProvider());
            Assert.NotNull(await pricing.GetCurrentPolicyAsync(new(tenant, account), deadline.Token));
            Assert.Single(await pricing.GetPublishedCandidatesAsync(new(tenant, account),
                new(fixture.Item.ItemId, fixture.Unit.Code, "USD", fixture.Unit.UnitId,
                    new(UnitConversionRevision: 1)), deadline.Token));
            await AssertPublicationWriterHasNoRowLocksAsync("publication-writer");
            // The exclusive writer is queued BEFORE production comparison starts.
            // A second shared pin on any query backend would deadlock this commit.
            gate.Compare.TrySetResult();
            await gate.Compared.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(write.IsCompleted);
        }
        finally
        {
            gate.Compare.TrySetResult(); gate.Continue.TrySetResult();
        }
        Assert.Equal(CommitOrderDraftStatus.Committed, (await commit.WaitAsync(TimeSpan.FromSeconds(20))).Status);
        await write.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(2, await CountReceiptsAsync(tenant));
        Assert.Equal(OrderDraftState.Committed, (await orders.FindAsync(context, original.OrderId, default))!.State);
    }

    [Fact]
    public async Task IndependentTenantPublicationsAndConcurrentSharedPinsDoNotSerializeTogether()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid(); var other = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant, other);
        var runtime = await CreateCommercialRuntimeAsync();
        await using var first = CommercialSource(runtime, "tenant-first", 2);
        await using var second = CommercialSource(runtime, "tenant-second", 2);
        await using var writer = CommercialSource(runtime, "tenant-other-writer", 1);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(first, context);
        var intent = await RealCommercialIntentAsync(first, context, fixture);
        var firstGate = new ObservedCommercialGuard(CommercialGuard(first));
        var secondGate = new ObservedCommercialGuard(CommercialGuard(second));
        var firstOrders = new PostgresOrderDraftStore(first, new FixedTimeProvider(), firstGate);
        var secondOrders = new PostgresOrderDraftStore(second, new FixedTimeProvider(), secondGate);
        var original = (await firstOrders.CreateAsync(context, intent, "first-create", default)).Order!;
        var parallel = (await secondOrders.CreateAsync(context, intent, "second-create", default)).Order!;
        var firstCommit = new CommitOrderDraft(firstOrders).ExecuteAsync(context, new(original.OrderId, 1), "first-commit", default);
        var secondCommit = new CommitOrderDraft(secondOrders).ExecuteAsync(context, new(parallel.OrderId, 1), "second-commit", default);
        try
        {
            await Task.WhenAll(firstGate.Compared.Task, secondGate.Compared.Task).WaitAsync(TimeSpan.FromSeconds(10));
            await AssertEffectTransactionOwnsPublicationPinAsync("tenant-first");
            await AssertEffectTransactionOwnsPublicationPinAsync("tenant-second");
            var result = await new PostgresPricingStore(writer).PublishPolicyAsync(new(other, account),
                new(0, 0, 100), "other-policy", default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(PublishPricingPolicyStatus.Published, result.Status);
        }
        finally { firstGate.Continue.TrySetResult(); secondGate.Continue.TrySetResult(); }
        Assert.All(await Task.WhenAll(firstCommit, secondCommit), result => Assert.Equal(CommitOrderDraftStatus.Committed, result.Status));
    }

    [Fact]
    public async Task CanceledCommitWaitingForPublicationPinRollsBackAndReturnsItsSinglePooledConnection()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        var runtime = await CreateCommercialRuntimeAsync();
        await using var writer = CommercialSource(runtime, "cancel-writer", 2);
        await using var reader = CommercialSource(runtime, "cancel-reader", 1);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(writer, context);
        var orders = new PostgresOrderDraftStore(reader, new FixedTimeProvider(), CommercialGuard(reader));
        var original = (await orders.CreateAsync(context, await RealCommercialIntentAsync(writer, context, fixture), "create", default)).Order!;
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var rowLock = await admin.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand("SELECT revision_id FROM pricing.price_revisions WHERE revision_id=@id FOR UPDATE", admin, rowLock))
        {
            hold.Parameters.AddWithValue("id", fixture.Published.RevisionId);
            await hold.ExecuteScalarAsync();
        }
        using var writerDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var retirement = new PostgresPricingStore(writer).RetireAsync(new(tenant, account),
            new(fixture.Published.RevisionId), "retire", writerDeadline.Token);
        await WaitForPublicationLockAsync("cancel-writer", granted: true, "ExclusiveLock");
        using var cancel = new CancellationTokenSource();
        var waiting = new CommitOrderDraft(orders).ExecuteAsync(context, new(original.OrderId, 1), "commit", cancel.Token);
        await WaitForPublicationLockAsync("cancel-reader", granted: false, "ShareLock");
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        await rowLock.CommitAsync();
        Assert.Equal(RetirePriceStatus.Retired, (await retirement).Status);
        await AssertCommercialPoolIsCleanAsync(reader);
        Assert.Equivalent(original, await orders.FindAsync(context, original.OrderId, default));
        Assert.Equal(1, await CountReceiptsAsync(tenant));
    }

    [Fact]
    public async Task CancellationDuringRealGuardCatalogComparisonRollsBackThePinnedOrdersTransaction()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        var runtime = await CreateCommercialRuntimeAsync();
        await using var reader = CommercialSource(runtime, "canceled-comparison", 2);
        await using var writer = CommercialSource(runtime, "after-canceled-comparison", 1);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(reader, context);
        var orders = new PostgresOrderDraftStore(reader, new FixedTimeProvider(), CommercialGuard(reader));
        var original = (await orders.CreateAsync(context, await RealCommercialIntentAsync(reader, context, fixture), "create", default)).Order!;
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var hold = await admin.BeginTransactionAsync();
        await using (var tableLock = new NpgsqlCommand("LOCK TABLE catalog.items IN ACCESS EXCLUSIVE MODE", admin, hold))
            await tableLock.ExecuteNonQueryAsync();
        using var cancel = new CancellationTokenSource();
        var comparison = new CommitOrderDraft(orders).ExecuteAsync(context, new(original.OrderId, 1), "commit", cancel.Token);
        await WaitForPublicationLockAsync("canceled-comparison", granted: true, "ShareLock");
        await WaitForRelationLockAsync("canceled-comparison", "catalog.items", "RowShareLock", granted: false);
        // Prove the shared pin exists before canceling the blocked public query.
        var policy = new PostgresPricingStore(writer).PublishPolicyAsync(new(tenant, account), new(1, 0, 200), "policy", default);
        await WaitForPublicationLockAsync("after-canceled-comparison", granted: false, "ExclusiveLock");
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => comparison);
        Assert.Equal(PublishPricingPolicyStatus.Published, (await policy.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        await hold.RollbackAsync();
        Assert.Equivalent(original, await orders.FindAsync(context, original.OrderId, default));
        Assert.Equal(1, await CountReceiptsAsync(tenant));
        await AssertCommercialPoolIsCleanAsync(reader);
    }

    [Fact]
    public async Task OrdersTransactionKeepsPublicationExcludedDuringReceiptInsertThenReplaysAfterRetirementWithTwoPooledConnections()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        var runtime = await CreateCommercialRuntimeAsync();
        await using var reader = CommercialSource(runtime, "orders-commercial-commit", 2);
        await using var writer = CommercialSource(runtime, "orders-racing-retirement", 1);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(reader, context);
        var production = CommercialGuard(reader);
        var gate = new ObservedCommercialGuard(production);
        var orders = new PostgresOrderDraftStore(reader, new FixedTimeProvider(), gate);
        var original = (await orders.CreateAsync(context, await RealCommercialIntentAsync(reader, context, fixture), "create", default)).Order!;
        var request = new CommitOrderDraftRequest(original.OrderId, 1);
        var commit = new CommitOrderDraft(orders).ExecuteAsync(context, request, "commit", default);
        await gate.Compared.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var retirement = new PostgresPricingStore(writer).RetireAsync(new(tenant, account),
            new(fixture.Published.RevisionId), "retire", default);
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var receiptBlock = await admin.BeginTransactionAsync();
        try
        {
            await WaitForPublicationLockAsync("orders-racing-retirement", granted: false, "ExclusiveLock");
            await HoldCommercialReceiptInsertAsync(admin, receiptBlock);
            gate.Continue.TrySetResult();
            await WaitForRelationLockAsync("orders-commercial-commit", "orders.command_receipts", "RowExclusiveLock", granted: false);
            await AssertEffectTransactionOwnsPublicationPinAsync("orders-commercial-commit");
            Assert.False(commit.IsCompleted);
            Assert.False(retirement.IsCompleted);
            // The header UPDATE has run, but neither it nor its receipt is visible.
            Assert.Equivalent(original, await orders.FindAsync(context, original.OrderId, default));
            Assert.Equal(1, await CountReceiptsAsync(tenant));
        }
        finally { gate.Continue.TrySetResult(); await receiptBlock.RollbackAsync(); }
        var committed = await commit.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        Assert.Equivalent(original.Lines, committed.Order!.Lines);
        Assert.Equal(RetirePriceStatus.Retired, (await retirement).Status);
        Assert.Equal(2, await CountReceiptsAsync(tenant));
        Assert.Equivalent(committed.Order, await orders.FindAsync(context, original.OrderId, default));
        var replay = await new CommitOrderDraft(orders).ExecuteAsync(context, request, "commit", default);
        Assert.Equal(CommitOrderDraftStatus.Replayed, replay.Status);
        Assert.Equivalent(committed.Order, replay.Order);
        Assert.Equal(1, gate.Calls);

        var stale = (await orders.CreateAsync(context, await RealCommercialIntentFromRetainedAsync(reader, context, fixture), "stale-create", default)).Order!;
        var guarded = new PostgresOrderDraftStore(reader, new FixedTimeProvider(), production);
        Assert.Equal(CommitOrderDraftStatus.CommercialFactsConflict,
            (await new CommitOrderDraft(guarded).ExecuteAsync(context, new(stale.OrderId, 1), "stale-commit", default)).Status);
        Assert.Equal(OrderDraftState.Draft, (await orders.FindAsync(context, stale.OrderId, default))!.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TerminatingTheOrdersPinBackendAfterRealComparisonCannotCommitStaleFactsAndPublisherProgresses(bool headerAlreadyUpdated)
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        var runtime = await CreateCommercialRuntimeAsync();
        await using var source = CommercialSource(runtime, "terminated-orders", 2);
        await using var writer = CommercialSource(runtime, "after-orders-termination", 1);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var production = CommercialGuard(source);
        var gate = new ObservedCommercialGuard(production);
        var orders = new PostgresOrderDraftStore(source, new FixedTimeProvider(), gate);
        var original = (await orders.CreateAsync(context, await RealCommercialIntentAsync(source, context, fixture), "create", default)).Order!;
        var commit = new CommitOrderDraft(orders).ExecuteAsync(context, new(original.OrderId, 1), "commit", default);
        await gate.Compared.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var retirement = new PostgresPricingStore(writer).RetireAsync(new(tenant, account), new(fixture.Published.RevisionId), "retire", default);
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var receiptBlock = await admin.BeginTransactionAsync();
        try
        {
            await WaitForPublicationLockAsync("after-orders-termination", granted: false, "ExclusiveLock");
            if (headerAlreadyUpdated)
            {
                await HoldCommercialReceiptInsertAsync(admin, receiptBlock);
                gate.Continue.TrySetResult();
                await WaitForRelationLockAsync("terminated-orders", "orders.command_receipts", "RowExclusiveLock", granted: false);
            }
            var pid = await AssertEffectTransactionOwnsPublicationPinAsync("terminated-orders");
            await TerminateCommercialBackendAsync(pid);
            // No guard unwind, heartbeat, final read or separate pin disposal is
            // needed for progress: killing THIS backend releases its transaction.
            Assert.Equal(RetirePriceStatus.Retired, (await retirement.WaitAsync(TimeSpan.FromSeconds(10))).Status);
            gate.Continue.TrySetResult();
            await Assert.ThrowsAnyAsync<NpgsqlException>(() => commit);
        }
        finally { gate.Continue.TrySetResult(); await receiptBlock.RollbackAsync(); }
        Assert.Equivalent(original, await orders.FindAsync(context, original.OrderId, default));
        Assert.Equal(1, await CountReceiptsAsync(tenant));
        var recovered = new PostgresOrderDraftStore(source, new FixedTimeProvider(), production);
        Assert.Equal(CommitOrderDraftStatus.CommercialFactsConflict,
            (await new CommitOrderDraft(recovered).ExecuteAsync(context, new(original.OrderId, 1), "commit", default)).Status);
        Assert.Equivalent(original, await orders.FindAsync(context, original.OrderId, default));
        Assert.Equal(1, await CountReceiptsAsync(tenant));
        await AssertCommercialPoolIsCleanAsync(source);
    }

    [Fact]
    public async Task PublicationWinningBeforeOrdersPinIsObservedByRealComparisonAndRejectsWithoutEffectOrReceipt()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        var runtime = await CreateCommercialRuntimeAsync();
        await using var source = CommercialSource(runtime, "commit-behind-publication", 2);
        await using var writer = CommercialSource(runtime, "winning-publication", 1);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var orders = new PostgresOrderDraftStore(source, new FixedTimeProvider(), CommercialGuard(source));
        var original = (await orders.CreateAsync(context, await RealCommercialIntentAsync(source, context, fixture), "create", default)).Order!;
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var hold = await admin.BeginTransactionAsync();
        await HoldPublishedPriceRowAsync(admin, hold, fixture.Published.RevisionId);
        var retirement = new PostgresPricingStore(writer).RetireAsync(new(tenant, account), new(fixture.Published.RevisionId), "retire", default);
        await WaitForPublicationLockAsync("winning-publication", granted: true, "ExclusiveLock");
        var commit = new CommitOrderDraft(orders).ExecuteAsync(context, new(original.OrderId, 1), "commit", default);
        await WaitForPublicationLockAsync("commit-behind-publication", granted: false, "ShareLock");
        await hold.RollbackAsync();
        Assert.Equal(RetirePriceStatus.Retired, (await retirement.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        Assert.Equal(CommitOrderDraftStatus.CommercialFactsConflict, (await commit.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        Assert.Equivalent(original, await orders.FindAsync(context, original.OrderId, default));
        Assert.Equal(1, await CountReceiptsAsync(tenant));
        await AssertCommercialPoolIsCleanAsync(source);
    }

    [Fact]
    public async Task SameKeyReplayConflictAndManualCommitDoNotWaitForAnExclusivePublisher()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        var runtime = await CreateCommercialRuntimeAsync();
        await using var source = CommercialSource(runtime, "replay-and-manual", 2);
        await using var writer = CommercialSource(runtime, "publisher-during-replay", 1);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var orders = new PostgresOrderDraftStore(source, new FixedTimeProvider(), CommercialGuard(source));
        var original = (await orders.CreateAsync(context, await RealCommercialIntentAsync(source, context, fixture), "commercial-create", default)).Order!;
        var manual = (await orders.CreateAsync(context, CreateIntent("Manual draft"), "manual-create", default)).Order!;
        var committer = new CommitOrderDraft(orders);
        var request = new CommitOrderDraftRequest(original.OrderId, 1);
        var committed = await committer.ExecuteAsync(context, request, "commit", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var hold = await admin.BeginTransactionAsync();
        await HoldPublishedPriceRowAsync(admin, hold, fixture.Published.RevisionId);
        var retirement = new PostgresPricingStore(writer).RetireAsync(new(tenant, account), new(fixture.Published.RevisionId), "retire", default);
        try
        {
            await WaitForPublicationLockAsync("publisher-during-replay", granted: true, "ExclusiveLock");
            var replay = await committer.ExecuteAsync(context, request, "commit", default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(CommitOrderDraftStatus.Replayed, replay.Status);
            Assert.Equivalent(committed.Order, replay.Order);
            Assert.Equal(CommitOrderDraftStatus.IdempotencyKeyConflict,
                (await committer.ExecuteAsync(context, request with { ExpectedRevision = 2 }, "commit", default).WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.Equal(CommitOrderDraftStatus.Committed,
                (await committer.ExecuteAsync(context, new(manual.OrderId, 1), "manual-commit", default).WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.False(retirement.IsCompleted);
        }
        finally { await hold.RollbackAsync(); }
        Assert.Equal(RetirePriceStatus.Retired, (await retirement.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        Assert.Equal(4, await CountReceiptsAsync(tenant));
    }

    private static NpgsqlDataSource CommercialSource(string connectionString, string name, int maximumPoolSize) =>
        new NpgsqlDataSourceBuilder(new NpgsqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = name,
            MaxPoolSize = maximumPoolSize,
            Timeout = 5,
            CommandTimeout = 20,
        }.ConnectionString).Build();

    [Fact]
    public async Task SameKeyCommitWaitingOnDraftLockReplaysBeforePinOrComparisonEvenWithoutAConfiguredGuard()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        var runtime = await CreateCommercialRuntimeAsync();
        await using var first = CommercialSource(runtime, "same-key-first", 2);
        await using var second = CommercialSource(runtime, "same-key-waiter", 1);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(first, context);
        var gate = new ObservedCommercialGuard(CommercialGuard(first));
        var orders = new PostgresOrderDraftStore(first, new FixedTimeProvider(), gate);
        var original = (await orders.CreateAsync(context, await RealCommercialIntentAsync(first, context, fixture), "create", default)).Order!;
        var request = new CommitOrderDraftRequest(original.OrderId, 1);
        var committing = new CommitOrderDraft(orders).ExecuteAsync(context, request, "commit", default);
        await gate.Compared.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var unconfigured = new PostgresOrderDraftStore(second, new FixedTimeProvider());
        var waiter = new CommitOrderDraft(unconfigured).ExecuteAsync(context, request, "commit", default);
        try
        {
            await WaitForRelationLockAsync("same-key-waiter", "orders.order_drafts", "RowShareLock", granted: true);
            Assert.False(waiter.IsCompleted);
        }
        finally { gate.Continue.TrySetResult(); }
        var committed = await committing.WaitAsync(TimeSpan.FromSeconds(10));
        var replay = await waiter.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        Assert.Equal(CommitOrderDraftStatus.Replayed, replay.Status);
        Assert.Equivalent(committed.Order, replay.Order);
        Assert.Equal(1, gate.Calls);
        Assert.Equal(2, await CountReceiptsAsync(tenant));
        await AssertCommercialPoolIsCleanAsync(second);
    }

    private async Task<string> CreateCommercialRuntimeAsync()
    {
        var runtime = await CreateRuntimeRoleAsync();
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var transaction = await admin.BeginTransactionAsync();
        await using (var role = new NpgsqlCommand("SELECT set_config('app.provision_runtime_role', @role, true)", admin, transaction))
        {
            role.Parameters.AddWithValue("role", "application_orders_runtime");
            await role.ExecuteNonQueryAsync();
        }
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "DatabaseProvisioning", "grant-core-api-runtime.sql"));
        await using (var grants = new NpgsqlCommand(script, admin, transaction))
            await grants.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return runtime;
    }

    private static async Task<CommercialFixture> CreateCommercialFixtureAsync(NpgsqlDataSource source, TenantContext context)
    {
        var catalog = new PostgresCatalogStore(source, new FixedTimeProvider());
        var unit = (await new CreateCatalogUnit(catalog).ExecuteAsync(context, new("BOX", "Box", 4), "unit", default)).Unit!;
        var basis = (await new CreateCatalogUnit(catalog).ExecuteAsync(context, new("EA", "Each", 4), "base", default)).Unit!;
        var item = (await new CreateCatalogItem(catalog).ExecuteAsync(context,
            new("SKU", "Commercial fixture", null, CatalogItemKind.Product, basis.UnitId, CatalogStockMode.AvailabilityOnly), "item", default)).Item!;
        item = (await new ChangeCatalogAvailability(catalog).ExecuteAsync(context, new(item.ItemId, 1, CatalogAvailability.Available), "available", default)).Item!;
        await new PublishCatalogConversion(catalog).ExecuteAsync(context, new(unit.UnitId, basis.UnitId, 0, 2, 1), "conversion", default);
        var actor = new PricingActorContext(context.TenantId, context.AccountId);
        var pricing = new PostgresPricingStore(source, new FixedTimeProvider());
        await pricing.PublishPolicyAsync(actor, new(0, 0, 100), "initial-policy", default);
        var draft = (await pricing.CreateDraftAsync(actor,
            new(item.ItemId, unit.Code, "USD", PriceScope.Default(), 12,
                new(new FixedTimeProvider().GetUtcNow().AddDays(-1)), unit.UnitId, UnitConversionRevision: 1), "price", default)).Price!;
        var published = (await pricing.PublishAsync(actor, new(draft.RevisionId), "initial-publish", default)).Price!;
        var replacement = (await pricing.CreateDraftAsync(actor,
            new(item.ItemId, unit.Code, "USD", PriceScope.Default(), 14, published.Validity,
                unit.UnitId, published.PriceId, 1), "replacement", default)).Price!;
        return new(unit, basis, item, published, replacement);
    }

    private static async Task ExecuteCommercialMutationAsync(NpgsqlDataSource source, TenantContext context, CommercialFixture fixture,
        string mutation, CancellationToken ct)
    {
        var catalog = new PostgresCatalogStore(source, new FixedTimeProvider());
        var pricing = new PostgresPricingStore(source, new FixedTimeProvider());
        var actor = new PricingActorContext(context.TenantId, context.AccountId);
        switch (mutation)
        {
            case "create-unit": Assert.Equal(CreateCatalogUnitStatus.Created, (await new CreateCatalogUnit(catalog).ExecuteAsync(context, new("NEW", "New", 4), "new-unit", ct)).Status); break;
            case "create-item": Assert.Equal(CreateCatalogItemStatus.Created, (await new CreateCatalogItem(catalog).ExecuteAsync(context, new("NEW", "New", null, CatalogItemKind.Product, fixture.Unit.UnitId, CatalogStockMode.NonStock), "new-item", ct)).Status); break;
            case "rename-unit": Assert.Equal(RenameCatalogUnitStatus.Renamed, (await new RenameCatalogUnit(catalog).ExecuteAsync(context, new(fixture.Unit.UnitId, 1, "Renamed"), "rename-unit", ct)).Status); break;
            case "rename-item": Assert.Equal(RenameCatalogItemStatus.Renamed, (await new RenameCatalogItem(catalog).ExecuteAsync(context, new(fixture.Item.ItemId, 2, "Renamed", null), "rename-item", ct)).Status); break;
            case "retire-unit": Assert.Equal(RetireCatalogUnitStatus.Retired, (await new RetireCatalogUnit(catalog).ExecuteAsync(context, new(fixture.Unit.UnitId, 1), "retire-unit", ct)).Status); break;
            case "retire-item": Assert.Equal(RetireCatalogItemStatus.Retired, (await new RetireCatalogItem(catalog).ExecuteAsync(context, new(fixture.Item.ItemId, 2), "retire-item", ct)).Status); break;
            case "conversion": Assert.Equal(PublishCatalogConversionStatus.Published, (await new PublishCatalogConversion(catalog).ExecuteAsync(context, new(fixture.Unit.UnitId, fixture.BaseUnit.UnitId, 1, 3, 1), "new-conversion", ct)).Status); break;
            case "availability": Assert.Equal(ChangeCatalogAvailabilityStatus.Changed, (await new ChangeCatalogAvailability(catalog).ExecuteAsync(context, new(fixture.Item.ItemId, 2, CatalogAvailability.Unavailable), "unavailable", ct)).Status); break;
            case "publish": Assert.Equal(PublishPriceStatus.Published, (await pricing.PublishAsync(actor, new(fixture.Replacement.RevisionId, fixture.Published.RevisionId), "publish", ct)).Status); break;
            case "retire": Assert.Equal(RetirePriceStatus.Retired, (await pricing.RetireAsync(actor, new(fixture.Published.RevisionId), "retire", ct)).Status); break;
            case "policy": Assert.Equal(PublishPricingPolicyStatus.Published, (await pricing.PublishPolicyAsync(actor, new(1, 0, 200), "policy", ct)).Status); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
    }

    private async Task WaitForPublicationLockAsync(string applicationName, bool granted, string mode)
    {
        await using var monitor = new NpgsqlConnection(ConnectionString);
        await monitor.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var probe = new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid
                  WHERE a.application_name=@name AND l.locktype='advisory' AND l.mode=@mode AND l.granted=@granted)
                """, monitor);
            probe.Parameters.AddWithValue("name", applicationName); probe.Parameters.AddWithValue("mode", mode);
            probe.Parameters.AddWithValue("granted", granted);
            if ((bool)(await probe.ExecuteScalarAsync(timeout.Token))!) return;
            await Task.Delay(10, timeout.Token); // Poll an authoritative lock signal; elapsed time is not evidence.
        }
    }

    private async Task AssertPublicationWriterHasNoRowLocksAsync(string applicationName)
    {
        await using var monitor = new NpgsqlConnection(ConnectionString);
        await monitor.OpenAsync();
        await using var probe = new NpgsqlCommand("""
            SELECT count(*) FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid
            WHERE a.application_name=@name AND (l.locktype='tuple' OR l.mode IN ('RowShareLock','RowExclusiveLock'))
            """, monitor);
        probe.Parameters.AddWithValue("name", applicationName);
        Assert.Equal(0L, await probe.ExecuteScalarAsync());
    }

    private async Task WaitForRelationLockAsync(string name, string relation, string mode, bool granted)
    {
        await using var monitor = new NpgsqlConnection(ConnectionString);
        await monitor.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var probe = new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid
                  WHERE a.application_name=@name AND l.relation=@relation::regclass AND l.mode=@mode AND l.granted=@granted)
                """, monitor);
            probe.Parameters.AddWithValue("name", name); probe.Parameters.AddWithValue("relation", relation);
            probe.Parameters.AddWithValue("mode", mode); probe.Parameters.AddWithValue("granted", granted);
            if ((bool)(await probe.ExecuteScalarAsync(timeout.Token))!) return;
            await Task.Delay(10, timeout.Token);
        }
    }

    private async Task<int> AssertEffectTransactionOwnsPublicationPinAsync(string name, string relation = "orders.order_drafts")
    {
        await using var monitor = new NpgsqlConnection(ConnectionString);
        await monitor.OpenAsync();
        await using var probe = new NpgsqlCommand("""
            SELECT p.pid, EXISTS(SELECT 1 FROM pg_locks r WHERE r.pid=p.pid AND r.granted
              AND r.relation=@relation::regclass AND r.mode='RowShareLock')
            FROM pg_locks p JOIN pg_stat_activity a ON a.pid=p.pid
            WHERE a.application_name=@name AND p.locktype='advisory' AND p.mode='ShareLock' AND p.granted
            """, monitor);
        probe.Parameters.AddWithValue("name", name);
        probe.Parameters.AddWithValue("relation", relation);
        await using var reader = await probe.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var pid = reader.GetInt32(0);
        Assert.True(reader.GetBoolean(1)); // Same backend owns the Orders row lock and shared publication pin.
        Assert.False(await reader.ReadAsync()); // No dedicated/second shared-pin backend exists.
        return pid;
    }

    private async Task TerminateCommercialBackendAsync(int pid)
    {
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@pid)", admin);
        terminate.Parameters.AddWithValue("pid", pid);
        Assert.True((bool)(await terminate.ExecuteScalarAsync())!);
    }

    private static async Task HoldCommercialReceiptInsertAsync(NpgsqlConnection admin, NpgsqlTransaction transaction)
    {
        await using var block = new NpgsqlCommand("LOCK TABLE orders.command_receipts IN SHARE MODE", admin, transaction);
        await block.ExecuteNonQueryAsync();
    }

    private static async Task HoldPublishedPriceRowAsync(NpgsqlConnection admin, NpgsqlTransaction transaction, Guid revision)
    {
        await using var block = new NpgsqlCommand("SELECT revision_id FROM pricing.price_revisions WHERE revision_id=@id FOR UPDATE", admin, transaction);
        block.Parameters.AddWithValue("id", revision);
        await block.ExecuteScalarAsync();
    }

    private static async Task AssertCommercialPoolIsCleanAsync(NpgsqlDataSource source)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var connections = new List<NpgsqlConnection>();
        try
        {
            // Hold every slot simultaneously: a leaked transaction/query slot
            // cannot hide behind recovery of one other idle pooled connection.
            var slots = new NpgsqlConnectionStringBuilder(source.ConnectionString).MaxPoolSize;
            for (var index = 0; index < slots; index++)
            {
                var connection = await source.OpenConnectionAsync(timeout.Token);
                connections.Add(connection);
                Assert.Null(await ReadTenantContextAsync(connection));
                await using var probe = new NpgsqlCommand("SELECT count(*) FROM pg_locks WHERE pid=pg_backend_pid() AND locktype='advisory'", connection);
                Assert.Equal(0L, await probe.ExecuteScalarAsync(timeout.Token));
            }
        }
        finally
        {
            foreach (var connection in connections) await connection.DisposeAsync();
        }
    }

    private OrderCommercialCommitGuard CommercialGuard(NpgsqlDataSource source)
    {
        var catalog = new PostgresCatalogStore(source, new FixedTimeProvider());
        var pricing = new PostgresPricingStore(source, new FixedTimeProvider());
        var references = new CommercialReferences(catalog, ResolveContextAsync);
        return new(new(catalog),
            new(pricing, new(pricing), pricing, references, new FixedTimeProvider()), new NoOverrideAuthority());
    }

    private async Task<OrderDraftIntent> RealCommercialIntentAsync(NpgsqlDataSource source, TenantContext context, CommercialFixture fixture)
    {
        var catalog = (await new PostgresCatalogStore(source).SelectLineFactsAsync(context,
            new(fixture.Item.ItemId, fixture.Unit.UnitId, 2, 1), default)).Facts!;
        var pricing = new PostgresPricingStore(source, new FixedTimeProvider());
        var application = new PricingApplication(pricing, new(pricing), pricing,
            new CommercialReferences(new(source), ResolveContextAsync), new FixedTimeProvider());
        var selected = Assert.IsType<PriceResolved>(await application.ResolveAsync(new(context.TenantId, context.AccountId),
            fixture.Item.ItemId, fixture.Unit.UnitId, "USD", new(UnitConversionRevision: 1), null, null, false, false, default));
        return IntentFromSelection(catalog, selected);
    }

    private static async Task<OrderDraftIntent> RealCommercialIntentFromRetainedAsync(NpgsqlDataSource source, TenantContext context, CommercialFixture fixture)
    {
        var catalog = (await new PostgresCatalogStore(source).SelectLineFactsAsync(context,
            new(fixture.Item.ItemId, fixture.Unit.UnitId, 2, 1), default)).Facts!;
        var selected = Assert.IsType<PriceResolved>(PriceSelectionEngine.Resolve(
            new(context.TenantId, fixture.Item.ItemId, fixture.Unit.Code, "USD", new(UnitConversionRevision: 1),
                new FixedTimeProvider().GetUtcNow(), 1, fixture.Unit.UnitId), [fixture.Published], new(1, 0, 100)));
        return IntentFromSelection(catalog, selected);
    }

    private static OrderDraftIntent IntentFromSelection(CatalogLineFacts catalog, PriceResolved selection)
    {
        var intent = OrderDraftIntent.Create(new("Real commercial selection", "USD", [new(catalog.ItemName, 2, catalog.UnitCode, selection.UnitPrice)]));
        return intent with
        {
            Lines = [intent.Lines[0] with { CommercialFacts = new(catalog, selection.BasePrice,
            new(catalog.ItemId, catalog.UnitId, "USD", selection.UnitPrice, selection.Explanation)) }]
        };
    }

    private sealed record CommercialFixture(CatalogUnitSnapshot Unit, CatalogUnitSnapshot BaseUnit, CatalogItemSnapshot Item,
        PriceRevision Published, PriceRevision Replacement);

    private sealed class NoOverrideAuthority : IOrderPricingAuthorityReader
    {
        public Task<OrderPricingAuthority> ReadAsync(TenantContext context, CancellationToken ct) =>
            throw new InvalidOperationException("These real-source fixtures have no override requiring authority.");
    }

    private sealed class CommercialReferences(PostgresCatalogStore catalog, Func<Guid, Guid, Task<TenantContext>> resolve) : IPricingReferenceReader
    {
        public async Task<string> RequireCompatibleUnitAsync(PricingActorContext actor, Guid itemId, Guid unitId, long? revision, CancellationToken ct)
        {
            var context = await resolve(actor.TenantId, actor.AccountId);
            var item = await catalog.FindItemAsync(context, itemId, ct);
            var unit = await catalog.FindUnitAsync(context, unitId, ct);
            if (item is null || unit is null || item.Status != CatalogEntityStatus.Active || unit.Status != CatalogEntityStatus.Active)
                throw new PricingValidationException("catalog_unavailable", "The selected catalog facts are unavailable.");
            if (item.BaseUnitId != unitId && (!revision.HasValue ||
                await catalog.FindConversionAsync(context, unitId, item.BaseUnitId, revision.Value, ct) is null))
                throw new PricingValidationException("catalog_conversion_invalid", "The selected conversion is unavailable.");
            return unit.Code;
        }

        public async Task RequireContextAsync(PricingActorContext actor, PriceSelectionContext context, CancellationToken ct)
        {
            _ = await resolve(actor.TenantId, actor.AccountId);
            Assert.Null(context.CustomerId); Assert.Null(context.OrganizationId); Assert.Null(context.ProgramId);
        }
    }

    // Pauses around the actual production comparison; never supplies its result or
    // locks. The real Orders provider must own the only shared pin in its transaction.
    private sealed class ObservedCommercialGuard(IOrderCommercialCommitGuard production, bool pauseBeforeComparison = false) : IOrderCommercialCommitGuard
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Compare { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Compared { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls { get; private set; }
        public async Task<bool> IsCompatibleAsync(TenantContext context, OrderDraftSnapshot order, CancellationToken ct)
        {
            Calls++;
            Entered.TrySetResult();
            if (pauseBeforeComparison) await Compare.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            var compatible = await production.IsCompatibleAsync(context, order, ct);
            Assert.True(compatible);
            Compared.TrySetResult();
            await Continue.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            return compatible;
        }
    }
}
