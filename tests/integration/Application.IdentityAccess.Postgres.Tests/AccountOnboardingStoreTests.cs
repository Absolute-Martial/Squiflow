using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Application.IdentityAccess.Postgres.Tests;

public sealed class AccountOnboardingStoreTests : PostgresTestDatabase
{
    private static readonly DateTimeOffset FirstTime = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly IdentityAdministrationActor FirstActor =
        IdentityAdministrationActor.Create(Guid.Parse("a844711c-5e95-49cc-b14d-229d14a14101"), Guid.Parse("a844711c-5e95-49cc-b14d-229d14a14102"));
    private static readonly IdentityAdministrationActor OtherActor =
        IdentityAdministrationActor.Create(Guid.Parse("b844711c-5e95-49cc-b14d-229d14a14101"), Guid.Parse("b844711c-5e95-49cc-b14d-229d14a14102"));

    [Fact]
    public async Task OnboardingAndLinkingRetainCallerScopedReceiptsAcrossStoreInstances()
    {
        await CreateMigrationRunner().ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresAccountOnboardingStore(dataSource);
        var identity = Identity("first");
        var intent = AccountOnboardingIntent.Create(identity, "request-1");

        var created = await store.OnboardAsync(FirstActor, intent, FirstTime, CancellationToken.None);
        Assert.Equal(OnboardAccountStatus.Created, created.Status);
        Assert.NotNull(created.Account);
        Assert.Equal(FirstActor.PrincipalId, created.Account.ProvisionedByPrincipalId);
        Assert.Equal(FirstActor.DeviceId, created.Account.ProvisionedByDeviceId);

        var restartedStore = new PostgresAccountOnboardingStore(dataSource);
        var replayed = await restartedStore.OnboardAsync(FirstActor, intent, FirstTime.AddDays(1), CancellationToken.None);
        Assert.Equal(OnboardAccountStatus.Replayed, replayed.Status);
        Assert.Equal(created.Account, replayed.Account);
        Assert.Equal(OnboardAccountStatus.IdempotencyKeyConflict,
            (await restartedStore.OnboardAsync(FirstActor, AccountOnboardingIntent.Create(Identity("different"), "request-1"), FirstTime, CancellationToken.None)).Status);
        Assert.Equal(OnboardAccountStatus.IdentityAlreadyBound,
            (await restartedStore.OnboardAsync(OtherActor, AccountOnboardingIntent.Create(identity, "request-1"), FirstTime, CancellationToken.None)).Status);

        var linkIdentity = Identity("linked");
        var link = ExternalIdentityLinkIntent.Create(created.Account.AccountId, linkIdentity, "link-1");
        var linked = await store.LinkAsync(FirstActor, link, FirstTime, CancellationToken.None);
        Assert.Equal(LinkExternalIdentityStatus.Linked, linked.Status);
        Assert.Equal(created.Account.AccountId, linked.Link?.AccountId);
        Assert.Equal(linked.Link,
            (await restartedStore.LinkAsync(FirstActor, link, FirstTime.AddDays(1), CancellationToken.None)).Link);
        Assert.Equal(LinkExternalIdentityStatus.IdempotencyKeyConflict,
            (await restartedStore.LinkAsync(FirstActor, ExternalIdentityLinkIntent.Create(created.Account.AccountId, Identity("other-link"), "link-1"), FirstTime, CancellationToken.None)).Status);
        Assert.Equal(LinkExternalIdentityStatus.IdentityAlreadyLinked,
            (await restartedStore.LinkAsync(OtherActor, ExternalIdentityLinkIntent.Create(created.Account.AccountId, linkIdentity, "link-1"), FirstTime, CancellationToken.None)).Status);

        await using var context = CreateContext();
        Assert.Equal(1, await context.Accounts.CountAsync());
        Assert.Equal(2, await context.ExternalIdentityBindings.CountAsync());
        Assert.Equal(1, await context.AccountOnboardingReceipts.CountAsync());
        Assert.Equal(1, await context.IdentityLinkReceipts.CountAsync());
    }

