using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.Invoices;
using Application.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class InvoiceEndpointTests(WhiteLabelApiFactory baseline) : IClassFixture<WhiteLabelApiFactory>
{
    [Fact]
    public async Task AuthenticationAndIndependentPermissionDenyBeforeMalformedBodyAndApplicationCalls()
    {
        var fixture = Fixture();
        using var host = Host(fixture.Authorization, fixture.Store);
        using var client = host.CreateClient();
        using (var anonymous = new HttpRequestMessage(HttpMethod.Post, Path(fixture.Tenant, fixture.Order)) { Content = Json("{") })
        using (var response = await client.SendAsync(anonymous))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
        using var denied = Request(HttpMethod.Post, Path(fixture.Tenant, fixture.Order), fixture.Token, "{");
        using var denial = await client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Forbidden, denial.StatusCode);
        Assert.True(denial.Headers.CacheControl?.NoStore);
        Assert.Equal(0, fixture.Store.Calls);
        fixture.Authorization.Issue = true;
        using var get = Request(HttpMethod.Get, $"/api/v1/tenants/{fixture.Tenant:D}/invoices/{fixture.Store.Invoice.InvoiceId:D}", fixture.Token);
        using var readDenied = await client.SendAsync(get);
        Assert.Equal(HttpStatusCode.Forbidden, readDenied.StatusCode);
        Assert.Equal(0, fixture.Store.Calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"expectedOrderRevision\":2,\"unexpected\":true}")]
    [InlineData("{\"expectedOrderRevision\":2,\"ExpectedOrderRevision\":2}")]
    [InlineData("{\"expectedOrderRevision\":2,\"debtor\":{}}")]
    [InlineData("{\"expectedOrderRevision\":2,\"debtor\":{\"kind\":\"Organization\",\"Kind\":\"Organization\"}}")]
    [InlineData("{\"expectedOrderRevision\":2,\"debtor\":{\"kind\":\"Organization\",\"total\":99}}")]
    [InlineData("{\"expectedOrderRevision\":2,\"debtor\":{\"kind\":\"organization\"}}")]
    [InlineData("{\"expectedOrderRevision\":2,\"debtor\":{\"kind\":\"Individual\"}}")]
    [InlineData("{\"expectedOrderRevision\":2,\"businessDate\":\"2026-10-10\"}")]
    [InlineData("{\"expectedOrderRevision\":2,\"debtor\":{\"kind\":\"Program\",\"individualId\":\"00000000-0000-0000-0000-000000000001\"}}")]
    [InlineData("{\"expectedOrderRevision\":2,\"x\":{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":1}}}}}}")]
    public async Task StrictInvoiceShapeRejectsUnknownDuplicateMissingAndUnsupportedFields(string body)
    {
        var fixture = Fixture(); fixture.Authorization.Issue = true;
        using var host = Host(fixture.Authorization, fixture.Store);
        using var client = host.CreateClient();
        using var request = Request(HttpMethod.Post, Path(fixture.Tenant, fixture.Order), fixture.Token, body);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invoice_request_invalid", await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, fixture.Store.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredAndChunkedSizeLimitsRejectBeforeApplicationCalls(bool chunked)
    {
        var fixture = Fixture(); fixture.Authorization.Issue = true;
        using var host = Host(fixture.Authorization, fixture.Store);
        using var client = host.CreateClient();
        using var request = Request(HttpMethod.Post, Path(fixture.Tenant, fixture.Order), fixture.Token);
        var body = Encoding.UTF8.GetBytes("{\"expectedOrderRevision\":2}".PadRight(8193, ' '));
        request.Content = chunked ? new UnknownLengthJsonContent(body) : new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (chunked) request.Headers.TransferEncodingChunked = true;
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("request_too_large", await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, fixture.Store.Calls);
    }

    [Fact]
    public async Task ReplayReturnsOnlyInvoiceIdentityAndFrozenReadsRequireTheIndependentViewRight()
    {
        var fixture = Fixture(); fixture.Authorization.Issue = true;
        using var host = Host(fixture.Authorization, fixture.Store);
        using var client = host.CreateClient();
        using (var request = Request(HttpMethod.Post, Path(fixture.Tenant, fixture.Order), fixture.Token, "{\"expectedOrderRevision\":2}"))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("true", response.Headers.GetValues("Idempotency-Replayed").Single());
            var text = await response.Content.ReadAsStringAsync();
            using var body = JsonDocument.Parse(text);
            Assert.Single(body.RootElement.EnumerateObject());
            Assert.Equal(fixture.Store.Invoice.InvoiceId.ToString("D"), body.RootElement.GetProperty("invoiceId").GetString());
            Assert.DoesNotContain("private-invoice-canary", text, StringComparison.Ordinal);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
        fixture.Authorization.Issue = false; fixture.Authorization.View = true;
        using var read = Request(HttpMethod.Get, Path(fixture.Tenant, fixture.Order), fixture.Token);
        using var readResponse = await client.SendAsync(read);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.True(readResponse.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await readResponse.Content.ReadAsStringAsync());
        Assert.Equal(10m, document.RootElement.GetProperty("total").GetDecimal());
        Assert.Equal("private-invoice-canary", document.RootElement.GetProperty("debtor").GetProperty("organizationDisplayName").GetString());
        using var issue = Request(HttpMethod.Post, Path(fixture.Tenant, fixture.Order), fixture.Token, "{");
        using var denied = await client.SendAsync(issue);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task MaximumPositiveOrderRevisionReachesTheOwningRevisionComparison()
    {
        var fixture = Fixture(); fixture.Authorization.Issue = true; fixture.Store.ReceiptMissing = true;
        using var host = Host(fixture.Authorization, fixture.Store);
        using var client = host.CreateClient();
        using var request = Request(HttpMethod.Post, Path(fixture.Tenant, fixture.Order), fixture.Token,
            "{\"expectedOrderRevision\":9223372036854775807}");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("invoice_order_revision_conflict", await Code(response));
        Assert.Equal(1, fixture.Store.SourceCalls);
    }

    [Fact]
    public async Task AuthorizationOutageHasSafeNoStoreFailureBeforeApplicationExecution()
    {
        var fixture = Fixture(); fixture.Authorization.Unavailable = true;
        using var host = Host(fixture.Authorization, fixture.Store);
        using var client = host.CreateClient();
        using var request = Request(HttpMethod.Post, Path(fixture.Tenant, fixture.Order), fixture.Token, "{");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("private-invoice-canary", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, fixture.Store.Calls);
    }

    private (Guid Tenant, Guid Order, string Token, RecordingAuthorization Authorization, RecordingStore Store) Fixture()
    {
        var account = Guid.NewGuid(); var tenant = Guid.NewGuid(); var order = Guid.NewGuid();
        var subject = account.ToString("D");
        baseline.Bind(subject, account); baseline.AddTenantMembership(account, tenant, "Invoice test tenant");
        var organization = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 10, 8, 0, 0, TimeSpan.Zero);
        var invoice = new InvoiceIssuedFacts(Guid.NewGuid(), tenant, order, 2, new(organization, null), "NPR",
            [new(1, "private-invoice-canary", 1m, "EA", 10m, 10m)], 10m,
            new InvoiceDebtorFacts.Organization(organization, "private-invoice-canary"), account, at, DateOnly.FromDateTime(at.UtcDateTime), 1);
        return (tenant, order, baseline.CreateToken(subject), new(), new(invoice));
    }

    private WebApplicationFactory<Program> Host(RecordingAuthorization authorization, RecordingStore store) => baseline.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantInvoiceAuthorization>(); services.AddSingleton<ITenantInvoiceAuthorization>(authorization);
            services.RemoveAll<IInvoiceIssueAuthority>(); services.AddSingleton<IInvoiceIssueAuthority>(new AllowedAuthority());
            services.RemoveAll<IInvoiceIssueStore>(); services.AddSingleton<IInvoiceIssueStore>(store);
            services.RemoveAll<IInvoiceIssuedFactsReader>(); services.AddSingleton<IInvoiceIssuedFactsReader>(store);
            services.RemoveAll<IInvoiceSourceOrderReader>(); services.AddSingleton<IInvoiceSourceOrderReader>(new RecordingSource(store));
        }));
    private static string Path(Guid tenant, Guid order) => $"/api/v1/tenants/{tenant:D}/orders/{order:D}/invoice";
    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
    private static HttpRequestMessage Request(HttpMethod method, string path, string token, string? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (method == HttpMethod.Post) request.Headers.Add("Idempotency-Key", "invoice-wire-test");
        if (body is not null) request.Content = Json(body);
        return request;
    }
    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("code").GetString();
    }
    private sealed class AllowedAuthority : IInvoiceIssueAuthority
    {
        public Task<InvoiceIssueAuthorityStatus> CheckAsync(TenantContext context, CancellationToken ct) => Task.FromResult(InvoiceIssueAuthorityStatus.Allowed);
    }
    private sealed class RecordingAuthorization : ITenantInvoiceAuthorization
    {
        public bool Issue { get; set; }
        public bool View { get; set; }
        public bool Unavailable { get; set; }
        public Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, TenantInvoicePermission permission, CancellationToken ct) =>
            Unavailable ? throw new AuthorizationProviderUnavailableException("private-invoice-canary", new InvalidOperationException()) :
                Task.FromResult(permission == TenantInvoicePermission.Issue ? Issue : View);
    }
    private sealed class RecordingStore(InvoiceIssuedFacts invoice) : IInvoiceIssueStore, IInvoiceIssuedFactsReader
    {
        public InvoiceIssuedFacts Invoice { get; } = invoice;
        public int Calls { get; private set; }
        public int SourceCalls { get; set; }
        public bool ReceiptMissing { get; set; }
        public Task<InvoiceIssueReceiptResult> FindReceiptAsync(TenantContext context, string key, string fingerprint, CancellationToken ct)
        { Calls++; return Task.FromResult(ReceiptMissing ? new InvoiceIssueReceiptResult(InvoiceIssueReceiptStatus.Missing) : new InvoiceIssueReceiptResult(InvoiceIssueReceiptStatus.Replayed, Invoice)); }
        public Task<InvoiceIssueCommitResult> CommitAsync(TenantContext context, InvoiceIssueCandidate candidate, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
        public Task<InvoiceReadResult> FindByIdAsync(TenantContext context, Guid id, CancellationToken ct)
        { Calls++; return Task.FromResult(new InvoiceReadResult(InvoiceReadStatus.Found, Invoice)); }
        public Task<InvoiceReadResult> FindByOrderAsync(TenantContext context, Guid id, CancellationToken ct) => FindByIdAsync(context, id, ct);
    }
    private sealed class RecordingSource(RecordingStore store) : IInvoiceSourceOrderReader
    {
        public Task<InvoiceSourceOrderReadResult> ReadAsync(TenantContext context, Guid orderId, CancellationToken ct)
        {
            store.SourceCalls++;
            var invoice = store.Invoice;
            return Task.FromResult(new InvoiceSourceOrderReadResult(InvoiceSourceOrderReadStatus.Found,
                new(invoice.TenantId, invoice.SourceOrderId, invoice.SourceOrderRevision, true, invoice.CurrencyCode,
                    invoice.SourceCustomerContext, invoice.Lines.Select(line => new InvoiceSourceLine(line.Position,
                        line.Description, line.Quantity, line.UnitCode, line.UnitPrice, line.LineTotal)), invoice.Total)));
        }
    }
    private sealed class UnknownLengthJsonContent(byte[] body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(body).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
