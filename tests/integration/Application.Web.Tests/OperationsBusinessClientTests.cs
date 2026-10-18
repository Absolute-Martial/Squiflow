using System.Net;
using System.Text;
using Application.Web.Business;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessClientTests
{
    [Theory]
    [InlineData("orders/20000000-0000-0000-0000-000000000001/fulfillment/handoffs")]
    [InlineData("orders/20000000-0000-0000-0000-000000000001/fulfillment/corrections")]
    [InlineData("purchasing/requests/20000000-0000-0000-0000-000000000001/payments")]
    [InlineData("purchasing/requests/20000000-0000-0000-0000-000000000001/refunds")]
    [InlineData("purchasing/requests/20000000-0000-0000-0000-000000000001/corrections")]
    [InlineData("purchasing/suppliers/20000000-0000-0000-0000-000000000001/retire")]
    public async Task OperationsUnknownSaveRetriesExactEnvelopeWithoutChangingRevisionOrEvidence(string resource)
    {
        var id = Guid.NewGuid(); var requests = new List<(string Path, string Body, string Key)>();
        using var handler = new Handler(async (request, ct) =>
        {
            requests.Add((request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(ct), request.Headers.GetValues("Idempotency-Key").Single()));
            if (requests.Count == 1) throw new HttpRequestException("Private supplier diagnostic");
            var response = Json(new PurchasingReceipt(id, 4)); response.Headers.Add("Idempotency-Replayed", "true"); return response;
        });
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        var body = new Dictionary<string, object> { ["expectedRevision"] = 3, ["reason"] = "Original exact evidence" };
        var command = BusinessCommand.Create(Tenant, resource, body); body["expectedRevision"] = 99;
        Assert.True((await api.SendAsync<PurchasingReceipt>(command, value => PurchasingContracts.Receipt(value, id, 4))).Unknown);
        var replay = await api.SendAsync<PurchasingReceipt>(command, value => PurchasingContracts.Receipt(value, id, 4));
        Assert.True(replay.Replayed); Assert.Equal(requests[0], requests[1]); Assert.DoesNotContain("99", requests[1].Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"id\":\"20000000-0000-0000-0000-000000000001\",\"revision\":0}")]
    [InlineData("{\"id\":\"20000000-0000-0000-0000-000000000002\",\"revision\":4}")]
    public async Task MissingOrWrongOperationsReceiptLeavesSaveUnknown(string json)
    {
        var id = Guid.Parse("20000000-0000-0000-0000-000000000001");
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        Assert.True((await api.SendAsync<PurchasingReceipt>(BusinessCommand.Create(Tenant, $"purchasing/requests/{id:D}/payments", new { expectedRevision = 3 }), value => PurchasingContracts.Receipt(value, id, 4))).Unknown);
    }

    [Fact]
    public async Task OperationsReadRejectsWrongResourceAndUnsafePaginationWithoutFollowingIt()
    {
        var supplier = SupplierFixture();
        using var handler = new Handler((_, _) => Task.FromResult(Json(supplier)));
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        var id = Guid.NewGuid();
        Assert.Equal(BusinessFailure.InvalidResponse, (await api.GetAsync<SupplierRecord>(Tenant, $"purchasing/suppliers/{id:D}", value => PurchasingContracts.Supplier(value, id))).Failure);
        Assert.False(OperationsContracts.Page(new OperationsPage<SupplierRecord>([supplier], "https://outside.example"), value => PurchasingContracts.Supplier(value), value => value.SupplierId, null));
        Assert.False(OperationsContracts.Page(new OperationsPage<SupplierRecord>([supplier, supplier], null), value => PurchasingContracts.Supplier(value), value => value.SupplierId, null));
        Assert.False(OperationsContracts.Page(new OperationsPage<SupplierRecord>([supplier], "repeat"), value => PurchasingContracts.Supplier(value), value => value.SupplierId, "repeat"));
    }

    [Fact]
    public void OperationsTextCountsUnicodeScalarsAndRejectsBrokenUtf16AndControlCharacters()
    {
        Assert.True(OperationsContracts.Text(string.Concat(Enumerable.Repeat("😀", 128)), 128));
        Assert.False(OperationsContracts.Text(string.Concat(Enumerable.Repeat("😀", 129)), 128));
        Assert.False(OperationsContracts.Text("broken\ud800", 128)); Assert.False(OperationsContracts.Text("a\nb", 128)); Assert.False(OperationsContracts.Text("\n", 128, false));
        Assert.False(OperationsContracts.Text(" ", 128)); Assert.True(OperationsContracts.Text(null, 128, false));
        Assert.False(OperationsContracts.Money(1.001m, true)); Assert.True(OperationsContracts.Money(.01m, true));
        Assert.False(OperationsContracts.Quantity(1.01m, 0)); Assert.True(OperationsContracts.Quantity(1.00m, 0));
    }

    [Fact]
    public void OperationsHistoryRejectsRepeatedRevisionsAndUnboundedOrForwardCursors()
    {
        var supplier = SupplierFixture();
        var entry = new SupplierEventRecord(Guid.NewGuid(), 2, Guid.NewGuid(), DateTimeOffset.UtcNow, 1, "Revised", supplier);
        Assert.True(OperationsContracts.History(new OperationsHistory<SupplierEventRecord>(2, [entry], 2), x => PurchasingContracts.SupplierEvent(x, supplier.SupplierId), x => x.Revision, null));
        Assert.False(OperationsContracts.History(new OperationsHistory<SupplierEventRecord>(2, [entry, entry], null), _ => true, x => x.Revision, null));
        Assert.False(OperationsContracts.History(new OperationsHistory<SupplierEventRecord>(2, [entry], 3), _ => true, x => x.Revision, null));
        Assert.False(OperationsContracts.History(new OperationsHistory<SupplierEventRecord>(2, [entry], null), _ => true, x => x.Revision, 2));
    }

    private static SupplierRecord SupplierFixture() => new(Guid.NewGuid(), 1, "<script>Supplier</script>", null, "Active", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