    [Fact]
    public async Task ConcurrentClaimsForOneIdentityCreateOnlyOneAccount()
    {
        await CreateMigrationRunner().ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresAccountOnboardingStore(dataSource);
        var identity = Identity("shared");
        var operations = Enumerable.Range(0, 8).Select(index =>
            store.OnboardAsync(
                index == 0 ? FirstActor : OtherActor,
                AccountOnboardingIntent.Create(identity, $"claim-{index}"),
                FirstTime,
                CancellationToken.None));

        var results = await Task.WhenAll(operations);
        Assert.Single(results, result => result.Status == OnboardAccountStatus.Created);
        Assert.Equal(7, results.Count(result => result.Status == OnboardAccountStatus.IdentityAlreadyBound));
        await using var context = CreateContext();
        Assert.Equal(1, await context.Accounts.CountAsync());
        Assert.Equal(1, await context.ExternalIdentityBindings.CountAsync());
        Assert.Equal(1, await context.AccountOnboardingReceipts.CountAsync());

        var firstAccountId = results.Single(result => result.Status == OnboardAccountStatus.Created).Account!.AccountId;
        var second = await store.OnboardAsync(OtherActor, AccountOnboardingIntent.Create(Identity("second-account"), "second"), FirstTime, CancellationToken.None);
        var contested = Identity("contested-link");
        var links = await Task.WhenAll(
            store.LinkAsync(FirstActor, ExternalIdentityLinkIntent.Create(firstAccountId, contested, "first-link"), FirstTime, CancellationToken.None),
            store.LinkAsync(OtherActor, ExternalIdentityLinkIntent.Create(second.Account!.AccountId, contested, "second-link"), FirstTime, CancellationToken.None));
        Assert.Single(links, result => result.Status == LinkExternalIdentityStatus.Linked);
        Assert.Single(links, result => result.Status == LinkExternalIdentityStatus.IdentityBoundElsewhere);

        var sameKey = AccountOnboardingIntent.Create(Identity("same-key"), "same-key");
        var sameKeyResults = await Task.WhenAll(
            store.OnboardAsync(FirstActor, sameKey, FirstTime, CancellationToken.None),
            store.OnboardAsync(FirstActor, sameKey, FirstTime.AddMinutes(1), CancellationToken.None));
        Assert.Single(sameKeyResults, result => result.Status == OnboardAccountStatus.Created);
        Assert.Single(sameKeyResults, result => result.Status == OnboardAccountStatus.Replayed);
        Assert.Equal(sameKeyResults[0].Account, sameKeyResults[1].Account);
    }

    [Fact]
    public async Task LinkingLocksAccountAvailabilityAndRejectsDisabledOrUnknownAccounts()
    {
        await CreateMigrationRunner().ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        const string linkApplication = "identity-link-lock-test";
        var linkConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            ApplicationName = linkApplication,
        }.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(linkConnectionString);
        var store = new PostgresAccountOnboardingStore(dataSource);
        var created = await store.OnboardAsync(FirstActor, AccountOnboardingIntent.Create(Identity("owner"), "onboard"), FirstTime, CancellationToken.None);
        var accountId = created.Account!.AccountId;
        Assert.Equal(LinkExternalIdentityStatus.AccountNotFound,
            (await store.LinkAsync(FirstActor, ExternalIdentityLinkIntent.Create(Guid.NewGuid(), Identity("missing"), "missing"), FirstTime, CancellationToken.None)).Status);

        await using var blocker = new NpgsqlConnection(ConnectionString);
        await blocker.OpenAsync();
        await using var disableTransaction = await blocker.BeginTransactionAsync();
        await using (var disable = new NpgsqlCommand(
                         "UPDATE identity_access.accounts SET availability = 2, disabled_at = @at WHERE id = @id", blocker, disableTransaction))
        {
            disable.Parameters.AddWithValue("at", FirstTime);
            disable.Parameters.AddWithValue("id", accountId);
            Assert.Equal(1, await disable.ExecuteNonQueryAsync());
        }

