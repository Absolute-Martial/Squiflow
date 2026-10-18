using System.Net;
using System.Text;
using Application.Web.Business;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessClientTests
{
    [Fact]
    public async Task FinancialReceiptReplayKeepsTheOriginalPutBodyKeyAndResource()
    {
        var payment = Guid.NewGuid(); var eventId = Guid.NewGuid();
        var calls = new List<(string Method, string Path, string Body, string Key)>();
        using var handler = new Handler(async (request, ct) =>
        {
            calls.Add((request.Method.Method, request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(ct), request.Headers.GetValues("Idempotency-Key").Single()));
            if (calls.Count == 1) throw new HttpRequestException("Private bank provider diagnostic");
            var response = Json(new { paymentId = payment, eventId, revision = 1 });
            response.Headers.Add("Idempotency-Replayed", "true"); return response;
        });
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        var command = BusinessCommand.Create(Tenant, $"customer-payments/{payment:D}",
            new { debtorKind = "Organization", debtorId = Guid.NewGuid(), currencyCode = "NPR", amount = 1.0001m, channel = "Cash", evidence = "Synthetic receipt" }, HttpMethod.Put);
        Assert.True((await api.SendAsync<PaymentCommandReceipt>(command, value => FinancialContracts.Receipt(value, payment))).Unknown);
        var replay = await api.SendAsync<PaymentCommandReceipt>(command, value => FinancialContracts.Receipt(value, payment));
        Assert.True(replay.Replayed); Assert.Equal(eventId, replay.Value!.EventId);
        Assert.Equal(calls[0], calls[1]); Assert.Equal("PUT", calls[0].Method);
        Assert.Equal($"/api/v1/tenants/{Tenant:D}/customer-payments/{payment:D}", calls[0].Path);
    }

    [Fact]
    public async Task InvoiceReadConsumesActualSourceOrderFieldNamesAndRejectsAChangedResource()
    {
        var invoice = Guid.NewGuid(); var order = Guid.NewGuid(); var organization = Guid.NewGuid();
        var wire = new
        {
            invoiceId = invoice,
            sourceOrderId = order,
            sourceOrderRevision = 2,
            organizationId = organization,
            organizationOrdinal = 1,
            currencyCode = "NPR",
            total = 1.2345m,
            issuedByAccountId = Guid.NewGuid(),
            issuedAt = "2026-10-16T00:00:00Z",
            businessDate = "2026-10-16",
            debtor = new { kind = "Organization", organizationId = organization, organizationDisplayName = "<script>Frozen name</script>" },
            lines = new[] { new { position = 1, description = "Retained item", quantity = 1m, unitCode = "EA", unitPrice = 1.2345m, lineTotal = 1.2345m } },
        };
        using var handler = new Handler((_, _) => Task.FromResult(Json(wire)));
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        var loaded = await api.GetAsync<InvoiceRecord>(Tenant, $"invoices/{invoice:D}", value => FinancialContracts.Invoice(value, invoice));
        Assert.Equal(order, loaded.Value!.SourceOrderId); Assert.Equal(2, loaded.Value.SourceOrderRevision);
        Assert.Equal(BusinessFailure.InvalidResponse, (await api.GetAsync<InvoiceRecord>(Tenant, $"invoices/{Guid.NewGuid():D}", value => FinancialContracts.Invoice(value, Guid.NewGuid()))).Failure);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"paymentId\":\"20000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"paymentId\":\"20000000-0000-0000-0000-000000000001\",\"eventId\":\"00000000-0000-0000-0000-000000000000\",\"revision\":1}")]
    [InlineData("{\"paymentId\":\"20000000-0000-0000-0000-000000000002\",\"eventId\":\"20000000-0000-0000-0000-000000000003\",\"revision\":1}")]
    public async Task MissingOrWrongFinancialReceiptsPreserveUnknownOutcome(string body)
    {
        var payment = Guid.Parse("20000000-0000-0000-0000-000000000001");
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        var result = await api.SendAsync<PaymentCommandReceipt>(BusinessCommand.Create(Tenant, $"customer-payments/{payment:D}/confirm", new { expectedRevision = 1, evidence = "Synthetic verification" }), value => FinancialContracts.Receipt(value, payment));
        Assert.True(result.Unknown); Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("https://untrusted.example")]
    [InlineData("p1//20000000000000000000000000000000")]
    [InlineData("p1/abc?tenantId=other")]
    [InlineData("..")]
    public async Task UnsupportedFinancialCursorNeverBecomesAnOutgoingUrl(string cursor)
    {
        var payment = PaymentFixture();
        using var handler = new Handler((_, _) => Task.FromResult(Json(new { items = new[] { payment }, nextCursor = cursor })));
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        var result = await api.GetAsync<FinancialPage<PaymentRecord>>(Tenant, "customer-payments",
            value => FinancialContracts.Page(value, row => FinancialContracts.Payment(row), row => row.PaymentId, null));
        Assert.Equal(BusinessFailure.InvalidResponse, result.Failure);
    }

    [Fact]
    public async Task FinancialListsAcceptFiftyRecordsAndRejectAnOversizedPage()
    {
        var count = 50;
        using var handler = new Handler((_, _) => Task.FromResult(Json(new
        {
            items = Enumerable.Range(0, count).Select(_ => PaymentFixture()).ToArray(),
            nextCursor = (string?)null,
        })));
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        Task<BusinessResult<FinancialPage<PaymentRecord>>> Load() => api.GetAsync<FinancialPage<PaymentRecord>>(Tenant, "customer-payments",
            value => FinancialContracts.Page(value, row => FinancialContracts.Payment(row), row => row.PaymentId, null));
        Assert.Equal(50, (await Load()).Value!.Items.Length);
        count = 51;
        Assert.Equal(BusinessFailure.InvalidResponse, (await Load()).Failure);
    }

    [Fact]
    public async Task AggregateDebtorBalanceCanExceedOneReceiptLimitWithoutRounding()
    {
        var debtor = Guid.NewGuid();
        var amount = 1999999999999999.9998m;
        using var handler = new Handler((_, _) => Task.FromResult(Json(new
        {
            debtorKind = "Organization",
            debtorId = debtor,
            currencyCode = "NPR",
            recognizedAmount = amount,
            outstandingAmount = amount,
            confirmedUnappliedAmount = 0m,
        })));
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        var result = await api.GetAsync<DebtorBalanceRecord>(Tenant, $"debtors/Organization/{debtor:D}/balance", value => FinancialContracts.Balance(value, "Organization", debtor));
        Assert.Equal(amount, result.Value!.OutstandingAmount);
    }

    private static PaymentRecord PaymentFixture() => new(Guid.NewGuid(), "Organization", Guid.NewGuid(), "NPR", 12.3456m,
        "Cash", "Synthetic received-money evidence", null, Guid.NewGuid(), DateTimeOffset.Parse("2026-10-16T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        1, "Pending", 0m, 0m);
}
