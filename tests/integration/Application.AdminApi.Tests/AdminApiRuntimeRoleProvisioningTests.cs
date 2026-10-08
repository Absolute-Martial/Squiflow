using Npgsql;
using Xunit;

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class AdminApiRuntimeRoleProvisioningTests(AdminApiTestEnvironment environment)
{
    [Theory]
    [InlineData("INSERT", "profiles.policy_heads", "profile column privileges are unsafe")]
    [InlineData("REFERENCES", "profiles.policy_heads", "profile table privileges are unsafe")]
    [InlineData("REFERENCES", "profiles.publications", "profile table privileges are unsafe")]
    public async Task ProvisioningRejectsPublicProfileColumnPrivilege(string privilege, string table, string expectedFailure)
    {
        await using (var owner = new NpgsqlConnection(environment.OwnerConnectionString))
        {
            await owner.OpenAsync(CancellationToken.None);
            await using var grant = owner.CreateCommand();
            grant.CommandText = $"GRANT {privilege} (tenant_id) ON {table} TO PUBLIC";
            await grant.ExecuteNonQueryAsync(CancellationToken.None);
        }

        try
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(
                environment.ApplyRuntimeRoleGrantScriptAsync);
            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
            Assert.Contains(expectedFailure, failure.MessageText, StringComparison.Ordinal);
        }
        finally
        {
            await using var owner = new NpgsqlConnection(environment.OwnerConnectionString);
            await owner.OpenAsync(CancellationToken.None);
            await using var revoke = owner.CreateCommand();
            revoke.CommandText = $"REVOKE {privilege} (tenant_id) ON {table} FROM PUBLIC";
            await revoke.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task NativeAdminRoleReadsProfilesButCannotMutateTenantPolicyOrOrderHeads()
    {
        await using var runtime = new NpgsqlConnection(environment.RuntimeConnectionString);
        await runtime.OpenAsync(CancellationToken.None);

        foreach (var table in new[]
                 {
                     "profiles.policy_heads", "profiles.policy_revisions", "profiles.publications",
                     "profiles.authority", "profiles.command_receipts", "orders.program_order_metadata",
                     "orders.order_drafts",
                 })
        {
            await using var read = runtime.CreateCommand();
            read.CommandText = $"SELECT count(*) FROM {table}";
            Assert.Equal(0L, await read.ExecuteScalarAsync(CancellationToken.None));
        }

        await using (var allowedAuthorityRevision = runtime.CreateCommand())
        {
            allowedAuthorityRevision.CommandText =
                "UPDATE profiles.authority SET revision = revision WHERE false";
            Assert.Equal(0, await allowedAuthorityRevision.ExecuteNonQueryAsync(CancellationToken.None));
        }
        await using (var allowedOrderRevision = runtime.CreateCommand())
        {
            allowedOrderRevision.CommandText = "UPDATE orders.order_drafts SET revision = revision WHERE false";
            Assert.Equal(0, await allowedOrderRevision.ExecuteNonQueryAsync(CancellationToken.None));
        }

        foreach (var sql in new[]
                 {
                     "INSERT INTO profiles.policy_heads(tenant_id, revision, require_reference) " +
                     "VALUES ('00000000-0000-0000-0000-000000000001', 1, false)",
                     "UPDATE profiles.policy_heads SET require_reference = require_reference WHERE false",
                     "UPDATE orders.order_drafts SET summary = summary WHERE false",
                     "SELECT total FROM orders.order_drafts WHERE false",
                     "UPDATE tenancy.tenant_authorization_state SET revision=revision WHERE false",
                     "UPDATE orders.program_order_metadata SET external_reference = external_reference WHERE false",
                 })
        {
            await using var forbidden = new NpgsqlCommand(sql, runtime);
            var failure = await Assert.ThrowsAsync<PostgresException>(
                () => forbidden.ExecuteNonQueryAsync(CancellationToken.None));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
        }
    }
}
