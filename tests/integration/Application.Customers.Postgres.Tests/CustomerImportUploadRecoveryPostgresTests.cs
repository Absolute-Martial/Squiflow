using System.Text;
using Application.Customers;
using Application.ObjectStorage;
using Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Application.Customers.Postgres.Tests;

public sealed partial class CustomerPostgresTests
{
    [Fact]
    public async Task UploadRetriesRecoverTheOriginalIdentityAndDifferentCallersCanUseTheSameKey()
    {
        await MigrateAsync();
        var (tenant, account) = await SeedAsync();
        var (_, otherAccount) = await SeedAsync();
        await FixtureSqlAsync($"""
            INSERT INTO tenancy.memberships(tenant_id,account_id,availability,created_at,revision,activated_at)
            VALUES ('{tenant:D}','{otherAccount:D}',1,clock_timestamp(),1,clock_timestamp());
            """);
        var context = await ResolveContextAsync(tenant, account);
        var other = await ResolveContextAsync(tenant, otherAccount);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var policy = CustomerImportSourcePolicy.Create("test/recovery", 1_000_000);
        var plan = await RecoveryPlanAsync();
        var lease = await store.BeginSourceAsync(context, plan, "shared-key", CustomerImportRetention.DefaultSevenDays, policy, default);
        var busy = await Assert.ThrowsAsync<CustomerValidationException>(() => store.BeginSourceAsync(context,
            plan with { ImportId = Guid.NewGuid() }, "shared-key", CustomerImportRetention.DefaultSevenDays, policy, default));
        Assert.Equal("import_source_busy", busy.Code);
        await store.MarkSourceFailureAsync(context, lease, CustomerImportSourceFailureKind.OutcomeUnknown, "response_lost", default);
        var retryPlan = plan with { ImportId = Guid.NewGuid() };
        var retry = await store.BeginSourceAsync(context, retryPlan, "shared-key", CustomerImportRetention.DefaultSevenDays, policy, default);
        Assert.Equal(lease.ImportId, retry.ImportId);
        Assert.Equal(lease.ObjectKey, retry.ObjectKey);
        Assert.True(retry.Generation > lease.Generation);
        var otherLease = await store.BeginSourceAsync(other, retryPlan, "shared-key", CustomerImportRetention.DefaultSevenDays, policy, default);
        Assert.NotEqual(retry.ImportId, otherLease.ImportId);
        Assert.NotEqual(retry.ObjectKey, otherLease.ObjectKey);
        Assert.Equal(plan.ByteLength * 2, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Reserved);
        await store.CompleteSourceAsync(context, plan, "shared-key", CustomerImportRetention.DefaultSevenDays, policy, retry, default);
        await store.CompleteSourceAsync(other, retryPlan, "shared-key", CustomerImportRetention.DefaultSevenDays, policy, otherLease, default);
        Assert.Equal(2L, await CountAsync("customers.imports"));
        Assert.Equal(0, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Reserved);
        Assert.Equal(plan.ByteLength * 2, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Retained);
    }

    [Fact]
    public async Task ExpiredUploadOwnershipCannotCompleteOrReleaseASuccessorsAllowance()
    {
        await MigrateAsync();
        var (tenant, account) = await SeedAsync(); var context = await ResolveContextAsync(tenant, account);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await RecoveryPlanAsync(); var policy = CustomerImportSourcePolicy.Create("test/recovery", 1_000_000);
        var old = await store.BeginSourceAsync(context, plan, "recover-lease", CustomerImportRetention.DefaultSevenDays, policy, default);
        await FixtureSqlAsync("UPDATE customers.object_storage_reservations SET upload_lease_expires_at=clock_timestamp()-interval '1 second'");
        var current = await store.BeginSourceAsync(context, plan with { ImportId = Guid.NewGuid() }, "recover-lease", CustomerImportRetention.DefaultSevenDays, policy, default);
        await store.MarkSourceFailureAsync(context, old, CustomerImportSourceFailureKind.ProvenAbsent, "late_failure", default);
        Assert.Equal(plan.ByteLength, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Reserved);
        await Assert.ThrowsAsync<CustomerValidationException>(() => store.CompleteSourceAsync(context, plan, "recover-lease",
            CustomerImportRetention.DefaultSevenDays, policy, old, default));
        var created = await store.CompleteSourceAsync(context, plan, "recover-lease", CustomerImportRetention.DefaultSevenDays, policy, current, default);
        Assert.True(created.Created);
        await store.MarkSourceFailureAsync(context, old, CustomerImportSourceFailureKind.OutcomeUnknown, "late_failure", default);
        Assert.Equal(CustomerImportSourceState.Available, (await store.ReadSourceAsync(context, plan.ImportId, default))!.State);
        Assert.Equal(plan.ByteLength, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Retained);
        var replay = await store.CompleteSourceAsync(context, plan, "recover-lease", CustomerImportRetention.DefaultSevenDays, policy, current, default);
        Assert.True(replay.Replayed);
        Assert.Equal(plan.ByteLength, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Retained);
    }

