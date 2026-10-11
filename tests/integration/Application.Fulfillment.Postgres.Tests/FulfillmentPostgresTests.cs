using System.Collections.Concurrent;
using System.Text.Json;
using Application.Fulfillment;
using Application.Fulfillment.Postgres;
using Application.Tenancy;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.Fulfillment.Postgres.Tests;

public sealed class FulfillmentPostgresTests : IAsyncLifetime
{
    private const string RuntimeRole = "fulfillment_runtime";
    private const string RuntimePassword = "local-fulfillment-runtime-only";
    private const long AuthorizationRevision = 7;
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
        .WithDatabase("fulfillment_tests")
        .WithUsername("postgres")
        .WithPassword("local-fulfillment-owner-only")
        .Build();

    private string ConnectionString => _database.GetConnectionString();

    [Fact]
    public async Task PartialCompletionAndPickupPersistOnlyOperationalFactsWithoutPriceDisclosure()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var completed = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 1.25m), null, "partial-complete", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, completed.Status);
        var handedOff = await fixture.Store.ExecuteAsync(fixture.Handoff, fixture.OrderId,
            new(FulfillmentCommandKind.Handoff, completed.Work!.Revision, Quantity: 1m, Recipient: "A. Customer"),
            null, "partial-handoff", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, handedOff.Status);
        Assert.Equal(1.25m, handedOff.Work!.CompletedQuantity);
        Assert.Equal(1m, handedOff.Work.HandedOffQuantity);
        Assert.Equal(FulfillmentStage.InProgress, handedOff.Work.Stage);

        var history = await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(), default);
        Assert.Equal(3, history!.Items.Count);
        Assert.Contains(history.Items, item => item.Event.Kind == FulfillmentEventKind.Handoff && item.Event.Recipient == "A. Customer");
        var json = JsonSerializer.Serialize(new { handedOff, history });
        Assert.DoesNotContain("Price", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UnitPrice", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZeroExcessAndOverPrecisionQuantitiesLeaveJournalAndReceiptsUnchanged()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        foreach (var (quantity, key) in new[] { (0m, "zero"), (4m, "excess"), (1.001m, "precision") })
        {
            var rejected = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
                new(FulfillmentCommandKind.Complete, started.Revision, Quantity: quantity), null, key, default);
            Assert.Equal(FulfillmentStoreStatus.TransitionRejected, rejected.Status);
            Assert.Equal(FulfillmentMutationStatus.QuantityInvalid, rejected.Rejection);
        }

        Assert.Equal(1, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(1, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
        var current = (await fixture.Store.FindAsync(fixture.Worker, fixture.OrderId, default))!;
        Assert.Equal(1, current.Revision);
        Assert.Equal(0m, current.CompletedQuantity);
    }

    [Fact]
    public async Task TwoActorsHaveOneRevisionWinnerAndSemanticReplayRejectsChangedIntent()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var attempts = await Task.WhenAll(
            fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
                new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 1m), null, "worker-complete", default),
            fixture.NewStore().ExecuteAsync(fixture.Owner, fixture.OrderId,
                new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 1.5m), null, "owner-complete", default));
        Assert.Single(attempts, result => result.Status == FulfillmentStoreStatus.Applied);
        Assert.Single(attempts, result => result.Status == FulfillmentStoreStatus.TransitionRejected &&
            result.Rejection == FulfillmentMutationStatus.RevisionConflict);

        var current = (await fixture.Store.FindAsync(fixture.Worker, fixture.OrderId, default))!;
        var quantity = current.CompletedQuantity;
        var replayKey = "same-completion";
        var first = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, current.Revision, Quantity: 0.5m), null, replayKey, default);
        Assert.Equal(FulfillmentStoreStatus.Applied, first.Status);
        var receiptsAfterApply = await CountAsync("fulfillment.command_receipts", fixture.TenantId);
        var replay = await fixture.NewStore().ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, current.Revision, Quantity: 0.5m), null, replayKey, default);
        Assert.Equal(FulfillmentStoreStatus.Replayed, replay.Status);
        Assert.Equal(first.Work, replay.Work);
        Assert.Equal(receiptsAfterApply, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
        var changed = await fixture.NewStore().ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, current.Revision, Quantity: 0.25m), null, replayKey, default);
        Assert.Equal(FulfillmentStoreStatus.IdempotencyConflict, changed.Status);
        Assert.Equal(quantity + 0.5m, (await fixture.Store.FindAsync(fixture.Worker, fixture.OrderId, default))!.CompletedQuantity);
    }

    [Fact]
    public async Task SameActorSameKeyAcrossOrdersHasOneWinnerAndOneIdempotencyConflict()
    {
        await using var fixture = await CreateFixtureAsync();
        var secondOrderId = Guid.NewGuid();
        fixture.SetSource(secondOrderId, fixture.SourceFacts with { OrderId = secondOrderId });

        var results = await Task.WhenAll(
            fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
                new(FulfillmentCommandKind.Start, 0), 12, "cross-order-key", default),
            fixture.NewStore().ExecuteAsync(fixture.Worker, secondOrderId,
                new(FulfillmentCommandKind.Start, 0), 12, "cross-order-key", default));

        Assert.Single(results, result => result.Status == FulfillmentStoreStatus.Applied);
        Assert.Single(results, result => result.Status == FulfillmentStoreStatus.IdempotencyConflict);
        Assert.Equal(1, await CountAsync("fulfillment.works", fixture.TenantId));
        Assert.Equal(1, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(1, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
    }

    [Fact]
    public async Task SemanticReplayNormalizesWhitespaceAndDecimalScale()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var completed = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 1.0m), null, "normalized-complete", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, completed.Status);
        var completeReplay = await fixture.NewStore().ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 1.00m), null, "normalized-complete", default);
        Assert.Equal(FulfillmentStoreStatus.Replayed, completeReplay.Status);

        var handedOff = await fixture.Store.ExecuteAsync(fixture.Handoff, fixture.OrderId,
            new(FulfillmentCommandKind.Handoff, completed.Work!.Revision, Quantity: 0.50m, Recipient: "  Customer  "),
            null, "normalized-handoff", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, handedOff.Status);
        var handoffReplay = await fixture.NewStore().ExecuteAsync(fixture.Handoff, fixture.OrderId,
            new(FulfillmentCommandKind.Handoff, completed.Work!.Revision, Quantity: 0.500m, Recipient: "Customer"),
            null, "normalized-handoff", default);
        Assert.Equal(FulfillmentStoreStatus.Replayed, handoffReplay.Status);
        Assert.Equal(3, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(3, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
    }

    [Fact]
    public async Task OlderReceiptReplayReturnsItsOriginalSnapshotAfterLaterCompletionAndHandoff()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var completionMutation = new FulfillmentMutation(FulfillmentCommandKind.Complete, started.Revision, Quantity: 2m);
        var completed = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            completionMutation, null, "older-completion", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, completed.Status);
        var handoff = await fixture.Store.ExecuteAsync(fixture.Handoff, fixture.OrderId,
            new(FulfillmentCommandKind.Handoff, completed.Work!.Revision, Quantity: 1m, Recipient: "Recipient"),
            null, "later-handoff", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, handoff.Status);

        var worksBeforeReplay = await CountAsync("fulfillment.works", fixture.TenantId);
        var eventsBeforeReplay = await CountAsync("fulfillment.events", fixture.TenantId);
        var activitiesBeforeReplay = await CountAsync("fulfillment.entry_activity", fixture.TenantId);
        var receiptsBeforeReplay = await CountAsync("fulfillment.command_receipts", fixture.TenantId);
        var replay = await fixture.NewStore().ExecuteAsync(fixture.Worker, fixture.OrderId,
            completionMutation, null, "older-completion", default);

        Assert.Equal(FulfillmentStoreStatus.Replayed, replay.Status);
        Assert.Equal(completed.Work, replay.Work);
        Assert.Equal(2, replay.Work!.Revision);
        Assert.Equal(0m, replay.Work.HandedOffQuantity);
        Assert.Equal(handoff.Work, await fixture.NewStore().FindAsync(fixture.Worker, fixture.OrderId, default));
        Assert.Equal(worksBeforeReplay, await CountAsync("fulfillment.works", fixture.TenantId));
        Assert.Equal(eventsBeforeReplay, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(activitiesBeforeReplay, await CountAsync("fulfillment.entry_activity", fixture.TenantId));
        Assert.Equal(receiptsBeforeReplay, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
    }

    [Fact]
    public async Task StartedSourceClassificationRemainsPinnedAfterTheSourceReaderChangesAndStoreIsRecreated()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture, "pinned-source-start");
        fixture.SetSource(fixture.OrderId, fixture.SourceFacts with
        {
            OrderRevision = 13,
            ItemId = Guid.NewGuid(),
            OrderedQuantity = 0.25m,
            IsCommitted = false,
            IsCatalog = false,
            IsNonStock = false,
            IsService = false,
        });

        await using var restartedSource = fixture.CreateRuntimeSource();
        var recreatedStore = fixture.NewStore(restartedSource);
        var replay = await recreatedStore.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Start, 0), 12, "pinned-source-start", default);
        Assert.Equal(FulfillmentStoreStatus.Replayed, replay.Status);
        Assert.Equal(fixture.SourceFacts, replay.Work!.Source);

        var completion = await recreatedStore.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 2.5m), null, "pinned-source-complete", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, completion.Status);
        Assert.Equal(3m, completion.Work!.Source.OrderedQuantity);
        Assert.True(completion.Work.Source.IsCommitted);
        Assert.True(completion.Work.Source.IsCatalog);
        Assert.Equal(2.5m, completion.Work.CompletedQuantity);
    }

    [Fact]
    public async Task CorrectingStartAndRestartingUsesThePersistedSourceClassification()
    {
        await using var fixture = await CreateFixtureAsync();
        var originalSource = fixture.SourceFacts;
        var started = await StartAsync(fixture, "initial-start");
        var startEvent = (await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default))!
            .Items.Single().Event;
        fixture.SetSource(fixture.OrderId, originalSource with
        {
            OrderRevision = 13,
            ItemId = Guid.NewGuid(),
            OrderedQuantity = 0.25m,
            IsCommitted = false,
            IsCatalog = false,
            IsNonStock = false,
            IsService = false,
        });

        var correction = await fixture.Store.ExecuteAsync(fixture.Owner, fixture.OrderId,
            new(FulfillmentCommandKind.Correct, started.Revision, Reason: "Voiding an incorrect start",
                TargetEventId: startEvent.EventId), null, "void-start", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, correction.Status);
        Assert.False(correction.Work!.Started);
        var completionBeforeRestart = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, correction.Work.Revision, Quantity: 0.5m), null,
            "completion-before-restart", default);
        Assert.Equal(FulfillmentStoreStatus.TransitionRejected, completionBeforeRestart.Status);
        Assert.Equal(FulfillmentMutationStatus.NotStarted, completionBeforeRestart.Rejection);

        await using var restartedSource = fixture.CreateRuntimeSource();
        var restartedStore = fixture.NewStore(restartedSource);
        var restarted = await restartedStore.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Start, correction.Work.Revision), originalSource.OrderRevision,
            "restart-pinned-source", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, restarted.Status);
        Assert.True(restarted.Work!.Started);
        Assert.Equal(originalSource, restarted.Work.Source);

        var completed = await restartedStore.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, restarted.Work.Revision, Quantity: 0.5m), null,
            "completion-after-restart", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, completed.Status);
        Assert.Equal(originalSource, completed.Work!.Source);
        Assert.Equal(0.5m, completed.Work.CompletedQuantity);
    }

    [Fact]
    public async Task DispositionBlocksWorkAndResolutionDoesNotManufactureQuantity()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var completed = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 1.5m), null, "complete-before-block", default);
        var blocked = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Block, completed.Work!.Revision, Quantity: 0.5m,
                Reason: "Inspect the work", Disposition: FulfillmentDispositionKind.Disputed), null, "block", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, blocked.Status);
        var denied = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, blocked.Work!.Revision, Quantity: 0.25m), null, "blocked-completion", default);
        Assert.Equal(FulfillmentMutationStatus.DispositionActive, denied.Rejection);
        var resolved = await fixture.Store.ExecuteAsync(fixture.Owner, fixture.OrderId,
            new(FulfillmentCommandKind.Resolve, blocked.Work.Revision, Reason: "Inspection complete"), null, "resolve", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, resolved.Status);
        Assert.Equal(1.5m, resolved.Work!.CompletedQuantity);
        Assert.Equal(0m, resolved.Work.HandedOffQuantity);
        Assert.Null(resolved.Work.ActiveDispositionId);
    }

    [Fact]
    public async Task OwnerCorrectionRetainsOriginalsSupportsCorrectingReplacementAndProtectsNetHandoff()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var completed = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 2m), null, "complete", default);
        var completionEvent = (await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(), default))!
            .Items.Single(item => item.Event.Kind == FulfillmentEventKind.Completion).Event;

        var nonOwnerCorrection = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Correct, completed.Work!.Revision, Reason: "Entry correction",
                TargetEventId: completionEvent.EventId,
                Replacement: new(FulfillmentEventKind.Completion, Quantity: 1.5m)), null, "not-owner", default);
        Assert.Equal(FulfillmentStoreStatus.Forbidden, nonOwnerCorrection.Status);

        fixture.AuthorityReader.Set(fixture.Owner.AccountId, CreateAuthority(
            canView: true, work: true, handoff: true, correct: true, currentOwner: true, recent: false));
        var staleOwnerAuthentication = await fixture.Store.ExecuteAsync(fixture.Owner, fixture.OrderId,
            new(FulfillmentCommandKind.Correct, completed.Work.Revision, Reason: "Entry correction",
                TargetEventId: completionEvent.EventId,
                Replacement: new(FulfillmentEventKind.Completion, Quantity: 1.5m)), null, "stale-owner-auth", default);
        Assert.Equal(FulfillmentStoreStatus.Forbidden, staleOwnerAuthentication.Status);
        fixture.AuthorityReader.Set(fixture.Owner.AccountId, CreateAuthority(
            canView: true, work: true, handoff: true, correct: true, currentOwner: true, recent: true));

        var corrected = await fixture.Store.ExecuteAsync(fixture.Owner, fixture.OrderId,
            new(FulfillmentCommandKind.Correct, completed.Work.Revision, Reason: "Corrected entry",
                TargetEventId: completionEvent.EventId,
                Replacement: new(FulfillmentEventKind.Completion, Quantity: 1.5m)), null, "correction-one", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, corrected.Status);
        Assert.Equal(1.5m, corrected.Work!.CompletedQuantity);

        var firstCorrection = (await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(), default))!
            .Items.Single(item => item.Event.Kind == FulfillmentEventKind.Correction).Event;
        var correctedAgain = await fixture.Store.ExecuteAsync(fixture.Owner, fixture.OrderId,
            new(FulfillmentCommandKind.Correct, corrected.Work.Revision, Reason: "Refined correction",
                TargetEventId: firstCorrection.EventId,
                Replacement: new(FulfillmentEventKind.Completion, Quantity: 1.25m)), null, "correction-two", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, correctedAgain.Status);

        var handoff = await fixture.Store.ExecuteAsync(fixture.Handoff, fixture.OrderId,
            new(FulfillmentCommandKind.Handoff, correctedAgain.Work!.Revision, Quantity: 1m, Recipient: "Recipient"),
            null, "handoff", default);
        var secondCorrection = (await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(), default))!
            .Items.Single(item => item.Event.Kind == FulfillmentEventKind.Correction && item.Event.Revision == correctedAgain.Work.Revision).Event;
        var unsafeNet = await fixture.Store.ExecuteAsync(fixture.Owner, fixture.OrderId,
            new(FulfillmentCommandKind.Correct, handoff.Work!.Revision, Reason: "Would undercut handoff",
                TargetEventId: secondCorrection.EventId,
                Replacement: new(FulfillmentEventKind.Completion, Quantity: 0.75m)), null, "unsafe-correction", default);
        Assert.Equal(FulfillmentMutationStatus.CorrectionHasDependents, unsafeNet.Rejection);

        var history = (await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default))!;
        Assert.Contains(history.Items, item => item.Event.EventId == completionEvent.EventId && !item.IsActive);
        Assert.Contains(history.Items, item => item.Event.EventId == firstCorrection.EventId && !item.IsActive);
        Assert.Contains(history.Items, item => item.Event.EventId == secondCorrection.EventId && item.IsActive);
        Assert.Equal(1m, (await fixture.Store.FindAsync(fixture.Worker, fixture.OrderId, default))!.HandedOffQuantity);
    }

    [Fact]
    public async Task RuntimeRoleEnforcesTenantIsolationAcrossAllFourTablesAndRejectsCrossTenantInsert()
    {
        await using var fixture = await CreateFixtureAsync();
        await using (var privileges = new NpgsqlConnection(ConnectionString))
        {
            await privileges.OpenAsync();
            await using var verify = new NpgsqlCommand("""
                SELECT has_column_privilege(@role::name,'fulfillment.works','revision','UPDATE')
                   AND has_column_privilege(@role::name,'fulfillment.works','snapshot','UPDATE')
                   AND has_column_privilege(@role::name,'fulfillment.works','updated_at','UPDATE')
                   AND NOT has_column_privilege(@role::name,'fulfillment.works','source','UPDATE')
                   AND NOT has_table_privilege(@role::name,'fulfillment.works','DELETE')
                   AND has_column_privilege(@role::name,'fulfillment.entry_activity','active','UPDATE')
                   AND has_column_privilege(@role::name,'fulfillment.entry_activity','voided_by','UPDATE')
                   AND NOT has_column_privilege(@role::name,'fulfillment.entry_activity','event_id','UPDATE')
                   AND NOT has_table_privilege(@role::name,'fulfillment.entry_activity','DELETE')
                   AND NOT has_table_privilege(@role::name,'fulfillment.events','UPDATE')
                   AND NOT has_table_privilege(@role::name,'fulfillment.events','DELETE')
                   AND NOT has_table_privilege(@role::name,'fulfillment.command_receipts','UPDATE')
                   AND NOT has_table_privilege(@role::name,'fulfillment.command_receipts','DELETE')
                """, privileges);
            verify.Parameters.AddWithValue("role", RuntimeRole);
            Assert.True((bool)(await verify.ExecuteScalarAsync())!);
        }
        await StartAsync(fixture);
        await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, 1, Quantity: 0.5m), null, "rls-complete", default);
        await using var runtimeSource = fixture.CreateRuntimeSource();
        await using var runtime = await runtimeSource.OpenConnectionAsync();
        var tables = new[]
        {
            "fulfillment.works", "fulfillment.events", "fulfillment.entry_activity", "fulfillment.command_receipts",
        };

        foreach (var table in tables)
            Assert.Equal(0L, await CountVisibleAsync(runtime, table));
        await using (var wrong = await runtime.BeginTransactionAsync())
        {
            await SetTenantAsync(runtime, wrong, Guid.NewGuid());
            foreach (var table in tables)
                Assert.Equal(0L, await CountVisibleAsync(runtime, table, wrong));
            await wrong.RollbackAsync();
        }
        await using (var current = await runtime.BeginTransactionAsync())
        {
            await SetTenantAsync(runtime, current, fixture.TenantId);
            foreach (var table in tables)
                Assert.True(await CountVisibleAsync(runtime, table, current) > 0);
            var foreignTenant = Guid.NewGuid();
            var foreignOrder = Guid.NewGuid();
            var foreignWork = Guid.NewGuid();
            var timestamp = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
            var foreignSource = fixture.SourceFacts with { TenantId = foreignTenant, OrderId = foreignOrder };
            var foreignSnapshot = new FulfillmentSnapshot(foreignSource, foreignWork, foreignTenant, foreignOrder, 1, true,
                0m, 0m, null, null, timestamp, timestamp);
            var options = JsonSerializerOptions.Web;
            await using var insert = new NpgsqlCommand("""
                INSERT INTO fulfillment.works
                    (tenant_id,order_id,work_id,revision,source,snapshot,created_at,updated_at)
                VALUES (@tenant,@order,@work,1,@source::jsonb,@snapshot::jsonb,@timestamp,@timestamp)
                """, runtime, current);
            insert.Parameters.AddWithValue("tenant", foreignTenant);
            insert.Parameters.AddWithValue("order", foreignOrder);
            insert.Parameters.AddWithValue("work", foreignWork);
            insert.Parameters.AddWithValue("source", JsonSerializer.Serialize(foreignSource, options));
            insert.Parameters.AddWithValue("snapshot", JsonSerializer.Serialize(foreignSnapshot, options));
            insert.Parameters.AddWithValue("timestamp", timestamp);
            var error = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
            Assert.Equal("42501", error.SqlState);
            await current.RollbackAsync();
        }
    }

    [Fact]
    public async Task AuthorityRevocationDeniesRestartedReplayAndReceiptFailureRollsBackAllEvidence()
    {
        await using (var fixture = await CreateFixtureAsync())
        {
            var started = await StartAsync(fixture);
            var key = "replay-after-revoke";
            var mutation = new FulfillmentMutation(FulfillmentCommandKind.Complete, started.Revision, Quantity: 0.5m);
            Assert.Equal(FulfillmentStoreStatus.Applied,
                (await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId, mutation, null, key, default)).Status);
            fixture.AuthorityReader.Set(fixture.Worker.AccountId, CreateAuthority(canView: false));
            var replay = await fixture.NewStore().ExecuteAsync(fixture.Worker, fixture.OrderId, mutation, null, key, default);
            Assert.Equal(FulfillmentStoreStatus.Forbidden, replay.Status);
        }

        foreach (var table in new[] { "events", "command_receipts" })
        {
            await using var failed = await CreateFixtureAsync();
            var trigger = $"fail_fulfillment_{table}_for_test";
            var function = $"fail_fulfillment_{table}_for_test_fn";
            await ExecuteOwnerSqlAsync($"""
                CREATE FUNCTION fulfillment.{function}() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'test evidence failure' USING ERRCODE='23514'; END; $$;
                CREATE TRIGGER {trigger} BEFORE INSERT ON fulfillment.{table}
                    FOR EACH ROW EXECUTE FUNCTION fulfillment.{function}();
                """);
            try
            {
                await Assert.ThrowsAsync<PostgresException>(() => StartAsync(failed));
            }
            finally
            {
                await ExecuteOwnerSqlAsync($"DROP TRIGGER {trigger} ON fulfillment.{table}; DROP FUNCTION fulfillment.{function}();");
            }
            Assert.Equal(0, await CountAsync("fulfillment.works", failed.TenantId));
            Assert.Equal(0, await CountAsync("fulfillment.events", failed.TenantId));
            Assert.Equal(0, await CountAsync("fulfillment.entry_activity", failed.TenantId));
            Assert.Equal(0, await CountAsync("fulfillment.command_receipts", failed.TenantId));
        }
    }

    [Fact]
    public async Task DatabaseRejectsUnpairedActivityEventsReceiptsAndWorkAdvancementAndRollsBackFailures()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var startEvent = (await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default))!
            .Items.Single().Event;

        await AssertRuntimeRejectsAsync(fixture, """
            UPDATE fulfillment.entry_activity
            SET active=false, voided_by=@event
            WHERE tenant_id=@tenant AND order_id=@order AND event_id=@event
            """, ("tenant", fixture.TenantId), ("order", fixture.OrderId), ("event", startEvent.EventId));
        await AssertRuntimeRejectsAsync(fixture, """
            INSERT INTO fulfillment.events
                (tenant_id,order_id,event_id,revision,actor_id,authorization_revision,kind,facts,recorded_at)
            VALUES (@tenant,@order,@event,@revision,@actor,7,1,'{}'::jsonb,@recorded)
            """, ("tenant", fixture.TenantId), ("order", fixture.OrderId), ("event", Guid.NewGuid()),
            ("revision", started.Revision), ("actor", fixture.Worker.AccountId), ("recorded", started.UpdatedAt));
        await AssertRuntimeRejectsAsync(fixture, """
            INSERT INTO fulfillment.command_receipts
                (tenant_id,actor_id,operation,idempotency_key,fingerprint,order_id,revision,response)
            VALUES (@tenant,@actor,1,'unmatched-receipt',repeat('a',64),@order,@revision,'{}'::jsonb)
            """, ("tenant", fixture.TenantId), ("actor", fixture.Worker.AccountId), ("order", fixture.OrderId),
            ("revision", started.Revision + 1));

        await using (var source = fixture.CreateRuntimeSource())
        await using (var connection = await source.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetTenantAsync(connection, transaction, fixture.TenantId);
            await using var advance = new NpgsqlCommand("""
                UPDATE fulfillment.works
                SET revision=revision+1,
                    updated_at=updated_at+interval '1 microsecond',
                    snapshot=jsonb_set(
                        jsonb_set(snapshot,'{revision}',to_jsonb((revision+1)::text)),
                        '{updatedAt}',to_jsonb((updated_at+interval '1 microsecond')::text))
                WHERE tenant_id=@tenant AND order_id=@order
                """, connection, transaction);
            advance.Parameters.AddWithValue("tenant", fixture.TenantId);
            advance.Parameters.AddWithValue("order", fixture.OrderId);
            Assert.Equal(1, await advance.ExecuteNonQueryAsync());
            var failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal("23514", failure.SqlState);
        }

        var trigger = "fail_fulfillment_event_for_atomicity_test";
        var function = "fail_fulfillment_event_for_atomicity_test_fn";
        await ExecuteOwnerSqlAsync($"""
            CREATE FUNCTION fulfillment.{function}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test event failure' USING ERRCODE='23514'; END; $$;
            CREATE TRIGGER {trigger} BEFORE INSERT ON fulfillment.events
                FOR EACH ROW EXECUTE FUNCTION fulfillment.{function}();
            """);
        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
                new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 0.5m), null, "event-trigger-rollback", default));
        }
        finally
        {
            await ExecuteOwnerSqlAsync($"DROP TRIGGER {trigger} ON fulfillment.events; DROP FUNCTION fulfillment.{function}();");
        }

        var current = (await fixture.Store.FindAsync(fixture.Worker, fixture.OrderId, default))!;
        Assert.Equal(started.Revision, current.Revision);
        Assert.Equal(0m, current.CompletedQuantity);
        Assert.Equal(1, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(1, await CountAsync("fulfillment.entry_activity", fixture.TenantId));
        Assert.Equal(1, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
    }

    [Fact]
    public async Task RuntimeRejectsAnOversizedEventWithMatchingHeaderWithoutChangingEvidence()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var startEvent = (await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default))!
            .Items.Single().Event;
        var oversizedEvent = startEvent with
        {
            EventId = Guid.NewGuid(),
            Kind = FulfillmentEventKind.Disposition,
            Reason = new string('x', 70_000),
            Disposition = FulfillmentDispositionKind.Stopped,
        };
        var facts = JsonSerializer.Serialize(oversizedEvent, JsonSerializerOptions.Web);

        await using var source = fixture.CreateRuntimeSource();
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantAsync(connection, transaction, fixture.TenantId);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO fulfillment.events
                (tenant_id,order_id,event_id,revision,actor_id,authorization_revision,kind,target_event_id,facts,recorded_at)
            VALUES (@tenant,@order,@event,@revision,@actor,@authorization,@kind,@target,@facts::jsonb,@recorded)
            """, connection, transaction);
        insert.Parameters.AddWithValue("tenant", fixture.TenantId);
        insert.Parameters.AddWithValue("order", fixture.OrderId);
        insert.Parameters.AddWithValue("event", oversizedEvent.EventId);
        insert.Parameters.AddWithValue("revision", oversizedEvent.Revision);
        insert.Parameters.AddWithValue("actor", oversizedEvent.ActorAccountId);
        insert.Parameters.AddWithValue("authorization", oversizedEvent.AuthorizationRevision);
        insert.Parameters.AddWithValue("kind", (int)oversizedEvent.Kind);
        insert.Parameters.Add(new NpgsqlParameter("target", NpgsqlTypes.NpgsqlDbType.Uuid) { Value = DBNull.Value });
        insert.Parameters.AddWithValue("facts", facts);
        insert.Parameters.AddWithValue("recorded", oversizedEvent.RecordedAt);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal("events_facts_check", failure.ConstraintName);
        await transaction.RollbackAsync();

        Assert.Equal(started.Revision, (await fixture.Store.FindAsync(fixture.Worker, fixture.OrderId, default))!.Revision);
        Assert.Equal(1, await CountAsync("fulfillment.works", fixture.TenantId));
        Assert.Equal(1, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(1, await CountAsync("fulfillment.entry_activity", fixture.TenantId));
        Assert.Equal(1, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
    }

    [Fact]
    public async Task HistoryAndInboxBoundsHoldAndMalformedStoredFactsFailClosed()
    {
        await using var fixture = await CreateFixtureAsync();
        await StartAsync(fixture);
        var revision = 1L;
        for (var index = 0; index < 6; index++)
        {
            var blocked = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
                new(FulfillmentCommandKind.Block, revision, Reason: $"Block {index}", Disposition: FulfillmentDispositionKind.Stopped),
                null, $"block-{index}", default);
            Assert.Equal(FulfillmentStoreStatus.Applied, blocked.Status);
            var resolved = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
                new(FulfillmentCommandKind.Resolve, blocked.Work!.Revision, Reason: $"Resolved {index}"), null, $"resolve-{index}", default);
            Assert.Equal(FulfillmentStoreStatus.Applied, resolved.Status);
            revision = resolved.Work!.Revision;
        }

        var firstPage = await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default);
        Assert.Equal(10, firstPage!.Items.Count);
        Assert.NotNull(firstPage.NextRevision);
        for (var index = 0; index < 50; index++)
        {
            var orderId = Guid.NewGuid();
            var source = fixture.SourceFacts with { OrderId = orderId };
            fixture.SetSource(orderId, source);
            var created = await fixture.Store.ExecuteAsync(fixture.Worker, orderId,
                new(FulfillmentCommandKind.Start, 0), source.OrderRevision, $"inbox-{index}", default);
            Assert.Equal(FulfillmentStoreStatus.Applied, created.Status);
        }
        var inbox = await fixture.Store.ListAsync(fixture.Worker, new(50), default);
        Assert.Equal(50, inbox.Items.Count);
        Assert.NotNull(inbox.NextCursor);
        await Assert.ThrowsAsync<FulfillmentValidationException>(() =>
            fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(11), default));
        await Assert.ThrowsAsync<FulfillmentValidationException>(() =>
            fixture.Store.ListAsync(fixture.Worker, new(51), default));

        await CorruptStoredSourceAsync(fixture.TenantId, fixture.OrderId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.NewStore().FindAsync(fixture.Worker, fixture.OrderId, default));
    }

    [Theory]
    [InlineData("work-version")]
    [InlineData("event-version")]
    [InlineData("activity-version")]
    [InlineData("receipt-version")]
    [InlineData("malformed-event")]
    [InlineData("invalid-disposition-event")]
    [InlineData("oversized-event")]
    public async Task UnsupportedSchemaVersionsAndMalformedEventsFailClosedAfterOwnerMutation(string corruption)
    {
        await using var fixture = await CreateFixtureAsync();
        await StartAsync(fixture);
        switch (corruption)
        {
            case "work-version":
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.works DROP CONSTRAINT works_schema_version_check;");
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.works DISABLE TRIGGER guard_work;");
                try
                {
                    await ExecuteOwnerSqlAsync("UPDATE fulfillment.works SET schema_version=2 WHERE tenant_id=@tenant;",
                        ("tenant", fixture.TenantId));
                }
                finally
                {
                    await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.works ENABLE TRIGGER guard_work;");
                }
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    fixture.NewStore().FindAsync(fixture.Worker, fixture.OrderId, default));
                break;
            case "event-version":
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.events DROP CONSTRAINT events_schema_version_check;");
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.events DISABLE TRIGGER immutable_events;");
                try
                {
                    await ExecuteOwnerSqlAsync("UPDATE fulfillment.events SET schema_version=2 WHERE tenant_id=@tenant;",
                        ("tenant", fixture.TenantId));
                }
                finally
                {
                    await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.events ENABLE TRIGGER immutable_events;");
                }
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    fixture.NewStore().HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default));
                break;
            case "activity-version":
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.entry_activity DROP CONSTRAINT entry_activity_schema_version_check;");
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.entry_activity DISABLE TRIGGER guard_activity;");
                try
                {
                    await ExecuteOwnerSqlAsync("UPDATE fulfillment.entry_activity SET schema_version=2 WHERE tenant_id=@tenant;",
                        ("tenant", fixture.TenantId));
                }
                finally
                {
                    await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.entry_activity ENABLE TRIGGER guard_activity;");
                }
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    fixture.NewStore().HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default));
                break;
            case "receipt-version":
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.command_receipts DROP CONSTRAINT command_receipts_schema_version_check;");
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.command_receipts DISABLE TRIGGER immutable_receipts;");
                try
                {
                    await ExecuteOwnerSqlAsync("UPDATE fulfillment.command_receipts SET schema_version=2 WHERE tenant_id=@tenant;",
                        ("tenant", fixture.TenantId));
                }
                finally
                {
                    await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.command_receipts ENABLE TRIGGER immutable_receipts;");
                }
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    fixture.NewStore().ExecuteAsync(fixture.Worker, fixture.OrderId,
                        new(FulfillmentCommandKind.Start, 0), 12, "start", default));
                break;
            case "malformed-event":
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.events DISABLE TRIGGER immutable_events;");
                try
                {
                    await ExecuteOwnerSqlAsync("UPDATE fulfillment.events SET facts='{}'::jsonb WHERE tenant_id=@tenant;",
                        ("tenant", fixture.TenantId));
                }
                finally
                {
                    await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.events ENABLE TRIGGER immutable_events;");
                }
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    fixture.NewStore().HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default));
                break;
            case "invalid-disposition-event":
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.events DISABLE TRIGGER immutable_events;");
                try
                {
                    await ExecuteOwnerSqlAsync("""
                        UPDATE fulfillment.events
                        SET kind=4,
                            facts=jsonb_set(
                                jsonb_set(jsonb_set(facts,'{kind}','4'::jsonb),'{reason}','null'::jsonb),
                                '{disposition}','null'::jsonb)
                        WHERE tenant_id=@tenant
                        """, ("tenant", fixture.TenantId));
                }
                finally
                {
                    await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.events ENABLE TRIGGER immutable_events;");
                }
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    fixture.NewStore().HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default));
                break;
            case "oversized-event":
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.events DROP CONSTRAINT events_facts_check;");
                await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.events DISABLE TRIGGER immutable_events;");
                try
                {
                    await ExecuteOwnerSqlAsync("""
                        UPDATE fulfillment.events
                        SET facts=jsonb_set(facts,'{reason}',to_jsonb(repeat('x',70000)))
                        WHERE tenant_id=@tenant
                        """, ("tenant", fixture.TenantId));
                }
                finally
                {
                    await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.events ENABLE TRIGGER immutable_events;");
                }
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    fixture.NewStore().HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(corruption));
        }
    }

    [Fact]
    public async Task LongMaxExpectedRevisionsAreRejectedWithoutExtraDurableWork()
    {
        await using var fixture = await CreateFixtureAsync();
        var failure = await Assert.ThrowsAsync<FulfillmentValidationException>(() =>
            fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
                new(FulfillmentCommandKind.Start, 0), long.MaxValue, "max-order-revision", default));
        Assert.Equal("fulfillment_request_invalid", failure.Code);
        Assert.Equal(0, await CountAsync("fulfillment.works", fixture.TenantId));
        Assert.Equal(0, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(0, await CountAsync("fulfillment.command_receipts", fixture.TenantId));

        var started = await StartAsync(fixture);
        var mutationFailure = await Assert.ThrowsAsync<FulfillmentValidationException>(() =>
            fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
                new(FulfillmentCommandKind.Complete, long.MaxValue, Quantity: 1m), null, "max-mutation-revision", default));
        Assert.Equal("fulfillment_request_invalid", mutationFailure.Code);
        Assert.Equal(1, await CountAsync("fulfillment.works", fixture.TenantId));
        Assert.Equal(1, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(1, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
        Assert.Equal(1, started.Revision);
    }

    [Fact]
    public async Task FulfillmentMigrationRefusesDestructiveRollback()
    {
        await using var context = FulfillmentPostgresRegistration.CreateContext(ConnectionString);
        var migrator = context.GetService<IMigrator>();
        await Assert.ThrowsAsync<NotSupportedException>(() => migrator.MigrateAsync("0"));
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT to_regclass('fulfillment.works') IS NOT NULL", connection);
        Assert.True((bool)(await query.ExecuteScalarAsync())!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitingStartRollsBackOnCallerCancellationOrNamedBackendLossAndSameKeyRetrySucceeds(bool terminateBackend)
    {
        await using var fixture = await CreateFixtureAsync();
        var applicationName = $"fulfillment-start-{(terminateBackend ? "terminated" : "canceled")}-{Guid.NewGuid():N}";
        await using var blockedSource = fixture.CreateRuntimeSource(applicationName, maxPoolSize: 1);
        var blockedStore = fixture.NewStore(blockedSource);
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var held = await admin.BeginTransactionAsync();
        await using (var lockOrder = new NpgsqlCommand("""
            SELECT pg_advisory_xact_lock(('x' || substr(md5(
                'application:fulfillment-work:v1:' || @tenant::uuid::text || ':' || @order::uuid::text),1,16))::bit(64)::bigint)
            """, admin, held))
        {
            lockOrder.Parameters.AddWithValue("tenant", fixture.TenantId);
            lockOrder.Parameters.AddWithValue("order", fixture.OrderId);
            await lockOrder.ExecuteNonQueryAsync();
        }

        using var cancellation = new CancellationTokenSource();
        var submission = blockedStore.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Start, 0), fixture.SourceFacts.OrderRevision, "lock-retry", cancellation.Token);
        try
        {
            await WaitForAdvisoryWaitAsync(applicationName);
            Assert.False(submission.IsCompleted);
            if (terminateBackend)
            {
                Assert.True(await TerminateNamedBackendAsync(applicationName));
                await Assert.ThrowsAnyAsync<NpgsqlException>(() => submission);
            }
            else
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => submission);
            }
        }
        finally
        {
            cancellation.Cancel();
            await held.RollbackAsync();
            try { await submission.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception exception) when (exception is OperationCanceledException or NpgsqlException) { }
        }

        Assert.Equal(0, await CountAsync("fulfillment.works", fixture.TenantId));
        Assert.Equal(0, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(0, await CountAsync("fulfillment.entry_activity", fixture.TenantId));
        Assert.Equal(0, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
        var retrySource = fixture.CreateRuntimeSource();
        await using (retrySource)
        {
            var retry = await fixture.NewStore(retrySource).ExecuteAsync(fixture.Worker, fixture.OrderId,
                new(FulfillmentCommandKind.Start, 0), fixture.SourceFacts.OrderRevision, "lock-retry", default);
            Assert.Equal(FulfillmentStoreStatus.Applied, retry.Status);
        }
    }

    [Theory]
    [InlineData("events")]
    [InlineData("command_receipts")]
    public async Task HandoffEvidenceWriteFailureRollsBackAndSameKeyRetryCreatesOneFact(string target)
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var completed = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 2m), null, "handoff-complete", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, completed.Status);
        var completedWork = completed.Work!;
        var before = (
            Works: await CountAsync("fulfillment.works", fixture.TenantId),
            Events: await CountAsync("fulfillment.events", fixture.TenantId),
            Activities: await CountAsync("fulfillment.entry_activity", fixture.TenantId),
            Receipts: await CountAsync("fulfillment.command_receipts", fixture.TenantId));
        var (column, kind) = target switch
        {
            "events" => ("kind", (int)FulfillmentEventKind.Handoff),
            "command_receipts" => ("operation", (int)FulfillmentCommandKind.Handoff),
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        var trigger = $"fail_handoff_{target}_test";
        var function = $"fail_handoff_{target}_test_fn";
        await ExecuteOwnerSqlAsync($"""
            CREATE FUNCTION fulfillment.{function}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.{column}={kind} THEN
                    RAISE EXCEPTION 'test handoff evidence failure' USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER {trigger} BEFORE INSERT ON fulfillment.{target}
                FOR EACH ROW EXECUTE FUNCTION fulfillment.{function}();
            """);
        const string key = "handoff-failure-recovery";
        var mutation = new FulfillmentMutation(FulfillmentCommandKind.Handoff, completedWork.Revision,
            Quantity: 1m, Recipient: "Pickup recipient");
        try
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => fixture.Store.ExecuteAsync(
                fixture.Handoff, fixture.OrderId, mutation, null, key, default));
            Assert.Equal("23514", failure.SqlState);
        }
        finally
        {
            await ExecuteOwnerSqlAsync($"DROP TRIGGER {trigger} ON fulfillment.{target}; DROP FUNCTION fulfillment.{function}();");
        }

        var unchanged = (await fixture.Store.FindAsync(fixture.Worker, fixture.OrderId, default))!;
        Assert.Equal(completedWork.Revision, unchanged.Revision);
        Assert.Equal(2m, unchanged.CompletedQuantity);
        Assert.Equal(0m, unchanged.HandedOffQuantity);
        Assert.Equal(before, (
            Works: await CountAsync("fulfillment.works", fixture.TenantId),
            Events: await CountAsync("fulfillment.events", fixture.TenantId),
            Activities: await CountAsync("fulfillment.entry_activity", fixture.TenantId),
            Receipts: await CountAsync("fulfillment.command_receipts", fixture.TenantId)));

        var applied = await fixture.Store.ExecuteAsync(fixture.Handoff, fixture.OrderId, mutation, null, key, default);
        Assert.Equal(FulfillmentStoreStatus.Applied, applied.Status);
        var appliedWork = applied.Work!;
        Assert.Equal(1m, appliedWork.HandedOffQuantity);
        Assert.Equal(before.Works, await CountAsync("fulfillment.works", fixture.TenantId));
        var handoffCount = await CountAsync("fulfillment.events", fixture.TenantId);
        var receiptCount = await CountAsync("fulfillment.command_receipts", fixture.TenantId);
        Assert.Equal(before.Events + 1, handoffCount);
        Assert.Equal(before.Activities + 1, await CountAsync("fulfillment.entry_activity", fixture.TenantId));
        Assert.Equal(before.Receipts + 1, receiptCount);
        var replay = await fixture.NewStore().ExecuteAsync(fixture.Handoff, fixture.OrderId, mutation, null, key, default);
        Assert.Equal(FulfillmentStoreStatus.Replayed, replay.Status);
        Assert.Equal(appliedWork, replay.Work);
        Assert.Equal(handoffCount, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(receiptCount, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
        Assert.Single((await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default))!.Items,
            item => item.Event.Kind == FulfillmentEventKind.Handoff);
    }

    [Theory]
    [InlineData("event-update")]
    [InlineData("event-delete")]
    [InlineData("receipt-update")]
    [InlineData("receipt-delete")]
    public async Task RuntimeRoleCannotRewriteOrDeleteRetainedHandoffEvidence(string operation)
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var completed = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 2m), null, "protected-handoff-complete", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, completed.Status);
        const string key = "protected-handoff";
        var handoff = await fixture.Store.ExecuteAsync(fixture.Handoff, fixture.OrderId,
            new(FulfillmentCommandKind.Handoff, completed.Work!.Revision, Quantity: 1m, Recipient: "Pickup recipient"),
            null, key, default);
        Assert.Equal(FulfillmentStoreStatus.Applied, handoff.Status);
        var handoffEvent = Assert.Single((await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(10), default))!.Items,
            item => item.Event.Kind == FulfillmentEventKind.Handoff).Event;
        var sql = operation switch
        {
            "event-update" => "UPDATE fulfillment.events SET facts=facts WHERE tenant_id=@tenant AND order_id=@order AND event_id=@event",
            "event-delete" => "DELETE FROM fulfillment.events WHERE tenant_id=@tenant AND order_id=@order AND event_id=@event",
            "receipt-update" => "UPDATE fulfillment.command_receipts SET fingerprint=repeat('a',64) WHERE tenant_id=@tenant AND actor_id=@actor AND operation=@operation AND idempotency_key=@key",
            "receipt-delete" => "DELETE FROM fulfillment.command_receipts WHERE tenant_id=@tenant AND actor_id=@actor AND operation=@operation AND idempotency_key=@key",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        if (operation.StartsWith("event-", StringComparison.Ordinal))
            await AssertRuntimePermissionDeniedAsync(fixture, sql,
                ("tenant", fixture.TenantId), ("order", fixture.OrderId), ("event", handoffEvent.EventId));
        else
            await AssertRuntimePermissionDeniedAsync(fixture, sql,
                ("tenant", fixture.TenantId), ("actor", fixture.Handoff.AccountId),
                ("operation", (int)FulfillmentCommandKind.Handoff), ("key", key));
        Assert.Equal(3L, await CountAsync("fulfillment.events", fixture.TenantId));
        Assert.Equal(3L, await CountAsync("fulfillment.command_receipts", fixture.TenantId));
        Assert.Equal(1m, (await fixture.Store.FindAsync(fixture.Worker, fixture.OrderId, default))!.HandedOffQuantity);
    }

    [Fact]
    public async Task HistoryPagesRemainStableWhenANewerEventArrivesBetweenPages()
    {
        await using var fixture = await CreateFixtureAsync();
        var started = await StartAsync(fixture);
        var completed = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, started.Revision, Quantity: 2m), null, "page-complete", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, completed.Status);
        var handoff = await fixture.Store.ExecuteAsync(fixture.Handoff, fixture.OrderId,
            new(FulfillmentCommandKind.Handoff, completed.Work!.Revision, Quantity: 1m, Recipient: "Pickup recipient"),
            null, "page-handoff", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, handoff.Status);
        var handoffWork = handoff.Work!;

        var firstPage = await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(1), default);
        Assert.Equal(handoffWork.Revision, Assert.Single(firstPage!.Items).Event.Revision);
        var beforeRevision = firstPage.NextRevision ?? throw new InvalidOperationException("Expected another history page.");
        Assert.Equal(handoffWork.Revision, beforeRevision);

        var laterCompletion = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Complete, handoffWork.Revision, Quantity: 0.25m), null, "page-later-complete", default);
        Assert.Equal(FulfillmentStoreStatus.Applied, laterCompletion.Status);

        var secondPage = await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(1, beforeRevision), default);
        Assert.Equal(started.Revision + 1, Assert.Single(secondPage!.Items).Event.Revision);
        var nextBeforeRevision = secondPage.NextRevision ?? throw new InvalidOperationException("Expected the final history page.");
        Assert.Equal(started.Revision + 1, nextBeforeRevision);
        var lastPage = await fixture.Store.HistoryAsync(fixture.Worker, fixture.OrderId, new(1, nextBeforeRevision), default);
        Assert.Equal(started.Revision, Assert.Single(lastPage!.Items).Event.Revision);
        Assert.Null(lastPage.NextRevision);
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await using (var context = FulfillmentPostgresRegistration.CreateContext(ConnectionString))
            await context.Database.MigrateAsync();
        await ExecuteOwnerSqlAsync($"""
            CREATE ROLE {RuntimeRole} LOGIN PASSWORD '{RuntimePassword}';
            GRANT CONNECT ON DATABASE fulfillment_tests TO {RuntimeRole};
            GRANT USAGE ON SCHEMA fulfillment TO {RuntimeRole};
            GRANT SELECT, INSERT ON fulfillment.works, fulfillment.events, fulfillment.entry_activity,
                fulfillment.command_receipts TO {RuntimeRole};
            GRANT UPDATE(revision,snapshot,updated_at) ON fulfillment.works TO {RuntimeRole};
            GRANT UPDATE(active,voided_by) ON fulfillment.entry_activity TO {RuntimeRole};
            """);
    }

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    private async Task<Scenario> CreateFixtureAsync()
    {
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var worker = Guid.NewGuid();
        var handoff = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var members = new TestMembershipDirectory(new HashSet<Guid> { requester, worker, handoff, owner });
        var resolver = new ResolveTenantContext(members);
        async Task<TenantContext> Context(Guid accountId) =>
            await resolver.ExecuteAsync(accountId, tenantId, default) ?? throw new InvalidOperationException("Fixture membership was not active.");

        var source = new TestStartSource();
        source.Set(tenantId, orderId, new FulfillmentSourceFacts(
            tenantId, orderId, 12, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "PCS", 2, 3m,
            true, 1, true, true, false, true));
        var authority = new TestAuthorityReader();
        authority.Set(requester, CreateAuthority(canView: true, work: true));
        authority.Set(worker, CreateAuthority(canView: true, work: true));
        authority.Set(handoff, CreateAuthority(canView: true, handoff: true));
        authority.Set(owner, CreateAuthority(canView: true, work: true, handoff: true, correct: true,
            currentOwner: true, recent: true));
        var dataSource = CreateRuntimeSource();
        var clock = new FixedMicrosecondTimeProvider();
        var store = new PostgresFulfillmentStore(dataSource, source, authority, authority, clock);
        return new Scenario(this, dataSource, source, authority, clock, store, tenantId, orderId,
            source.Facts(tenantId, orderId),
            await Context(requester), await Context(worker), await Context(handoff), await Context(owner));
    }

    private NpgsqlDataSource CreateRuntimeSource(string? applicationName = null, int maxPoolSize = 8)
    {
        var settings = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = RuntimeRole,
            Password = RuntimePassword,
            ApplicationName = applicationName ?? string.Empty,
            MaxPoolSize = maxPoolSize,
        };
        return new NpgsqlDataSourceBuilder(settings.ConnectionString).Build();
    }

    private static async Task AssertRuntimeRejectsAsync(Scenario fixture, string sql, params (string Name, object Value)[] parameters)
    {
        await using var source = fixture.CreateRuntimeSource();
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantAsync(connection, transaction, fixture.TenantId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("23514", failure.SqlState);
        await transaction.RollbackAsync();
    }

    private static async Task AssertRuntimePermissionDeniedAsync(
        Scenario fixture, string sql, params (string Name, object Value)[] parameters)
    {
        await using var source = fixture.CreateRuntimeSource();
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantAsync(connection, transaction, fixture.TenantId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("42501", failure.SqlState);
        await transaction.RollbackAsync();
    }

    private async Task<long> CountAsync(string table, Guid tenantId)
    {
        if (table is not ("fulfillment.works" or "fulfillment.events" or "fulfillment.entry_activity" or "fulfillment.command_receipts"))
            throw new ArgumentOutOfRangeException(nameof(table));
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table} WHERE tenant_id=@tenant", connection);
        command.Parameters.AddWithValue("tenant", tenantId);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Count returned no value."));
    }

    private static async Task<long> CountVisibleAsync(
        NpgsqlConnection connection,
        string table,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection, transaction);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Visible count returned no value."));
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('app.current_tenant', @tenant, true)", connection, transaction);
        command.Parameters.AddWithValue("tenant", tenantId.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteOwnerSqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CorruptStoredSourceAsync(Guid tenantId, Guid orderId)
    {
        var invalidSource = JsonSerializer.Serialize(new { tenantId, orderId, orderRevision = 12 });
        await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.works DISABLE TRIGGER guard_work; ALTER TABLE fulfillment.works DISABLE TRIGGER require_atomic_evidence;");
        try
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                UPDATE fulfillment.works SET source=@source::jsonb,
                    snapshot=jsonb_set(snapshot,'{source}',@source::jsonb)
                WHERE tenant_id=@tenant AND order_id=@order
                """, connection);
            command.Parameters.AddWithValue("source", invalidSource);
            command.Parameters.AddWithValue("tenant", tenantId);
            command.Parameters.AddWithValue("order", orderId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
        finally
        {
            await ExecuteOwnerSqlAsync("ALTER TABLE fulfillment.works ENABLE TRIGGER guard_work; ALTER TABLE fulfillment.works ENABLE TRIGGER require_atomic_evidence;");
        }
    }

    private async Task WaitForAdvisoryWaitAsync(string applicationName)
    {
        await using var monitor = new NpgsqlConnection(ConnectionString);
        await monitor.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid
                  WHERE a.application_name=@name AND l.locktype='advisory' AND l.mode='ExclusiveLock' AND NOT l.granted)
                """, monitor);
            command.Parameters.AddWithValue("name", applicationName);
            if ((bool)(await command.ExecuteScalarAsync(timeout.Token))!) return;
            await Task.Delay(10, timeout.Token);
        }
    }

    private async Task<bool> TerminateNamedBackendAsync(string applicationName)
    {
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT pg_terminate_backend(pid) FROM pg_stat_activity
            WHERE application_name=@name AND backend_type='client backend'
            """, admin);
        command.Parameters.AddWithValue("name", applicationName);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<FulfillmentSnapshot> StartAsync(Scenario fixture, string key = "start")
    {
        var result = await fixture.Store.ExecuteAsync(fixture.Worker, fixture.OrderId,
            new(FulfillmentCommandKind.Start, 0), 12, key, default);
        Assert.Equal(FulfillmentStoreStatus.Applied, result.Status);
        return result.Work!;
    }

    private static FulfillmentAuthority CreateAuthority(
        bool canView,
        bool work = false,
        bool handoff = false,
        bool correct = false,
        bool currentOwner = false,
        bool recent = false) =>
        new(AuthorizationRevision, canView, work, handoff, correct, currentOwner, recent);

    private sealed class Scenario(
        FulfillmentPostgresTests test,
        NpgsqlDataSource dataSource,
        TestStartSource sourceReader,
        TestAuthorityReader authorityReader,
        TimeProvider clock,
        PostgresFulfillmentStore store,
        Guid tenantId,
        Guid orderId,
        FulfillmentSourceFacts sourceFacts,
        TenantContext requester,
        TenantContext worker,
        TenantContext handoff,
        TenantContext owner) : IAsyncDisposable
    {
        public TestAuthorityReader AuthorityReader { get; } = authorityReader;
        public Guid TenantId { get; } = tenantId;
        public Guid OrderId { get; } = orderId;
        public FulfillmentSourceFacts SourceFacts { get; } = sourceFacts;
        public TenantContext Requester { get; } = requester;
        public TenantContext Worker { get; } = worker;
        public TenantContext Handoff { get; } = handoff;
        public TenantContext Owner { get; } = owner;
        public PostgresFulfillmentStore Store { get; } = store;

        public void SetSource(Guid orderId, FulfillmentSourceFacts facts) => sourceReader.Set(TenantId, orderId, facts);

        public PostgresFulfillmentStore NewStore(NpgsqlDataSource? source = null) =>
            new(source ?? dataSource, sourceReader, AuthorityReader, AuthorityReader, clock);

        public NpgsqlDataSource CreateRuntimeSource(string? applicationName = null, int maxPoolSize = 8) =>
            test.CreateRuntimeSource(applicationName, maxPoolSize);

        public ValueTask DisposeAsync() => dataSource.DisposeAsync();
    }

    private sealed class TestStartSource : IFulfillmentStartSource
    {
        private readonly ConcurrentDictionary<(Guid Tenant, Guid Order), FulfillmentSourceFacts> _facts = new();

        public void Set(Guid tenantId, Guid orderId, FulfillmentSourceFacts facts) => _facts[(tenantId, orderId)] = facts;

        public FulfillmentSourceFacts Facts(Guid tenantId, Guid orderId) => _facts[(tenantId, orderId)];

        public Task<FulfillmentSourceFacts?> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            TenantContext context, Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult<FulfillmentSourceFacts?>(_facts.GetValueOrDefault((context.TenantId, orderId)));
    }

    private sealed class TestAuthorityReader : IFulfillmentAuthorityReader, IFulfillmentTransactionalAuthorityReader
    {
        private readonly ConcurrentDictionary<Guid, FulfillmentAuthority> _authorities = new();

        public void Set(Guid accountId, FulfillmentAuthority authority) => _authorities[accountId] = authority;

        public Task<FulfillmentAuthority> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            TenantContext context, CancellationToken cancellationToken)
        {
            Assert.Same(connection, transaction.Connection);
            return ResolveAsync(context, cancellationToken);
        }

        public Task<FulfillmentAuthority> ResolveAsync(TenantContext context, CancellationToken cancellationToken) =>
            Task.FromResult(_authorities.GetValueOrDefault(context.AccountId) ?? CreateAuthority(canView: false));
    }

    private sealed class TestMembershipDirectory(IReadOnlySet<Guid> activeAccounts) : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>(Array.Empty<TenantMembership>());

        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(activeAccounts.Contains(accountId));
    }

    private sealed class FixedMicrosecondTimeProvider : TimeProvider
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero).AddTicks(123450);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
