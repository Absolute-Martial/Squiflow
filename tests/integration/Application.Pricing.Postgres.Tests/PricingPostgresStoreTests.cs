using Application.Pricing;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Application.Pricing.Postgres.Tests;

public sealed class PricingPostgresStoreTests : PricingPostgresTestDatabase
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Account = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Item = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Unit = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task IdempotencyReplaysAndHistoryRetainsSupersededAndRetiredFacts()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, Account);
        await using var dataSource = await CreateRuntimeDataSourceAsync();
        var store = new PostgresPricingStore(dataSource, new FixedTimeProvider(Now));
        var actor = new PricingActorContext(TenantA, Account);
        var request = Draft(PriceScope.Default(), 10m);

        var created = await store.CreateDraftAsync(actor, request, "draft-key", CancellationToken.None);
        var replay = await store.CreateDraftAsync(actor, request, "draft-key", CancellationToken.None);
        var conflict = await store.CreateDraftAsync(
            actor,
            request with { BaseUnitPrice = 11m },
            "draft-key",
            CancellationToken.None);
        Assert.Equal(CreatePriceDraftStatus.Created, created.Status);
        Assert.Equal(CreatePriceDraftStatus.Replayed, replay.Status);
        Assert.Equal(CreatePriceDraftStatus.IdempotencyKeyConflict, conflict.Status);

        var published = await store.PublishAsync(
            actor,
            new PublishPriceRequest(created.Price!.RevisionId),
            "publish-key",
            CancellationToken.None);
        Assert.Equal(PublishPriceStatus.Published, published.Status);
        var publishedReplay = await store.PublishAsync(
            actor,
            new PublishPriceRequest(created.Price.RevisionId),
            "publish-key",
            CancellationToken.None);
        Assert.Equal(PublishPriceStatus.Replayed, publishedReplay.Status);

        var replacementDraft = await store.CreateDraftAsync(
            actor,
             Draft(PriceScope.Default(), 12m) with { PriceId = published.Price!.PriceId },
            "replacement-key",
            CancellationToken.None);
        var replacement = await store.PublishAsync(
            actor,
            new PublishPriceRequest(replacementDraft.Price!.RevisionId, published.Price!.RevisionId),
            "replacement-publish-key",
            CancellationToken.None);
        Assert.Equal(PublishPriceStatus.Published, replacement.Status);

        var retired = await store.RetireAsync(
            actor,
            new RetirePriceRequest(replacement.Price!.RevisionId),
            "retire-key",
            CancellationToken.None);
        Assert.Equal(RetirePriceStatus.Retired, retired.Status);

        var history = await store.ListRevisionsAsync(actor, new PriceLookupRequest(Item, "EA", "USD", Unit), CancellationToken.None);
        var replayAfterRetire = await store.PublishAsync(actor, new PublishPriceRequest(created.Price.RevisionId), "publish-key", CancellationToken.None);
        Assert.Equal(published.Price, replayAfterRetire.Price);
        var originalDraftReplay = await store.CreateDraftAsync(actor, request, "draft-key", CancellationToken.None);
        Assert.Equal(PricePublicationState.Draft, originalDraftReplay.Price!.State);
        Assert.Equal(2, history.Count);
        Assert.Equal(
            [PricePublicationState.Superseded, PricePublicationState.Retired],
            history.Select(revision => revision.State));
    }

    [Fact]
    public async Task ConcurrentPublicationProducesOneActiveRevisionAndOneConflict()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, Account);
        await using var dataSource = await CreateRuntimeDataSourceAsync();
        var store = new PostgresPricingStore(dataSource, new FixedTimeProvider(Now));
        var actor = new PricingActorContext(TenantA, Account);
        var first = await store.CreateDraftAsync(actor, Draft(PriceScope.Default(), 10m), "first", CancellationToken.None);
        var second = await store.CreateDraftAsync(actor, Draft(PriceScope.Default(), 11m), "second", CancellationToken.None);

        var results = await Task.WhenAll(
            store.PublishAsync(actor, new PublishPriceRequest(first.Price!.RevisionId), "publish-first", CancellationToken.None),
            store.PublishAsync(actor, new PublishPriceRequest(second.Price!.RevisionId), "publish-second", CancellationToken.None));

        Assert.Single(results, result => result.Status == PublishPriceStatus.Published);
        Assert.Single(results, result => result.Status == PublishPriceStatus.PublicationConflict);
        var candidates = await store.GetPublishedCandidatesAsync(
             actor, new PriceLookupRequest(Item, "EA", "USD", Unit), CancellationToken.None);
        Assert.Single(candidates);
    }

    [Fact]
    public async Task CandidateBoundsAreAppliedAfterExactConversionCompatibilityFiltering()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, Account);
        await using var source = await CreateRuntimeDataSourceAsync();
        var store = new PostgresPricingStore(source, new FixedTimeProvider(Now));
        var actor = new PricingActorContext(TenantA, Account);
        var draft = await store.CreateDraftAsync(actor, Draft(PriceScope.Default(), 10) with { UnitConversionRevision = 1 }, "converted-draft", default);
        await store.PublishAsync(actor, new(draft.Price!.RevisionId), "converted-publish", default);
        Assert.Empty(await store.GetPublishedCandidatesAsync(actor,
            new(Item, "EA", "USD", Unit, new(UnitConversionRevision: 2), at: Now), default));
        Assert.Empty(await store.GetPublishedCandidatesAsync(actor, new(Item, "EA", "USD", Unit, new(), at: Now), default));
        var matching = await store.GetPublishedCandidatesAsync(actor,
            new(Item, "EA", "USD", Unit, new(UnitConversionRevision: 1), at: Now), default);
        Assert.Equal(draft.Price.RevisionId, Assert.Single(matching).RevisionId);
    }

    [Fact]
    public async Task RlsKeepsAnotherTenantFromReadingOrRetiringThePrice()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, TenantB, Account);
        await using var dataSource = await CreateRuntimeDataSourceAsync();
        var store = new PostgresPricingStore(dataSource, new FixedTimeProvider(Now));
        var tenantA = new PricingActorContext(TenantA, Account);
        var tenantB = new PricingActorContext(TenantB, Account);
        var created = await store.CreateDraftAsync(tenantA, Draft(PriceScope.Default(), 10m), "tenant-a", CancellationToken.None);
        var published = await store.PublishAsync(
            tenantA,
            new PublishPriceRequest(created.Price!.RevisionId),
            "tenant-a-publish",
            CancellationToken.None);

        var candidates = await store.GetPublishedCandidatesAsync(
             tenantB, new PriceLookupRequest(Item, "EA", "USD", Unit), CancellationToken.None);
        var history = await store.ListRevisionsAsync(
             tenantB, new PriceLookupRequest(Item, "EA", "USD", Unit), CancellationToken.None);
        var foreignRetire = await store.RetireAsync(
            tenantB,
            new RetirePriceRequest(published.Price!.RevisionId),
            "foreign-retire",
            CancellationToken.None);

        Assert.Empty(candidates);
        Assert.Empty(history);
        Assert.Equal(RetirePriceStatus.NotFound, foreignRetire.Status);
    }

    [Fact]
    public async Task RuntimeRoleCannotBypassTenantPolicyWithAForeignInsert()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, TenantB, Account);
        await using var dataSource = await CreateRuntimeDataSourceAsync();
        await using var connection = await dataSource.OpenConnectionAsync(CancellationToken.None);
        await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None);
        await using var setTenant = new NpgsqlCommand(
            "SELECT set_config('app.current_tenant', @tenant, true)", connection, transaction);
        setTenant.Parameters.AddWithValue("tenant", TenantA.ToString("D"));
        await setTenant.ExecuteNonQueryAsync(CancellationToken.None);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO pricing.price_revisions
                (tenant_id, revision_id, revision_number, item_id, unit_code, currency_code,
                  scope_kind, scope_id, base_unit_price, valid_from, state, created_by_account_id, created_at, unit_id, price_id)
            VALUES (@tenant, @revision, nextval('pricing.price_revision_number_seq'), @item, 'EA', 'USD',
                     'default', '00000000-0000-0000-0000-000000000000', 1, @valid_from, 'draft', @account, @created_at, @unit, @revision)
            """, connection, transaction);
        insert.Parameters.AddWithValue("tenant", TenantB);
        insert.Parameters.AddWithValue("revision", Guid.CreateVersion7());
        insert.Parameters.AddWithValue("item", Item);
        insert.Parameters.AddWithValue("unit", Unit);
        insert.Parameters.AddWithValue("valid_from", Now);
        insert.Parameters.AddWithValue("account", Account);
        insert.Parameters.AddWithValue("created_at", Now);
        var error = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        await transaction.RollbackAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SameKeyPublicationAndRetirementRacesReplayExactlyOnce()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, Account);
        await using var source = await CreateRuntimeDataSourceAsync();
        var store = new PostgresPricingStore(source, new FixedTimeProvider(Now));
        var actor = new PricingActorContext(TenantA, Account);
        var draft = await store.CreateDraftAsync(actor, Draft(PriceScope.Default(), 10m), "draft", CancellationToken.None);
        var publish = new PublishPriceRequest(draft.Price!.RevisionId);
        var published = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.PublishAsync(actor, publish, "same-publish", CancellationToken.None)));
        Assert.Single(published, value => value.Status == PublishPriceStatus.Published);
        Assert.Equal(7, published.Count(value => value.Status == PublishPriceStatus.Replayed));
        Assert.All(published, value => Assert.Equal(published[0].Price, value.Price));
        var retire = new RetirePriceRequest(draft.Price.RevisionId);
        var retired = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.RetireAsync(actor, retire, "same-retire", CancellationToken.None)));
        Assert.Single(retired, value => value.Status == RetirePriceStatus.Retired);
        Assert.Equal(7, retired.Count(value => value.Status == RetirePriceStatus.Replayed));
        Assert.All(retired, value => Assert.Equal(retired[0].Price, value.Price));
        var replay = await store.PublishAsync(actor, publish, "same-publish", CancellationToken.None);
        Assert.Equal(PricePublicationState.Published, replay.Price!.State);
    }

    [Fact]
    public async Task VersionedPolicyRetainsOldEnvelopeAndReplayAndRejectsMutation()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, TenantB, Account);
        await using var source = await CreateRuntimeDataSourceAsync();
        var store = new PostgresPricingStore(source, new FixedTimeProvider(Now));
        var actor = new PricingActorContext(TenantA, Account);
        var first = new PublishPricingPolicyRequest(0, 1, 100, 10, 20);
        var published = await store.PublishPolicyAsync(actor, first, "policy-1", CancellationToken.None);
        Assert.Equal(PublishPricingPolicyStatus.Published, published.Status);
        var second = await store.PublishPolicyAsync(actor, new(1, 5, 80, 5, 10), "policy-2", CancellationToken.None);
        Assert.Equal(2, second.Snapshot!.Policy.PolicyRevision);
        var replay = await store.PublishPolicyAsync(actor, first, "policy-1", CancellationToken.None);
        Assert.Equal(published.Snapshot, replay.Snapshot);
        var stale = await store.PublishPolicyAsync(actor, new(1, 0, 100), "stale", CancellationToken.None);
        Assert.Equal(PublishPricingPolicyStatus.RevisionConflict, stale.Status);
        Assert.Null(await store.GetCurrentPolicyAsync(new(TenantB, Account), CancellationToken.None));
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var mutate = new NpgsqlCommand("UPDATE pricing.override_policies SET maximum_unit_price = 200 WHERE tenant_id = @tenant", connection);
        mutate.Parameters.AddWithValue("tenant", TenantA);
        var error = await Assert.ThrowsAsync<PostgresException>(() => mutate.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task ContextualHistoryAndCandidatesAreBoundedAndDoNotReadAnotherCustomer()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, Account);
        await using var source = await CreateRuntimeDataSourceAsync();
        var store = new PostgresPricingStore(source, new FixedTimeProvider(Now));
        var actor = new PricingActorContext(TenantA, Account);
        var customer = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        for (var index = 0; index < 55; index++)
            await store.CreateDraftAsync(actor, Draft(PriceScope.Customer(other), index), $"other-{index}", CancellationToken.None);
        for (var index = 0; index < 55; index++)
            await store.CreateDraftAsync(actor, Draft(PriceScope.Customer(customer), index), $"customer-{index}", CancellationToken.None);
        var context = new PriceSelectionContext(CustomerId: customer);
        var first = await store.ListRevisionsAsync(actor, new(Item, "EA", "USD", Unit, context, 50), CancellationToken.None);
        Assert.Equal(50, first.Count);
        Assert.All(first, value => Assert.Equal(customer, value.Key.Scope.TargetId));
        var second = await store.ListRevisionsAsync(actor, new(Item, "EA", "USD", Unit, context, 50, first[^1].RevisionNumber), CancellationToken.None);
        Assert.Equal(5, second.Count);
        var otherDraft = await store.CreateDraftAsync(actor, Draft(PriceScope.Customer(other), 999), "other-publish", CancellationToken.None);
        await store.PublishAsync(actor, new(otherDraft.Price!.RevisionId), "publish-other", CancellationToken.None);
        Assert.Empty(await store.GetPublishedCandidatesAsync(actor, new(Item, "EA", "USD", Unit, context, at: Now), CancellationToken.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PriceLookupRequest(Item, "EA", "USD", Unit, context, 51));
    }

    [Fact]
    public async Task EconomicFactsAndReceiptSnapshotsRejectDirectMutation()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, Account);
        await using var source = await CreateRuntimeDataSourceAsync();
        var store = new PostgresPricingStore(source, new FixedTimeProvider(Now));
        var draft = await store.CreateDraftAsync(new(TenantA, Account), Draft(PriceScope.Default(), 12), "retained", CancellationToken.None);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var fact = new NpgsqlCommand("UPDATE pricing.price_revisions SET base_unit_price = 99 WHERE revision_id = @id", connection);
        fact.Parameters.AddWithValue("id", draft.Price!.RevisionId);
        var economicError = await Assert.ThrowsAsync<PostgresException>(() => fact.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, economicError.SqlState);
        await using var receipt = new NpgsqlCommand("UPDATE pricing.command_receipts SET response_json = '{}'", connection);
        var receiptError = await Assert.ThrowsAsync<PostgresException>(() => receipt.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, receiptError.SqlState);
    }

    [Fact]
    public async Task SupersessionRacingRetirementReturnsOnlySafeRevisionOutcomes()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, Account);
        await using var source = await CreateRuntimeDataSourceAsync();
        var store = new PostgresPricingStore(source, new FixedTimeProvider(Now));
        var actor = new PricingActorContext(TenantA, Account);
        var original = await store.CreateDraftAsync(actor, Draft(PriceScope.Default(), 20), "original", CancellationToken.None);
        await store.PublishAsync(actor, new(original.Price!.RevisionId), "publish-original", CancellationToken.None);
        var replacement = await store.CreateDraftAsync(actor, Draft(PriceScope.Default(), 25) with { PriceId = original.Price.PriceId }, "replacement", CancellationToken.None);
        var publishing = store.PublishAsync(actor, new(replacement.Price!.RevisionId, original.Price.RevisionId), "replace", CancellationToken.None);
        var retiring = store.RetireAsync(actor, new(original.Price.RevisionId), "retire", CancellationToken.None);
        await Task.WhenAll(publishing, retiring);
        var published = await publishing;
        var retired = await retiring;
        Assert.Contains(published.Status, new[] { PublishPriceStatus.Published, PublishPriceStatus.RevisionConflict });
        Assert.Contains(retired.Status, new[] { RetirePriceStatus.Retired, RetirePriceStatus.RevisionConflict });
        Assert.True((published.Status == PublishPriceStatus.Published) != (retired.Status == RetirePriceStatus.Retired));
    }

    [Fact]
    public async Task AdditiveMigrationRetainsLegacyEconomicsWithoutGuessingUnitIdentity()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using (var authority = new NpgsqlCommand("""
            CREATE SCHEMA tenancy;
            CREATE TABLE tenancy.tenants (id uuid PRIMARY KEY);
            CREATE SCHEMA identity_access;
            CREATE TABLE identity_access.accounts (id uuid PRIMARY KEY);
            """, connection))
            await authority.ExecuteNonQueryAsync();
        await SeedAuthorityAsync(TenantA, Account);
        await using var context = PricingPostgresMigrations.CreateContext(ConnectionString);
        await context.GetService<IMigrator>().MigrateAsync("202610060001_PricingFoundation");
        var revision = Guid.NewGuid();
        await using (var legacy = new NpgsqlCommand("""
            INSERT INTO pricing.price_revisions (tenant_id, revision_id, item_id, unit_code, currency_code,
                scope_kind, scope_id, base_unit_price, valid_from, state, created_by_account_id, created_at)
            VALUES (@tenant, @revision, @item, 'EA', 'USD', 'default', '00000000-0000-0000-0000-000000000000',
                12.3456, @now, 'draft', @account, @now)
            """, connection))
        {
            legacy.Parameters.AddWithValue("tenant", TenantA);
            legacy.Parameters.AddWithValue("revision", revision);
            legacy.Parameters.AddWithValue("item", Item);
            legacy.Parameters.AddWithValue("now", Now);
            legacy.Parameters.AddWithValue("account", Account);
            await legacy.ExecuteNonQueryAsync();
        }
        await context.Database.MigrateAsync();
        await using var source = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var store = new PostgresPricingStore(source, new FixedTimeProvider(Now));
        var actor = new PricingActorContext(TenantA, Account);
        var retained = await store.FindAsync(actor, revision, CancellationToken.None);
        Assert.Equal(12.3456m, retained!.BaseUnitPrice);
        Assert.Equal("EA", retained.Key.UnitCode);
        Assert.Equal(Guid.Empty, retained.Key.UnitId);
        Assert.Equal(revision, retained.PriceId);
        var unsupported = await store.PublishAsync(actor, new(revision), "legacy", CancellationToken.None);
        Assert.Equal(PublishPriceStatus.RevisionConflict, unsupported.Status);
        Assert.Empty(await store.GetPublishedCandidatesAsync(actor, new(Item, "EA", "USD", Unit, at: Now), CancellationToken.None));
    }

    [Fact]
    public async Task RetainedReceiptUnderAnUnsupportedVersionIsAServerContractFaultNotClientInput()
    {
        await ApplyPricingSchemaAsync();
        await SeedAuthorityAsync(TenantA, Account);
        await using var dataSource = await CreateRuntimeDataSourceAsync();
        var store = new PostgresPricingStore(dataSource, new FixedTimeProvider(Now));
        var actor = new PricingActorContext(TenantA, Account);
        var request = Draft(PriceScope.Default(), 10m);
        Assert.Equal(CreatePriceDraftStatus.Created,
            (await store.CreateDraftAsync(actor, request, "current-key", CancellationToken.None)).Status);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using (var retained = new NpgsqlCommand("""
            INSERT INTO pricing.command_receipts
                (tenant_id, account_id, operation, idempotency_key, fingerprint, response_json, created_at)
            SELECT tenant_id, account_id, operation, 'foreign-version-key', fingerprint,
                   jsonb_set(response_json, '{version}', '1'::jsonb), created_at
            FROM pricing.command_receipts
            WHERE tenant_id = @tenant AND account_id = @account
              AND operation = 'create-price-draft' AND idempotency_key = 'current-key'
            """, connection))
        {
            retained.Parameters.AddWithValue("tenant", TenantA);
            retained.Parameters.AddWithValue("account", Account);
            Assert.Equal(1, await retained.ExecuteNonQueryAsync(CancellationToken.None));
        }

        var failure = await Assert.ThrowsAsync<PricingStoredContractException>(() =>
            store.CreateDraftAsync(actor, request, "foreign-version-key", CancellationToken.None));
        Assert.Equal("pricing_receipt_version_unsupported", failure.Code);
        // A host that maps ArgumentException to a client result must not be able to swallow this.
        Assert.False(typeof(ArgumentException).IsInstanceOfType(failure));

        var stillCurrent = await store.CreateDraftAsync(actor, request, "current-key", CancellationToken.None);
        Assert.Equal(CreatePriceDraftStatus.Replayed, stillCurrent.Status);
    }

    private static CreatePriceDraftRequest Draft(PriceScope scope, decimal price) =>
         new(Item, "EA", "USD", scope, price, new PriceValidity(Now.AddDays(-1), Now.AddDays(1)), Unit);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