    [Fact]
    public async Task AProvenAbsentReservationMustReacquireAllowanceBeforeRetrying()
    {
        await MigrateAsync(); var (tenant, account) = await SeedAsync(); var context = await ResolveContextAsync(tenant, account);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await RecoveryPlanAsync(); var policy = CustomerImportSourcePolicy.Create("test/recovery", plan.ByteLength);
        var lease = await store.BeginSourceAsync(context, plan, "absent", CustomerImportRetention.DefaultSevenDays, policy, default);
        await store.MarkSourceFailureAsync(context, lease, CustomerImportSourceFailureKind.ProvenAbsent, "proven_absent", default);
        Assert.Equal(0, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Reserved);
        var competingPlan = plan with { ImportId = Guid.NewGuid() };
        var competing = await store.BeginSourceAsync(context, competingPlan, "competitor", CustomerImportRetention.DefaultSevenDays, policy, default);
        Assert.Equal("import_storage_capacity", (await Assert.ThrowsAsync<CustomerValidationException>(() => store.BeginSourceAsync(context,
            plan, "absent", CustomerImportRetention.DefaultSevenDays, policy, default))).Code);
        await store.MarkSourceFailureAsync(context, competing, CustomerImportSourceFailureKind.ProvenAbsent, "proven_absent", default);
        var retry = await store.BeginSourceAsync(context, plan with { ImportId = Guid.NewGuid() }, "absent", CustomerImportRetention.DefaultSevenDays, policy, default);
        Assert.Equal(lease.ObjectKey, retry.ObjectKey);
        Assert.Equal(plan.ByteLength, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Reserved);
        await store.CompleteSourceAsync(context, plan, "absent", CustomerImportRetention.DefaultSevenDays, policy, retry, default);
        Assert.Equal(plan.ByteLength, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Retained);
    }

