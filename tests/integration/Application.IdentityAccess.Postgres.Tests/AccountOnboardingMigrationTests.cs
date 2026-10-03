using Application.IdentityAccess.Postgres.Migrations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Application.IdentityAccess.Postgres.Tests;

public sealed class AccountOnboardingMigrationTests : PostgresTestDatabase
{
    [Fact]
    public async Task HistoricalTargetRemainsOriginalAndEmptyUpgradeCanRollBackAndReapply()
    {
        const string original = "202609170001_InitialAccountBindings";
        const string onboarding = "202610030003_AccountOnboarding";
        const string receiptType = "Application.IdentityAccess.Postgres.AccountOnboardingReceiptRow";
        Assert.Null(new InitialAccountBindings().TargetModel.FindEntityType(receiptType));
        Assert.NotNull(new Application.IdentityAccess.Postgres.Migrations.AccountOnboarding()
            .TargetModel.FindEntityType(receiptType));

        await using var context = CreateContext();
        await context.Database.MigrateAsync(original);
        Assert.Null(await ReceiptTableAsync());

        await context.Database.MigrateAsync(onboarding);
        Assert.Equal("account_onboarding_receipts", await ReceiptTableAsync());
        Assert.False(context.Database.HasPendingModelChanges());

        await context.Database.MigrateAsync(original);
        Assert.Null(await ReceiptTableAsync());
        await context.Database.MigrateAsync(onboarding);
        Assert.Equal("account_onboarding_receipts", await ReceiptTableAsync());
    }

    [Fact]
    public async Task RollbackRejectsLossOfCommittedReceipts()
    {
        const string original = "202609170001_InitialAccountBindings";
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var store = new PostgresAccountOnboardingStore(dataSource);
        var actor = IdentityAdministrationActor.Create(Guid.NewGuid(), Guid.NewGuid());
        var result = await store.OnboardAsync(
            actor,
            AccountOnboardingIntent.Create(ExternalIdentity.Create("https://identity.example.test", "rollback"), "one"),
            new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        Assert.Equal(OnboardAccountStatus.Created, result.Status);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.MigrateAsync(original));
        Assert.Contains("Cannot roll back identity onboarding while receipts exist", exception.ToString(), StringComparison.Ordinal);
        Assert.Equal("account_onboarding_receipts", await ReceiptTableAsync());
        Assert.Equal(1, await context.AccountOnboardingReceipts.CountAsync());
    }

    private async Task<string?> ReceiptTableAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT relname FROM pg_class WHERE oid = to_regclass('identity_access.account_onboarding_receipts')", connection);
        return await command.ExecuteScalarAsync() as string;
    }
}
