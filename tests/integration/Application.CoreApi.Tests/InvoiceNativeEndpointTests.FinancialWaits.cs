using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed partial class InvoiceNativeEndpointTests
{
    private static readonly string[] FinancialWaitOperations = ["Record", "Confirm", "Allocate", "Release", "Reverse"];
    private async Task VerifyFinancialRevocationWaitsAsync(HttpClient client, NpgsqlConnection admin, string privilegedConnectionString,
        HttpClient fga, string store, string model, Guid tenant, Guid organization, Guid recorder, Guid viewer, Guid reconciler,
        Guid owner, Guid invoiceId, WhiteLabelApiFactory identity, string applicationName, NativeFinancialClock clock)
    {
        var recorderToken = identity.CreateToken(recorder.ToString("D"));
        var reconcilerToken = identity.CreateToken(reconciler.ToString("D"));
        var viewerToken = identity.CreateToken(viewer.ToString("D"));
        var payment = Guid.NewGuid(); var path = PaymentPath(tenant, payment);
        var recorded = await SendAsync(client, HttpMethod.Put, path, recorderToken, "wait-record", CashReceipt(organization), HttpStatusCode.Created);
        await SendAsync(client, HttpMethod.Post, path + "/confirm", reconcilerToken, "wait-confirm",
            new { expectedRevision = 1, evidence = "Independent native wait funds verification" }, HttpStatusCode.OK);

        var allocationBody = new { expectedRevision = 2, receivableId = invoiceId, amount = 20m };
        var before = await FinancialWaitSnapshotAsync(admin, tenant, payment, invoiceId);
        await DeniedFinancialWaitAsync(client, admin, privilegedConnectionString, applicationName, tenant, payment, invoiceId,
            false, HttpMethod.Post, path + "/allocations", recorderToken, "wait-allocate", allocationBody,
            () => DeleteTupleAsync(fga, store, model, tenant, recorder, "customer_payment_allocator"),
            () => GrantAsync(fga, store, model, tenant, recorder, ["customer_payment_allocator"]));
        Assert.Equal(before, await FinancialWaitSnapshotAsync(admin, tenant, payment, invoiceId));
        var allocated = await SendAsync(client, HttpMethod.Post, path + "/allocations", recorderToken, "wait-allocate", allocationBody, HttpStatusCode.OK);
        var allocationId = allocated.GetProperty("eventId").GetGuid();

        before = await FinancialWaitSnapshotAsync(admin, tenant, payment, invoiceId);
        await DeniedFinancialWaitAsync(client, admin, privilegedConnectionString, applicationName, tenant, payment, invoiceId,
            true, HttpMethod.Put, path, recorderToken, "wait-record", CashReceipt(organization),
            () => ChangeIssuerMembershipAsync(admin, tenant, recorder, false),
            () => ChangeIssuerMembershipAsync(admin, tenant, recorder, true));
        Assert.Equal(before, await FinancialWaitSnapshotAsync(admin, tenant, payment, invoiceId));
        using (var replay = await SendRawAsync(client, HttpMethod.Put, path, recorderToken, "wait-record", CashReceipt(organization)))
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
            using var document = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
            Assert.Equal(recorded.GetRawText(), document.RootElement.GetRawText());
            Assert.Equal(1, document.RootElement.GetProperty("revision").GetInt64());
        }
        Assert.Equal(before, await FinancialWaitSnapshotAsync(admin, tenant, payment, invoiceId));

        var ownerToken = FinancialToken(identity, owner);
        var releaseBody = new { expectedRevision = 3, reason = "Release the native wait allocation" };
        before = await FinancialWaitSnapshotAsync(admin, tenant, payment, invoiceId);
        await DeniedFinancialWaitAsync(client, admin, privilegedConnectionString, applicationName, tenant, payment, invoiceId,
            true, HttpMethod.Post, path + $"/allocations/{allocationId:D}/release", ownerToken, "wait-release", releaseBody,
            () => ChangeFinancialOwnerAsync(admin, tenant, owner, false),
            () => ChangeFinancialOwnerAsync(admin, tenant, owner, true));
        Assert.Equal(before, await FinancialWaitSnapshotAsync(admin, tenant, payment, invoiceId));
        await SendAsync(client, HttpMethod.Post, path + $"/allocations/{allocationId:D}/release", ownerToken, "wait-release", releaseBody, HttpStatusCode.OK);

        var freshOwnerToken = FinancialToken(identity, owner);
        var reverseBody = new { expectedRevision = 4, reason = "Correct native received-funds evidence" };
        before = await FinancialWaitSnapshotAsync(admin, tenant, payment, invoiceId);
        await DeniedFinancialWaitAsync(client, admin, privilegedConnectionString, applicationName, tenant, payment, invoiceId,
            true, HttpMethod.Post, path + "/reverse", freshOwnerToken, "wait-reverse", reverseBody,
            () => { clock.Shift(TimeSpan.FromSeconds(360)); return Task.CompletedTask; },
            () => { clock.Shift(TimeSpan.Zero); return Task.CompletedTask; });
        Assert.Equal(before, await FinancialWaitSnapshotAsync(admin, tenant, payment, invoiceId));
        await SendAsync(client, HttpMethod.Post, path + "/reverse", freshOwnerToken, "wait-reverse", reverseBody, HttpStatusCode.OK);
        var retained = await SendAsync(client, HttpMethod.Get, path, viewerToken, null, null, HttpStatusCode.OK);
        Assert.Equal("Reversed", retained.GetProperty("state").GetString());
        Assert.Equal(5, retained.GetProperty("revision").GetInt64());
        Assert.Equal(0m, retained.GetProperty("allocatedAmount").GetDecimal());
        var history = await SendAsync(client, HttpMethod.Get, path + "/history", viewerToken, null, null, HttpStatusCode.OK);
        Assert.Equal(5, history.GetProperty("items").GetArrayLength());
        Assert.Equal(FinancialWaitOperations,
            history.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("operation").GetString()));
    }

    private static async Task DeniedFinancialWaitAsync(HttpClient client, NpgsqlConnection admin, string privilegedConnectionString,
        string applicationName, Guid tenant, Guid payment, Guid invoice, bool paymentLock, HttpMethod method, string path,
        string token, string key, object body, Func<Task> revoke, Func<Task> restore)
    {
        await using var blocker = new NpgsqlConnection(privilegedConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        var sql = paymentLock
            ? "SELECT pg_advisory_xact_lock(hashtextextended('customer-payment:'||@tenant::text||':'||@resource::text,0))"
            : "SELECT pg_advisory_xact_lock(hashtextextended('customer-receivable:'||@tenant::text||':'||@resource::text,0))";
        await using (var command = new NpgsqlCommand(sql, blocker, transaction))
        {
            command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("resource", paymentLock ? payment : invoice);
            await command.ExecuteNonQueryAsync();
        }
        var released = false;
        var pending = SendRawAsync(client, method, path, token, key, body);
        try
        {
            await WaitForDatabaseLockAsync(admin, applicationName, "advisory");
            await revoke();
            await transaction.RollbackAsync(); released = true;
            using var response = await pending.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var text = await response.Content.ReadAsStringAsync();
            AssertPrivate(text);
            Assert.DoesNotContain(PrivatePaymentEvidence, text, StringComparison.Ordinal);
            Assert.DoesNotContain("eventId", text, StringComparison.Ordinal);
        }
        finally
        {
            if (!released) await transaction.RollbackAsync();
            await restore();
            using var ignored = await pending.WaitAsync(TimeSpan.FromSeconds(20));
        }
    }

    private static async Task<FinancialWaitSnapshot> FinancialWaitSnapshotAsync(NpgsqlConnection admin, Guid tenant, Guid payment, Guid invoice)
    {
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM invoices.customer_payment_events WHERE tenant_id=@tenant AND payment_id=@payment),
                (SELECT count(*) FROM invoices.customer_financial_receipts WHERE tenant_id=@tenant AND payment_id=@payment),
                p.revision,p.state,p.allocated,r.original_amount-r.allocated
            FROM invoices.customer_payment_balances p CROSS JOIN invoices.receivable_balances r
            WHERE p.tenant_id=@tenant AND p.payment_id=@payment AND r.tenant_id=@tenant AND r.invoice_id=@invoice
            """, admin);
        command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("payment", payment); command.Parameters.AddWithValue("invoice", invoice);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt32(3), reader.GetDecimal(4), reader.GetDecimal(5));
    }

    private static async Task ChangeFinancialOwnerAsync(NpgsqlConnection admin, Guid tenant, Guid owner, bool initialOwner)
    {
        await using var command = new NpgsqlCommand("UPDATE tenancy.memberships SET is_initial_owner=@owner,revision=revision+1 WHERE tenant_id=@tenant AND account_id=@actor AND availability=1", admin);
        command.Parameters.AddWithValue("owner", initialOwner); command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("actor", owner);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private sealed record FinancialWaitSnapshot(long Events, long Receipts, long Revision, int State, decimal Allocated, decimal Outstanding);
    private sealed class NativeFinancialClock : TimeProvider
    {
        private long shiftTicks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddTicks(Interlocked.Read(ref shiftTicks));
        internal void Shift(TimeSpan offset) => Interlocked.Exchange(ref shiftTicks, offset.Ticks);
    }
}
