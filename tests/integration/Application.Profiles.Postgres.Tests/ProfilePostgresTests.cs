using Application.IdentityAccess.Postgres;
using Application.PlatformAdministration;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.Profiles.Postgres.Tests;

public sealed class ProfilePostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
        .WithDatabase("application_profiles_tests").WithUsername("postgres").WithPassword("local-integration-test-only").Build();
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid foreignTenantId = Guid.NewGuid();
    private readonly Guid accountId = Guid.NewGuid();
    private readonly PlatformAdminAccess administrator = new(Guid.NewGuid(), Guid.NewGuid());
    private NpgsqlDataSource tenantSource = null!;
    private NpgsqlDataSource platformSource = null!;
    private static readonly string[] OwnedTables = ["policy_heads", "policy_revisions", "publications", "authority", "command_receipts"];
    private PostgresProfileStore TenantStore => new(tenantSource, new FixedClock());
    private PostgresProfileStore PlatformStore => new(platformSource, new FixedClock());
    private async Task<TenantContext> ContextAsync(Guid? tenant = null) =>
        (await new ResolveTenantContext(new ActiveMembership()).ExecuteAsync(accountId, tenant ?? tenantId, default))!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await using (var identity = IdentityAccessPostgresMigrations.CreateContext(database.GetConnectionString())) await identity.Database.MigrateAsync();
        await using (var tenancy = TenancyPostgresMigrations.CreateContext(database.GetConnectionString())) await tenancy.Database.MigrateAsync();
        await using (var profiles = ProfilesPostgresRegistration.CreateContext(database.GetConnectionString())) await profiles.Database.MigrateAsync();
        await OwnerSqlAsync("INSERT INTO identity_access.accounts(id,availability,created_at) VALUES(@actor,1,'2026-10-08T12:00:00Z'); INSERT INTO tenancy.tenants(id,display_name,availability,created_at) VALUES(@tenant,'Profile tenant',1,'2026-10-08T12:00:00Z'),(@foreign,'Foreign profile tenant',1,'2026-10-08T12:00:00Z')",
            ("actor", accountId), ("tenant", tenantId), ("foreign", foreignTenantId));
        await OwnerSqlAsync("""
            CREATE ROLE profile_tenant_runtime LOGIN PASSWORD 'local-runtime-test-only';
            CREATE ROLE profile_platform_runtime LOGIN PASSWORD 'local-runtime-test-only';
            GRANT CONNECT ON DATABASE application_profiles_tests TO profile_tenant_runtime,profile_platform_runtime;
            GRANT USAGE ON SCHEMA profiles TO profile_tenant_runtime,profile_platform_runtime;
            GRANT SELECT ON ALL TABLES IN SCHEMA profiles TO profile_tenant_runtime,profile_platform_runtime;
            GRANT INSERT ON profiles.policy_heads,profiles.policy_revisions,profiles.command_receipts TO profile_tenant_runtime;
            GRANT UPDATE(revision,require_reference,published_policy_id) ON profiles.policy_heads TO profile_tenant_runtime;
            GRANT INSERT ON profiles.publications,profiles.authority,profiles.command_receipts TO profile_platform_runtime;
            GRANT UPDATE(revision,active_profile_id,legacy_baseline_profile_id) ON profiles.authority TO profile_platform_runtime;
            """);
        tenantSource = Source("profile_tenant_runtime"); platformSource = Source("profile_platform_runtime");
    }
    public async Task DisposeAsync()
    {
        if (tenantSource is not null) await tenantSource.DisposeAsync();
        if (platformSource is not null) await platformSource.DisposeAsync();
        await database.DisposeAsync();
    }
    [Fact]
    public async Task DraftPolicyPublicationIsExplicitAndOldPolicyAndReceiptsSurviveLaterEdits()
    {
        var context = await ContextAsync(); var store = TenantStore;
        Assert.Null(await store.GetPolicyAsync(context, default));
        var draft = await store.EditPolicyAsync(context, new(0, false), 1, "first", default);
        Assert.Equal(ProfileCommandStatus.Edited, draft.Status); Assert.Equal(1, draft.PolicyState!.Revision);
        var published = await store.PublishPolicyAsync(context, new(1), 1, "publish", default);
        Assert.Equal(ProfileCommandStatus.PolicyPublished, published.Status); Assert.Equal(2, published.PublishedPolicy!.Revision);
        await store.EditPolicyAsync(context, new(2, true), 2, "next", default);
        Assert.False((await store.GetPublishedPolicyAsync(context, published.PublishedPolicy.PolicyRevisionId, default))!.RequireReferenceForProgramOrders);
        Assert.True((await store.GetPolicyAsync(context, default))!.RequireReferenceForProgramOrders);
        var replay = await new PostgresProfileStore(tenantSource, new FixedClock()).PublishPolicyAsync(context, new(1), 3, "publish", default);
        Assert.Equal(ProfileCommandStatus.Replayed, replay.Status); Assert.Equivalent(published.PublishedPolicy, replay.PublishedPolicy);
        Assert.Equal(ProfileCommandStatus.IdempotencyKeyConflict, (await store.EditPolicyAsync(context, new(0, true), 1, "first", default)).Status);
        Assert.Equal(ProfileCommandStatus.RevisionConflict, (await store.EditPolicyAsync(context, new(1, true), 1, "stale", default)).Status);
    }
    [Fact]
    public async Task SameCallerKeyConvergesAndIndependentCompareAndSwapCommandsHaveOneWinner()
    {
        var context = await ContextAsync(); var store = TenantStore;
        var same = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => store.EditPolicyAsync(context, new(0, false), 1, "same", default)));
        Assert.Single(same, result => result.Status == ProfileCommandStatus.Edited);
        Assert.Equal(5, same.Count(result => result.Status == ProfileCommandStatus.Replayed));
        Assert.Equal(1, await CountAsync("policy_heads")); Assert.Equal(1, await CountAsync("command_receipts"));
        var race = await Task.WhenAll(store.EditPolicyAsync(context, new(1, true), 1, "edit-race", default),
            store.PublishPolicyAsync(context, new(1), 1, "publish-race", default));
        Assert.Single(race, result => result.Status == ProfileCommandStatus.RevisionConflict);
        Assert.Single(race, result => result.Status is ProfileCommandStatus.Edited or ProfileCommandStatus.PolicyPublished);
        Assert.Equal(2, (await store.GetPolicyAsync(context, default))!.Revision);
    }
    [Fact]
    public async Task PlatformPublicationActivationAndRollbackRetainExactCatalogAndPolicy()
    {
        var optional = await PublishAsync(false, "optional");
        Assert.Equal(CommercialFeatureCatalog.Catalog.Fingerprint, optional.Profile!.CatalogFingerprint);
        Assert.Equal(CommercialFeatureCatalog.Catalog.Compile([]).SelectionFingerprint, optional.Profile.SelectionFingerprint);
        Assert.Equal(4, optional.Profile.FeatureIds.Count);
        var activated = await PlatformStore.ActivateProfileAsync(administrator, new(tenantId, optional.Authority!.Revision, optional.Profile.ProfileId), "activate", default);
        Assert.Equal(ProfileCommandStatus.Activated, activated.Status);
        var required = await PublishAsync(true, "required");
        Assert.Equal(optional.Profile.ProfileId, required.Authority!.ActiveProfileId);
        await PlatformStore.ActivateProfileAsync(administrator, new(tenantId, required.Authority.Revision, required.Profile!.ProfileId), "activate-required", default);
        var replay = await PlatformStore.ActivateProfileAsync(administrator, new(tenantId, optional.Authority.Revision, optional.Profile.ProfileId), "activate", default);
        Assert.Equal(ProfileCommandStatus.Replayed, replay.Status); Assert.Equivalent(activated.Authority, replay.Authority);
        var authority = (await PlatformStore.GetAuthorityAsync(tenantId, default))!;
        var rollback = await PlatformStore.ActivateProfileAsync(administrator, new(tenantId, authority.Revision, optional.Profile.ProfileId), "rollback", default);
        Assert.Equal(ProfileCommandStatus.Activated, rollback.Status);
        Assert.Equivalent(optional.Profile, await PlatformStore.GetProfileAsync(tenantId, optional.Profile.ProfileId, default));
        Assert.Equal(ProfileCommandStatus.IdempotencyKeyConflict, (await PlatformStore.ActivateProfileAsync(administrator,
            new(tenantId, optional.Authority.Revision, required.Profile.ProfileId), "activate", default)).Status);
        Assert.Equal(ProfileCommandStatus.IdempotencyKeyConflict, (await PlatformStore.ActivateProfileAsync(administrator with { DeviceId = Guid.NewGuid() },
            new(tenantId, optional.Authority.Revision, optional.Profile.ProfileId), "activate", default)).Status);
    }
    [Fact]
    public async Task ProfilePublicationReplayRetainsItsOriginalAuthorizationObservationAfterCurrentRevisionChanges()
    {
        var published = await PublishAsync(false, "observed");
        var replay = await PlatformStore.PublishProfileAsync(administrator,
            new(tenantId, 0, published.PublishedPolicy!.PolicyRevisionId, 2), "profile-observed", default);
        Assert.Equal(ProfileCommandStatus.Replayed, replay.Status);
        Assert.Equal(1, replay.Profile!.ObservedAuthorizationRevision);
        Assert.Equivalent(published.Profile, replay.Profile);
        Assert.Equal(1, await CountAsync("publications"));
        Assert.Equal(published.Authority!.Revision, (await PlatformStore.GetAuthorityAsync(tenantId, default))!.Revision);
        Assert.Equal(ProfileCommandStatus.IdempotencyKeyConflict, (await PlatformStore.PublishProfileAsync(administrator,
            new(tenantId, 0, published.PublishedPolicy.PolicyRevisionId, 2, true), "profile-observed", default)).Status);
    }
    [Fact]
    public async Task ProfilePublicationAndActivationRaceCannotOverwriteAuthorityWithStaleVersion()
    {
        var first = await PublishAsync(false, "first");
        var request = new PublishTenantProfileRequest(tenantId, first.Authority!.Revision, first.PublishedPolicy!.PolicyRevisionId, 1);
        var same = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => PlatformStore.PublishProfileAsync(administrator, request, "same-profile", default)));
        Assert.Single(same, result => result.Status == ProfileCommandStatus.ProfilePublished);
        Assert.Equal(5, same.Count(result => result.Status == ProfileCommandStatus.Replayed));
        Assert.Single(same.Select(result => result.Profile!.ProfileId).Distinct());
        var current = same[0];
        var race = await Task.WhenAll(PlatformStore.ActivateProfileAsync(administrator, new(tenantId, current.Authority!.Revision, first.Profile!.ProfileId), "activate-first", default),
            PlatformStore.ActivateProfileAsync(administrator, new(tenantId, current.Authority.Revision, current.Profile!.ProfileId), "activate-second", default));
        Assert.Single(race, result => result.Status == ProfileCommandStatus.Activated); Assert.Single(race, result => result.Status == ProfileCommandStatus.RevisionConflict);
    }
    [Fact]
    public async Task MissingActiveAndLegacyAuthorityNeverBecomeOptionalAndReadsDoNotCreateRows()
    {
        await using var connection = await tenantSource.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        Assert.Null(await PostgresProfileStore.ResolveActiveForOrderAsync(connection, transaction, tenantId, default));
        Assert.Null(await PostgresProfileStore.ResolveLegacyBaselineForOrderAsync(connection, transaction, tenantId, default));
        Assert.Null(await PostgresProfileStore.ResolveRetainedForOrderAsync(connection, transaction, tenantId, Guid.NewGuid(), default));
        Assert.Equal(connection, transaction.Connection); await transaction.CommitAsync();
        foreach (var table in OwnedTables) Assert.Equal(0, await CountAsync(table));
    }
    [Fact]
    public async Task ExplicitLegacySelectionUsesOptionalPolicyAndNeverChangesActiveOrAllowsReplacement()
    {
        var required = await PublishAsync(true, "required");
        await PlatformStore.ActivateProfileAsync(administrator, new(tenantId, required.Authority!.Revision, required.Profile!.ProfileId), "activate-required", default);
        var optional = await PublishAsync(false, "legacy", true);
        var selected = await PlatformStore.ActivateProfileAsync(administrator, new(tenantId, optional.Authority!.Revision, optional.Profile!.ProfileId, true), "select-baseline", default);
        Assert.Equal(ProfileCommandStatus.LegacyBaselineSelected, selected.Status); Assert.Equal(required.Profile.ProfileId, selected.Authority!.ActiveProfileId);
        Assert.Equal(optional.Profile.ProfileId, selected.Authority.LegacyBaselineProfileId);
        Assert.Equal(ProfileCommandStatus.BaselineAlreadySelected, (await PlatformStore.ActivateProfileAsync(administrator,
            new(tenantId, selected.Authority.Revision, optional.Profile.ProfileId, true), "replace-baseline", default)).Status);
        var bad = await PlatformStore.PublishProfileAsync(administrator, new(tenantId, selected.Authority.Revision, required.PublishedPolicy!.PolicyRevisionId, 1, true), "invalid-baseline", default);
        Assert.Equal(ProfileCommandStatus.InvalidBaseline, bad.Status);
        await using var connection = await tenantSource.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        Assert.True((await PostgresProfileStore.ResolveActiveForOrderAsync(connection, transaction, tenantId, default))!.Policy.RequireReferenceForProgramOrders);
        Assert.False((await PostgresProfileStore.ResolveLegacyBaselineForOrderAsync(connection, transaction, tenantId, default))!.Policy.RequireReferenceForProgramOrders);
    }
    [Fact]
    public async Task TenantIsolationCoversPhysicalRowsAndCrossTenantPolicyAndProfileReferences()
    {
        var profile = await PublishAsync(false, "owned"); var foreign = await ContextAsync(foreignTenantId);
        Assert.Null(await TenantStore.GetPolicyAsync(foreign, default));
        Assert.Null(await TenantStore.GetPublishedPolicyAsync(foreign, profile.PublishedPolicy!.PolicyRevisionId, default));
        Assert.Null(await PlatformStore.GetProfileAsync(foreignTenantId, profile.Profile!.ProfileId, default));
        Assert.Equal(ProfileCommandStatus.NotFound, (await PlatformStore.PublishProfileAsync(administrator,
            new(foreignTenantId, 0, profile.PublishedPolicy.PolicyRevisionId, 1), "cross-policy", default)).Status);
        Assert.Equal(ProfileCommandStatus.NotFound, (await PlatformStore.ActivateProfileAsync(administrator,
            new(foreignTenantId, 0, profile.Profile.ProfileId), "cross-profile", default)).Status);
        await using var connection = await tenantSource.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        await using (var tenant = new NpgsqlCommand("SELECT set_config('app.current_tenant',@tenant,true)", connection, transaction))
        { tenant.Parameters.AddWithValue("tenant", foreignTenantId.ToString("D")); await tenant.ExecuteNonQueryAsync(); }
        await using (var rows = new NpgsqlCommand("SELECT count(*) FROM profiles.policy_revisions", connection, transaction)) Assert.Equal(0L, await rows.ExecuteScalarAsync());
        await using var insert = new NpgsqlCommand("INSERT INTO profiles.policy_heads(tenant_id,revision,require_reference) VALUES(@tenant,1,false)", connection, transaction);
        insert.Parameters.AddWithValue("tenant", tenantId);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync())).SqlState);
    }
    [Fact]
    public async Task RuntimeRolesCannotCrossPolicyAndPlatformWriteBoundariesOrMutateRetainedFacts()
    {
        var profile = await PublishAsync(false, "roles"); var context = await ContextAsync();
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => new PostgresProfileStore(platformSource, new FixedClock())
            .EditPolicyAsync(context, new(2, true), 1, "platform-edit", default))).SqlState);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => new PostgresProfileStore(tenantSource, new FixedClock())
            .PublishProfileAsync(administrator, new(tenantId, profile.Authority!.Revision, profile.PublishedPolicy!.PolicyRevisionId, 1), "tenant-publish", default))).SqlState);
        foreach (var table in OwnedTables)
        {
            Assert.False(await HasPrivilegeAsync("profile_tenant_runtime", table, "DELETE"));
            Assert.False(await HasPrivilegeAsync("profile_platform_runtime", table, "TRUNCATE"));
        }
    }
    [Fact]
    public async Task BroaderUpdateDeleteGrantsStillCannotRewriteOrDeleteRetainedPolicyPublicationAndReceipts()
    {
        var profile = await PublishAsync(false, "immutable");
        await PlatformStore.ActivateProfileAsync(administrator, new(tenantId, profile.Authority!.Revision, profile.Profile!.ProfileId), "activate", default);
        await OwnerSqlAsync("GRANT UPDATE,DELETE ON ALL TABLES IN SCHEMA profiles TO profile_tenant_runtime");
        foreach (var sql in new[] { "UPDATE profiles.policy_revisions SET version=version", "DELETE FROM profiles.policy_revisions", "UPDATE profiles.publications SET version=version",
            "DELETE FROM profiles.publications", "UPDATE profiles.command_receipts SET version=version", "DELETE FROM profiles.command_receipts", "DELETE FROM profiles.policy_heads", "DELETE FROM profiles.authority" })
            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => TenantSqlAsync(sql))).SqlState);
        Assert.Equivalent(profile.Profile, await TenantStore.GetProfileAsync(tenantId, profile.Profile.ProfileId, default));
    }
    [Fact]
    public async Task ReceiptFailureRollsBackPolicyEditAndProfilePublicationTogetherWithTheirHeads()
    {
        await OwnerSqlAsync("CREATE FUNCTION profiles.fail_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Synthetic receipt failure'; END $$; CREATE TRIGGER synthetic_receipt_failure BEFORE INSERT ON profiles.command_receipts FOR EACH ROW EXECUTE FUNCTION profiles.fail_receipt()");
        var context = await ContextAsync();
        await Assert.ThrowsAsync<PostgresException>(() => TenantStore.EditPolicyAsync(context, new(0, false), 1, "edit", default));
        Assert.Equal(0, await CountAsync("policy_heads"));
        await OwnerSqlAsync("DROP TRIGGER synthetic_receipt_failure ON profiles.command_receipts");
        await TenantStore.EditPolicyAsync(context, new(0, false), 1, "edit", default);
        var policy = (await TenantStore.PublishPolicyAsync(context, new(1), 1, "publish-policy", default)).PublishedPolicy!;
        await OwnerSqlAsync("CREATE TRIGGER synthetic_receipt_failure BEFORE INSERT ON profiles.command_receipts FOR EACH ROW EXECUTE FUNCTION profiles.fail_receipt()");
        await Assert.ThrowsAsync<PostgresException>(() => PlatformStore.PublishProfileAsync(administrator, new(tenantId, 0, policy.PolicyRevisionId, 1), "publish-profile", default));
        Assert.Equal(0, await CountAsync("publications")); Assert.Equal(0, await CountAsync("authority"));
        await OwnerSqlAsync("DROP TRIGGER synthetic_receipt_failure ON profiles.command_receipts");
        Assert.Equal(ProfileCommandStatus.ProfilePublished, (await PlatformStore.PublishProfileAsync(administrator, new(tenantId, 0, policy.PolicyRevisionId, 1), "publish-profile", default)).Status);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrBackendTerminationBeforeReceiptCommitLeavesNoEffectAndSameKeyCanRetry(bool terminateBackend)
    {
        await using var blocker = new NpgsqlConnection(database.GetConnectionString()); await blocker.OpenAsync(); await using var blockerTransaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(71011)", blocker, blockerTransaction)) await command.ExecuteNonQueryAsync();
        await OwnerSqlAsync("CREATE FUNCTION profiles.block_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(71011); RETURN NEW; END $$; CREATE TRIGGER synthetic_receipt_block BEFORE INSERT ON profiles.command_receipts FOR EACH ROW EXECUTE FUNCTION profiles.block_receipt()");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var context = await ContextAsync(); var pending = TenantStore.EditPolicyAsync(context, new(0, false), 1, "interrupted", cancellation.Token);
        var pid = await WaitForBackendAsync("INSERT INTO profiles.command_receipts", cancellation.Token);
        if (terminateBackend) await OwnerSqlAsync("SELECT pg_terminate_backend(@pid)", ("pid", pid)); else cancellation.Cancel();
        if (terminateBackend) await Assert.ThrowsAnyAsync<NpgsqlException>(() => pending); else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await blockerTransaction.RollbackAsync(); await OwnerSqlAsync("DROP TRIGGER synthetic_receipt_block ON profiles.command_receipts");
        Assert.Equal(0, await CountAsync("policy_heads")); Assert.Equal(0, await CountAsync("command_receipts"));
        Assert.Equal(ProfileCommandStatus.Edited, (await TenantStore.EditPolicyAsync(context, new(0, false), 1, "interrupted", default)).Status);
    }
    [Fact]
    public async Task RoutingLockPinsOneCompatibleSnapshotUntilTheCallerOwnedTransactionCompletes()
    {
        var optional = await PublishAsync(false, "optional");
        await PlatformStore.ActivateProfileAsync(administrator, new(tenantId, optional.Authority!.Revision, optional.Profile!.ProfileId), "activate-optional", default);
        var required = await PublishAsync(true, "required");
        await using var connection = await tenantSource.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        var pinned = await PostgresProfileStore.ResolveActiveForOrderAsync(connection, transaction, tenantId, default);
        Assert.Equal(optional.Profile.ProfileId, pinned!.ProfileId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var activation = PlatformStore.ActivateProfileAsync(administrator, new(tenantId, required.Authority!.Revision, required.Profile!.ProfileId), "activate-required", timeout.Token);
        await WaitForBackendAsync("profiles-routing:", timeout.Token);
        Assert.False(activation.IsCompleted); Assert.Equal(connection, transaction.Connection);
        await transaction.CommitAsync(); Assert.Equal(ProfileCommandStatus.Activated, (await activation).Status);
        await using var nextTransaction = await connection.BeginTransactionAsync();
        Assert.True((await PostgresProfileStore.ResolveActiveForOrderAsync(connection, nextTransaction, tenantId, default))!.Policy.RequireReferenceForProgramOrders);
        Assert.False((await PostgresProfileStore.ResolveRetainedForOrderAsync(connection, nextTransaction, tenantId, pinned.ProfileId, default))!.Policy.RequireReferenceForProgramOrders);
    }
    [Theory]
    [InlineData("version")]
    [InlineData("catalog")]
    [InlineData("policy")]
    [InlineData("receipt")]
    public async Task PersistedUnknownContractsAndContradictoryCatalogPolicyOrReceiptsFailClosed(string damage)
    {
        var profile = await PublishAsync(false, "damage");
        var jsonPath = damage switch { "version" => "{Version}", "catalog" => "{CatalogFingerprint}", "policy" => "{Policy,RequireReferenceForProgramOrders}", _ => "{Status}" };
        var value = damage switch { "version" => "\"tenant-profile/v2\"", "catalog" => "\"unknown\"", "policy" => "true", _ => "0" };
        if (damage == "receipt")
        {
            await OwnerSqlAsync("ALTER TABLE profiles.command_receipts DISABLE TRIGGER immutable_profile_receipt; UPDATE profiles.command_receipts SET result=jsonb_set(result,@path::text[],@value::jsonb) WHERE operation='publish-profile'", ("path", jsonPath), ("value", value));
            await Assert.ThrowsAsync<InvalidOperationException>(() => PlatformStore.PublishProfileAsync(administrator,
                new(tenantId, 0, profile.PublishedPolicy!.PolicyRevisionId, 1), "profile-damage", default));
        }
        else
        {
            await OwnerSqlAsync("ALTER TABLE profiles.publications DISABLE TRIGGER immutable_publication; UPDATE profiles.publications SET facts=jsonb_set(facts,@path::text[],@value::jsonb)", ("path", jsonPath), ("value", value));
            await Assert.ThrowsAsync<InvalidOperationException>(() => TenantStore.GetProfileAsync(tenantId, profile.Profile!.ProfileId, default));
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequiredPublishedBooleanAndBaselineFlagCannotSilentlyDefaultFromMissingJson(bool baselineFlag)
    {
        var profile = await PublishAsync(false, "missing-field");
        if (baselineFlag)
        {
            await OwnerSqlAsync("ALTER TABLE profiles.publications DISABLE TRIGGER immutable_publication; UPDATE profiles.publications SET facts=facts-'IsLegacyBaseline'");
            await Assert.ThrowsAsync<InvalidOperationException>(() => PlatformStore.GetProfileAsync(tenantId, profile.Profile!.ProfileId, default));
        }
        else
        {
            await OwnerSqlAsync("ALTER TABLE profiles.policy_revisions DISABLE TRIGGER immutable_policy_revision; UPDATE profiles.policy_revisions SET facts=facts-'RequireReferenceForProgramOrders'");
            var context = await ContextAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => TenantStore.GetPublishedPolicyAsync(context, profile.PublishedPolicy!.PolicyRevisionId, default));
        }
    }
    [Fact]
    public async Task DestructiveMigrationRollbackIsRejectedAndRetainedProfilesRemainReadable()
    {
        var profile = await PublishAsync(false, "retained");
        await using var context = ProfilesPostgresRegistration.CreateContext(database.GetConnectionString());
        await Assert.ThrowsAsync<NotSupportedException>(() => context.GetService<IMigrator>().MigrateAsync("0"));
        Assert.Equivalent(profile.Profile, await PlatformStore.GetProfileAsync(tenantId, profile.Profile!.ProfileId, default));
    }
    private async Task<ProfileCommandResult> PublishAsync(bool required, string suffix, bool baseline = false)
    {
        var context = await ContextAsync(); var previous = await TenantStore.GetPolicyAsync(context, default);
        var draft = await TenantStore.EditPolicyAsync(context, new(previous?.Revision ?? 0, required), 1, "draft-" + suffix, default);
        var policy = await TenantStore.PublishPolicyAsync(context, new(draft.PolicyState!.Revision), 1, "policy-" + suffix, default);
        var authority = await PlatformStore.GetAuthorityAsync(tenantId, default);
        return await PlatformStore.PublishProfileAsync(administrator, new(tenantId, authority?.Revision ?? 0, policy.PublishedPolicy!.PolicyRevisionId, 1, baseline), "profile-" + suffix, default);
    }
    private NpgsqlDataSource Source(string user) => NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(database.GetConnectionString()) { Username = user, Password = "local-runtime-test-only" }.ConnectionString);
    private async Task OwnerSqlAsync(string sql, params (string Name, object Value)[] values)
    {
        await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync(); await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value); await command.ExecuteNonQueryAsync();
    }
    private async Task TenantSqlAsync(string sql)
    {
        await using var connection = await tenantSource.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        await using (var context = new NpgsqlCommand("SELECT set_config('app.current_tenant',@tenant,true)", connection, transaction))
        { context.Parameters.AddWithValue("tenant", tenantId.ToString("D")); await context.ExecuteNonQueryAsync(); }
        await using var command = new NpgsqlCommand(sql, connection, transaction); await command.ExecuteNonQueryAsync();
    }
    private async Task<long> CountAsync(string table)
    {
        Assert.Contains(table, OwnedTables);
        await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM profiles.{table}", connection); return (long)(await command.ExecuteScalarAsync())!;
    }
    private async Task<bool> HasPrivilegeAsync(string role, string table, string privilege)
    {
        await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT has_table_privilege(@role,@table,@privilege)", connection);
        command.Parameters.AddWithValue("role", role); command.Parameters.AddWithValue("table", "profiles." + table); command.Parameters.AddWithValue("privilege", privilege);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
    private async Task<int> WaitForBackendAsync(string query, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync(ct);
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT pid FROM pg_stat_activity WHERE datname='application_profiles_tests' AND wait_event='advisory' AND query LIKE @query AND pid<>pg_backend_pid() LIMIT 1", connection);
            command.Parameters.AddWithValue("query", "%" + query + "%"); var pid = await command.ExecuteScalarAsync(ct);
            if (pid is int waiting) return waiting;
            await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
        }
    }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero); }
    private sealed class ActiveMembership : ITenantMembershipDirectory
    {
        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TenantMembership>>([]);
    }
}
