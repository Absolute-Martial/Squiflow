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
    public async Task DuplicateConsolidationRetainsBothRowsAndCreatesReplaySafeRedirect()
    {
        await MigrateAsync();
        var (tenantId, accountId) = await SeedAsync();
        var context = await ResolveContextAsync(tenantId, accountId);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime);
        var store = new PostgresCustomerStore(source);
        var first = (await new CreateCustomerIndividual(store).ExecuteAsync(context,
            new("Alice Smith", "alice@example.test", "+1 (555) 0100", "ERP-1"), "alice-1", CancellationToken.None)).Individual!;
        var second = (await new CreateCustomerIndividual(store).ExecuteAsync(context,
            new("Alice Smyth", "other@example.test", "+1 555 0100", "ERP-2"), "alice-2", CancellationToken.None)).Individual!;

        var candidates = await new FindCustomerDuplicates(store).ExecuteAsync(context,
            new(CustomerDuplicateSignals.Create("Alice Smith", null, "+15550100"), first.IndividualId, 10), CancellationToken.None);
        var match = Assert.Single(candidates.Candidates);
        Assert.Equal(second.IndividualId, match.Customer.IndividualId);
        Assert.Contains(match.Evidence, evidence => evidence.Kind == CustomerDuplicateEvidenceKind.NormalizedPhone);

        var consolidated = await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context,
            new(second.IndividualId, first.IndividualId, second.Revision, first.Revision), "consolidate-1", CancellationToken.None);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.Consolidated, consolidated.Status);
        var replay = await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context,
            new(second.IndividualId, first.IndividualId, second.Revision, first.Revision), "consolidate-1", CancellationToken.None);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.Replayed, replay.Status);
        Assert.Equal(2L, await CountAsync("customers.individuals"));
        Assert.Equal(1L, await CountAsync("customers.customer_redirects"));

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var redirect = connection.CreateCommand();
        redirect.CommandText = "SELECT canonical_customer_id FROM customers.customer_redirects WHERE tenant_id = @tenant_id AND source_customer_id = @source_id";
        redirect.Parameters.AddWithValue("tenant_id", tenantId);
        redirect.Parameters.AddWithValue("source_id", second.IndividualId);
        Assert.Equal(first.IndividualId, await redirect.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ImportPersistsPlanHashesAndUsesExplicitMappingWithoutSilentMerge()
    {
        await MigrateAsync();
        var (tenantId, accountId) = await SeedAsync();
        var context = await ResolveContextAsync(tenantId, accountId);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime);
        var store = new PostgresCustomerStore(source);
        var existing = (await new CreateCustomerIndividual(store).ExecuteAsync(context,
            new("Existing", "existing@example.test", null), "existing-1", CancellationToken.None)).Individual!;
        const string csv = "name,email,external_id\n" +
                           "Existing,existing@example.test,ERP-existing\n" +
                           "New Person,new@example.test,ERP-new\n";
        var planned = await new CreateCustomerImport(store).ExecuteAsync(context,
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)), Guid.NewGuid(), "import-plan-1", CancellationToken.None);
        Assert.True(planned.Created);
        Assert.Equal(64, planned.Plan.ManifestHash.Length);
        var authority = new ImportAuthority(context);
        var executed = await new ExecuteCustomerImport(store, authority).ExecuteAsync(context,
            new(planned.ImportId, new Dictionary<int, Guid> { [2] = existing.IndividualId }),
            "import-work-1", CancellationToken.None);

        Assert.Equal(CustomerImportWorkStatus.Accepted, executed.Work.Status);
        Assert.Equal(1L, await CountAsync("customers.individuals"));
        await new RunCustomerImportBatch(store, authority).ExecuteAsync(tenantId, Guid.NewGuid(), 50, CancellationToken.None);
        var completed = await store.ReadImportSummaryAsync(context, planned.ImportId, CancellationToken.None);
        Assert.Equal(CustomerImportWorkStatus.Completed, completed!.Work!.Status);
        Assert.Equal(2, completed.Work.ProcessedRows);
        Assert.Equal(0, completed.Work.RemainingRows);
        var rows = await store.ReadImportRowsPageAsync(context, planned.ImportId, 0, 25, CancellationToken.None);
        Assert.Equal(CustomerImportRowStatus.MappedToExisting, rows.Items.Single(row => row.RowNumber == 2).Status);
        Assert.Equal(existing.IndividualId, rows.Items.Single(row => row.RowNumber == 2).CustomerId);
        Assert.Equal(CustomerImportRowStatus.Imported, rows.Items.Single(row => row.RowNumber == 3).Status);
        Assert.Equal(rows.Items[1].RowId, rows.Items[1].CustomerId);
        Assert.Equal(2L, await CountAsync("customers.individuals"));
        Assert.Equal(1L, await CountAsync("customers.imports"));
        Assert.Equal(2L, await CountAsync("customers.import_rows"));
        Assert.Equal(1L, await CountAsync("customers.import_work"));
        var survivor = (await new CreateCustomerIndividual(store).ExecuteAsync(context,
            new("Later Canonical", null, null), "later-canonical", CancellationToken.None)).Individual!;
        await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context,
            new(existing.IndividualId, survivor.IndividualId, 1, 1), "later-consolidation", CancellationToken.None);
        var replay = await new ExecuteCustomerImport(store, authority).ExecuteAsync(context,
            new(planned.ImportId, new Dictionary<int, Guid> { [2] = existing.IndividualId }), "import-work-1", CancellationToken.None);
        Assert.Equal(CustomerImportWorkStatus.Completed, replay.Work.Status);
        Assert.Equal(existing.IndividualId, (await store.ReadImportRowsPageAsync(context, planned.ImportId, 0, 25, CancellationToken.None)).Items[0].CustomerId);
    }

    [Fact]
    public async Task ImportSourceRetirementClaimsAreFencedRecoverableAndReleaseUsageOnce()
    {
        await MigrateAsync();
        var (tenantId, accountId) = await SeedAsync();
        var context = await ResolveContextAsync(tenantId, accountId);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var plan = await CustomerImportCsv.PlanAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("name\nRetained Source\n")),
            Guid.NewGuid(), CancellationToken.None);
        var policy = CustomerImportSourcePolicy.Create("test/retained", 1_000_000);
        var lease = await store.BeginSourceAsync(context, plan, "source-1", CustomerImportRetention.DefaultSevenDays,
            policy, CancellationToken.None);
        Assert.True(lease.UploadRequired);
        var completed = await store.CompleteSourceAsync(context, plan, "source-1", CustomerImportRetention.DefaultSevenDays,
            policy, lease, CancellationToken.None);
        Assert.True(completed.Created);
        var sourceMetadata = await store.ReadSourceAsync(context, completed.ImportId, CancellationToken.None);
        Assert.NotNull(sourceMetadata);
        Assert.Equal(CustomerImportSourceState.Available, sourceMetadata!.State);
        Assert.Equal(CustomerImportRetention.DefaultSevenDays, sourceMetadata.Retention);
        Assert.NotNull(sourceMetadata.ExpiresAt);

        var replay = await store.BeginSourceAsync(context, plan, "source-1", CustomerImportRetention.DefaultSevenDays,
            policy, CancellationToken.None);
        Assert.False(replay.UploadRequired);
        Assert.Equal(sourceMetadata.ObjectKey, replay.ObjectKey);
        await Assert.ThrowsAsync<CustomerValidationException>(() => store.BeginSourceAsync(context, plan, "source-1",
            CustomerImportRetention.TenantArchived, policy, CancellationToken.None));

        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT set_config('app.current_tenant', @tenant::text, true); UPDATE customers.import_source_objects SET expires_at=clock_timestamp()-interval '1 minute' WHERE tenant_id=@tenant AND object_key=@key";
            command.Parameters.AddWithValue("tenant", tenantId);
            command.Parameters.AddWithValue("key", sourceMetadata.ObjectKey);
            await command.ExecuteNonQueryAsync();
        }

        await GrantImportDiscoveryAsync();
        var discovered = await store.DiscoverRunnableTenantsAsync(null, 10, CancellationToken.None);
        Assert.Contains(tenantId, discovered.TenantIds);

        await FixtureSqlAsync($"""
            INSERT INTO customers.object_storage_reservations
                (tenant_id, provider_scope, reservation_id, idempotency_key, fingerprint, object_key, byte_length, state, created_at, updated_at)
            VALUES ('{tenantId:D}', '{policy.ProviderScope}', '{Guid.CreateVersion7():D}', 'unknown-retirement-reservation', '{plan.Fingerprint}',
                '{sourceMetadata.ObjectKey}', {plan.ByteLength}, 4, clock_timestamp(), clock_timestamp())
            """);
        Assert.Null(await store.ClaimExpiredSourceRetirementAsync(context, DateTimeOffset.UtcNow, CancellationToken.None));
        await FixtureSqlAsync("DELETE FROM customers.object_storage_reservations WHERE idempotency_key='unknown-retirement-reservation'");

        var claims = await Task.WhenAll(
            store.ClaimExpiredSourceRetirementAsync(context, DateTimeOffset.UtcNow, CancellationToken.None),
            store.ClaimExpiredSourceRetirementAsync(context, DateTimeOffset.UtcNow, CancellationToken.None));
        var retirement = Assert.Single(claims, claim => claim is not null)!;
        Assert.Null(claims.Single(claim => claim is null));
        Assert.Equal(1L, retirement.Generation);
        Assert.NotEqual(Guid.Empty, retirement.LeaseId);

        await Assert.ThrowsAsync<CustomerValidationException>(() => store.BeginSourceAsync(context, plan, "source-1",
            CustomerImportRetention.DefaultSevenDays, policy, CancellationToken.None));

        await FixtureSqlAsync("UPDATE customers.import_source_objects SET retirement_lease_expires_at=clock_timestamp()-interval '1 second'");
        Assert.Contains(tenantId, (await store.DiscoverRunnableTenantsAsync(null, 10, CancellationToken.None)).TenantIds);
        var recovered = await store.ClaimExpiredSourceRetirementAsync(context, DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.NotNull(recovered);
        Assert.True(recovered!.Generation > retirement.Generation);
        Assert.NotEqual(retirement.LeaseId, recovered.LeaseId);

        await store.CompleteSourceRetirementAsync(context, retirement, true, null, CancellationToken.None);
        var stillPending = await store.ReadSourceAsync(context, completed.ImportId, CancellationToken.None);
        Assert.Equal(CustomerImportSourceState.RetirementPending, stillPending!.State);
        Assert.Equal(plan.ByteLength, (await ReadStorageUsageAsync(tenantId, policy.ProviderScope)).Retained);

        await store.CompleteSourceRetirementAsync(context, recovered, true, null, CancellationToken.None);
        var retired = await store.ReadSourceAsync(context, completed.ImportId, CancellationToken.None);
        Assert.Equal(CustomerImportSourceState.Retired, retired!.State);
        Assert.Equal(0L, (await ReadStorageUsageAsync(tenantId, policy.ProviderScope)).Retained);
        await store.CompleteSourceRetirementAsync(context, recovered, true, null, CancellationToken.None);
        Assert.Equal(0L, (await ReadStorageUsageAsync(tenantId, policy.ProviderScope)).Retained);
        await Assert.ThrowsAsync<CustomerValidationException>(() => store.BeginSourceAsync(context, plan, "source-1",
            CustomerImportRetention.DefaultSevenDays, policy, CancellationToken.None));
        Assert.NotNull(await store.ReadImportSummaryAsync(context, completed.ImportId, CancellationToken.None));
    }

    // customers.object_storage_reservations.state is the persisted integer domain fixed by the
    // Customers migration (state IN (1, 2, 3, 4)). It has no published enum, so the lifecycle
    // values are named here rather than asserted as bare integers.
    private const int ReservationHeld = 1;
    private const int ReservationCommitted = 2;
    private const int ReservationReleased = 3;
    private const int ReservationOutcomeUnknown = 4;

    [Fact]
    public async Task ImportSourceAllowanceIsReservedThenRetainedRefusedWhenExhaustedAndOnlyReleasedByAProvenAbsentUpload()
    {
        await MigrateAsync();
        var (tenantId, accountId) = await SeedAsync();
        var context = await ResolveContextAsync(tenantId, accountId);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);

        // The allowance is a small multiple of one source so an exhausted allowance can be probed
        // with a plan whose single CSV field stays inside the accepted raw field length.
        const int AllowanceInSources = 20;
        var retained = await PlanOfByteLengthAsync(21);
        var policy = CustomerImportSourcePolicy.Create(
            "test/source-allowance", retained.ByteLength * AllowanceInSources);

        var retainedLease = await store.BeginSourceAsync(context, retained, "allowance-retained",
            CustomerImportRetention.DefaultSevenDays, policy, CancellationToken.None);
        Assert.True(retainedLease.UploadRequired);
        Assert.NotEqual(Guid.Empty, retainedLease.ReservationId);

        // A staged source has already consumed its bytes from the tenant's allowance even though
        // nothing is retained, because a crashed uploader must not be able to re-spend the same
        // allowance on a second source.
        var staged = await ReadStorageUsageAsync(tenantId, policy.ProviderScope);
        Assert.Equal(retained.ByteLength, staged.Reserved);
        Assert.Equal(0L, staged.Retained);
        var stagedReservations = await ReadReservationStatesAsync(tenantId, policy.ProviderScope);
        Assert.Equal(ReservationHeld, stagedReservations[retainedLease.ObjectKey]);

        var completed = await store.CompleteSourceAsync(context, retained, "allowance-retained",
            CustomerImportRetention.DefaultSevenDays, policy, retainedLease, CancellationToken.None);
        Assert.True(completed.Created);

        // A published source moves the reservation out of the pending allowance and into the
        // retained bytes instead of releasing it, so the same bytes are charged exactly once.
        var published = await ReadStorageUsageAsync(tenantId, policy.ProviderScope);
        Assert.Equal(0L, published.Reserved);
        Assert.Equal(retained.ByteLength, published.Retained);
        Assert.Equal(ReservationCommitted,
            (await ReadReservationStatesAsync(tenantId, policy.ProviderScope))[retainedLease.ObjectKey]);

        var remaining = policy.MaximumRetainedBytes - published.Retained - published.Reserved;
        var refused = await PlanOfByteLengthAsync(remaining + 1);
        Assert.True(refused.ByteLength <= policy.MaximumRetainedBytes,
            "The refusal must come from accumulated usage, not from a single oversized plan.");
        var capacity = await Assert.ThrowsAsync<CustomerValidationException>(() => store.BeginSourceAsync(
            context, refused, "allowance-refused", CustomerImportRetention.DefaultSevenDays, policy, CancellationToken.None));
        Assert.Equal("import_storage_capacity", capacity.Code);
        var afterRefusal = await ReadStorageUsageAsync(tenantId, policy.ProviderScope);
        Assert.Equal(published.Reserved, afterRefusal.Reserved);
        Assert.Equal(published.Retained, afterRefusal.Retained);
        var refusedReservations = await ReadReservationStatesAsync(tenantId, policy.ProviderScope);
        Assert.Single(refusedReservations);
        Assert.DoesNotContain("allowance-refused", refusedReservations.Keys, StringComparer.Ordinal);

        var absent = await PlanOfByteLengthAsync(remaining);
        var absentLease = await store.BeginSourceAsync(context, absent, "allowance-proven-absent",
            CustomerImportRetention.DefaultSevenDays, policy, CancellationToken.None);
        Assert.Equal(remaining, (await ReadStorageUsageAsync(tenantId, policy.ProviderScope)).Reserved);
        await store.MarkSourceFailureAsync(context, absentLease, CustomerImportSourceFailureKind.ProvenAbsent,
            "object_absent", CancellationToken.None);

        // A proven-absent upload never happened, so its bytes return to the allowance instead of
        // being charged as retained.
        var released = await ReadStorageUsageAsync(tenantId, policy.ProviderScope);
        Assert.Equal(0L, released.Reserved);
        Assert.Equal(published.Retained, released.Retained);
        Assert.Equal(ReservationReleased,
            (await ReadReservationStatesAsync(tenantId, policy.ProviderScope))[absentLease.ObjectKey]);
        Assert.Equal(CustomerImportSourceState.Unavailable,
            await ReadSourceStateAsync(tenantId, absentLease.ObjectKey));

        var unknown = await PlanOfByteLengthAsync(remaining);
        var unknownLease = await store.BeginSourceAsync(context, unknown, "allowance-outcome-unknown",
            CustomerImportRetention.DefaultSevenDays, policy, CancellationToken.None);
        Assert.Equal(remaining, (await ReadStorageUsageAsync(tenantId, policy.ProviderScope)).Reserved);
        await store.MarkSourceFailureAsync(context, unknownLease, CustomerImportSourceFailureKind.OutcomeUnknown,
            "provider_response_lost", CancellationToken.None);

        // An unknown outcome may have left bytes in the bucket, so the allowance stays consumed
        // until retirement reconciles the object; only the reservation is marked for reconciliation.
        var unresolved = await ReadStorageUsageAsync(tenantId, policy.ProviderScope);
        Assert.Equal(remaining, unresolved.Reserved);
        Assert.Equal(published.Retained, unresolved.Retained);
        Assert.Equal(ReservationOutcomeUnknown,
            (await ReadReservationStatesAsync(tenantId, policy.ProviderScope))[unknownLease.ObjectKey]);
        Assert.Equal(CustomerImportSourceState.Orphaned,
            await ReadSourceStateAsync(tenantId, unknownLease.ObjectKey));
        var stillRefused = await PlanOfByteLengthAsync(remaining);
        var consumed = await Assert.ThrowsAsync<CustomerValidationException>(() => store.BeginSourceAsync(
            context, stillRefused, "allowance-unknown-consumed", CustomerImportRetention.DefaultSevenDays,
            policy, CancellationToken.None));
        Assert.Equal("import_storage_capacity", consumed.Code);
        Assert.Equal(unresolved.Reserved, (await ReadStorageUsageAsync(tenantId, policy.ProviderScope)).Reserved);
        Assert.Equal(unresolved.Retained, (await ReadStorageUsageAsync(tenantId, policy.ProviderScope)).Retained);
    }

    private static async Task<CustomerImportPlan> PlanOfByteLengthAsync(long byteLength)
    {
        // "name\n" header, one field, one row terminator.
        var nameLength = (int)byteLength - "name\n".Length - "\n".Length;
        Assert.InRange(nameLength, 1, 4096);
        var content = $"name\n{new string('C', nameLength)}\n";
        return await CustomerImportCsv.PlanAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)),
            Guid.NewGuid(), CancellationToken.None);
    }

    [Fact]
    public async Task NewCustomersTablesAreTenantScopedAndForcedRls()
    {
        await MigrateAsync();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        foreach (var table in new[] { "customer_redirects", "duplicate_cases", "duplicate_command_receipts",
            "imports", "import_rows", "import_work", "object_storage_usage", "import_source_objects",
            "object_storage_reservations" })
        {
            await using var policy = connection.CreateCommand();
            policy.CommandText = "SELECT relrowsecurity AND relforcerowsecurity FROM pg_class WHERE oid = @table_name::regclass";
            policy.Parameters.AddWithValue("table_name", $"customers.{table}");
            Assert.Equal(true, await policy.ExecuteScalarAsync());
        }
    }

    [Fact]
    public async Task RawSourceLifecycleMigrationRollsBackAndReappliesItsOwnedObjects()
    {
        await MigrateAsync();
        await using var database = CustomersPostgresMigrations.CreateContext(ConnectionString);
        await database.Database.MigrateAsync("20261007110000_CustomerForwardCanonicalization");

        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass('customers.import_source_objects'), to_regprocedure('customers.discover_runnable_import_tenants(uuid,integer)')";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(await reader.IsDBNullAsync(0));
            Assert.False(await reader.IsDBNullAsync(1));
            await reader.DisposeAsync();
            command.CommandText = "SELECT pg_get_functiondef('customers.discover_runnable_import_tenants(uuid,integer)'::regprocedure)";
            var rolledBackDefinition = (string?)await command.ExecuteScalarAsync();
            Assert.DoesNotContain("import_source_objects", rolledBackDefinition, StringComparison.Ordinal);
        }

        await database.Database.MigrateAsync();
        await using var verify = new NpgsqlConnection(ConnectionString);
        await verify.OpenAsync();
        await using var verifyCommand = verify.CreateCommand();
        verifyCommand.CommandText = "SELECT to_regclass('customers.import_source_objects'), to_regprocedure('customers.discover_runnable_import_tenants(uuid,integer)')";
        await using var verifyReader = await verifyCommand.ExecuteReaderAsync();
        Assert.True(await verifyReader.ReadAsync());
        Assert.False(await verifyReader.IsDBNullAsync(0));
        Assert.False(await verifyReader.IsDBNullAsync(1));
    }

    [Fact]
    public async Task RawSourceLifecycleDowngradeRefusesNonEmptyAccounting()
    {
        await MigrateAsync();
        var (tenantId, _) = await SeedAsync();
        await FixtureSqlAsync($"""
            INSERT INTO customers.object_storage_usage
                (tenant_id, provider_scope, maximum_bytes, reserved_bytes, retained_bytes, updated_at)
            VALUES ('{tenantId:D}', 'test/downgrade', 100, 0, 1, clock_timestamp())
            """);

        await using var database = CustomersPostgresMigrations.CreateContext(ConnectionString);
        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            database.Database.MigrateAsync("20261007110000_CustomerForwardCanonicalization"));
        var databaseError = Assert.IsType<PostgresException>(rejected.InnerException);
        Assert.Equal("55000", databaseError.SqlState);
        Assert.Equal(1L, await CountAsync("customers.object_storage_usage"));
        Assert.Equal(0L, await CountAsync("customers.import_source_objects"));
    }

    [Fact]
    public async Task RetainedPreExtensionCreateReceiptReplaysButCannotAcquireNewAttributes()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var create = new CreateCustomerIndividual(store);
        var created = await create.ExecuteAsync(context, new("Legacy Person", "legacy@example.test", null), "legacy", CancellationToken.None);
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE customers.individual_command_receipts SET fingerprint=@fingerprint WHERE operation='create' AND idempotency_key='legacy'";
        command.Parameters.AddWithValue("fingerprint", CustomerIdentityNormalization.Fingerprint("individual", "Legacy Person", "legacy@example.test", ""));
        await command.ExecuteNonQueryAsync();
        var replay = await create.ExecuteAsync(context, new("Legacy Person", "legacy@example.test", null), "legacy", CancellationToken.None);
        Assert.Equal(CreateCustomerIndividualStatus.Replayed, replay.Status); Assert.Equal(created.Individual, replay.Individual);
        var changed = await create.ExecuteAsync(context, new("Legacy Person", "legacy@example.test", null, "new-external-id"), "legacy", CancellationToken.None);
        Assert.Equal(CreateCustomerIndividualStatus.IdempotencyKeyConflict, changed.Status);
        Assert.Equal(1L, await CountAsync("customers.individuals"));
    }

    private sealed class ImportAuthority(TenantContext context) : ICustomerImportAuthority
    {
        public bool Allowed { get; set; } = true;
        public long Revision { get; set; } = 1;
        public Action? OnCheck { get; set; }
        public Task<CustomerImportAuthoritySnapshot?> CheckAsync(Guid tenantId, Guid accountId, CancellationToken ct)
        {
            OnCheck?.Invoke();
            return Task.FromResult(Allowed && tenantId == context.TenantId && accountId == context.AccountId
                ? new CustomerImportAuthoritySnapshot(context, Revision) : null);
        }
    }

    [Fact]
    public async Task ConsolidationMovesOnlyCurrentRepresentativesAndFlattensIncomingSuccessors()
    {
        await MigrateAsync();
        var (tenant, actor) = await SeedAsync();
        var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var create = new CreateCustomerIndividual(store);
        var a = (await create.ExecuteAsync(context, new("Canonical", null, null), "a", CancellationToken.None)).Individual!;
        var b = (await create.ExecuteAsync(context, new("Duplicate", null, null), "b", CancellationToken.None)).Individual!;
        var c = (await create.ExecuteAsync(context, new("Third", null, null), "c", CancellationToken.None)).Individual!;
        var org = (await new CreateCustomerOrganization(store).ExecuteAsync(context, new("Org"), "org", CancellationToken.None)).Organization!;
        var linked = await new LinkCustomerRepresentative(store).ExecuteAsync(context, new(org.OrganizationId, null, b.IndividualId), "link", CancellationToken.None);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => new ConsolidateCustomerDuplicate(store)
            .ExecuteAsync(context, new(b.IndividualId, a.IndividualId, 1, 1), "consolidate", CancellationToken.None)));
        Assert.Single(results, result => result.Status == ConsolidateCustomerDuplicateStatus.Consolidated);
        Assert.Equal(3, results.Count(result => result.Status == ConsolidateCustomerDuplicateStatus.Replayed));
        var current = await new GetCustomerRepresentative(store).ExecuteAsync(context, linked.Representative!.RepresentativeId, CancellationToken.None);
        Assert.Equal(a.IndividualId, current!.IndividualId);
        Assert.Equal(a.IndividualId, (await store.ResolveCurrentCustomerAsync(context, b.IndividualId, CancellationToken.None))!.IndividualId);
        var history = await new LinkCustomerRepresentative(store).ExecuteAsync(context, new(org.OrganizationId, null, b.IndividualId), "link", CancellationToken.None);
        Assert.Equal(b.IndividualId, history.Representative!.IndividualId);
        var chain = await new ConsolidateCustomerDuplicate(store).ExecuteAsync(context, new(a.IndividualId, c.IndividualId, 1, 1), "chain", CancellationToken.None);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.Consolidated, chain.Status);
        Assert.Equal(c.IndividualId, (await store.FindIndividualAsync(context, b.IndividualId, CancellationToken.None))!.RedirectTargetIndividualId);
        Assert.Equal(c.IndividualId, (await store.ResolveCurrentCustomerAsync(context, b.IndividualId, CancellationToken.None))!.IndividualId);
        Assert.Equal(c.IndividualId, (await store.FindRepresentativeAsync(context, linked.Representative.RepresentativeId, CancellationToken.None))!.IndividualId);
        Assert.Single(await store.ReadResolutionsAsync(context, b.IndividualId, null, 10, CancellationToken.None));
        Assert.Equal(2L, await CountAsync("customers.customer_redirects"));
    }

    [Fact]
    public async Task ContactRevisionRaceHasOneWinnerAndDuplicateResolutionReadsDoNotCrossTenants()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        var (otherTenant, otherActor) = await SeedAsync(); var otherContext = await ResolveContextAsync(otherTenant, otherActor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var a = (await new CreateCustomerIndividual(store).ExecuteAsync(context, new("First", "shared@example.test", null), "first", CancellationToken.None)).Individual!;
        var b = (await new CreateCustomerIndividual(store).ExecuteAsync(context, new("Second", null, null), "second", CancellationToken.None)).Individual!;
        var editTask = new EditCustomerIndividualContact(store).ExecuteAsync(context, new(a.IndividualId, 1, "Edited", "shared@example.test", null), "edit", CancellationToken.None);
        var consolidateTask = new ConsolidateCustomerDuplicate(store).ExecuteAsync(context, new(a.IndividualId, b.IndividualId, 1, 1), "merge", CancellationToken.None);
        await Task.WhenAll(editTask, consolidateTask);
        var edit = await editTask; var consolidate = await consolidateTask;
        Assert.True((edit.Status == EditCustomerIndividualContactStatus.Changed) != (consolidate.Status == ConsolidateCustomerDuplicateStatus.Consolidated));
        Assert.Equal(2, (await store.FindIndividualAsync(context, a.IndividualId, CancellationToken.None))!.Revision);
        Assert.Empty(await store.ReadResolutionsAsync(otherContext, a.IndividualId, null, 50, CancellationToken.None));
        Assert.Empty((await new FindCustomerDuplicates(store).ExecuteAsync(otherContext,
            new(CustomerDuplicateSignals.Create("First", "shared@example.test")), CancellationToken.None)).Candidates);
        var deniedTarget = await new ConsolidateCustomerDuplicate(store).ExecuteAsync(otherContext,
            new(a.IndividualId, b.IndividualId, 2, 1), "cross-tenant", CancellationToken.None);
        Assert.Equal(ConsolidateCustomerDuplicateStatus.NotFound, deniedTarget.Status);
    }

    [Fact]
    public async Task KeepSeparateEvidenceIsRetainedAndDiscoveryDoesNotDemandRepeatedResolution()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var a = (await new CreateCustomerIndividual(store).ExecuteAsync(context, new("Same Name", null, null), "first", CancellationToken.None)).Individual!;
        var b = (await new CreateCustomerIndividual(store).ExecuteAsync(context, new("Same Name", null, null), "second", CancellationToken.None)).Individual!;
        var resolved = await new ResolveCustomerDuplicate(store).ExecuteAsync(context,
            new(a.IndividualId, b.IndividualId, 1, 1, CustomerDuplicateOutcome.KeepSeparate, "Different people"), "resolve", CancellationToken.None);
        Assert.Equal(ResolveCustomerDuplicateStatus.Resolved, resolved.Status);
        var candidates = await new FindCustomerDuplicates(store).ExecuteAsync(context,
            new(CustomerDuplicateSignals.Create("Same Name", "not-shared@example.test"), a.IndividualId), CancellationToken.None);
        var candidate = Assert.Single(candidates.Candidates);
        Assert.False(candidate.RequiresManualReview); Assert.Equal(resolved.Resolution, candidate.Resolution);
        Assert.Contains("ExactName", candidate.Resolution!.Evidence, StringComparison.Ordinal);
        await new EditCustomerIndividualContact(store).ExecuteAsync(context, new(a.IndividualId, 1, "Changed Name", null, null), "edit", CancellationToken.None);
        Assert.Contains("ExactName", Assert.Single(await store.ReadResolutionsAsync(context, a.IndividualId, null, 50, CancellationToken.None)).Evidence, StringComparison.Ordinal);
        Assert.Equal(2L, await CountAsync("customers.individuals")); Assert.Equal(0L, await CountAsync("customers.customer_redirects"));
    }

    [Fact]
    public async Task ImportRequiresDecisionsForNamesAndAllMappingsBindRealRowsInTenant()
    {
        await MigrateAsync();
        var (tenant, actor) = await SeedAsync();
        var (otherTenant, otherActor) = await SeedAsync();
        var context = await ResolveContextAsync(tenant, actor);
        var otherContext = await ResolveContextAsync(otherTenant, otherActor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nSame Name\nSame Name\n");
        Assert.All(plan.Plan.Rows, row => Assert.True(row.RequiresDecision));
        var accept = new ExecuteCustomerImport(store, new ImportAuthority(context));
        var missing = await Assert.ThrowsAsync<CustomerValidationException>(() => accept.ExecuteAsync(context,
            new(plan.ImportId), "accept", CancellationToken.None));
        Assert.Equal("duplicate_decision_required", missing.Code);
        var unknownMapping = await Assert.ThrowsAsync<CustomerValidationException>(() => accept.ExecuteAsync(context,
            new(plan.ImportId, new Dictionary<int, Guid> { [999] = Guid.NewGuid() }), "accept", CancellationToken.None));
        Assert.Equal("mapping_invalid", unknownMapping.Code);
        var other = (await new CreateCustomerIndividual(store).ExecuteAsync(otherContext, new("Other", null, null), "other", CancellationToken.None)).Individual!;
        var crossTenant = await Assert.ThrowsAsync<CustomerValidationException>(() => accept.ExecuteAsync(context,
            new(plan.ImportId, new Dictionary<int, Guid> { [2] = other.IndividualId }), "accept", CancellationToken.None));
        Assert.Equal("mapping_target_not_found", crossTenant.Code);
        Assert.Null(await store.ReadImportSummaryAsync(otherContext, plan.ImportId, CancellationToken.None));
        var decisions = plan.Plan.Rows.Select(row => new CustomerImportDecision(row.RowNumber, row.SourceRowHash, CustomerImportDecisionKind.CreateNew)).ToArray();
        await accept.ExecuteAsync(context, new(plan.ImportId, null, decisions), "accept", CancellationToken.None);
        await Assert.ThrowsAsync<CustomerValidationException>(() => accept.ExecuteAsync(context,
            new(plan.ImportId, null, [decisions[0] with { Kind = CustomerImportDecisionKind.Reject }, decisions[1]]), "other-key", CancellationToken.None));
        Assert.Equal(1L, await CountAsync("customers.import_work"));
        var runner = new RunCustomerImportBatch(store, new ImportAuthority(context));
        await runner.ExecuteAsync(tenant, Guid.NewGuid(), 1, CancellationToken.None);
        var summary = await store.ReadImportSummaryAsync(context, plan.ImportId, CancellationToken.None);
        Assert.Equal(1, summary!.Imported); Assert.Equal(1, summary.Pending);
        var page = await store.ReadImportRowsPageAsync(context, plan.ImportId, 0, 1, CancellationToken.None);
        Assert.Single(page.Items); Assert.NotNull(page.NextRowNumber);
    }

    [Fact]
    public async Task ImportRestartRecoversExpiredClaimAndFencesOldGeneration()
    {
        await MigrateAsync();
        var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime); var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nFirst\nSecond\n");
        var authority = new ImportAuthority(context);
        await new ExecuteCustomerImport(store, authority).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        var abandoned = await store.ClaimImportAsync(tenant, Guid.NewGuid(), RunCustomerImportBatch.ClaimLease, CancellationToken.None);
        Assert.Equal(CustomerImportRowExecutionStatus.Processed, await store.ProcessNextImportRowAsync(abandoned!, context, CancellationToken.None));
        await FixtureSqlAsync("UPDATE customers.import_work SET lease_expires_at=clock_timestamp()-interval '1 second'");
        await using var restartedSource = NpgsqlDataSource.Create(runtime);
        var restarted = new PostgresCustomerStore(restartedSource);
        var recovered = await restarted.ClaimImportAsync(tenant, Guid.NewGuid(), RunCustomerImportBatch.ClaimLease, CancellationToken.None);
        Assert.True(recovered!.Generation > abandoned!.Generation);
        await Assert.ThrowsAsync<CustomerImportClaimLostException>(() => store.ProcessNextImportRowAsync(abandoned, context, CancellationToken.None));
        Assert.Equal(CustomerImportRowExecutionStatus.Processed, await restarted.ProcessNextImportRowAsync(recovered, context, CancellationToken.None));
        await restarted.ReleaseImportClaimAsync(recovered, false, CancellationToken.None);
        var summary = await restarted.ReadImportSummaryAsync(context, plan.ImportId, CancellationToken.None);
        Assert.Equal(CustomerImportWorkStatus.Completed, summary!.Work!.Status); Assert.Equal(2, summary.Imported);
        Assert.Equal(2L, await CountAsync("customers.individuals"));
    }

    [Fact]
    public async Task InterruptingTheEffectConnectionRollsBackAndRestartImportsTheStableRowExactlyOnce()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime); var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nInterrupted Fixture\n");
        var authority = new ImportAuthority(context);
        await new ExecuteCustomerImport(store, authority).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        var claim = await store.ClaimImportAsync(tenant, Guid.NewGuid(), RunCustomerImportBatch.ClaimLease, CancellationToken.None);
        await using var barrier = new NpgsqlConnection(ConnectionString); await barrier.OpenAsync();
        await using (var hold = barrier.CreateCommand())
        {
            hold.CommandText = "SELECT pg_advisory_lock(814700301)";
            await hold.ExecuteNonQueryAsync();
        }
        await FixtureSqlAsync("""
            CREATE FUNCTION customers.import_interruption_fixture() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(814700301); RETURN NEW; END $$;
            CREATE TRIGGER import_interruption_fixture BEFORE INSERT ON customers.individuals
              FOR EACH ROW EXECUTE FUNCTION customers.import_interruption_fixture();
            """);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var interrupted = store.ProcessNextImportRowAsync(claim!, context, deadline.Token);
        await using var monitor = new NpgsqlConnection(ConnectionString); await monitor.OpenAsync(deadline.Token);
        int pid;
        while (true)
        {
            await using var observe = monitor.CreateCommand();
            observe.CommandText = "SELECT pid FROM pg_stat_activity WHERE usename='application_customers_runtime' AND wait_event='advisory' LIMIT 1";
            var observed = await observe.ExecuteScalarAsync(deadline.Token);
            if (observed is int waiting) { pid = waiting; break; }
            deadline.Token.ThrowIfCancellationRequested(); await Task.Yield();
        }
        await using (var terminate = monitor.CreateCommand())
        {
            terminate.CommandText = "SELECT pg_terminate_backend(@pid)";
            terminate.Parameters.AddWithValue("pid", pid);
            Assert.True(true.Equals(await terminate.ExecuteScalarAsync(deadline.Token)));
        }
        await Assert.ThrowsAnyAsync<NpgsqlException>(() => interrupted);
        await FixtureSqlAsync("""
            DROP TRIGGER import_interruption_fixture ON customers.individuals;
            DROP FUNCTION customers.import_interruption_fixture();
            UPDATE customers.import_work SET lease_expires_at=clock_timestamp()-interval '1 second';
            """);
        Assert.Equal(0L, await CountAsync("customers.individuals"));
        await using var restartedSource = NpgsqlDataSource.Create(runtime);
        var restarted = new PostgresCustomerStore(restartedSource);
        await new RunCustomerImportBatch(restarted, authority).ExecuteAsync(tenant, Guid.NewGuid(), 50, CancellationToken.None);
        var row = Assert.Single((await restarted.ReadImportRowsPageAsync(context, plan.ImportId, 0, 50, CancellationToken.None)).Items);
        Assert.Equal(CustomerImportRowStatus.Imported, row.Status); Assert.Equal(row.RowId, row.CustomerId);
        Assert.Equal(1, row.Attempts); Assert.Equal(1L, await CountAsync("customers.individuals"));
    }

    [Fact]
    public async Task RestartAfterLastRowCommitFinalizesWorkWithoutRepeatingCustomerEffects()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nLast Row Fixture\n"); var authority = new ImportAuthority(context);
        await new ExecuteCustomerImport(store, authority).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        var claim = await store.ClaimImportAsync(tenant, Guid.NewGuid(), RunCustomerImportBatch.ClaimLease, CancellationToken.None);
        await store.ProcessNextImportRowAsync(claim!, context, CancellationToken.None);
        await FixtureSqlAsync("UPDATE customers.import_work SET lease_expires_at=clock_timestamp()-interval '1 second'");
        var recovered = await new RunCustomerImportBatch(store, authority).ExecuteAsync(tenant, Guid.NewGuid(), 50, CancellationToken.None);
        Assert.Equal(CustomerImportBatchStatus.Completed, recovered.Status); Assert.Equal(0, recovered.RowsProcessed);
        Assert.Equal(CustomerImportWorkStatus.Completed, (await store.ReadImportSummaryAsync(context, plan.ImportId, CancellationToken.None))!.Work!.Status);
        Assert.Equal(1L, await CountAsync("customers.individuals"));
    }

    [Fact]
    public async Task NewDuplicateEvidenceBeforeAcceptanceRejectsWithoutChangingTheImmutablePlan()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nNew Conflict\n");
        Assert.False(plan.Plan.Rows[0].RequiresDecision);
        await new CreateCustomerIndividual(store).ExecuteAsync(context, new("New Conflict", null, null), "other", CancellationToken.None);
        var error = await Assert.ThrowsAsync<CustomerValidationException>(() => new ExecuteCustomerImport(store, new ImportAuthority(context))
            .ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None));
        Assert.Equal("plan_outdated_requires_replan", error.Code);
        var retained = Assert.Single((await store.ReadImportRowsPageAsync(context, plan.ImportId, 0, 25, CancellationToken.None)).Items);
        Assert.False(retained.RequiresDecision); Assert.Equal(plan.Plan.Rows[0].SourceRowHash, retained.SourceRowHash);
        Assert.Equal(0L, await CountAsync("customers.import_work"));
    }

    [Fact]
    public async Task DeniedOrOutdatedAuthorityStopsBeforeCustomerEffectsAndGracefulDrainReleasesClaim()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nNo Effect\n");
        var authority = new ImportAuthority(context);
        await new ExecuteCustomerImport(store, authority).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        authority.Revision = 2;
        var runner = new RunCustomerImportBatch(store, authority);
        var outdated = await runner.ExecuteAsync(tenant, Guid.NewGuid(), 50, CancellationToken.None);
        Assert.Equal(CustomerImportBatchStatus.AuthorityDenied, outdated.Status);
        Assert.Equal(0L, await CountAsync("customers.individuals"));
        await new ExecuteCustomerImport(store, authority).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        authority.Allowed = false;
        Assert.Equal(CustomerImportBatchStatus.AuthorityDenied,
            (await runner.ExecuteAsync(tenant, Guid.NewGuid(), 50, CancellationToken.None)).Status);
        Assert.Equal(0L, await CountAsync("customers.individuals"));
        var drained = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.ExecuteAsync(tenant, Guid.NewGuid(), 50, drained));
        Assert.Equal(0L, await CountAsync("customers.import_work WHERE worker_id IS NOT NULL"));
        authority.Allowed = true;
        await new ExecuteCustomerImport(store, authority).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        authority.OnCheck = cancellation.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.ExecuteAsync(tenant, Guid.NewGuid(), 50, cancellation.Token));
        Assert.Equal(0L, await CountAsync("customers.import_work WHERE worker_id IS NOT NULL"));
        Assert.Equal(0L, await CountAsync("customers.individuals"));
    }

    [Fact]
    public async Task FailedRowsRetryWithSameIdentityAndBoundedAttempts()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nRetry Fixture\n");
        var authority = new ImportAuthority(context);
        await new ExecuteCustomerImport(store, authority).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        await FixtureSqlAsync("""
            CREATE FUNCTION customers.import_failure_fixture() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Synthetic row failure' USING ERRCODE='23514'; END $$;
            CREATE TRIGGER import_failure_fixture BEFORE INSERT ON customers.individuals FOR EACH ROW EXECUTE FUNCTION customers.import_failure_fixture();
            """);
        var runner = new RunCustomerImportBatch(store, authority);
        await runner.ExecuteAsync(tenant, Guid.NewGuid(), 50, CancellationToken.None);
        var failed = await store.ReadImportRowsPageAsync(context, plan.ImportId, 0, 50, CancellationToken.None);
        Assert.Equal(CustomerImportRowStatus.Failed, failed.Items[0].Status); Assert.Equal(1, failed.Items[0].Attempts);
        await FixtureSqlAsync("""
            DROP TRIGGER import_failure_fixture ON customers.individuals;
            DROP FUNCTION customers.import_failure_fixture();
            UPDATE customers.import_rows SET processed_at=clock_timestamp()-interval '10 seconds';
            UPDATE customers.import_work SET next_attempt_at=clock_timestamp()-interval '10 seconds';
            """);
        await runner.ExecuteAsync(tenant, Guid.NewGuid(), 50, CancellationToken.None);
        var completed = await store.ReadImportRowsPageAsync(context, plan.ImportId, 0, 50, CancellationToken.None);
        Assert.Equal(CustomerImportRowStatus.Imported, completed.Items[0].Status);
        Assert.Equal(failed.Items[0].RowId, completed.Items[0].CustomerId);
        Assert.Equal(2, completed.Items[0].Attempts); Assert.Equal(1L, await CountAsync("customers.individuals"));
    }

    [Fact]
    public async Task FailedRowAttemptBudgetTerminatesWithoutInventingSuccess()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nExhausted Fixture\n");
        var authority = new ImportAuthority(context);
        await new ExecuteCustomerImport(store, authority).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        await FixtureSqlAsync("""
            CREATE FUNCTION customers.import_failure_fixture() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Synthetic row failure' USING ERRCODE='23514'; END $$;
            CREATE TRIGGER import_failure_fixture BEFORE INSERT ON customers.individuals FOR EACH ROW EXECUTE FUNCTION customers.import_failure_fixture();
            """);
        var runner = new RunCustomerImportBatch(store, authority);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await FixtureSqlAsync("""
                UPDATE customers.import_rows SET processed_at=clock_timestamp()-interval '10 seconds';
                UPDATE customers.import_work SET next_attempt_at=clock_timestamp()-interval '10 seconds';
                """);
            await runner.ExecuteAsync(tenant, Guid.NewGuid(), 50, CancellationToken.None);
        }
        Assert.Equal(CustomerImportBatchStatus.Idle, (await runner.ExecuteAsync(tenant, Guid.NewGuid(), 50, CancellationToken.None)).Status);
        var summary = await store.ReadImportSummaryAsync(context, plan.ImportId, CancellationToken.None);
        Assert.Equal(1, summary!.Failed); Assert.Equal(0, summary.Imported);
        Assert.Equal(CustomerImportWorkStatus.Failed, summary.Work!.Status);
        Assert.Equal(3, (await store.ReadImportRowsPageAsync(context, plan.ImportId, 0, 25, CancellationToken.None)).Items[0].Attempts);
        Assert.Equal(0L, await CountAsync("customers.individuals"));
    }

    private static Task<CreateCustomerImportResult> PlanFixtureAsync(PostgresCustomerStore store, TenantContext context, string csv) =>
        new CreateCustomerImport(store).ExecuteAsync(context, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)),
            Guid.NewGuid(), Guid.NewGuid().ToString("N"), CancellationToken.None);

    [Fact]
    public async Task ImportDiscoveryRequiresExplicitExecuteGrantAndDoesNotWeakenRuntimeRls()
    {
        await MigrateAsync();
        var (tenantA, actorA) = await SeedAsync(); var contextA = await ResolveContextAsync(tenantA, actorA);
        var (tenantB, actorB) = await SeedAsync(); var contextB = await ResolveContextAsync(tenantB, actorB);
        var runtime = await CreateRestrictedRoleAsync();
        await using var source = NpgsqlDataSource.Create(runtime); var store = new PostgresCustomerStore(source);
        foreach (var context in new[] { contextA, contextB })
        {
            var plan = await PlanFixtureAsync(store, context, "name\nDiscovery Fixture\n");
            await new ExecuteCustomerImport(store, new ImportAuthority(context))
                .ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        }
        var denied = await Assert.ThrowsAsync<PostgresException>(() => store.DiscoverRunnableTenantsAsync(null, 1, CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        await GrantImportDiscoveryAsync();
        var first = await store.DiscoverRunnableTenantsAsync(null, 1, CancellationToken.None);
        var second = await store.DiscoverRunnableTenantsAsync(first.NextTenantId, 1, CancellationToken.None);
        Assert.Single(first.TenantIds); Assert.Single(second.TenantIds);
        Assert.NotEqual(first.TenantIds[0], second.TenantIds[0]);
        Assert.Empty((await store.DiscoverRunnableTenantsAsync(second.NextTenantId, 1, CancellationToken.None)).TenantIds);
        Assert.True(new HashSet<Guid> { tenantA, tenantB }.SetEquals(first.TenantIds.Concat(second.TenantIds)));

        await using var connection = new NpgsqlConnection(runtime); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM customers.import_work";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT count(*) FROM customers.import_rows";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT set_config('app.current_tenant', @tenant, false)";
        command.Parameters.AddWithValue("tenant", tenantA.ToString("D"));
        await command.ExecuteScalarAsync();
        command.CommandText = "SELECT count(*) FROM customers.import_work WHERE tenant_id=@other";
        command.Parameters.AddWithValue("other", tenantB);
        Assert.Equal(0L, await command.ExecuteScalarAsync());
        command.CommandText = "UPDATE customers.import_work SET status=3 WHERE tenant_id=@other";
        Assert.Equal(0, await command.ExecuteNonQueryAsync());
        command.CommandText = "SELECT count(*) FROM customers.import_rows WHERE tenant_id=@other";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT count(*) FROM customers.discover_runnable_import_tenants(NULL, 50)";
        Assert.Equal(2L, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT rolbypassrls OR rolsuper FROM pg_roles WHERE rolname=current_user";
        Assert.Equal(false, await command.ExecuteScalarAsync());
        command.CommandText = "SET ROLE postgres";
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
            (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
    }

    [Fact]
    public async Task ImportDiscoveryRejectsUnboundedSqlAndAdapterInputsAndHidesNonRunnableWork()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        var runtime = await CreateRestrictedRoleAsync(); await GrantImportDiscoveryAsync();
        await using var source = NpgsqlDataSource.Create(runtime); var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nDiscovery Status Fixture\n");
        Assert.Empty((await store.DiscoverRunnableTenantsAsync(null, 50, CancellationToken.None)).TenantIds);
        await new ExecuteCustomerImport(store, new ImportAuthority(context)).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        Assert.Single((await store.DiscoverRunnableTenantsAsync(null, 50, CancellationToken.None)).TenantIds);
        var claim = await store.ClaimImportAsync(tenant, Guid.NewGuid(), RunCustomerImportBatch.ClaimLease, CancellationToken.None);
        Assert.Empty((await store.DiscoverRunnableTenantsAsync(null, 50, CancellationToken.None)).TenantIds);
        await FixtureSqlAsync("UPDATE customers.import_work SET lease_expires_at=clock_timestamp()-interval '1 second'");
        Assert.Single((await store.DiscoverRunnableTenantsAsync(null, 50, CancellationToken.None)).TenantIds);
        await store.ReleaseImportClaimAsync(claim!, true, CancellationToken.None);
        Assert.Empty((await store.DiscoverRunnableTenantsAsync(null, 50, CancellationToken.None)).TenantIds);
        await new ExecuteCustomerImport(store, new ImportAuthority(context)).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        await FixtureSqlAsync("UPDATE customers.import_work SET next_attempt_at=clock_timestamp()+interval '1 minute'");
        Assert.Empty((await store.DiscoverRunnableTenantsAsync(null, 50, CancellationToken.None)).TenantIds);
        await FixtureSqlAsync("UPDATE customers.import_work SET next_attempt_at=NULL; UPDATE customers.import_rows SET attempts=3,status=5");
        Assert.Empty((await store.DiscoverRunnableTenantsAsync(null, 50, CancellationToken.None)).TenantIds);

        await using var connection = new NpgsqlConnection(runtime); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        foreach (var limit in new[] { 0, -1, 51, int.MaxValue })
        {
            command.CommandText = "SELECT * FROM customers.discover_runnable_import_tenants(NULL, @limit)";
            command.Parameters.Clear(); command.Parameters.AddWithValue("limit", limit);
            Assert.Equal(PostgresErrorCodes.InvalidParameterValue,
                (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
        command.CommandText = "SELECT * FROM customers.discover_runnable_import_tenants('00000000-0000-0000-0000-000000000000', 1)";
        Assert.Equal(PostgresErrorCodes.InvalidParameterValue,
            (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        var disposedSource = NpgsqlDataSource.Create(runtime); await disposedSource.DisposeAsync();
        var unopenedStore = new PostgresCustomerStore(disposedSource);
        foreach (var limit in new[] { -1, 0, 51, int.MaxValue })
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => unopenedStore.DiscoverRunnableTenantsAsync(null, limit, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => unopenedStore.DiscoverRunnableTenantsAsync(Guid.Empty, 1, CancellationToken.None));
    }

    [Fact]
    public async Task ImportDiscoveryWorksWithNonBypassDefinerAndFixedSearchPathAndNoPublicExecution()
    {
        await MigrateAsync();
        await using (var migrations = CustomersPostgresMigrations.CreateContext(ConnectionString))
            await migrations.Database.MigrateAsync("20261006170321_CustomerImportFencedExecution");
        await FixtureSqlAsync("""
            CREATE ROLE application_import_discovery_migrator NOLOGIN NOSUPERUSER NOBYPASSRLS;
            GRANT USAGE,CREATE ON SCHEMA customers TO application_import_discovery_migrator;
            ALTER TABLE customers.import_work OWNER TO application_import_discovery_migrator;
            ALTER TABLE customers.import_rows OWNER TO application_import_discovery_migrator;
            """);
        await using (var owner = new NpgsqlConnection(ConnectionString))
        {
            await owner.OpenAsync(); await using var command = owner.CreateCommand();
            command.CommandText = "SET ROLE application_import_discovery_migrator"; await command.ExecuteNonQueryAsync();
            foreach (var operation in new Application.Customers.Postgres.Migrations.CustomerImportAutonomousDiscovery().UpOperations)
            {
                command.CommandText = Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>(operation).Sql;
                await command.ExecuteNonQueryAsync();
            }
        }
        var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        var runtime = await CreateRestrictedRoleAsync(); await GrantImportDiscoveryAsync();
        await using var source = NpgsqlDataSource.Create(runtime); var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nRestricted Definer Fixture\n");
        await new ExecuteCustomerImport(store, new ImportAuthority(context)).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        Assert.Equal(tenant, Assert.Single((await store.DiscoverRunnableTenantsAsync(null, 50, CancellationToken.None)).TenantIds));
        await FixtureSqlAsync("""
            CREATE ROLE application_import_discovery_ungranted LOGIN PASSWORD 'local-discovery-negative-test-only';
            GRANT USAGE ON SCHEMA customers TO application_import_discovery_ungranted;
            """);
        await using var ungranted = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ConnectionString)
        { Username = "application_import_discovery_ungranted", Password = "local-discovery-negative-test-only" }.ConnectionString);
        await ungranted.OpenAsync(); await using var denied = ungranted.CreateCommand();
        denied.CommandText = "SELECT * FROM customers.discover_runnable_import_tenants(NULL, 50)";
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
            (await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync())).SqlState);

        await using var runtimeConnection = new NpgsqlConnection(runtime); await runtimeConnection.OpenAsync();
        await using var inspect = runtimeConnection.CreateCommand();
        inspect.CommandText = """
            CREATE TEMP TABLE import_work(tenant_id uuid);
            INSERT INTO import_work VALUES ('00000000-0000-0000-0000-000000000001');
            SELECT tenant_id FROM customers.discover_runnable_import_tenants(NULL, 50);
            """;
        Assert.Equal(tenant, await inspect.ExecuteScalarAsync());
        inspect.CommandText = """
            SELECT p.prosecdef AND NOT r.rolsuper AND NOT r.rolbypassrls
              AND 'search_path=pg_catalog'=ANY(p.proconfig) AND 'row_security=on'=ANY(p.proconfig)
            FROM pg_proc p JOIN pg_roles r ON r.oid=p.proowner
            WHERE p.oid='customers.discover_runnable_import_tenants(uuid,integer)'::regprocedure;
            """;
        Assert.Equal(true, await inspect.ExecuteScalarAsync());
        inspect.CommandText = "SELECT count(*) FROM customers.import_work";
        Assert.Equal(0L, await inspect.ExecuteScalarAsync());
        inspect.CommandText = "SET ROLE application_import_discovery_migrator";
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
            (await Assert.ThrowsAsync<PostgresException>(() => inspect.ExecuteNonQueryAsync())).SqlState);
    }

    private Task GrantImportDiscoveryAsync() => FixtureSqlAsync(
        "GRANT EXECUTE ON FUNCTION customers.discover_runnable_import_tenants(uuid,integer) TO application_customers_runtime");

    [Fact]
    public async Task ActualMembershipSuspensionBetweenBatchesPreventsRemainingEffects()
    {
        await MigrateAsync(); var (tenant, actor) = await SeedAsync(); var context = await ResolveContextAsync(tenant, actor);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await PlanFixtureAsync(store, context, "name\nAllowed First\nDenied Second\n");
        var authority = new MembershipImportAuthority(this, context);
        await new ExecuteCustomerImport(store, authority).ExecuteAsync(context, new(plan.ImportId), "accept", CancellationToken.None);
        var runner = new RunCustomerImportBatch(store, authority);
        await runner.ExecuteAsync(tenant, Guid.NewGuid(), 1, CancellationToken.None);
        await FixtureSqlAsync("UPDATE tenancy.memberships SET availability=2, revision=revision+1, suspended_at=clock_timestamp()");
        var denied = await runner.ExecuteAsync(tenant, Guid.NewGuid(), 50, CancellationToken.None);
        Assert.Equal(CustomerImportBatchStatus.AuthorityDenied, denied.Status);
        Assert.Equal(1L, await CountAsync("customers.individuals"));
        Assert.Equal(1, (await store.ReadImportSummaryAsync(context, plan.ImportId, CancellationToken.None))!.Pending);
    }

    private sealed class MembershipImportAuthority(CustomerPostgresTests fixture, TenantContext context) : ICustomerImportAuthority
    {
        public async Task<CustomerImportAuthoritySnapshot?> CheckAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(fixture.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT EXISTS(SELECT 1 FROM tenancy.memberships m
                  JOIN tenancy.tenants t ON t.id=m.tenant_id JOIN identity_access.accounts a ON a.id=m.account_id
                  WHERE m.tenant_id=@tenant_id AND m.account_id=@account_id AND m.availability=1 AND t.availability=1 AND a.availability=1)
                """;
            command.Parameters.AddWithValue("tenant_id", tenantId); command.Parameters.AddWithValue("account_id", accountId);
            return true.Equals(await command.ExecuteScalarAsync(cancellationToken)) ? new(context, 1) : null;
        }
    }

    private async Task FixtureSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<(long Reserved, long Retained)> ReadStorageUsageAsync(Guid tenantId, string providerScope)
    {
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT reserved_bytes, retained_bytes FROM customers.object_storage_usage WHERE tenant_id=@tenant AND provider_scope=@provider";
        command.Parameters.AddWithValue("tenant", tenantId); command.Parameters.AddWithValue("provider", providerScope);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private async Task<IReadOnlyDictionary<string, int>> ReadReservationStatesAsync(Guid tenantId, string providerScope)
    {
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT object_key, state FROM customers.object_storage_reservations WHERE tenant_id=@tenant AND provider_scope=@provider ORDER BY object_key";
        command.Parameters.AddWithValue("tenant", tenantId); command.Parameters.AddWithValue("provider", providerScope);
        var states = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) states.Add(reader.GetString(0), reader.GetInt32(1));
        return states;
    }

    private async Task<CustomerImportSourceState> ReadSourceStateAsync(Guid tenantId, string objectKey)
    {
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT state FROM customers.import_source_objects WHERE tenant_id=@tenant AND object_key=@key";
        command.Parameters.AddWithValue("tenant", tenantId); command.Parameters.AddWithValue("key", objectKey);
        return (CustomerImportSourceState)(int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The import source row is missing."));
    }
}