        var pending = store.LinkAsync(FirstActor, ExternalIdentityLinkIntent.Create(accountId, Identity("after-disable"), "link-disabled"), FirstTime, CancellationToken.None);
        Assert.True(await LinkIsBlockedByDisableAsync(linkApplication), "The link query never appeared blocked by the account update.");
        Assert.False(pending.IsCompleted);
        await disableTransaction.CommitAsync();
        Assert.Equal(LinkExternalIdentityStatus.AccountDisabled, (await pending).Status);
        await using var context = CreateContext();
        var directory = new PostgresAccountDirectory(dataSource);
        Assert.Equal(AccountAvailability.Disabled, await directory.FindAvailabilityAsync(accountId, CancellationToken.None));
        Assert.Null(await directory.FindAvailabilityAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(1, await context.ExternalIdentityBindings.CountAsync());
        Assert.Empty(await context.IdentityLinkReceipts.ToListAsync());
    }

    [Fact]
    public async Task ReceiptInsertFailureRollsBackAccountAndBindingAtomically()
    {
        await CreateMigrationRunner().ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresAccountOnboardingStore(dataSource);
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using (var failure = new NpgsqlCommand(
                         """
                         CREATE FUNCTION identity_access.reject_receipt_for_test() RETURNS trigger LANGUAGE plpgsql AS
                         $$ BEGIN RAISE EXCEPTION 'receipt failure test'; END $$;
                         CREATE TRIGGER reject_account_receipt BEFORE INSERT ON identity_access.account_onboarding_receipts
                         FOR EACH ROW EXECUTE FUNCTION identity_access.reject_receipt_for_test();
                         """, admin))
        {
            await failure.ExecuteNonQueryAsync();
        }

        var onboarding = AccountOnboardingIntent.Create(Identity("atomic"), "atomic-account");
        await Assert.ThrowsAsync<PostgresException>(() => store.OnboardAsync(FirstActor, onboarding, FirstTime, CancellationToken.None));
        await using (var context = CreateContext())
        {
            Assert.Empty(await context.Accounts.ToListAsync());
            Assert.Empty(await context.ExternalIdentityBindings.ToListAsync());
            Assert.Empty(await context.AccountOnboardingReceipts.ToListAsync());
        }

        await using (var restore = new NpgsqlCommand(
                         "DROP TRIGGER reject_account_receipt ON identity_access.account_onboarding_receipts", admin))
        {
            await restore.ExecuteNonQueryAsync();
        }

        var account = await store.OnboardAsync(FirstActor, onboarding, FirstTime, CancellationToken.None);
        Assert.Equal(OnboardAccountStatus.Created, account.Status);
        await using (var failure = new NpgsqlCommand(
                         """
                         CREATE TRIGGER reject_link_receipt BEFORE INSERT ON identity_access.identity_link_receipts
                         FOR EACH ROW EXECUTE FUNCTION identity_access.reject_receipt_for_test();
                         """, admin))
        {
            await failure.ExecuteNonQueryAsync();
        }

        var link = ExternalIdentityLinkIntent.Create(account.Account!.AccountId, Identity("atomic-link"), "atomic-link");
        await Assert.ThrowsAsync<PostgresException>(() => store.LinkAsync(FirstActor, link, FirstTime, CancellationToken.None));
        await using (var context = CreateContext())
        {
            Assert.Equal(1, await context.ExternalIdentityBindings.CountAsync());
            Assert.Empty(await context.IdentityLinkReceipts.ToListAsync());
        }

        await using (var restore = new NpgsqlCommand(
                         "DROP TRIGGER reject_link_receipt ON identity_access.identity_link_receipts", admin))
        {
            await restore.ExecuteNonQueryAsync();
        }
        Assert.Equal(LinkExternalIdentityStatus.Linked,
            (await store.LinkAsync(FirstActor, link, FirstTime, CancellationToken.None)).Status);
    }

    private static ExternalIdentity Identity(string subject) =>
        ExternalIdentity.Create("https://identity.example.test", subject);

    private async Task<bool> LinkIsBlockedByDisableAsync(string applicationName)
    {
        await using var observer = new NpgsqlConnection(ConnectionString);
        await observer.OpenAsync();
        await using var query = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM pg_stat_activity
                WHERE application_name = @application_name
                  AND cardinality(pg_blocking_pids(pid)) > 0)
            """, observer);
        query.Parameters.AddWithValue("application_name", applicationName);

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!deadline.IsCancellationRequested)
        {
            if ((bool)(await query.ExecuteScalarAsync(deadline.Token) ?? false)) return true;
            await Task.Delay(TimeSpan.FromMilliseconds(25), deadline.Token);
        }

        return false;
    }
}
