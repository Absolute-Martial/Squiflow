using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Application.Tenancy.Postgres.Tests;

public sealed class TenantMembershipLifecyclePostgresTests : PostgresTestDatabase
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InvitationAndActivationAreDurableAndRemovalRevokesMembership()
    {
        var (tenantId, accountId) = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(await CreateLifecycleRoleAsync());
        var store = new PostgresTenantMembershipLifecycleStore(source);
        var actor = Actor();
        var invited = await ExecuteAsync(store, actor, TenantMembershipLifecycleIntent.Invite(tenantId, accountId, "invite"));
        Assert.Equal(MembershipLifecycleStatus.Invited, invited.Status);
        Assert.Null(invited.Membership!.ActivatedAt);
        await using var context = CreateContext();
        var directory = new PostgresTenantMembershipDirectory(context);
        Assert.False(await directory.IsActiveAsync(accountId, tenantId, CancellationToken.None));
        var activation = TenantMembershipLifecycleIntent.Transition(MembershipLifecycleOperation.Activate, tenantId, accountId, 1, "activate");
        var active = await ExecuteAsync(store, actor, activation);
        Assert.Equal(MembershipLifecycleStatus.Activated, active.Status);
        Assert.True(await directory.IsActiveAsync(accountId, tenantId, CancellationToken.None));
        var removed = await ExecuteAsync(store, actor,
            TenantMembershipLifecycleIntent.Transition(MembershipLifecycleOperation.Remove, tenantId, accountId, 2, "remove"));
        Assert.Equal(MembershipLifecycleStatus.Removed, removed.Status);
        Assert.False(await directory.IsActiveAsync(accountId, tenantId, CancellationToken.None));
        var replay = await ExecuteAsync(store, actor, activation);
        Assert.True(replay.Replayed);
        Assert.Equal(active.Membership, replay.Membership);
        Assert.False(await directory.IsActiveAsync(accountId, tenantId, CancellationToken.None));
        var invalid = await ExecuteAsync(store, actor,
            TenantMembershipLifecycleIntent.Transition(MembershipLifecycleOperation.Activate, tenantId, accountId, 3, "reactivate"));
        Assert.Equal(MembershipLifecycleStatus.InvalidTransition, invalid.Status);
        Assert.Equal(3L, await ReceiptCountAsync());
    }

    [Fact]
    public async Task ConcurrentSameKeyReturnsOneReceiptAndChangedIntentConflicts()
    {
        var (tenantId, accountId) = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(await CreateLifecycleRoleAsync());
        var store = new PostgresTenantMembershipLifecycleStore(source);
        var actor = Actor();
        var intent = TenantMembershipLifecycleIntent.Invite(tenantId, accountId, "same");
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ExecuteAsync(store, actor, intent)));
        Assert.Single(results, result => !result.Replayed);
        Assert.All(results, result => Assert.Equal(results[0].Membership, result.Membership));
        Assert.Equal(1L, await ReceiptCountAsync());
        var conflict = await ExecuteAsync(store, actor,
            TenantMembershipLifecycleIntent.Transition(MembershipLifecycleOperation.Activate, tenantId, accountId, 1, "same"));
        Assert.Equal(MembershipLifecycleStatus.IdempotencyKeyConflict, conflict.Status);
    }

    [Fact]
    public async Task ConcurrentDifferentInvitationKeysReturnExistingMembershipWithoutSqlFailure()
    {
        var (tenantId, accountId) = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(await CreateLifecycleRoleAsync());
        var store = new PostgresTenantMembershipLifecycleStore(source);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => ExecuteAsync(store, Actor(),
            TenantMembershipLifecycleIntent.Invite(tenantId, accountId, $"invite-{index}"))));
        Assert.Single(results, result => result.Status == MembershipLifecycleStatus.Invited);
        Assert.Equal(7, results.Count(result => result.Status == MembershipLifecycleStatus.MembershipAlreadyExists));
        Assert.Equal(1L, await ReceiptCountAsync());
    }

    [Fact]
    public async Task ConcurrentTransitionsRequireTheCurrentRevision()
    {
        var (tenantId, accountId) = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(await CreateLifecycleRoleAsync());
        var store = new PostgresTenantMembershipLifecycleStore(source);
        await ExecuteAsync(store, Actor(), TenantMembershipLifecycleIntent.Invite(tenantId, accountId, "invite"));
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => ExecuteAsync(store, Actor(),
            TenantMembershipLifecycleIntent.Transition(MembershipLifecycleOperation.Activate, tenantId, accountId, 1, $"activate-{index}"))));
        Assert.Single(results, result => result.Status == MembershipLifecycleStatus.Activated);
        Assert.Equal(5, results.Count(result => result.Status == MembershipLifecycleStatus.RevisionConflict));
        Assert.Equal(2L, await ReceiptCountAsync());
    }

    [Fact]
    public async Task MissingTenantAndAccountReturnExplicitFailuresWithoutRetainingFailedReceipts()
    {
        var (tenantId, _) = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(await CreateLifecycleRoleAsync());
        var store = new PostgresTenantMembershipLifecycleStore(source);
        var accountId = Guid.NewGuid();
        var actor = Actor();
        var intent = TenantMembershipLifecycleIntent.Invite(tenantId, accountId, "retry");
        var missingAccount = await ExecuteAsync(store, actor, intent);
        Assert.Equal(MembershipLifecycleStatus.AccountNotFound, missingAccount.Status);
        var missingTenant = await ExecuteAsync(store, actor,
            TenantMembershipLifecycleIntent.Invite(Guid.NewGuid(), accountId, "missing-tenant"));
        Assert.Equal(MembershipLifecycleStatus.TenantNotFound, missingTenant.Status);
        Assert.Equal(0L, await ReceiptCountAsync());
        await SqlAsync("INSERT INTO identity_access.accounts (id, availability, created_at) VALUES (@id, 1, now())", accountId);
        Assert.Equal(MembershipLifecycleStatus.Invited, (await ExecuteAsync(store, actor, intent)).Status);
    }

    [Fact]
    public async Task ReceiptFailureRollsBackTheMembershipAndRetryCanProceed()
    {
        var (tenantId, accountId) = await SeedAsync();
        await SqlAsync("""
            CREATE FUNCTION public.reject_membership_receipt() RETURNS trigger LANGUAGE plpgsql AS
                $$ BEGIN RAISE EXCEPTION 'fixture receipt failure'; END $$;
            CREATE TRIGGER reject_receipt BEFORE INSERT ON tenancy.membership_lifecycle_receipts
                FOR EACH ROW EXECUTE FUNCTION public.reject_membership_receipt();
            """);
        await using var source = NpgsqlDataSource.Create(await CreateLifecycleRoleAsync());
        var store = new PostgresTenantMembershipLifecycleStore(source);
        var actor = Actor();
        var intent = TenantMembershipLifecycleIntent.Invite(tenantId, accountId, "retry");
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(store, actor, intent));
        await using (var db = CreateContext()) Assert.Empty(await db.Memberships.ToListAsync());
        Assert.Equal(0L, await ReceiptCountAsync());
        await SqlAsync("DROP TRIGGER reject_receipt ON tenancy.membership_lifecycle_receipts");
        Assert.Equal(MembershipLifecycleStatus.Invited, (await ExecuteAsync(store, actor, intent)).Status);
    }

    [Fact]
    public async Task SuspendedTenantCannotReceiveAnInvitationOrActivation()
    {
        var (tenantId, accountId) = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(await CreateLifecycleRoleAsync());
        var store = new PostgresTenantMembershipLifecycleStore(source);
        await ExecuteAsync(store, Actor(), TenantMembershipLifecycleIntent.Invite(tenantId, accountId, "invite"));
        await SqlAsync("UPDATE tenancy.tenants SET availability = 2, suspended_at = now() WHERE id = @id", tenantId);
        var result = await ExecuteAsync(store, Actor(),
            TenantMembershipLifecycleIntent.Transition(MembershipLifecycleOperation.Activate, tenantId, accountId, 1, "activate"));
        Assert.Equal(MembershipLifecycleStatus.TenantNotFound, result.Status);
        Assert.Equal(1L, await ReceiptCountAsync());
    }

    [Fact]
    public async Task InitialOwnerIsActiveUniqueAndProtectedUntilRoleAdministrationExists()
    {
        var (tenantId, accountId) = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(await CreateLifecycleRoleAsync());
        var store = new PostgresTenantMembershipLifecycleStore(source);
        var owner = await ExecuteAsync(store, Actor(),
            TenantMembershipLifecycleIntent.BootstrapOwner(tenantId, accountId, "owner"));
        Assert.Equal(MembershipLifecycleStatus.OwnerBootstrapped, owner.Status);
        Assert.True(owner.Membership!.IsInitialOwner);
        Assert.Equal(MembershipAvailability.Active, owner.Membership.Availability);
        var protectedResult = await ExecuteAsync(store, Actor(),
            TenantMembershipLifecycleIntent.Transition(
                MembershipLifecycleOperation.Remove, tenantId, accountId, 1, "remove-owner"));
        Assert.Equal(MembershipLifecycleStatus.InitialOwnerProtected, protectedResult.Status);

        var secondAccount = Guid.NewGuid();
        await SqlAsync("INSERT INTO identity_access.accounts(id, availability, created_at) VALUES (@id, 1, now())", secondAccount);
        var second = await ExecuteAsync(store, Actor(),
            TenantMembershipLifecycleIntent.BootstrapOwner(tenantId, secondAccount, "second-owner"));
        Assert.Equal(MembershipLifecycleStatus.InitialOwnerAlreadyExists, second.Status);
    }

    [Fact]
    public async Task TenantSuspensionImmediatelyBlocksAndReactivationRestoresActiveMembership()
    {
        var (tenantId, accountId) = await SeedAsync();
        await using var source = NpgsqlDataSource.Create(await CreateLifecycleRoleAsync());
        var membershipStore = new PostgresTenantMembershipLifecycleStore(source);
        await ExecuteAsync(membershipStore, Actor(),
            TenantMembershipLifecycleIntent.BootstrapOwner(tenantId, accountId, "owner"));
        var tenantStore = new PostgresTenantLifecycleStore(source);
        var actor = Actor();
        var suspended = await tenantStore.ExecuteAsync(
            actor,
            TenantLifecycleIntent.Create(TenantLifecycleOperation.Suspend, tenantId, 1, "suspend"),
            OccurredAt,
            CancellationToken.None);
        Assert.Equal(TenantLifecycleStatus.Suspended, suspended.Status);
        await using var context = CreateContext();
        var directory = new PostgresTenantMembershipDirectory(context);
        Assert.False(await directory.IsActiveAsync(accountId, tenantId, CancellationToken.None));
        var replay = await tenantStore.ExecuteAsync(
            actor,
            TenantLifecycleIntent.Create(TenantLifecycleOperation.Suspend, tenantId, 1, "suspend"),
            OccurredAt.AddMinutes(1),
            CancellationToken.None);
        Assert.True(replay.Replayed);
        var reactivated = await tenantStore.ExecuteAsync(
            actor,
            TenantLifecycleIntent.Create(TenantLifecycleOperation.Reactivate, tenantId, 2, "reactivate"),
            OccurredAt.AddMinutes(2),
            CancellationToken.None);
        Assert.Equal(TenantLifecycleStatus.Reactivated, reactivated.Status);
        Assert.True(await directory.IsActiveAsync(accountId, tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task MigrationPreservesHistoricalModelsAndRefusesToDiscardLifecycleEvidence()
    {
        var (tenantId, accountId) = await SeedAsync();
        await using var db = CreateContext();
        Assert.False(db.Database.HasPendingModelChanges());
        var migrations = db.GetService<IMigrationsAssembly>();
        // Walk every migration instead of a fixed list: revisions may predate the membership
        // lifecycle, but once retained lifecycle authority exists no later migration may drop it.
        var lifecycleAuthorityRetained = false;
        foreach (var name in migrations.Migrations.Keys)
        {
            var historical = migrations.CreateMigration(migrations.Migrations[name], "Npgsql.EntityFrameworkCore.PostgreSQL");
            var membership = historical.TargetModel.FindEntityType("Application.Tenancy.Postgres.TenantMembershipRow")!;
            var revision = membership.FindProperty("Revision");
            var receipt = historical.TargetModel.FindEntityType("Application.Tenancy.Postgres.MembershipLifecycleReceiptRow");
            if (revision is null && receipt is null)
            {
                Assert.False(lifecycleAuthorityRetained);
                continue;
            }

            lifecycleAuthorityRetained = true;
            Assert.NotNull(revision);
            Assert.NotNull(receipt);
        }

        Assert.True(lifecycleAuthorityRetained, "the membership lifecycle model must exist in some migration");
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Contains("202610030005_TenantAndInitialOwnerLifecycle", applied);
        await using var source = NpgsqlDataSource.Create(await CreateLifecycleRoleAsync());
        await ExecuteAsync(new PostgresTenantMembershipLifecycleStore(source), Actor(),
            TenantMembershipLifecycleIntent.Invite(tenantId, accountId, "retained"));
        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync("0"));
        Assert.Contains("lifecycle evidence exists", exception.MessageText, StringComparison.Ordinal);
        Assert.Equal(1L, await ReceiptCountAsync());
        // The refused revert must discard no lifecycle evidence: the guard aborts before any
        // table, column or constraint is dropped and the retained receipt survives. EF retires
        // the history row of the migration whose own Down it was running, so that one migration
        // is reported unapplied afterwards; this asserts the evidence and every other recorded
        // migration instead of an empty pending set, which would hide a real schema loss.
        var retained = await db.Database.GetAppliedMigrationsAsync();
        Assert.All(
            applied.Where(migration => migration != "202610030005_TenantAndInitialOwnerLifecycle"),
            migration => Assert.Contains(migration, retained));
        Assert.Equal(1L, await ReceiptCountAsync());
    }

    [Fact]
    public async Task RestrictedLifecycleRoleCannotAlterIdentityOrDeleteRetainedMemberships()
    {
        await SeedAsync();
        await using var connection = new NpgsqlConnection(await CreateLifecycleRoleAsync());
        await connection.OpenAsync();
        foreach (var sql in new[] { "DELETE FROM tenancy.memberships", "UPDATE identity_access.accounts SET availability = 2", "ALTER TABLE tenancy.memberships ADD COLUMN unauthorized int" })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
        }
    }

    private async Task<(Guid TenantId, Guid AccountId)> SeedAsync()
    {
        await CreateMigrationRunner().ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await SqlAsync("INSERT INTO identity_access.accounts(id, availability, created_at) VALUES (@id, 1, now())", accountId);
        await SqlAsync("INSERT INTO tenancy.tenants(id, display_name, availability, created_at) VALUES (@id, 'Fixture tenant', 1, now())", tenantId);
        return (tenantId, accountId);
    }

    private async Task<string> CreateLifecycleRoleAsync()
    {
        await SqlAsync("""
            CREATE ROLE application_membership_writer LOGIN NOSUPERUSER NOBYPASSRLS PASSWORD 'fixture-only';
            GRANT CONNECT ON DATABASE application_tests TO application_membership_writer;
            GRANT USAGE ON SCHEMA identity_access TO application_membership_writer;
            GRANT USAGE ON SCHEMA tenancy TO application_membership_writer;
            GRANT SELECT ON identity_access.accounts TO application_membership_writer;
            GRANT SELECT ON tenancy.tenants, tenancy.memberships, tenancy.membership_lifecycle_receipts TO application_membership_writer;
            GRANT SELECT ON tenancy.tenant_lifecycle_receipts TO application_membership_writer;
            GRANT INSERT ON tenancy.memberships, tenancy.membership_lifecycle_receipts, tenancy.tenant_lifecycle_receipts TO application_membership_writer;
            GRANT UPDATE (availability, revision, suspended_at) ON tenancy.tenants TO application_membership_writer;
            GRANT UPDATE (availability, revision, activated_at, suspended_at, removed_at) ON tenancy.memberships TO application_membership_writer;
            """);
        return new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "application_membership_writer",
            Password = "fixture-only",
        }.ConnectionString;
    }

    private async Task SqlAsync(string sql, Guid? id = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        if (id.HasValue) command.Parameters.AddWithValue("id", id.Value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> ReceiptCountAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM tenancy.membership_lifecycle_receipts", connection);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException());
    }

    private static Task<TenantMembershipLifecycleResult> ExecuteAsync(PostgresTenantMembershipLifecycleStore store,
        TenantMembershipAdministrationActor actor, TenantMembershipLifecycleIntent intent) =>
        store.ExecuteAsync(actor, intent, OccurredAt.AddTicks(17), CancellationToken.None);

    private static TenantMembershipAdministrationActor Actor() =>
        TenantMembershipAdministrationActor.Create(Guid.NewGuid(), Guid.NewGuid());
}
