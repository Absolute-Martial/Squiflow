using Application.Customers;
using Application.Customers.Postgres;
using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Application.Customers.Postgres.Tests;

public sealed partial class CustomerPostgresTests
{
    [Fact]
    public async Task ForwardCanonicalizationFlattensIncomingSourcesButRetainsOriginalAuditAndReplay()
    {
        await MigrateAsync();
        var (tenant, actor) = await SeedAsync();
        var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var create = new CreateCustomerIndividual(store);
        var a = (await create.ExecuteAsync(context, new("A", "a@example.test", null), "a", CancellationToken.None)).Individual!;
        var b = (await create.ExecuteAsync(context, new("B", "b@example.test", null), "b", CancellationToken.None)).Individual!;
        var c = (await create.ExecuteAsync(context, new("C", null, null), "c", CancellationToken.None)).Individual!;
        var d = (await create.ExecuteAsync(context, new("D", null, null), "d", CancellationToken.None)).Individual!;
        var consolidate = new ConsolidateCustomerDuplicate(store);
        var originalRequest = new ConsolidateCustomerDuplicateRequest(b.IndividualId, a.IndividualId, 1, 1, "Manual review");
        var original = await consolidate.ExecuteAsync(context, originalRequest, "b-to-a", CancellationToken.None);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.Consolidated, original.Status);
        var evidence = await RetainedEvidenceAsync(tenant, b.IndividualId);

