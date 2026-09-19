using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquiFlow.DbMigrator;
using Xunit;

namespace SquiFlow.Tenancy.Postgres.Tests;

public sealed class TenantMembershipDirectoryTests : PostgresTestDatabase
{
    [Fact]
    public async Task DirectoryReturnsOnlyCurrentActiveMembershipsWithoutCrossAccountLeakage()
    {
        await new MigrationRunner(ConnectionString)
            .ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        var accountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var activeTenantId = Guid.NewGuid();
        var suspendedMembershipTenantId = Guid.NewGuid();
        var suspendedTenantId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
        await SeedAccountsAsync(accountId, otherAccountId);

        await using (var setup = CreateContext())
        {
            setup.Tenants.AddRange(
                new TenantRow { Id = activeTenantId, DisplayName = "Active Tenant", Availability = TenantAvailability.Active, CreatedAt = createdAt },
                new TenantRow { Id = suspendedMembershipTenantId, DisplayName = "Suspended Membership", Availability = TenantAvailability.Active, CreatedAt = createdAt },
                new TenantRow { Id = suspendedTenantId, DisplayName = "Suspended Tenant", Availability = TenantAvailability.Suspended, CreatedAt = createdAt, SuspendedAt = createdAt });
            setup.Memberships.AddRange(
                new TenantMembershipRow { TenantId = activeTenantId, AccountId = accountId, Availability = MembershipAvailability.Active, CreatedAt = createdAt },
                new TenantMembershipRow { TenantId = activeTenantId, AccountId = otherAccountId, Availability = MembershipAvailability.Active, CreatedAt = createdAt },
                new TenantMembershipRow { TenantId = suspendedMembershipTenantId, AccountId = accountId, Availability = MembershipAvailability.Suspended, CreatedAt = createdAt, SuspendedAt = createdAt },
                new TenantMembershipRow { TenantId = suspendedTenantId, AccountId = accountId, Availability = MembershipAvailability.Active, CreatedAt = createdAt });
            await setup.SaveChangesAsync(CancellationToken.None);
        }

        var runtimeConnectionString = await CreateReadOnlyRuntimeRoleAsync();
        await using var queryDatabase = CreateContext(runtimeConnectionString);
        var directory = new PostgresTenantMembershipDirectory(queryDatabase);

        var memberships = await directory.ListActiveAsync(accountId, CancellationToken.None);
        var active = await directory.IsActiveAsync(accountId, activeTenantId, CancellationToken.None);
        var crossAccount = await directory.IsActiveAsync(Guid.NewGuid(), activeTenantId, CancellationToken.None);
        var suspendedMembership = await directory.IsActiveAsync(accountId, suspendedMembershipTenantId, CancellationToken.None);
        var suspendedTenant = await directory.IsActiveAsync(accountId, suspendedTenantId, CancellationToken.None);

        var membership = Assert.Single(memberships);
        Assert.Equal(new TenantMembership(activeTenantId, "Active Tenant"), membership);
        Assert.True(active);
        Assert.False(crossAccount);
        Assert.False(suspendedMembership);
        Assert.False(suspendedTenant);

        await using var runtimeConnection = new NpgsqlConnection(runtimeConnectionString);
        await runtimeConnection.OpenAsync(CancellationToken.None);
        await using var forbiddenMutation = runtimeConnection.CreateCommand();
        forbiddenMutation.CommandText = "UPDATE tenancy.tenants SET display_name = 'Changed'";
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            forbiddenMutation.ExecuteNonQueryAsync(CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task MembershipRequiresAnExistingSquiFlowAccount()
    {
        await new MigrationRunner(ConnectionString)
            .ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        var createdAt = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
        await using var database = CreateContext();
        var tenantId = Guid.NewGuid();
        database.Tenants.Add(new TenantRow
        {
            Id = tenantId,
            DisplayName = "Tenant",
            Availability = TenantAvailability.Active,
            CreatedAt = createdAt,
        });
        database.Memberships.Add(new TenantMembershipRow
        {
            TenantId = tenantId,
            AccountId = Guid.NewGuid(),
            Availability = MembershipAvailability.Active,
            CreatedAt = createdAt,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TenancyModelMatchesItsAppliedMigration()
    {
        await new MigrationRunner(ConnectionString)
            .ApplyAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        await using var database = CreateContext();
        Assert.False(database.Database.HasPendingModelChanges());
    }

    private async Task SeedAccountsAsync(params Guid[] accountIds)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);

        foreach (var accountId in accountIds)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO identity_access.accounts (id, availability, created_at)
                VALUES (@id, 1, '2026-09-17T12:00:00Z')
                """;
            command.Parameters.AddWithValue("id", accountId);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private async Task<string> CreateReadOnlyRuntimeRoleAsync()
    {
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync(CancellationToken.None);
        await using var command = admin.CreateCommand();
        command.CommandText = """
            CREATE ROLE squiflow_tenancy_reader LOGIN PASSWORD 'local-runtime-test-only';
            GRANT CONNECT ON DATABASE squiflow_tests TO squiflow_tenancy_reader;
            GRANT USAGE ON SCHEMA tenancy TO squiflow_tenancy_reader;
            GRANT SELECT ON ALL TABLES IN SCHEMA tenancy TO squiflow_tenancy_reader;
            """;
        await command.ExecuteNonQueryAsync(CancellationToken.None);

        var runtime = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "squiflow_tenancy_reader",
            Password = "local-runtime-test-only",
        };
        return runtime.ConnectionString;
    }
}
