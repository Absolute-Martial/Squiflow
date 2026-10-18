using System.Net;
using System.Text;
using System.Security.Cryptography;
using Application.Web.Business;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessClientTests
{
    [Fact]
    public async Task ImportSourceRetryFreezesCsvBytesRetentionAndActorEnvelope()
    {
        var observed = new List<(string Body, string Key, string Retention)>();
        var bytes = Encoding.UTF8.GetBytes("name\r\nOriginal customer\r\n"); var hash = Convert.ToHexString(SHA256.HashData(bytes));
        using var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal("text/csv", request.Content!.Headers.ContentType!.MediaType);
            observed.Add((await request.Content.ReadAsStringAsync(ct), request.Headers.GetValues("Idempotency-Key").Single(), request.Headers.GetValues("X-Tenant-Import-Retention").Single()));
            if (observed.Count == 1) throw new HttpRequestException("private object provider diagnostic");
            return Json(new ImportPlanReceipt(Guid.NewGuid(), "customer-import/v1", hash, 1, 1, 0, 0));
        });
        using var http = new HttpClient(handler); var (api, tickets, auth, _) = await ClientAsync(http);
        var command = BusinessCommand.ImportCsv(Tenant, bytes, "archive"); Array.Fill<byte>(bytes, 42);
        Assert.True((await api.SendAsync<ImportPlanReceipt>(command, CustomerOperationsContracts.Plan)).Unknown);
        Assert.NotNull((await api.SendAsync<ImportPlanReceipt>(command, CustomerOperationsContracts.Plan)).Value);
        Assert.Equal(observed[0], observed[1]); Assert.Contains("Original customer", observed[1].Body, StringComparison.Ordinal); Assert.Equal("archive", observed[1].Retention);
        auth.Principal = await AddTicketAsync(tickets, "new-customer-session");
        Assert.Equal(BusinessFailure.SignInRequired, (await api.SendAsync<ImportPlanReceipt>(command, _ => true)).Failure); Assert.Equal(2, observed.Count);
    }

    [Fact]
    public void ImportBoundsDoNotExpandOrdinaryJsonOrPermitArbitraryRetention()
    {
        Assert.Throws<ArgumentException>(() => BusinessCommand.ImportCsv(Tenant, new byte[BusinessApiClient.MaximumImportBytes + 1], "default"));
        Assert.Throws<ArgumentException>(() => BusinessCommand.ImportCsv(Tenant, [1], "arbitrary"));
        Assert.Throws<ArgumentException>(() => BusinessCommand.Create(Tenant, "orders", new { text = new string('x', 8193) }));
        Assert.Throws<ArgumentException>(() => BusinessCommand.Create(Tenant, $"customers/imports/{Guid.NewGuid():D}/accept", new { text = new string('x', BusinessApiClient.MaximumImportDecisionBytes) }));
        _ = BusinessCommand.Create(Tenant, $"customers/imports/{Guid.NewGuid():D}/accept", new { decisions = new string('x', 9000) });
        Assert.Throws<ArgumentException>(() => BusinessCommand.Create(Tenant, "customers/imports/not-an-id/accept", new { text = new string('x', 9000) }));
    }

    [Fact]
    public async Task CsvDownloadKeepsFixedOriginBoundsAndExactRetainedSourceHash()
    {
        var bytes = Encoding.UTF8.GetBytes("name\r\n<script>Customer</script>\r\n");
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal("api.example.test", request.RequestUri!.Host); Assert.Equal("text/csv", request.Headers.Accept.Single().MediaType);
            Assert.False(request.Headers.Contains("Idempotency-Key")); Assert.NotNull(request.Headers.Authorization);
            var content = new ByteArrayContent(bytes); content.Headers.ContentType = new("text/csv"); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        var source = new ImportSource(bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), "text/csv", 3, 1, DateTimeOffset.UtcNow, null, null);
        Assert.NotNull((await api.GetCsvAsync(Tenant, $"customers/imports/{Guid.NewGuid():D}/source", x => CustomerOperationsContracts.Csv(x, source))).Value);
        Assert.Equal(BusinessFailure.InvalidResponse, (await api.GetCsvAsync(Tenant, "customers/import-template", x => CustomerOperationsContracts.Csv(x, source with { Sha256 = new string('0', 64) }))).Failure);
        Assert.Throws<ArgumentException>(() => { _ = api.GetCsvAsync(Tenant, "orders", _ => true); });
        Assert.Throws<ArgumentException>(() => { _ = api.GetCsvAsync(Tenant, "customers/imports/not-an-id/source", _ => true); });
        Assert.False(CustomerOperationsContracts.Csv(new([0xff])));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceCsvResponseCannotExceedItsBoundEvenWithoutContentLength(bool knownLength)
    {
        using var handler = new Handler((_, _) =>
        {
            var bytes = new byte[BusinessApiClient.MaximumImportBytes + 1]; HttpContent content = knownLength ? new ByteArrayContent(bytes) : new ChunkedContent(bytes);
            content.Headers.ContentType = new("text/csv"); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        Assert.Equal(BusinessFailure.InvalidResponse, (await api.GetCsvAsync(Tenant, $"customers/imports/{Guid.NewGuid():D}/source", _ => true)).Failure);
    }

    [Fact]
    public async Task AcceptedImportIsDurableWorkRatherThanCompletedCustomerCreation()
    {
        var import = Guid.NewGuid(); var work = new ImportWork(Guid.NewGuid(), import, 1, 0, 2, DateTimeOffset.UtcNow, null, null);
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = System.Net.Http.Json.JsonContent.Create(work) }));
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        var value = await api.SendAsync<ImportWork>(BusinessCommand.Create(Tenant, $"customers/imports/{import:D}/accept", new { decisions = Array.Empty<object>() }), x => CustomerOperationsContracts.Work(x, import));
        Assert.True(value.Accepted); Assert.Equal(1, value.Value!.Status); Assert.Contains("accepted for processing", value.Message, StringComparison.Ordinal);
        Assert.True((await api.SendAsync<ImportWork>(BusinessCommand.Create(Tenant, "customers/imports", new { }), _ => true)).Unknown);
    }

    [Fact]
    public void ImportAndDuplicateResponsesRejectCrossScopeOrMalformedEvidence()
    {
        var id = Guid.NewGuid(); var work = new ImportWork(Guid.NewGuid(), id, 1, 0, 2, DateTimeOffset.UtcNow, null, null);
        var summary = new ImportSummary(id, "customer-import/v1", new string('a', 64), 2, 2, 0, 0, 0, 0, work, null);
        Assert.True(CustomerOperationsContracts.Summary(summary, id)); Assert.False(CustomerOperationsContracts.Summary(summary, Guid.NewGuid()));
        Assert.False(CustomerOperationsContracts.Summary(summary with { Imported = 1 }, id)); Assert.False(CustomerOperationsContracts.Summary(summary with { ContractVersion = "customer-import/v9" }, id));
        Assert.False(CustomerOperationsContracts.Rows(new([], null), 0, 2));
        Assert.True(CustomerOperationsContracts.Rows(new([], null), 0, 0));
        Assert.False(CustomerOperationsContracts.Work(work with { Status = 0 }, id)); Assert.False(CustomerOperationsContracts.Rows(new([], 2), 0, 2));
        var resolution = new DuplicateResolution(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, Guid.NewGuid(), DateTimeOffset.UtcNow, "Reviewed", "ExactName");
        Assert.True(CustomerOperationsContracts.Receipt(new(1, resolution, null), resolution.CustomerId, resolution.OtherCustomerId, 2, false));
        Assert.False(CustomerOperationsContracts.Receipt(new(1, resolution, null), resolution.OtherCustomerId, resolution.CustomerId, 2, false));
        Assert.False(CustomerOperationsContracts.Receipt(new(1, resolution, null), resolution.CustomerId, resolution.OtherCustomerId, 3, true));
    }
}
