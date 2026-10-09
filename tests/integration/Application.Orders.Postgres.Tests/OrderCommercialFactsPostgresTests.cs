using Application.Catalog;
using Application.Catalog.Postgres;
using Application.Customers;
using Application.Customers.Postgres;
using Application.Pricing;
using Application.Pricing.Postgres;
using Application.Profiles.Postgres;
using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateDurableReplayRequiresRetainedElevatedAuthorityEvenAfterPolicyWidens(bool revision)
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        await using var source = CommercialSource(await CreateCommercialRuntimeAsync(), "elevated-replay", 2);
        var context = await ResolveContextAsync(tenant, account);
        var fixture = await CreateCommercialFixtureAsync(source, context);
        var store = new PostgresOrderDraftStore(source, new FixedTimeProvider());
        var catalog = new PostgresCatalogStore(source, new FixedTimeProvider());
        var pricing = new PostgresPricingStore(source, new FixedTimeProvider());
        var priceApplication = new PricingApplication(pricing, new(pricing), pricing,
            new CommercialReferences(catalog, ResolveContextAsync), new FixedTimeProvider());
        var authority = new ReplayOverrideAuthority();
        var application = new CatalogOrderDraftApplication(store, store, new(catalog), priceApplication,
            new ResolveCustomerOrderContext(new PostgresCustomerStore(source)), currentPricingAuthority: authority);
        var request = new CatalogOrderDraftRequest("Elevated offer", "USD",
            [new(fixture.Item.ItemId, fixture.Unit.UnitId, 2, 1, 200, "Commercial exception")]);
        var created = (await application.CreateAsync(context, request, "create", new(true, false), default)).Order!;
        var replacement = new ReviseCatalogOrderDraftRequest(created.OrderId, 1, request);
        var retained = revision
            ? (await application.ReviseAsync(context, replacement, "revise", new(true, false), default)).Order!
            : created;
        Assert.True(retained.Lines[0].CommercialFacts!.PriceSelection.Explanation.Override!.BeyondPolicy);
        var receiptCount = await CountReceiptsAsync(tenant);
        await pricing.PublishPolicyAsync(new(tenant, account), new(1, 0, 1000), "widened-policy", default);
        authority.Beyond = false;
        var checks = authority.Checks;

        // Simulate a receipt becoming visible only inside the mutation transaction.
        // Selection and the late replay still use the actual PostgreSQL stores.
        var late = new CatalogOrderDraftApplication(store, new MissedCommercialReceipts(), new(catalog), priceApplication,
            new ResolveCustomerOrderContext(new PostgresCustomerStore(source)), currentPricingAuthority: authority);
        var denied = await Assert.ThrowsAsync<OrderCommercialSelectionException>(async () =>
        {
            if (revision) await late.ReviseAsync(context, replacement, "revise", new(true, false), default);
            else await late.CreateAsync(context, request, "create", new(true, false), default);
        });
        Assert.Equal("pricing_override_forbidden", denied.Code);
        Assert.Equal(checks + 1, authority.Checks);
        Assert.Equal(receiptCount, await CountReceiptsAsync(tenant));
        Assert.Equivalent(retained, await store.FindAsync(context, created.OrderId, default));

        authority.Beyond = true;
        OrderDraftSnapshot? recovered;
        if (revision)
        {
            var replay = await late.ReviseAsync(context, replacement, "revise", new(true, false), default);
            Assert.Equal(ReviseOrderDraftStatus.Replayed, replay.Status);
            recovered = replay.Order;
        }
        else
        {
            var replay = await late.CreateAsync(context, request, "create", new(true, false), default);
            Assert.Equal(CreateOrderDraftStatus.Replayed, replay.Status);
            recovered = replay.Order;
        }
        Assert.Equivalent(retained, recovered);
        Assert.Equal(1, recovered!.Lines[0].CommercialFacts!.PriceSelection.Explanation.PolicyRevision);
        Assert.Equal(receiptCount, await CountReceiptsAsync(tenant));
    }

    private sealed class ReplayOverrideAuthority : IOrderPricingAuthorityReader
    {
        internal bool Beyond { get; set; } = true;
        internal int Checks { get; private set; }
        public Task<OrderPricingAuthority> ReadAsync(TenantContext context, CancellationToken ct) =>
            Task.FromResult(new OrderPricingAuthority(true, false));
        public Task<bool> CanOverrideBeyondPolicyAsync(TenantContext context, CancellationToken ct)
        { Checks++; return Task.FromResult(Beyond); }
    }

    private sealed class MissedCommercialReceipts : IOrderDraftReceiptReader
    {
        public Task<CreateOrderDraftResult?> FindCreateReceiptAsync(TenantContext context, string key, string fingerprint, CancellationToken ct) =>
            Task.FromResult<CreateOrderDraftResult?>(null);
        public Task<ReviseOrderDraftResult?> FindRevisionReceiptAsync(TenantContext context, string key, string fingerprint, CancellationToken ct) =>
            Task.FromResult<ReviseOrderDraftResult?>(null);
    }

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
            await AssertEffectTransactionOwnsPublicationPinAsync("locked-commercial-draft");
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
        // Exercise this historical guard directly; COM-010 independently rejects whole-chain downgrade.
        var script = migration.GetService<IMigrator>().GenerateScript("202610070001_OrderCommercialFacts", "202610030001_OrderCommitment");
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var downgrade = new NpgsqlCommand(script, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => downgrade.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Contains("202610070001_OrderCommercialFacts", await migration.Database.GetAppliedMigrationsAsync());
        Assert.Equal(2, await CountReceiptsAsync(tenant));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task CommercialSchemaRollbackRefusesEveryRetainedReceiptEnvelopeVersion(int schemaVersion)
    {
        await ApplyOrderSchemaAsync();
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await SeedAuthorityRowsAsync(account, tenant);
        var context = await ResolveContextAsync(tenant, account);
        Guid orderId;
        await using (var source = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build())
        {
            var store = new PostgresOrderDraftStore(source, new FixedTimeProvider());
            orderId = (await store.CreateAsync(context, CreateIntent("Manual envelope holder"), "create", default)).Order!.OrderId;
        }
        // Leave only the envelope under test: the production create receipt would otherwise satisfy
        // the guard by itself and hide which versions it actually recognizes.
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var clear = new NpgsqlCommand("DELETE FROM orders.command_receipts WHERE tenant_id=@tenant", connection);
            clear.Parameters.AddWithValue("tenant", tenant);
            await clear.ExecuteNonQueryAsync();
        }
        // Neither half of the guard can see this envelope through order_draft_lines: the order is a
        // manual draft with no commercial facts and a unit code the narrower column still accepts.
        Assert.Equal(0L, await CountRowsAsync("""
            SELECT count(*) FROM orders.order_draft_lines WHERE tenant_id = @tenant_id
            AND (commercial_facts IS NOT NULL OR unit_code !~ '^[A-Z0-9]{1,16}$')
            """, tenant));
        await InsertReceiptEnvelopeAsync(tenant, account, orderId, schemaVersion);
        Assert.Equal(1L, await CountReceiptsAsync(tenant));

        await using var migration = CreateContext();
        var script = migration.GetService<IMigrator>().GenerateScript("202610070001_OrderCommercialFacts", "202610030001_OrderCommitment");
        await using var downgrade = new NpgsqlCommand(script, new NpgsqlConnection(ConnectionString));
        await ((NpgsqlConnection)downgrade.Connection!).OpenAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => downgrade.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Contains("202610070001_OrderCommercialFacts", await migration.Database.GetAppliedMigrationsAsync());
        // The guard recognizes the shared set exactly, not a hand-maintained copy of part of it.
        Assert.Contains($"response_json->>'schemaVersion' IN ('1', '2', '3', '4', '5', '6')", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProgramPolicyEnvelopeOnAManualOrderBlocksTheCommercialSchemaRollback()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.NewGuid(); var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid(); var programId = Guid.NewGuid();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await SeedCustomerContextAsync(tenantId, accountId, organizationId, programId);
        await using var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var clock = new FixedTimeProvider();
        var profiles = new PostgresProfileStore(profileSource, clock);
        var context = await ResolveContextAsync(tenantId, accountId);
        await PublishAndActivateProfileAsync(profiles, context, new(Guid.NewGuid(), Guid.NewGuid()),
            requireReference: false, legacyBaseline: true, "rollback-guard-baseline");
        await PublishAndActivateProfileAsync(profiles, context, new(Guid.NewGuid(), Guid.NewGuid()),
            requireReference: true, legacyBaseline: false, "rollback-guard-required");
        await using var runtimeSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var orders = new PostgresOrderDraftStore(runtimeSource, clock, profilePolicySource: new ProfileOrderPolicySource());
        var draft = (await orders.CreateAsync(context, CreateIntent("Manual program order",
            new CustomerOrderContext(organizationId, programId)), "create", default)).Order!;
        Assert.Equal(ProgramPolicyEnvelopeVersion,
            await SingleReceiptSchemaVersionAsync(tenantId, accountId, draft.OrderId, "create-order-draft", "create"));
        Assert.Equal(SetOrderProgramReferenceStatus.Updated,
            (await new SetOrderProgramReference(orders).ExecuteAsync(context,
                new SetOrderProgramReferenceRequest(draft.OrderId, draft.Revision, "PO-ROLLBACK-GUARD"),
                "reference-key", default)).Status);
        Assert.Equal(ProgramPolicyEnvelopeVersion,
            await SingleReceiptSchemaVersionAsync(tenantId, accountId, draft.OrderId, "set-order-program-reference", "reference-key"));
        // The reported defect exactly: a manual order with no commercial facts and an unchanged
        // unit_code, whose version-six envelope the previous guard did not recognize.
        Assert.Equal(0L, await CountRowsAsync("""
            SELECT count(*) FROM orders.order_draft_lines WHERE tenant_id = @tenant_id
            AND (commercial_facts IS NOT NULL OR unit_code !~ '^[A-Z0-9]{1,16}$')
            """, tenantId));

        await using var migration = CreateContext();
        var script = migration.GetService<IMigrator>().GenerateScript("202610070001_OrderCommercialFacts", "202610030001_OrderCommitment");
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var downgrade = new NpgsqlCommand(script, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => downgrade.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Contains("202610070001_OrderCommercialFacts", await migration.Database.GetAppliedMigrationsAsync());
    }

    // Mirrors OrderReceiptSchemaVersions; asserted here so the shared set cannot drift unnoticed.
    private const int ProgramPolicyEnvelopeVersion = 6;

    private async Task<int> SingleReceiptSchemaVersionAsync(Guid tenantId, Guid accountId, Guid orderId,
        string operation, string idempotencyKey)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT DISTINCT (response_json->>'schemaVersion')::int FROM orders.command_receipts
            WHERE tenant_id=@tenant AND account_id=@account AND order_id=@order
              AND operation=@operation AND idempotency_key=@key
            """, connection);
        command.Parameters.AddWithValue("tenant", tenantId); command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("order", orderId); command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("key", idempotencyKey);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The production writer did not emit a schema version."));
    }

    private async Task InsertReceiptEnvelopeAsync(Guid tenantId, Guid accountId, Guid orderId, int schemaVersion)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO orders.command_receipts (tenant_id, account_id, operation, idempotency_key,
                                                 fingerprint, order_id, response_json, created_at)
            VALUES (@tenant, @account, 'order-draft-created', 'envelope', @fingerprint, @order,
                    jsonb_build_object('schemaVersion', @version::int, 'operation', 'order-draft-created',
                                       'resultType', 'order-draft-created', 'payload', '{}'::jsonb),
                    '2026-10-07T12:00:00Z')
            """, connection);
        command.Parameters.AddWithValue("tenant", tenantId); command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("order", orderId); command.Parameters.AddWithValue("version", schemaVersion);
        command.Parameters.AddWithValue("fingerprint", new string('a', 64));
        await command.ExecuteNonQueryAsync();
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
            conversion, "EA", "Each", 4, 1, 5m);
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
