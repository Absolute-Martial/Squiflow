using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Application.IdentityAccess.Postgres.Tests;

public sealed class AccountLifecycleStoreTests : PostgresTestDatabase
{
    private static readonly IdentityAdministrationActor Actor = IdentityAdministrationActor.Create(Guid.NewGuid(), Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TransitionsRetainReceiptsAcrossRestartAndDoNotRestoreLinksOrMembershipAuthority()
    {
        await CreateMigrationRunner().ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var account = await CreateAccountAsync(source);
        var store = new PostgresAccountLifecycleStore(source);
        var suspend = Intent(account, AccountLifecycleOperation.Suspend, 1, "suspend");
        var changed = await store.ExecuteAsync(Actor, suspend, Now, CancellationToken.None);
        Assert.Equal(AccountLifecycleStatus.Changed, changed.Status);
        Assert.Equal(AccountAvailability.Disabled, changed.Account?.Availability);
        Assert.Equal(2, changed.Account?.Revision);
        Assert.Equal(Actor.DeviceId, changed.Account?.ChangedByDeviceId);
        Assert.Equal(AccountAvailability.Disabled,
            await new PostgresAccountDirectory(source).FindAvailabilityAsync(account, CancellationToken.None));
        var registry = await new PostgresPlatformAccountRegistry(source).FindAsync(account, CancellationToken.None);
        Assert.Equal(2, registry?.Revision);
        Assert.Equal(Now, registry?.DisabledAt);

        var restarted = new PostgresAccountLifecycleStore(source);
        var replay = await restarted.ExecuteAsync(Actor, suspend, Now.AddDays(1), CancellationToken.None);
        Assert.True(replay.Replayed);
        Assert.Equal(changed.Account, replay.Account);
        Assert.Equal(AccountLifecycleStatus.IdempotencyKeyConflict,
            (await restarted.ExecuteAsync(Actor, Intent(account, AccountLifecycleOperation.Suspend, 1, "suspend", "Different reason"), Now, CancellationToken.None)).Status);
        Assert.Equal(AccountLifecycleStatus.RevisionConflict,
            (await restarted.ExecuteAsync(Actor, Intent(account, AccountLifecycleOperation.Reactivate, 1, "stale"), Now, CancellationToken.None)).Status);
        Assert.Equal(AccountLifecycleStatus.InvalidTransition,
            (await restarted.ExecuteAsync(Actor, Intent(account, AccountLifecycleOperation.Suspend, 2, "duplicate"), Now, CancellationToken.None)).Status);
        var reactivated = await restarted.ExecuteAsync(Actor, Intent(account, AccountLifecycleOperation.Reactivate, 2, "reactivate"), Now.AddHours(1), CancellationToken.None);
        Assert.Equal(AccountAvailability.Active, reactivated.Account?.Availability);
        Assert.Equal(3, reactivated.Account?.Revision);
        Assert.Null(reactivated.Account?.DisabledAt);
        // Replaying a suspension receipt is historical evidence, never a fresh suspension.
        Assert.Equal(changed.Account, (await restarted.ExecuteAsync(Actor, suspend, Now, CancellationToken.None)).Account);
        Assert.Equal(AccountAvailability.Active,
            await new PostgresAccountDirectory(source).FindAvailabilityAsync(account, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentKeysAndSameRevisionProduceOnlyOneTransition()
    {
        await CreateMigrationRunner().ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var account = await CreateAccountAsync(source);
        var store = new PostgresAccountLifecycleStore(source);
        var same = Intent(account, AccountLifecycleOperation.Suspend, 1, "same-key");
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => store.ExecuteAsync(Actor, same, Now, CancellationToken.None)));
        Assert.All(results, result => Assert.Equal(AccountLifecycleStatus.Changed, result.Status));
        Assert.Single(results, result => !result.Replayed);
        Assert.All(results, result => Assert.Equal(results[0].Account, result.Account));
        var races = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => store.ExecuteAsync(Actor,
            Intent(account, AccountLifecycleOperation.Reactivate, 2, $"race-{index}"), Now, CancellationToken.None)));
        Assert.Single(races, result => result.Status == AccountLifecycleStatus.Changed);
        Assert.Equal(5, races.Count(result => result.Status == AccountLifecycleStatus.RevisionConflict));
        await using var count = source.CreateCommand("SELECT count(*) FROM identity_access.account_lifecycle_receipts");
        Assert.Equal(2L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task FailureToRetainReceiptRollsBackAccountAndRetryCanSucceed()
    {
        await CreateMigrationRunner().ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var account = await CreateAccountAsync(source);
        await using (var failure = source.CreateCommand("""
            CREATE FUNCTION identity_access.reject_lifecycle_test() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN RAISE EXCEPTION 'receipt failure'; END $$;
            CREATE TRIGGER reject_lifecycle BEFORE INSERT ON identity_access.account_lifecycle_receipts
            FOR EACH ROW EXECUTE FUNCTION identity_access.reject_lifecycle_test();
            """)) await failure.ExecuteNonQueryAsync();
        var store = new PostgresAccountLifecycleStore(source);
        var intent = Intent(account, AccountLifecycleOperation.Suspend, 1, "retry");
        await Assert.ThrowsAsync<PostgresException>(() => store.ExecuteAsync(Actor, intent, Now, CancellationToken.None));
        Assert.Equal(AccountAvailability.Active,
            await new PostgresAccountDirectory(source).FindAvailabilityAsync(account, CancellationToken.None));
        await using (var restore = source.CreateCommand("DROP TRIGGER reject_lifecycle ON identity_access.account_lifecycle_receipts"))
            await restore.ExecuteNonQueryAsync();
        Assert.Equal(AccountLifecycleStatus.Changed, (await store.ExecuteAsync(Actor, intent, Now, CancellationToken.None)).Status);
        await using var context = CreateContext();
        await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync("202610030003_AccountOnboarding"));
    }

    [Fact]
    public async Task SuspensionUsesTheSameAccountLockAsIdentityLinking()
    {
        await CreateMigrationRunner().ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        var account = await CreateAccountAsync(source);
        await using var blocker = await source.OpenConnectionAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var rowLock = new NpgsqlCommand("SELECT identity_access.lock_account_for_identity_link(@account)", blocker, transaction))
        {
            rowLock.Parameters.AddWithValue("account", account);
            await rowLock.ExecuteScalarAsync();
        }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var intent = Intent(account, AccountLifecycleOperation.Suspend, 1, "locked");
        var pending = new PostgresAccountLifecycleStore(source).ExecuteAsync(Actor, intent, Now, deadline.Token);
        await using var observer = source.CreateCommand("SELECT count(*) FROM pg_stat_activity WHERE cardinality(pg_blocking_pids(pid)) > 0 AND query LIKE '%lock_account_for_identity_link%'");
        while ((long)(await observer.ExecuteScalarAsync(deadline.Token))! == 0)
            await Task.Delay(20, deadline.Token);
        Assert.False(pending.IsCompleted);
        await transaction.CommitAsync(deadline.Token);
        Assert.Equal(AccountLifecycleStatus.Changed, (await pending).Status);
        var link = await new PostgresAccountOnboardingStore(source).LinkAsync(Actor,
            ExternalIdentityLinkIntent.Create(account, ExternalIdentity.Create("https://identity.example.test", "later-link"), "link"), Now, CancellationToken.None);
        Assert.Equal(LinkExternalIdentityStatus.AccountDisabled, link.Status);
    }

    private static AccountLifecycleIntent Intent(Guid account, AccountLifecycleOperation operation, int revision, string key, string reason = "Support request") =>
        AccountLifecycleIntent.Create(operation, account, revision, reason, key);

    private static async Task<Guid> CreateAccountAsync(NpgsqlDataSource source)
    {
        var created = await new PostgresAccountOnboardingStore(source).OnboardAsync(Actor,
            AccountOnboardingIntent.Create(ExternalIdentity.Create("https://identity.example.test", Guid.NewGuid().ToString("N")), "onboard"), Now, CancellationToken.None);
        return created.Account!.AccountId;
    }
}
