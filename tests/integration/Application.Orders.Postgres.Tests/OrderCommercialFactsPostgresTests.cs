using Application.Catalog;
using Application.Pricing;
using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
    [Fact]
    public async Task CommercialFactsRoundTripThroughDetailRevisionHistoryAbandonmentAndOriginalReceipts()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid(); var foreign = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant, foreign);
        await using var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(source, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenant, account);
        var intent = CommercialIntent(context, unitCode: "UNIT-M2_WITH-LONG-CODE");
        var created = (await store.CreateAsync(context, intent, "commercial-create", default)).Order!;
        Assert.Equivalent(created, await store.FindAsync(context, created.OrderId, default));
        Assert.Null(await store.FindAsync(await ResolveContextAsync(foreign, account), created.OrderId, default));
        var revisedIntent = CommercialIntent(context, "Explicit replacement", 2, "NPR");
        var revised = (await store.ReviseAsync(context, new(created.OrderId, 1, "Explicit replacement", "NPR", []),
            revisedIntent, "commercial-revise", revisedIntent.Fingerprint, default)).Order!;
        Assert.Equivalent(revisedIntent.Lines, (await store.FindAsync(context, created.OrderId, default))!.Lines);
        var abandoned = (await new AbandonOrderDraft(store).ExecuteAsync(context, new(created.OrderId, 2), "abandon", default)).Order!;
        Assert.Equivalent(revised.Lines, abandoned.Lines);
        var replay = await store.FindCreateReceiptAsync(context, "commercial-create", intent.Fingerprint, default);
        Assert.Equal(CreateOrderDraftStatus.Replayed, replay!.Status);
        Assert.Equivalent(created, replay.Order);
        var history = await store.ListHistoryAsync(context, new(created.OrderId, 10), default);
        Assert.Equal(3, history!.Items.Count);
        Assert.Equivalent(created.Lines, history.Items.Single(item => item.Order.Revision == 1).Order.Lines);
        Assert.Equivalent(revised.Lines, history.Items.Single(item => item.Order.Revision == 2).Order.Lines);
    }

    [Fact]
    public async Task SameKeyCommercialCreatesConvergeOnOneWholeFrozenSelection()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        await using var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(source, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenant, account);
        var basis = CommercialIntent(context);
        // The input fingerprint is stable; competing server observations can differ.
        // The first receipt freezes one complete observation, never a mixture.
        var intents = Enumerable.Range(0, 8).Select(index => CommercialIntent(context, $"Observation {index}") with { Fingerprint = basis.Fingerprint }).ToArray();
        var results = await Task.WhenAll(intents.Select(intent => store.CreateAsync(context, intent, "same-key", default)));
        var winner = Assert.Single(results, result => result.Status == CreateOrderDraftStatus.Created).Order!;
        Assert.Equal(7, results.Count(result => result.Status == CreateOrderDraftStatus.Replayed));
        Assert.All(results, result => Assert.Equivalent(winner, result.Order));
        Assert.Equal(1, await CountReceiptsAsync(tenant));
        Assert.Equivalent(winner, await store.FindAsync(context, winner.OrderId, default));
    }

    [Fact]
    public async Task MissingOrConflictingCommercialGuardLeavesDraftAndReceiptSetUnchanged()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        await using var source = new NpgsqlDataSourceBuilder(await CreateCommercialRuntimeAsync()).Build();
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var unconfigured = new PostgresOrderDraftStore(source, new FixedTimeProvider());
        var created = (await unconfigured.CreateAsync(context, await RealCommercialIntentAsync(source, context, fixture), "create", default)).Order!;
        var request = new CommitOrderDraftRequest(created.OrderId, 1);
        Assert.Equal(CommitOrderDraftStatus.CommercialFactsConflict,
            (await new CommitOrderDraft(unconfigured).ExecuteAsync(context, request, "commit", default)).Status);
        await new Application.Pricing.Postgres.PostgresPricingStore(source).PublishPolicyAsync(new(tenant, account), new(1, 0, 200), "changed-policy", default);
        var rejected = new PostgresOrderDraftStore(source, new FixedTimeProvider(), CommercialGuard(source));
        Assert.Equal(CommitOrderDraftStatus.CommercialFactsConflict,
            (await new CommitOrderDraft(rejected).ExecuteAsync(context, request, "commit", default)).Status);
        Assert.Equivalent(created, await unconfigured.FindAsync(context, created.OrderId, default));
        Assert.Equal(1, await CountReceiptsAsync(tenant));
    }

    [Fact]
    public async Task CommitmentLocksTheDraftDuringRealRevalidationAndReplaysWithoutComparingAgain()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        await using var source = CommercialSource(await CreateCommercialRuntimeAsync(), "locked-commercial-draft", 2);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var guard = new ObservedCommercialGuard(CommercialGuard(source), pauseBeforeComparison: true);
        var store = new PostgresOrderDraftStore(source, new FixedTimeProvider(), guard);
        var created = (await store.CreateAsync(context, await RealCommercialIntentAsync(source, context, fixture), "create", default)).Order!;
        var commit = new CommitOrderDraft(store);
        var request = new CommitOrderDraftRequest(created.OrderId, 1);
        var committing = commit.ExecuteAsync(context, request, "commit", default);
        await guard.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await AssertOrderTransactionOwnsPublicationPinAsync("locked-commercial-draft");
            await using var connection = await source.OpenConnectionAsync();
            await SetTenantContextAsync(connection, tenant);
            await using var probe = new NpgsqlCommand("SELECT id FROM orders.order_drafts WHERE tenant_id=@tenant AND id=@id FOR UPDATE NOWAIT", connection);
            probe.Parameters.AddWithValue("tenant", tenant); probe.Parameters.AddWithValue("id", created.OrderId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => probe.ExecuteScalarAsync());
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);
        }
        finally { guard.Compare.TrySetResult(); guard.Continue.TrySetResult(); }
        var result = await committing.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(CommitOrderDraftStatus.Committed, result.Status);
        Assert.Equal(2, await CountReceiptsAsync(tenant));
        Assert.Equivalent(created.Lines, result.Order!.Lines);
        Assert.Equivalent(result.Order, await store.FindAsync(context, created.OrderId, default));
        var replay = await commit.ExecuteAsync(context, request, "commit", default);
        Assert.Equal(CommitOrderDraftStatus.Replayed, replay.Status);
        Assert.Equal(1, guard.Calls);
        Assert.Equivalent(result.Order, replay.Order);
    }

    [Fact]
    public async Task CommercialSchemaRollbackCannotDiscardAReplacedDraftsHistoricalReceipt()
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        await using var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(source, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenant, account);
        var created = (await store.CreateAsync(context, CommercialIntent(context), "create", default)).Order!;
        var manual = OrderDraftIntent.Create(new("Explicit manual replacement", "USD", [new("Manual", 1, "EA", 10)]));
        await store.ReviseAsync(context, new(created.OrderId, 1, manual.Summary, "USD", []), manual, "manual", manual.Fingerprint, default);
        Assert.Null((await store.FindAsync(context, created.OrderId, default))!.Lines[0].CommercialFacts);
        await using var migration = CreateContext();
        var error = await Assert.ThrowsAsync<PostgresException>(() => migration.GetService<IMigrator>().MigrateAsync("202610030001_OrderCommitment"));
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Contains("202610070001_OrderCommercialFacts", await migration.Database.GetAppliedMigrationsAsync());
        Assert.Equal(2, await CountReceiptsAsync(tenant));
    }

    [Fact]
    public void CommercialMigrationDoesNotAlterHistoricalTargetsAndMatchesCurrentModel()
    {
        const string entity = "Application.Orders.Postgres.OrderDraftLineRow";
        Assert.Null(new Migrations.OrderCommitment().TargetModel.FindEntityType(entity)!.FindProperty("CommercialFacts"));
        Assert.NotNull(new Migrations.OrderCommercialFacts().TargetModel.FindEntityType(entity)!.FindProperty("CommercialFacts"));
        using var context = CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static OrderDraftIntent CommercialIntent(TenantContext context, string name = "Original catalog facts", long revision = 1, string currency = "USD", string unitCode = "BOX")
    {
        var item = Guid.NewGuid(); var unit = Guid.NewGuid(); var baseUnit = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        var conversion = new CatalogConversionFacts(unit, baseUnit, 3, 5m, 2m);
        var catalog = new CatalogLineFacts(item, "SKU", unit, name, unitCode, "Box", 2m, 4, revision, 1,
            conversion, QuantityArithmetic.Version1, QuantityArithmetic.Version1Rounding,
            "EA", "Each", 4, 1, 5m);
        var price = new PriceRevision(context.TenantId, Guid.NewGuid(), revision,
            new(item, unitCode, currency, PriceScope.Default(), unit, conversion.Revision), 1.2345m,
            new(now.AddDays(-1)), PricePublicationState.Published, context.AccountId, now.AddDays(-1), now.AddDays(-1));
        var selected = Assert.IsType<PriceResolved>(PriceSelectionEngine.Resolve(new(context.TenantId, item, unitCode, currency,
            new(UnitConversionRevision: conversion.Revision), now, 1, unit), [price], new(1, 0, 100)));
        var intent = OrderDraftIntent.Create(new(name, currency, [new(name, 2m, "BOX", selected.UnitPrice)]));
        var line = intent.Lines[0] with
        {
            UnitCode = unitCode,
            CommercialFacts = new(catalog, price,
            new(item, unit, currency, selected.UnitPrice, selected.Explanation))
        };
        return intent with { Lines = [line] };
    }

}