    [Fact]
    public async Task SourcePublicationRollsBackRowsReceiptReferenceAndAllowanceTogether()
    {
        await MigrateAsync(); var (tenant, account) = await SeedAsync(); var context = await ResolveContextAsync(tenant, account);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await RecoveryPlanAsync(); var policy = CustomerImportSourcePolicy.Create("test/recovery", 1_000_000);
        var lease = await store.BeginSourceAsync(context, plan, "atomic-source", CustomerImportRetention.DefaultSevenDays, policy, default);
        await FixtureSqlAsync("""
            CREATE FUNCTION customers.fail_source_link() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Injected publication failure' USING ERRCODE='55000'; END $$;
            CREATE TRIGGER fail_source_link BEFORE UPDATE OF source_object_key ON customers.imports
              FOR EACH ROW EXECUTE FUNCTION customers.fail_source_link();
            """);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => store.CompleteSourceAsync(context, plan, "atomic-source",
            CustomerImportRetention.DefaultSevenDays, policy, lease, default));
        Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, failure.SqlState);
        Assert.Equal(0L, await CountAsync("customers.imports")); Assert.Equal(0L, await CountAsync("customers.import_rows"));
        Assert.Equal(CustomerImportSourceState.Staged, await ReadSourceStateAsync(tenant, lease.ObjectKey));
        var usage = await ReadStorageUsageAsync(tenant, policy.ProviderScope);
        Assert.Equal(plan.ByteLength, usage.Reserved); Assert.Equal(0, usage.Retained);
        await FixtureSqlAsync("DROP TRIGGER fail_source_link ON customers.imports; DROP FUNCTION customers.fail_source_link()");
        var result = await store.CompleteSourceAsync(context, plan, "atomic-source", CustomerImportRetention.DefaultSevenDays, policy, lease, default);
        Assert.True(result.Created); Assert.Equal(plan.Rows.Count, await CountAsync("customers.import_rows"));
        Assert.Equal(lease.ObjectKey, (await store.ReadSourceAsync(context, result.ImportId, default))!.ObjectKey);
        Assert.True((await store.CompleteSourceAsync(context, plan, "atomic-source", CustomerImportRetention.DefaultSevenDays, policy, lease, default)).Replayed);
        Assert.Equal(plan.ByteLength, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Retained);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnacceptedArchiveAndDefaultStagingAreDiscoveredAndOnlyReleaseAllowanceAfterVerifiedRetirement(bool archive)
    {
        await MigrateAsync(); var (tenant, account) = await SeedAsync(); var context = await ResolveContextAsync(tenant, account);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await RecoveryPlanAsync(); var policy = CustomerImportSourcePolicy.Create("test/recovery", 1_000_000);
        var retention = archive ? CustomerImportRetention.TenantArchived : CustomerImportRetention.DefaultSevenDays;
        var lease = await store.BeginSourceAsync(context, plan, "abandoned-source", retention, policy, default);
        await store.MarkSourceFailureAsync(context, lease, CustomerImportSourceFailureKind.OutcomeUnknown, "put_timeout", default);
        await FixtureSqlAsync("""
            UPDATE customers.import_source_objects SET created_at=clock_timestamp()-interval '8 days',
              staging_expires_at=clock_timestamp()-interval '2 days';
            """);
        await GrantImportDiscoveryAsync();
        Assert.Contains(tenant, (await store.DiscoverRunnableTenantsAsync(null, 50, default)).TenantIds);
        Assert.Equal(0L, await CountAsync("customers.import_work"));
        var markerStore = new RecoveryMarkerStore();
        var recovery = new ReconcileCustomerImportSource(store, markerStore, policy);
        Assert.Equal(CustomerImportSourceRecoveryOutcome.Deferred, await recovery.ReconcileOneExpiredAsync(tenant, DateTimeOffset.UtcNow, default));
        Assert.Equal(plan.ByteLength, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Reserved);
        markerStore.Verified = true;
        Assert.Equal(CustomerImportSourceRecoveryOutcome.Retired, await recovery.ReconcileOneExpiredAsync(tenant, DateTimeOffset.UtcNow, default));
        Assert.Equal(CustomerImportSourceState.Retired, await ReadSourceStateAsync(tenant, lease.ObjectKey));
        Assert.Equal(0, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Reserved);
        Assert.Equal(ReservationReleased, (await ReadReservationStatesAsync(tenant, policy.ProviderScope))[lease.ObjectKey]);
        Assert.Equal(CustomerImportSourceRecoveryOutcome.NoWork, await recovery.ReconcileOneExpiredAsync(tenant, DateTimeOffset.UtcNow, default));
        await store.MarkSourceFailureAsync(context, lease, CustomerImportSourceFailureKind.OutcomeUnknown, "late_failure", default);
        Assert.Equal(CustomerImportSourceState.Retired, await ReadSourceStateAsync(tenant, lease.ObjectKey));
        await Assert.ThrowsAsync<CustomerValidationException>(() => store.BeginSourceAsync(context, plan, "abandoned-source", retention, policy, default));
    }

    [Fact]
    public async Task AcceptedArchivesKeepIndefiniteRetentionAndRecoveryCannotUseAnotherProviderScope()
    {
        await MigrateAsync(); var (tenant, account) = await SeedAsync(); var context = await ResolveContextAsync(tenant, account);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var plan = await RecoveryPlanAsync(); var policy = CustomerImportSourcePolicy.Create("test/recovery", 1_000_000);
        var lease = await store.BeginSourceAsync(context, plan, "archive", CustomerImportRetention.TenantArchived, policy, default);
        await store.CompleteSourceAsync(context, plan, "archive", CustomerImportRetention.TenantArchived, policy, lease, default);
        Assert.Null(await store.ClaimExpiredSourceRetirementAsync(tenant, DateTimeOffset.UtcNow.AddYears(100), default));
        Assert.Null((await store.ReadSourceAsync(context, plan.ImportId, default))!.ExpiresAt);
        var staging = plan with { ImportId = Guid.NewGuid() };
        await store.BeginSourceAsync(context, staging, "unaccepted-archive", CustomerImportRetention.TenantArchived, policy, default);
        var markers = new RecoveryMarkerStore { Verified = true };
        var wrongScope = new ReconcileCustomerImportSource(store, markers, CustomerImportSourcePolicy.Create("other/provider", 1_000_000));
        Assert.Equal(CustomerImportSourceRecoveryOutcome.Deferred, await wrongScope.ReconcileOneExpiredAsync(tenant, DateTimeOffset.UtcNow.AddDays(8), default));
        Assert.Equal(0, markers.Calls);
        Assert.Equal(staging.ByteLength, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Reserved);
        Assert.Equal(plan.ByteLength, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Retained);
    }

    [Fact]
    public async Task ImplicitIdentityRetryPublishesTheOriginalUploadAfterAnUnknownPut()
    {
        await MigrateAsync(); var (tenant, account) = await SeedAsync(); var context = await ResolveContextAsync(tenant, account);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var policy = CustomerImportSourcePolicy.Create("test/recovery", 1_000_000);
        var objects = new RetryUploadStore(); var command = new CreateCustomerImport(store, store, objects, policy);
        static Stream Csv() => new MemoryStream(Encoding.UTF8.GetBytes("name\nRetry With Implicit Identity\n"));
        using (var csv = Csv())
            await Assert.ThrowsAsync<CustomerValidationException>(() => command.ExecuteAsync(context, csv, null, "implicit-retry", default));
        using var retryCsv = Csv();
        var result = await command.ExecuteAsync(context, retryCsv, null, "implicit-retry", default);
        Assert.True(result.Created); Assert.Equal(2, objects.Keys.Count); Assert.Equal(objects.Keys[0], objects.Keys[1]);
        Assert.Contains(result.ImportId.ToString("N"), objects.Keys[0], StringComparison.Ordinal);
        Assert.Equal(objects.Keys[0], (await store.ReadSourceAsync(context, result.ImportId, default))!.ObjectKey);
        Assert.Equal(1L, await CountAsync("customers.imports"));
        Assert.Equal(result.Plan.ByteLength, (await ReadStorageUsageAsync(tenant, policy.ProviderScope)).Retained);
    }

    private sealed class RetryUploadStore : IObjectStore
    {
        internal List<string> Keys { get; } = [];
        public Task<ObjectStorePutResult> PutAsync(ObjectStorePutRequest request, CancellationToken cancellationToken)
        {
            Keys.Add(request.Key.Value);
            if (!OperatingSystem.IsWindows())
            {
                var staged = Assert.IsType<FileStream>(request.Content);
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(staged.Name));
            }
            return Task.FromResult(Keys.Count == 1 ? new ObjectStorePutResult(ObjectStorePutOutcome.TimedOut)
                : new(ObjectStorePutOutcome.Created, new(request.Key, request.ExpectedLength, request.ExpectedSha256, request.ContentType)));
        }
        public Task<ObjectStoreReadResult> OpenReadAsync(ObjectStoreReadRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ObjectStoreMetadataResult> GetMetadataAsync(ObjectStoreKey key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ObjectStoreDeleteResult> DeleteAsync(ObjectStoreKey key, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static Task<CustomerImportPlan> RecoveryPlanAsync() => CustomerImportCsv.PlanAsync(
        new MemoryStream(Encoding.UTF8.GetBytes("name\nRecovery One\nRecovery Two\n")), Guid.NewGuid(), default);

    private sealed class RecoveryMarkerStore : IObjectStore
    {
        internal bool Verified;
        internal int Calls;
        public Task<ObjectStorePutResult> RetireAsync(ObjectStoreKey key, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Verified ? new ObjectStorePutResult(ObjectStorePutOutcome.Created,
                new(key, 0, ObjectStoreRetirement.EmptySha256, ObjectStoreRetirement.ContentType)) : new(ObjectStorePutOutcome.OutcomeUnknown));
        }
        public Task<ObjectStorePutResult> PutAsync(ObjectStorePutRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ObjectStoreReadResult> OpenReadAsync(ObjectStoreReadRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ObjectStoreMetadataResult> GetMetadataAsync(ObjectStoreKey key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ObjectStoreDeleteResult> DeleteAsync(ObjectStoreKey key, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
