using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Application.IdentityAccess.Postgres.Tests;

public sealed class AccountBindingDirectoryTests : PostgresTestDatabase
{
    [Fact]
    public async Task ExactExternalIdentityResolvesOneStableAccount()
    {
        var runner = CreateMigrationRunner();
        await runner.ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        var accountId = Guid.CreateVersion7();
        var createdAt = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
        await using (var setup = CreateContext())
        {
            setup.Accounts.Add(new AccountRow
            {
                Id = accountId,
                Availability = AccountAvailability.Active,
                CreatedAt = createdAt,
            });
            setup.ExternalIdentityBindings.AddRange(
                new ExternalIdentityBindingRow
                {
                    AccountId = accountId,
                    Issuer = "https://identity.example.test",
                    Subject = "stable-subject",
                    CreatedAt = createdAt,
                },
                new ExternalIdentityBindingRow
                {
                    AccountId = accountId,
                    Issuer = "https://enterprise-id.example.test",
                    Subject = "linked-subject",
                    CreatedAt = createdAt,
                });
            await setup.SaveChangesAsync(CancellationToken.None);
        }

        var runtimeConnectionString = await CreateReadOnlyRuntimeRoleAsync();
        await using var queryDatabase = CreateContext(runtimeConnectionString);
        var directory = new PostgresAccountBindingDirectory(queryDatabase);

        var result = await directory.FindAsync(
            ExternalIdentity.Create("https://identity.example.test", "stable-subject"),
            CancellationToken.None);
        var wrongSubject = await directory.FindAsync(
            ExternalIdentity.Create("https://identity.example.test", "different-subject"),
            CancellationToken.None);
        var linkedIdentity = await directory.FindAsync(
            ExternalIdentity.Create("https://enterprise-id.example.test", "linked-subject"),
            CancellationToken.None);

        Assert.Equal(new AccountBinding(accountId, AccountAvailability.Active), result);
        Assert.Null(wrongSubject);
        Assert.Equal(accountId, linkedIdentity?.AccountId);

        await using var runtimeConnection = new NpgsqlConnection(runtimeConnectionString);
        await runtimeConnection.OpenAsync(CancellationToken.None);
        await using var forbiddenDdl = runtimeConnection.CreateCommand();
        forbiddenDdl.CommandText = "CREATE TABLE identity_access.runtime_must_not_create_tables (id uuid)";
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenDdl.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsTwoAccountsForTheSameExternalIdentity()
    {
        var runner = CreateMigrationRunner();
        await runner.ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        var createdAt = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
        var firstAccount = Guid.CreateVersion7();
        var secondAccount = Guid.CreateVersion7();
        await using (var firstWrite = CreateContext())
        {
            firstWrite.Accounts.AddRange(
                new AccountRow { Id = firstAccount, Availability = AccountAvailability.Active, CreatedAt = createdAt },
                new AccountRow { Id = secondAccount, Availability = AccountAvailability.Active, CreatedAt = createdAt });
            firstWrite.ExternalIdentityBindings.Add(new ExternalIdentityBindingRow
            {
                AccountId = firstAccount,
                Issuer = "https://identity.example.test",
                Subject = "same-subject",
                CreatedAt = createdAt,
            });
            await firstWrite.SaveChangesAsync(CancellationToken.None);
        }

        await using var conflictingWrite = CreateContext();
        conflictingWrite.ExternalIdentityBindings.Add(new ExternalIdentityBindingRow
        {
            AccountId = secondAccount,
            Issuer = "https://identity.example.test",
            Subject = "same-subject",
            CreatedAt = createdAt,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            conflictingWrite.SaveChangesAsync(CancellationToken.None));
    }

    private async Task<string> CreateReadOnlyRuntimeRoleAsync()
    {
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync(CancellationToken.None);
        await using var command = admin.CreateCommand();
        command.CommandText = """
            CREATE ROLE application_identity_reader LOGIN PASSWORD 'local-runtime-test-only';
            GRANT CONNECT ON DATABASE application_tests TO application_identity_reader;
            GRANT USAGE ON SCHEMA identity_access TO application_identity_reader;
            GRANT SELECT ON ALL TABLES IN SCHEMA identity_access TO application_identity_reader;
            """;
        await command.ExecuteNonQueryAsync(CancellationToken.None);

        var runtime = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "application_identity_reader",
            Password = "local-runtime-test-only",
        };
        return runtime.ConnectionString;
    }
}