        // C -> B cannot bind A's revision to the caller's expected revision of B.
        var alias = await consolidate.ExecuteAsync(context, new(c.IndividualId, b.IndividualId, 1, 2), "c-to-b", CancellationToken.None);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.RevisionConflict, alias.Status);
        Assert.Equal(a.IndividualId, alias.CanonicalCustomerId);
        Assert.Equal(1L, await CountAsync("customers.customer_redirects"));
        Assert.Equal(ConsolidateCustomerDuplicateStatus.Consolidated,
            (await consolidate.ExecuteAsync(context, new(c.IndividualId, a.IndividualId, 1, 1), "c-to-a", CancellationToken.None)).Status);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.Consolidated,
            (await consolidate.ExecuteAsync(context, new(a.IndividualId, d.IndividualId, 1, 1), "a-to-d", CancellationToken.None)).Status);

        foreach (var originalCustomer in new[] { a, b, c })
        {
            var current = (await store.FindIndividualAsync(context, originalCustomer.IndividualId, CancellationToken.None))!;
            Assert.Equal(d.IndividualId, current.RedirectTargetIndividualId);
            Assert.Equal(originalCustomer.IndividualId == a.IndividualId ? 2 : 3, current.Revision);
            Assert.Equal(originalCustomer, current with { Revision = 1, RedirectTargetIndividualId = null });
            Assert.Equal(d.IndividualId, (await store.ResolveCurrentCustomerAsync(context, originalCustomer.IndividualId, CancellationToken.None))!.IndividualId);
        }
        Assert.Equal(evidence, await RetainedEvidenceAsync(tenant, b.IndividualId));
        Assert.Equal(original.Resolution, Assert.Single(await store.ReadResolutionsAsync(context, b.IndividualId, null, 50, CancellationToken.None)));
        var replay = await consolidate.ExecuteAsync(context, originalRequest, "b-to-a", CancellationToken.None);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.Replayed, replay.Status);
        Assert.Equal(original.Resolution, replay.Resolution);
        Assert.Equal(a.IndividualId, replay.CanonicalCustomerId); // original result, not today's D
        Assert.Equal(CreateCustomerIndividualStatus.Replayed,
            (await create.ExecuteAsync(context, new("B", "b@example.test", null), "b", CancellationToken.None)).Status);
        var changedKey = await consolidate.ExecuteAsync(context, new(b.IndividualId, d.IndividualId, 3, 1), "b-to-a", CancellationToken.None);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.IdempotencyKeyConflict, changedKey.Status);

        var edit = await new EditCustomerIndividualContact(store).ExecuteAsync(context,
            new(d.IndividualId, 1, "Current survivor", "current@example.test", null), "edit-survivor", CancellationToken.None);
        Assert.Equal(EditCustomerIndividualContactStatus.Changed, edit.Status);
        Assert.Equal(edit.Individual, await store.ResolveCurrentCustomerAsync(context, b.IndividualId, CancellationToken.None));
        Assert.Equal(evidence, await RetainedEvidenceAsync(tenant, b.IndividualId));
        Assert.Equal(4L, await CountAsync("customers.individuals"));
        Assert.Equal(3L, await CountAsync("customers.customer_redirects"));
        await AssertCanonicalGraphAsync();
    }

    [Fact]
    public async Task CanonicalizationRejectsCycleStaleRevisionInactiveAndCrossTenantTargetsWithoutEffects()
    {
        await MigrateAsync();
        var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        var (otherTenant, otherActor) = await SeedAsync(); var otherContext = await ResolveContextAsync(otherTenant, otherActor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var a = await CanonicalFixtureAsync(store, context, "A");
        var b = await CanonicalFixtureAsync(store, context, "B");
        var d = await CanonicalFixtureAsync(store, context, "D");
        var outside = await CanonicalFixtureAsync(store, otherContext, "Outside");
        var consolidate = new ConsolidateCustomerDuplicate(store);
        await consolidate.ExecuteAsync(context, new(b.IndividualId, a.IndividualId, 1, 1), "b-a", CancellationToken.None);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.CycleDetected,
            (await consolidate.ExecuteAsync(context, new(a.IndividualId, b.IndividualId, 1, 2), "cycle", CancellationToken.None)).Status);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.RevisionConflict,
            (await consolidate.ExecuteAsync(context, new(a.IndividualId, d.IndividualId, 2, 1), "stale-source", CancellationToken.None)).Status);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.RevisionConflict,
            (await consolidate.ExecuteAsync(context, new(a.IndividualId, d.IndividualId, 1, 2), "stale-target", CancellationToken.None)).Status);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.NotFound,
            (await consolidate.ExecuteAsync(context, new(a.IndividualId, outside.IndividualId, 1, 1), "outside", CancellationToken.None)).Status);
        Assert.Null(await store.ResolveCurrentCustomerAsync(otherContext, b.IndividualId, CancellationToken.None));
        await new ChangeCustomerIndividualAvailability(store).ExecuteAsync(context,
            new(d.IndividualId, 1, CustomerIndividualAvailability.Inactive), "inactive", CancellationToken.None);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.InvalidTarget,
            (await consolidate.ExecuteAsync(context, new(a.IndividualId, d.IndividualId, 1, 2), "inactive-target", CancellationToken.None)).Status);
        // Inactive sources are preserved, not forced active by consolidation.
        Assert.Equal(ConsolidateCustomerDuplicateStatus.Consolidated,
            (await consolidate.ExecuteAsync(context, new(d.IndividualId, a.IndividualId, 2, 1), "inactive-source", CancellationToken.None)).Status);
        Assert.Equal(CustomerIndividualAvailability.Inactive,
            (await store.FindIndividualAsync(context, d.IndividualId, CancellationToken.None))!.Availability);
        Assert.Equal(2L, await CountAsync("customers.customer_redirects"));
        Assert.Equal(2L, await CountAsync("customers.duplicate_command_receipts"));
        await AssertCanonicalGraphAsync();
    }

    [Fact]
    public async Task ContactAndReviewReceiptsReplayTheirOriginalFactsAfterSuccessorAdvances()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var a = await CanonicalFixtureAsync(store, context, "A"); var b = await CanonicalFixtureAsync(store, context, "B");
        var d = await CanonicalFixtureAsync(store, context, "D");
        var contactRequest = new EditCustomerIndividualContactRequest(b.IndividualId, 1, "Reviewed B", "b@example.test", null);
        var contact = await new EditCustomerIndividualContact(store).ExecuteAsync(context, contactRequest, "contact", CancellationToken.None);
        var reviewRequest = new ResolveCustomerDuplicateRequest(b.IndividualId, a.IndividualId, 2, 1, CustomerDuplicateOutcome.PotentialDuplicate);
        var review = await new ResolveCustomerDuplicate(store).ExecuteAsync(context, reviewRequest, "review", CancellationToken.None);
        await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context, new(b.IndividualId, a.IndividualId, 2, 1), "b-a", CancellationToken.None);
        var evidence = await RetainedEvidenceAsync(tenant, b.IndividualId);
        await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context, new(a.IndividualId, d.IndividualId, 1, 1), "a-d", CancellationToken.None);
        var contactReplay = await new EditCustomerIndividualContact(store).ExecuteAsync(context, contactRequest, "contact", CancellationToken.None);
        Assert.Equal(EditCustomerIndividualContactStatus.Replayed, contactReplay.Status);
        Assert.Equal(contact.Individual, contactReplay.Individual);
        Assert.Null(contactReplay.Individual!.RedirectTargetIndividualId);
        var reviewReplay = await new ResolveCustomerDuplicate(store).ExecuteAsync(context, reviewRequest, "review", CancellationToken.None);
        Assert.Equal(ResolveCustomerDuplicateStatus.Replayed, reviewReplay.Status);
        Assert.Equal(review.Resolution, reviewReplay.Resolution);
        Assert.Equal(evidence, await RetainedEvidenceAsync(tenant, b.IndividualId));
        var physical = (await store.FindIndividualAsync(context, b.IndividualId, CancellationToken.None))!;
        Assert.Equal(contact.Individual, physical with { Revision = 2, RedirectTargetIndividualId = null });
        Assert.Equal(4, physical.Revision);
        Assert.Equal(d.IndividualId, physical.RedirectTargetIndividualId);
        Assert.Equal(2, (await store.ReadResolutionsAsync(context, b.IndividualId, null, 50, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task ForwardCanonicalizationPreservesInactiveRelationshipsAndCollidingLinkReceipts()
    {
        await MigrateAsync();
        var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var a = await CanonicalFixtureAsync(store, context, "A"); var b = await CanonicalFixtureAsync(store, context, "B");
        var d = await CanonicalFixtureAsync(store, context, "D");
        var org = (await new CreateCustomerOrganization(store).ExecuteAsync(context, new("Org"), "org", CancellationToken.None)).Organization!;
        var program = (await new CreateCustomerProgram(store).ExecuteAsync(context, new(org.OrganizationId, "Program"), "program", CancellationToken.None)).Program!;
        var links = new LinkCustomerRepresentative(store);
        var collidingRequest = new LinkCustomerRepresentativeRequest(org.OrganizationId, null, b.IndividualId);
        var colliding = (await links.ExecuteAsync(context, collidingRequest, "b-link", CancellationToken.None)).Representative!;
        await links.ExecuteAsync(context, new(org.OrganizationId, null, a.IndividualId), "a-link", CancellationToken.None);
        var movingRequest = new LinkCustomerRepresentativeRequest(org.OrganizationId, program.ProgramId, b.IndividualId);
        var inactive = (await links.ExecuteAsync(context, movingRequest, "inactive-link", CancellationToken.None)).Representative!;
        var unlinkRequest = new UnlinkCustomerRepresentativeRequest(org.OrganizationId, inactive.RepresentativeId, 1);
        var unlinked = (await new UnlinkCustomerRepresentative(store).ExecuteAsync(context, unlinkRequest, "unlink", CancellationToken.None)).Representative!;
        var moving = (await links.ExecuteAsync(context, movingRequest, "moving-link", CancellationToken.None)).Representative!;
        await links.ExecuteAsync(context, new(org.OrganizationId, program.ProgramId, d.IndividualId), "d-program", CancellationToken.None);
        var consolidate = new ConsolidateCustomerDuplicate(store);
        await consolidate.ExecuteAsync(context, new(b.IndividualId, a.IndividualId, 1, 1), "b-a", CancellationToken.None);
        await consolidate.ExecuteAsync(context, new(a.IndividualId, d.IndividualId, 1, 1), "a-d", CancellationToken.None);
        Assert.Equal(unlinked, await store.FindRepresentativeAsync(context, inactive.RepresentativeId, CancellationToken.None));
        var collision = (await store.FindRepresentativeAsync(context, colliding.RepresentativeId, CancellationToken.None))!;
        Assert.Equal(CustomerRepresentativeAvailability.Inactive, collision.Availability);
        Assert.Equal(b.IndividualId, collision.IndividualId); Assert.Equal(2, collision.Revision);
        var moved = (await store.FindRepresentativeAsync(context, moving.RepresentativeId, CancellationToken.None))!;
        Assert.Equal(CustomerRepresentativeAvailability.Inactive, moved.Availability);
        Assert.Equal(a.IndividualId, moved.IndividualId); Assert.Equal(3, moved.Revision);
        Assert.Equal(colliding, (await links.ExecuteAsync(context, collidingRequest, "b-link", CancellationToken.None)).Representative);
        Assert.Equal(moving, (await links.ExecuteAsync(context, movingRequest, "moving-link", CancellationToken.None)).Representative);
        Assert.Equal(unlinked, (await new UnlinkCustomerRepresentative(store).ExecuteAsync(context, unlinkRequest, "unlink", CancellationToken.None)).Representative);
        // Original representative snapshots never follow today's successor pointer.
        Assert.Equal(LinkCustomerRepresentativeStatus.IndividualInactive,
            (await links.ExecuteAsync(context, collidingRequest, "new-source-link", CancellationToken.None)).Status);
        await AssertCanonicalGraphAsync();
    }

    [Fact]
    public async Task ManyActorConsolidationAndRepresentativeRacesRemainOneHopAndReplayHistoricalResults()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync();
        var contexts = new List<TenantContext> { await ResolveContextAsync(tenant, actor) };
        for (var index = 0; index < 9; index++)
        {
            var nextActor = Guid.NewGuid(); await SeedAdditionalAccountAsync(tenant, nextActor);
            contexts.Add(await ResolveContextAsync(tenant, nextActor));
        }
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var people = new List<CustomerIndividualSnapshot>();
        for (var index = 0; index < contexts.Count; index++)
            people.Add(await CanonicalFixtureAsync(store, contexts[0], $"Person {index}"));
        var org = (await new CreateCustomerOrganization(store).ExecuteAsync(contexts[0], new("Race org"), "org", CancellationToken.None)).Organization!;
        var originalLink = (await new LinkCustomerRepresentative(store).ExecuteAsync(contexts[0],
            new(org.OrganizationId, null, people[0].IndividualId), "seed-link", CancellationToken.None)).Representative!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consolidations = contexts.Select(async (context, index) =>
        {
            await release.Task;
            var request = new ConsolidateCustomerDuplicateRequest(people[index].IndividualId, people[(index + 1) % people.Count].IndividualId, 1, 1);
            return await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context, request, "race-consolidation", deadline.Token);
        }).ToArray();
        var links = contexts.Select(async (context, index) =>
        {
            await release.Task;
            return await new LinkCustomerRepresentative(store).ExecuteAsync(context,
                new(org.OrganizationId, null, people[index].IndividualId), "race-link", deadline.Token);
        }).ToArray();
        var unlinks = contexts.Select(async context =>
        {
            await release.Task;
            return await new UnlinkCustomerRepresentative(store).ExecuteAsync(context,
                new(org.OrganizationId, originalLink.RepresentativeId, 1), "race-unlink", deadline.Token);
        }).ToArray();
        async Task ObserveCanonicalReadsAsync()
        {
            await release.Task;
            for (var iteration = 0; iteration < 20; iteration++)
                foreach (var person in people)
                {
                    var observed = await store.ResolveCurrentCustomerAsync(contexts[0], person.IndividualId, deadline.Token);
                    Assert.NotNull(observed); Assert.Null(observed.RedirectTargetIndividualId);
                }
        }
        var canonicalReads = ObserveCanonicalReadsAsync();
        release.SetResult();
        await Task.WhenAll(consolidations.Cast<Task>().Concat(links).Concat(unlinks).Append(canonicalReads));
        var results = consolidations.Select(task => task.Result).ToArray();
        Assert.Contains(results, result => result.Status == ConsolidateCustomerDuplicateStatus.Consolidated);
        Assert.All(results, result => Assert.Contains(result.Status, new[] { ConsolidateCustomerDuplicateStatus.Consolidated,
            ConsolidateCustomerDuplicateStatus.AlreadyRedirected, ConsolidateCustomerDuplicateStatus.RevisionConflict,
            ConsolidateCustomerDuplicateStatus.CycleDetected }));
        await AssertCanonicalGraphAsync();

        var final = await CanonicalFixtureAsync(store, contexts[0], "Final survivor");
        // Advance every remaining root; no special case bans roots with incoming sources.
        foreach (var person in people)
        {
            var current = (await store.FindIndividualAsync(contexts[0], person.IndividualId, deadline.Token))!;
            if (current.RedirectTargetIndividualId.HasValue) continue;
            Assert.Equal(ConsolidateCustomerDuplicateStatus.Consolidated,
                (await new ConsolidateCustomerDuplicate(store).ExecuteAsync(contexts[0],
                    new(current.IndividualId, final.IndividualId, current.Revision, 1), $"final-{current.IndividualId:N}", deadline.Token)).Status);
        }
        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal(final.IndividualId, (await store.ResolveCurrentCustomerAsync(contexts[0], people[index].IndividualId, deadline.Token))!.IndividualId);
            if (results[index].Status != ConsolidateCustomerDuplicateStatus.Consolidated) continue;
            var replay = await new ConsolidateCustomerDuplicate(store).ExecuteAsync(contexts[index],
                new(people[index].IndividualId, people[(index + 1) % people.Count].IndividualId, 1, 1), "race-consolidation", deadline.Token);
            Assert.Equal(ConsolidateCustomerDuplicateStatus.Replayed, replay.Status);
            Assert.Equal(results[index].Resolution, replay.Resolution);
            Assert.Equal(results[index].CanonicalCustomerId, replay.CanonicalCustomerId);
        }
        Assert.Equal(11L, await CountAsync("customers.individuals"));
        await AssertCanonicalGraphAsync();
    }

    [Fact]
    public async Task DatabaseGuardsRollBackUnflattenedChangesAndRefuseHistoryOrBackwardPointerMutations()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime); var store = new PostgresCustomerStore(source);
        var a = await CanonicalFixtureAsync(store, context, "A"); var b = await CanonicalFixtureAsync(store, context, "B");
        var d = await CanonicalFixtureAsync(store, context, "D");
        await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context, new(b.IndividualId, a.IndividualId, 1, 1), "b-a", CancellationToken.None);
        var retained = await RetainedEvidenceAsync(tenant, b.IndividualId);
        await using (var session = await CustomerTenantDbSession.OpenAsync(source, tenant, CancellationToken.None))
        {
            await using var command = session.CreateCommand("""
                INSERT INTO customers.customer_redirects(tenant_id,source_customer_id,canonical_customer_id,created_by_account_id,created_at)
                  VALUES(@tenant,@a,@d,@actor,clock_timestamp());
                UPDATE customers.individuals SET redirect_target_individual_id=@d,revision=revision+1 WHERE tenant_id=@tenant AND id=@a;
                """);
            command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("a", a.IndividualId); command.Parameters.AddWithValue("d", d.IndividualId);
            await command.ExecuteNonQueryAsync();
            // A becomes redirected without flattening B: reject the entire commit.
            Assert.Equal(PostgresErrorCodes.CheckViolation,
                (await Assert.ThrowsAsync<PostgresException>(() => session.CommitAsync(CancellationToken.None))).SqlState);
        }
        Assert.Null((await store.FindIndividualAsync(context, a.IndividualId, CancellationToken.None))!.RedirectTargetIndividualId);
        foreach (var sql in new[]
        {
            "UPDATE customers.individuals SET redirect_target_individual_id=@d,revision=revision+1 WHERE tenant_id=@tenant AND id=@b",
            "UPDATE customers.individuals SET redirect_target_individual_id=NULL,revision=revision+1 WHERE tenant_id=@tenant AND id=@b",
            "UPDATE customers.individuals SET revision=revision+1 WHERE tenant_id=@tenant AND id=@b",
            "UPDATE customers.individuals SET display_name='Rewritten' WHERE tenant_id=@tenant AND id=@b",
        })
        {
            await using var session = await CustomerTenantDbSession.OpenAsync(source, tenant, CancellationToken.None);
            await using var command = session.CreateCommand(sql);
            command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("b", b.IndividualId);
            command.Parameters.AddWithValue("d", d.IndividualId);
            Assert.Equal(PostgresErrorCodes.CheckViolation,
                (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
        foreach (var sql in new[] { "UPDATE customers.customer_redirects SET canonical_customer_id=@d WHERE tenant_id=@tenant",
            "DELETE FROM customers.customer_redirects WHERE tenant_id=@tenant",
            "UPDATE customers.duplicate_command_receipts SET other_customer_id=@d WHERE tenant_id=@tenant",
            "DELETE FROM customers.duplicate_command_receipts WHERE tenant_id=@tenant" })
        {
            await using var session = await CustomerTenantDbSession.OpenAsync(source, tenant, CancellationToken.None);
            await using var command = session.CreateCommand(sql);
            command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("d", d.IndividualId);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
                (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
        // The additive history guard also prevents mutation by a table owner.
        await using (var owner = new NpgsqlConnection(ConnectionString))
        {
            await owner.OpenAsync(); await using var command = owner.CreateCommand();
            command.CommandText = "UPDATE customers.customer_redirects SET canonical_customer_id=@d WHERE tenant_id=@tenant";
            command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("d", d.IndividualId);
            Assert.Equal(PostgresErrorCodes.CheckViolation,
                (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
        Assert.Equal(retained, await RetainedEvidenceAsync(tenant, b.IndividualId));
        Assert.Equal(1L, await CountAsync("customers.customer_redirects"));
        await AssertCanonicalGraphAsync();
    }

    [Fact]
    public async Task AdditiveCanonicalizationMigrationPreservesExistingHistoryAndBlocksLossyDowngrade()
    {
        await MigrateAsync();
        await using (var db = CustomersPostgresMigrations.CreateContext(ConnectionString))
            await db.Database.MigrateAsync("20261007080000_CustomerImportAutonomousDiscovery");
        var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var a = await CanonicalFixtureAsync(store, context, "Legacy A"); var b = await CanonicalFixtureAsync(store, context, "Legacy B");
        var d = await CanonicalFixtureAsync(store, context, "Forward D");
        await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context, new(b.IndividualId, a.IndividualId, 1, 1), "legacy", CancellationToken.None);
        var original = await RetainedEvidenceAsync(tenant, b.IndividualId);
        await using var current = CustomersPostgresMigrations.CreateContext(ConnectionString);
        await current.Database.MigrateAsync();
        Assert.Single(typeof(Application.Customers.Postgres.Migrations.CustomerForwardCanonicalization)
            .GetCustomAttributes(typeof(Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute), inherit: true));
        Assert.False(current.Database.HasPendingModelChanges());
        Assert.Empty(await current.Database.GetPendingMigrationsAsync());
        Assert.Equal(original, await RetainedEvidenceAsync(tenant, b.IndividualId));
        await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context, new(a.IndividualId, d.IndividualId, 1, 1), "forward", CancellationToken.None);
        Assert.Equal(original, await RetainedEvidenceAsync(tenant, b.IndividualId));
        var downgrade = await Assert.ThrowsAsync<PostgresException>(() => current.Database.MigrateAsync("20261007080000_CustomerImportAutonomousDiscovery"));
        Assert.Contains("Cannot downgrade forward-canonicalized", downgrade.MessageText, StringComparison.Ordinal);
        Assert.Contains("20261007180000_CustomerImportRawSourceLifecycle", await current.Database.GetPendingMigrationsAsync());
        await AssertCanonicalGraphAsync();
    }

    [Fact]
    public async Task RedirectSourceReadReportsItsSuccessorWithoutRewritingRetainedAvailability()
    {
        await MigrateAsync();
        var (tenant, actor) = await SeedAsync();
        var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var a = await CanonicalFixtureAsync(store, context, "Source");
        var b = await CanonicalFixtureAsync(store, context, "Survivor");
        await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context,
            new(a.IndividualId, b.IndividualId, 1, 1), "a-b", CancellationToken.None);

        // The individual read is a physical/historical read, not the canonical directory:
        // it must report the successor without rewriting what consolidation retained.
        var read = await store.FindIndividualAsync(context, a.IndividualId, CancellationToken.None);
        Assert.NotNull(read);
        Assert.Equal(b.IndividualId, read!.RedirectTargetIndividualId);
        Assert.Equal(CustomerIndividualAvailability.Active, read.Availability);
        Assert.Equal("Source", read.DisplayName);
        Assert.Equal(2, read.Revision);
        Assert.Equal(b.IndividualId, (await store.ResolveCurrentCustomerAsync(context, a.IndividualId, CancellationToken.None))!.IndividualId);
        var survivor = await store.FindIndividualAsync(context, b.IndividualId, CancellationToken.None);
        Assert.NotNull(survivor);
        Assert.Null(survivor!.RedirectTargetIndividualId);

        // Availability alone cannot carry that meaning, so the read must keep it explicit:
        // this row still reports Active yet every mutation against it is refused.
        Assert.Equal(EditCustomerIndividualContactStatus.NotFound,
            (await new EditCustomerIndividualContact(store).ExecuteAsync(context,
                new(a.IndividualId, read.Revision, "Rewritten", "rewritten@example.test", null),
                "edit-source", CancellationToken.None)).Status);
        Assert.Equal(ChangeCustomerIndividualAvailabilityStatus.NotFound,
            (await new ChangeCustomerIndividualAvailability(store).ExecuteAsync(context,
                new(a.IndividualId, read.Revision, CustomerIndividualAvailability.Inactive),
                "availability-source", CancellationToken.None)).Status);
        Assert.Equal(a.IndividualId,
            (await store.FindIndividualAsync(context, a.IndividualId, CancellationToken.None))!.IndividualId);
        await AssertCanonicalGraphAsync();
    }

    [Fact]
    public async Task DiscoveryExcludesConsolidatedSourcesAndResolutionRefusesRedirectedPairs()
    {
        await MigrateAsync();
        var (tenant, actor) = await SeedAsync();
        var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var create = new CreateCustomerIndividual(store);
        const string shared = "shared@example.test";
        var a = (await create.ExecuteAsync(context, new("Alice Smith", shared, null), "a", CancellationToken.None)).Individual!;
        var b = (await create.ExecuteAsync(context, new("Alice Smyth", shared, null), "b", CancellationToken.None)).Individual!;
        var c = (await create.ExecuteAsync(context, new("Alice Smythe", shared, null), "c", CancellationToken.None)).Individual!;
        var distinct = (await create.ExecuteAsync(context, new("Alice Distinct", shared, null), "d", CancellationToken.None)).Individual!;
        var signals = CustomerDuplicateSignals.Create("Alice Smith", shared);

        // Every matching row is a live candidate until B is consolidated into A.
        Assert.Equal(4, (await new FindCustomerDuplicates(store).ExecuteAsync(context,
            new(signals, null, 10), CancellationToken.None)).Candidates.Count);
        await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context,
            new(b.IndividualId, a.IndividualId, 1, 1), "b-a", CancellationToken.None);

        var offered = (await new FindCustomerDuplicates(store).ExecuteAsync(context,
            new(signals, null, 10), CancellationToken.None)).Candidates;
        var offeredIds = offered.Select(candidate => candidate.Customer.IndividualId).ToArray();
        Assert.Equal(3, offeredIds.Length);
        Assert.DoesNotContain(b.IndividualId, offeredIds);
        Assert.Contains(a.IndividualId, offeredIds);
        // Every offered candidate must still be a live, writable customer identity.
        Assert.All(offered, candidate =>
        {
            Assert.Null(candidate.Customer.RedirectTargetIndividualId);
            Assert.Equal(CustomerIndividualAvailability.Active, candidate.Customer.Availability);
        });

        // Recording KeepSeparate/Dismiss for a pair containing a consolidated source would
        // assert a review outcome about an identity that is no longer a customer at all.
        var resolve = new ResolveCustomerDuplicate(store);
        foreach (var (left, right) in new[]
        {
            (b.IndividualId, a.IndividualId), (a.IndividualId, b.IndividualId)
        })
        {
            var refused = await resolve.ExecuteAsync(context,
                new(left, right, 1, 1, CustomerDuplicateOutcome.KeepSeparate, "Different people"),
                $"redirected-{left:N}", CancellationToken.None);
            Assert.Equal(ResolveCustomerDuplicateStatus.AlreadyRedirected, refused.Status);
            Assert.Null(refused.Resolution);
        }
        // Redirect is decided before the caller's revision, so a stale revision cannot be
        // mistaken for the reason the pair was refused.
        Assert.Equal(ResolveCustomerDuplicateStatus.AlreadyRedirected,
            (await resolve.ExecuteAsync(context,
                new(a.IndividualId, b.IndividualId, 3, 1, CustomerDuplicateOutcome.KeepSeparate, "Different people"),
                "redirected-stale", CancellationToken.None)).Status);
        Assert.Equal(ResolveCustomerDuplicateStatus.AlreadyRedirected,
            (await resolve.ExecuteAsync(context,
                new(a.IndividualId, b.IndividualId, 1, 1, CustomerDuplicateOutcome.Dismiss),
                "redirected-dismiss", CancellationToken.None)).Status);
        // No refused attempt left a review decision record behind.
        foreach (var customer in new[] { a, b })
            Assert.All(await store.ReadResolutionsAsync(context, customer.IndividualId, null, 50, CancellationToken.None),
                resolution => Assert.NotEqual(CustomerDuplicateOutcome.KeepSeparate, resolution.Outcome));

        // A live pair still resolves, and its retained resolution is still honoured.
        var resolved = await resolve.ExecuteAsync(context,
            new(a.IndividualId, distinct.IndividualId, 1, 1, CustomerDuplicateOutcome.KeepSeparate, "Different people"),
            "resolved-pair", CancellationToken.None);
        Assert.Equal(ResolveCustomerDuplicateStatus.Resolved, resolved.Status);
        var retained = (await new FindCustomerDuplicates(store).ExecuteAsync(context,
            new(signals, a.IndividualId, 10), CancellationToken.None)).Candidates
            .Single(candidate => candidate.Customer.IndividualId == distinct.IndividualId);
        Assert.False(retained.RequiresManualReview);
        Assert.Equal(resolved.Resolution, retained.Resolution);
        Assert.Equal(1L, await CountAsync("customers.customer_redirects"));
        Assert.Equal(4L, await CountAsync("customers.individuals"));
        await AssertCanonicalGraphAsync();
    }

    private static async Task<CustomerIndividualSnapshot> CanonicalFixtureAsync(PostgresCustomerStore store, TenantContext context, string name) =>
        (await new CreateCustomerIndividual(store).ExecuteAsync(context, new(name, null, null), name, CancellationToken.None)).Individual!;

    private async Task<string> RetainedEvidenceAsync(Guid tenantId, Guid customerId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT jsonb_build_object(
              'redirects',(SELECT jsonb_agg(to_jsonb(r) ORDER BY source_customer_id) FROM customers.customer_redirects r WHERE tenant_id=@tenant AND source_customer_id=@id),
              'resolutions',(SELECT jsonb_agg(to_jsonb(r) ORDER BY resolution_id) FROM customers.duplicate_command_receipts r WHERE tenant_id=@tenant AND customer_id=@id),
              'creates',(SELECT jsonb_agg(to_jsonb(r) ORDER BY operation,idempotency_key) FROM customers.individual_command_receipts r WHERE tenant_id=@tenant AND individual_id=@id),
              'links',(SELECT jsonb_agg(to_jsonb(r) ORDER BY operation,idempotency_key) FROM customers.representative_command_receipts r WHERE tenant_id=@tenant AND individual_id=@id))::text
            """;
        command.Parameters.AddWithValue("tenant", tenantId); command.Parameters.AddWithValue("id", customerId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task AssertCanonicalGraphAsync()
    {
        Assert.Equal(0L, await CountAsync("""
            customers.individuals i LEFT JOIN customers.individuals target
              ON target.tenant_id=i.tenant_id AND target.id=i.redirect_target_individual_id
            WHERE i.redirect_target_individual_id IS NOT NULL
              AND (target.id IS NULL OR target.redirect_target_individual_id IS NOT NULL OR target.id=i.id)
            """));
        Assert.Equal(0L, await CountAsync("""
            customers.representatives r JOIN customers.individuals i ON i.tenant_id=r.tenant_id AND i.id=r.individual_id
            WHERE r.availability=1 AND i.redirect_target_individual_id IS NOT NULL
            """));
        Assert.Equal(0L, await CountAsync("""
            (SELECT tenant_id,organization_id,program_id,individual_id FROM customers.representatives
             WHERE availability=1 GROUP BY tenant_id,organization_id,program_id,individual_id HAVING count(*)>1) collisions
            """));
    }
}
