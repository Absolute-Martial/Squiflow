using System.Net;
using System.Text.Json;
using Application.Customers;
using Application.Customers.Postgres;
using Application.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed partial class InvoiceNativeEndpointTests
{
    private async Task VerifyRevocationDuringOrganizationWaitAsync(HttpClient client, NpgsqlConnection admin, string privilegedConnectionString,
        HttpClient fga, string store, string model, Guid tenant, Guid organization, Guid issuer, string token,
        Guid permissionOrder, Guid membershipOrder, string applicationName)
    {
        foreach (var revokeMembership in new[] { false, true })
        {
            var order = revokeMembership ? membershipOrder : permissionOrder;
            var key = revokeMembership ? "membership-revoked-during-wait" : "permission-revoked-during-wait";
            var before = await ReadOrdinalAsync(admin, tenant, organization);
            await using var blocker = new NpgsqlConnection(privilegedConnectionString);
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();
            await LockInvoiceOrganizationAsync(blocker, transaction, tenant, organization);
            var released = false;
            var pending = SendRawAsync(client, HttpMethod.Post, OrderPath(tenant, order), token, key,
                new { expectedOrderRevision = 2 });
            try
            {
                await WaitForDatabaseLockAsync(admin, applicationName, "advisory");
                if (revokeMembership) await ChangeIssuerMembershipAsync(admin, tenant, issuer, false);
                else await DeleteTupleAsync(fga, store, model, tenant, issuer, "invoice_issuer");
                await transaction.RollbackAsync();
                released = true;
                using var response = await pending.WaitAsync(TimeSpan.FromSeconds(20));
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                Assert.True(response.Headers.CacheControl?.NoStore);
                AssertPrivate(await response.Content.ReadAsStringAsync());
                await AssertInvoiceAbsentAsync(admin, tenant, order);
                Assert.Equal(before, await ReadOrdinalAsync(admin, tenant, organization));
            }
            finally
            {
                // Releasing the lock is also bounded on a failing assertion: no pending request survives this fixture.
                if (!released) await transaction.RollbackAsync();
                if (revokeMembership) await ChangeIssuerMembershipAsync(admin, tenant, issuer, true);
                else await GrantAsync(fga, store, model, tenant, issuer, ["invoice_issuer"]);
                using var ignored = await pending.WaitAsync(TimeSpan.FromSeconds(20));
            }
            var issued = await SendAsync(client, HttpMethod.Post, OrderPath(tenant, order), token, key,
                new { expectedOrderRevision = 2 }, HttpStatusCode.Created);
            Assert.NotEqual(Guid.Empty, CommandId(issued));
            Assert.Equal(before + 1, await ReadOrdinalAsync(admin, tenant, organization));
        }
    }

    private async Task VerifyConsolidationDuringInvoiceAsync(WebApplicationFactory<Program> host, HttpClient client,
        NpgsqlConnection admin, string privilegedConnectionString, string runtimeConnectionString, Guid tenant, Guid organization, Guid owner,
        string issuerToken, string viewerToken, Guid firstOrder, Guid nextOrder, Guid sourceIndividual, Guid survivorIndividual)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = await scope.ServiceProvider.GetRequiredService<ResolveTenantContext>().ExecuteAsync(owner, tenant, default)
            ?? throw new InvalidOperationException("The consolidation setup requires a current Owner membership.");
        var replica = new NpgsqlConnectionStringBuilder(runtimeConnectionString)
        {
            ApplicationName = "invoice-consolidation-" + tenant.ToString("N"),
            MaxPoolSize = 1,
        };
        await using var source = NpgsqlDataSource.Create(replica.ConnectionString);
        var customers = new PostgresCustomerStore(source);
        await using var blocker = new NpgsqlConnection(privilegedConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await LockInvoiceOrganizationAsync(blocker, transaction, tenant, organization);
        var body = new { expectedOrderRevision = 2, debtor = new { kind = "Individual", individualId = sourceIndividual } };
        var released = false;
        var pendingIssue = SendRawAsync(client, HttpMethod.Post, OrderPath(tenant, firstOrder), issuerToken, "frozen-before-consolidation", body);
        Task<ConsolidateCustomerDuplicateResult>? pendingConsolidation = null;
        JsonElement frozen;
        try
        {
            // The real invoice transaction holds the Customers FOR SHARE observation while waiting on its organization.
            var applicationName = new NpgsqlConnectionStringBuilder(runtimeConnectionString).ApplicationName;
            await WaitForDatabaseLockAsync(admin, applicationName!, "advisory");
            pendingConsolidation = customers.ConsolidateDuplicateAsync(context,
                new(sourceIndividual, survivorIndividual, 1, 1, "Confirmed duplicate for invoice regression"),
                "native-invoice-consolidation", default);
            await WaitForDatabaseLockAsync(admin, replica.ApplicationName!, "transactionid");
            await transaction.RollbackAsync();
            released = true;
            using var issued = await pendingIssue.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
            using var document = JsonDocument.Parse(await issued.Content.ReadAsStringAsync());
            var invoiceId = CommandId(document.RootElement);
            Assert.Equal(ConsolidateCustomerDuplicateStatus.Consolidated,
                (await pendingConsolidation.WaitAsync(TimeSpan.FromSeconds(20))).Status);
            frozen = await SendAsync(client, HttpMethod.Get, InvoicePath(tenant, invoiceId), viewerToken, null, null, HttpStatusCode.OK);
            Assert.Equal(sourceIndividual, frozen.GetProperty("debtor").GetProperty("individualId").GetGuid());
            Assert.Equal("Frozen source debtor", frozen.GetProperty("debtor").GetProperty("individualDisplayName").GetString());
            await using var recognized = new NpgsqlCommand("SELECT debtor_id FROM invoices.receivables WHERE tenant_id=@tenant AND invoice_id=@invoice", admin);
            recognized.Parameters.AddWithValue("tenant", tenant); recognized.Parameters.AddWithValue("invoice", invoiceId);
            Assert.Equal(sourceIndividual, await recognized.ExecuteScalarAsync());
        }
        finally
        {
            if (!released) await transaction.RollbackAsync();
            using var ignored = await pendingIssue.WaitAsync(TimeSpan.FromSeconds(20));
            if (pendingConsolidation is not null) await pendingConsolidation.WaitAsync(TimeSpan.FromSeconds(20));
        }
        var rejected = await SendAsync(client, HttpMethod.Post, OrderPath(tenant, nextOrder), issuerToken,
            "retired-debtor", body, HttpStatusCode.Conflict);
        Assert.Equal("invoice_debtor_ineligible", rejected.GetProperty("code").GetString());
        await AssertInvoiceAbsentAsync(admin, tenant, nextOrder);
        var survivor = await SendAsync(client, HttpMethod.Post, OrderPath(tenant, nextOrder), issuerToken, "explicit-survivor",
            new { expectedOrderRevision = 2, debtor = new { kind = "Individual", individualId = survivorIndividual } }, HttpStatusCode.Created);
        var survivorRead = await SendAsync(client, HttpMethod.Get, InvoicePath(tenant, CommandId(survivor)), viewerToken, null, null, HttpStatusCode.OK);
        Assert.Equal(survivorIndividual, survivorRead.GetProperty("debtor").GetProperty("individualId").GetGuid());
        var replay = await SendAsync(client, HttpMethod.Post, OrderPath(tenant, firstOrder), issuerToken,
            "frozen-before-consolidation", body, HttpStatusCode.OK);
        Assert.Equal(frozen.GetRawText(), (await SendAsync(client, HttpMethod.Get, InvoicePath(tenant, CommandId(replay)),
            viewerToken, null, null, HttpStatusCode.OK)).GetRawText());
    }

    private static async Task LockInvoiceOrganizationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenant, Guid organization)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@tenant::text || ':invoices:organization:' || @organization::text,0))", connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("organization", organization);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task WaitForDatabaseLockAsync(NpgsqlConnection admin, string name, string lockType)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            while (true)
            {
                await using var command = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid WHERE a.application_name=@name AND l.locktype=@type AND NOT l.granted)", admin);
                command.Parameters.AddWithValue("name", name); command.Parameters.AddWithValue("type", lockType);
                if (await command.ExecuteScalarAsync(deadline.Token) is true) return;
                await Task.Delay(TimeSpan.FromMilliseconds(25), deadline.Token);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        { throw new TimeoutException("The expected native database lock wait was not observed."); }
    }

    private static async Task<long> ReadOrdinalAsync(NpgsqlConnection admin, Guid tenant, Guid organization)
    {
        await using var command = new NpgsqlCommand("SELECT coalesce((SELECT last_ordinal FROM invoices.organization_ordinals WHERE tenant_id=@tenant AND organization_id=@organization),0)", admin);
        command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("organization", organization);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task AssertInvoiceAbsentAsync(NpgsqlConnection admin, Guid tenant, Guid order)
    {
        await using var command = new NpgsqlCommand("SELECT count(*) FROM invoices.issued_invoices WHERE tenant_id=@tenant AND source_order_id=@order", admin);
        command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("order", order);
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    private static async Task ChangeIssuerMembershipAsync(NpgsqlConnection admin, Guid tenant, Guid issuer, bool active)
    {
        await using var command = new NpgsqlCommand("UPDATE tenancy.memberships SET availability=@availability, revision=revision+1, suspended_at=CASE WHEN @active THEN NULL ELSE now() END, activated_at=CASE WHEN @active THEN now() ELSE activated_at END WHERE tenant_id=@tenant AND account_id=@issuer", admin);
        command.Parameters.AddWithValue("availability", active ? 1 : 2); command.Parameters.AddWithValue("active", active);
        command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("issuer", issuer);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}
