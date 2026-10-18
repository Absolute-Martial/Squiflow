using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.CoreApi.Composition;
using Application.Invoices;
using Application.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class CustomerSettlementEndpointTests : IDisposable
{
    private const string PrivateEvidence = "private-payment-response-canary";
    private readonly WhiteLabelApiFactory baseline = new();
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid payment = Guid.NewGuid();
    private readonly Guid account = Guid.NewGuid();
    private readonly Guid debtor = Guid.NewGuid();

    [Fact]
    public async Task MissingCurrentPermissionDeniesBeforeMalformedBodyAndStoreExecution()
    {
        var store = new RecordingStore(); var authorization = new RecordingAuthorization { Allowed = false };
        using var host = Host(store, authorization); using var client = host.CreateClient();
        using var request = Request(HttpMethod.Put, Path(), "{malformed");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("number-string")]
    [InlineData("array")]
    [InlineData("deep")]
    [InlineData("null")]
    public async Task StrictBoundedPayloadsRejectAmbiguousShapesBeforeStore(string variation)
    {
        var store = new RecordingStore(); using var host = Host(store); using var client = host.CreateClient();
        var valid = JsonSerializer.Serialize(Record());
        var body = variation switch
        {
            "unknown" => valid[..^1] + ",\"actorAccountId\":\"forged\"}",
            "duplicate" => valid[..^1] + ",\"Amount\":2}",
            "missing" => valid.Replace("\"currencyCode\":\"NPR\",", "", StringComparison.Ordinal),
            "number-string" => valid.Replace("\"amount\":1", "\"amount\":\"1\"", StringComparison.Ordinal),
            "array" => "[]",
            "deep" => "{\"debtorKind\":{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":1}}}}}}}",
            "null" => "null",
            _ => throw new InvalidOperationException(),
        };
        using var request = Request(HttpMethod.Put, Path(), body);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("financial_request_invalid", await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredAndChunkedBodiesCannotExceedTheActualByteBound(bool chunked)
    {
        var store = new RecordingStore(); using var host = Host(store); using var client = host.CreateClient();
        using var request = Request(HttpMethod.Put, Path());
        var bytes = new byte[CustomerSettlementRoutes.MaximumRequestBodyBytes + 1];
        request.Content = chunked ? new StreamContent(new NonSeekableBody(bytes)) : new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (chunked) request.Headers.TransferEncodingChunked = true;
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("request_too_large", await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData("?unsupported=1")]
    [InlineData("?cursor=x&cursor=y")]
    [InlineData("?debtorKind=Organization")]
    [InlineData("?debtorId=11111111-1111-1111-1111-111111111111")]
    [InlineData("?debtorKind=4&debtorId=11111111-1111-1111-1111-111111111111")]
    public async Task FinancialBrowseDoesNotAcceptUnknownDuplicateOrPartialFilters(string query)
    {
        var store = new RecordingStore(); using var host = Host(store); using var client = host.CreateClient();
        using var request = Request(HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/receivables" + query);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task RecordAndReplayReturnOnlyTheRetainedCommandReceipt()
    {
        var store = new RecordingStore(); using var host = Host(store); using var client = host.CreateClient();
        using (var request = Request(HttpMethod.Put, Path(), JsonSerializer.Serialize(Record())))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(Path(), response.Headers.Location?.OriginalString);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(PrivateEvidence, text, StringComparison.Ordinal);
            using var body = JsonDocument.Parse(text);
            Assert.Equal(3, body.RootElement.EnumerateObject().Count());
            Assert.Equal(payment, body.RootElement.GetProperty("paymentId").GetGuid());
        }
        store.Replay = true;
        using var replay = Request(HttpMethod.Put, Path(), JsonSerializer.Serialize(Record()));
        using var replayed = await client.SendAsync(replay);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.Equal("true", replayed.Headers.GetValues("Idempotency-Replayed").Single());
    }

    [Theory]
    [InlineData("payment")]
    [InlineData("receivable")]
    [InlineData("payments")]
    [InlineData("receivables")]
    [InlineData("history")]
    [InlineData("command")]
    public async Task AProviderResultForAnotherResourceFailsWithoutDisclosingFinancialFacts(string operation)
    {
        var store = new RecordingStore { WrongScope = true }; using var host = Host(store); using var client = host.CreateClient();
        var path = operation switch
        {
            "payment" or "command" => Path(),
            "receivable" => $"/api/v1/tenants/{tenant:D}/receivables/{payment:D}",
            "payments" => $"/api/v1/tenants/{tenant:D}/customer-payments",
            "receivables" => $"/api/v1/tenants/{tenant:D}/receivables",
            "history" => Path() + "/history",
            _ => throw new InvalidOperationException(),
        };
        using var request = Request(operation == "command" ? HttpMethod.Put : HttpMethod.Get, path,
            operation == "command" ? JsonSerializer.Serialize(Record()) : null);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain(PrivateEvidence, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizationOutageFailsClosedBeforeFinancialExecution()
    {
        var store = new RecordingStore(); using var host = Host(store, new RecordingAuthorization { Unavailable = true }); using var client = host.CreateClient();
        using var request = Request(HttpMethod.Put, Path(), "{");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain(PrivateEvidence, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task OpenApiRequiresIdempotencyForEveryFinancialMutationAndNoneOfItsReads()
    {
        using var host = Host(new RecordingStore()); using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var checkedOperations = 0;
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject().Where(path => path.Name.Contains("customer-payments", StringComparison.Ordinal)))
            foreach (var operation in path.Value.EnumerateObject().Where(operation => operation.Name is "get" or "post" or "put"))
            {
                checkedOperations++;
                Assert.NotEmpty(operation.Value.GetProperty("security").EnumerateArray());
                var idempotency = operation.Value.TryGetProperty("parameters", out var parameters)
                    ? parameters.EnumerateArray().Where(value => value.TryGetProperty("name", out var name) && name.GetString() == "Idempotency-Key").ToArray() : [];
                if (operation.Name == "get") Assert.Empty(idempotency);
                else
                {
                    Assert.True(Assert.Single(idempotency).GetProperty("required").GetBoolean());
                    Assert.True(operation.Value.GetProperty("requestBody").TryGetProperty("content", out _));
                }
            }
        Assert.True(checkedOperations >= 10);
    }

    private string Path() => $"/api/v1/tenants/{tenant:D}/customer-payments/{payment:D}";
    private object Record() => new { debtorKind = "Organization", debtorId = debtor, currencyCode = "NPR", amount = 1m, channel = "Cash", evidence = PrivateEvidence };

    private WebApplicationFactory<Program> Host(RecordingStore store, RecordingAuthorization? authorization = null)
    {
        baseline.Bind(account.ToString("D"), account); baseline.AddTenantMembership(account, tenant, "Financial wire tenant");
        return baseline.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerSettlementStore>(); services.AddSingleton<ICustomerSettlementStore>(store);
            services.RemoveAll<ITenantFinancialAuthorization>(); services.AddSingleton<ITenantFinancialAuthorization>(authorization ?? new());
        }));
    }

    private HttpRequestMessage Request(HttpMethod method, string path, string? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseline.CreateToken(account.ToString("D")));
        if (method != HttpMethod.Get) request.Headers.Add("Idempotency-Key", "financial-wire-request");
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    public void Dispose() => baseline.Dispose();

    private sealed class RecordingAuthorization : ITenantFinancialAuthorization
    {
        public bool Allowed { get; init; } = true;
        public bool Unavailable { get; init; }
        public Task<bool> IsAllowedAsync(Guid actor, Guid tenant, FinancialCapability permission, CancellationToken ct) => Unavailable
            ? throw new AuthorizationProviderUnavailableException("Synthetic authorization failure.", new InvalidOperationException(PrivateEvidence))
            : Task.FromResult(Allowed);
    }

    private sealed class RecordingStore : ICustomerSettlementStore
    {
        public int Calls { get; private set; }
        public bool Replay { get; set; }
        public bool WrongScope { get; init; }
        private readonly Guid unrelated = Guid.NewGuid();
        private Guid Identity(Guid expected) => WrongScope ? unrelated : expected;
        private CustomerPaymentSnapshot Payment(TenantContext context, Guid id) => new(Identity(id), Identity(context.TenantId),
            new(InvoiceDebtorKind.Organization, unrelated), "NPR", 1m, CustomerPaymentChannel.Cash, PrivateEvidence, null,
            context.AccountId, DateTimeOffset.UtcNow, 1, CustomerPaymentState.Pending, 0m, null);
        private ReceivableSnapshot Receivable(TenantContext context, Guid id) => new(Identity(id), Identity(context.TenantId),
            new(InvoiceDebtorKind.Organization, unrelated), "NPR", 1m, new(2026, 10, 15), DateTimeOffset.UtcNow, 0m);
        public Task<FinancialResult<FinancialCommandReceipt>> RecordAsync(TenantContext context, Guid id, CustomerPaymentRecord record, string key, CancellationToken ct)
        {
            Calls++; CustomerSettlementRules.Normalize(record);
            return Task.FromResult(new FinancialResult<FinancialCommandReceipt>(Replay ? FinancialStatus.Replayed : FinancialStatus.Applied, new(Identity(id), unrelated, 1)));
        }
        public Task<FinancialResult<FinancialCommandReceipt>> ExecuteAsync(TenantContext context, Guid id, CustomerPaymentMutation mutation, string key, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<FinancialResult<CustomerPaymentSnapshot>> ReadPaymentAsync(TenantContext context, Guid id, CancellationToken ct)
        { Calls++; return Task.FromResult(new FinancialResult<CustomerPaymentSnapshot>(FinancialStatus.Found, Payment(context, id))); }
        public Task<FinancialResult<ReceivableSnapshot>> ReadReceivableAsync(TenantContext context, Guid id, CancellationToken ct)
        { Calls++; return Task.FromResult(new FinancialResult<ReceivableSnapshot>(FinancialStatus.Found, Receivable(context, id))); }
        public Task<FinancialResult<FinancialPage<CustomerPaymentSnapshot>>> BrowsePaymentsAsync(TenantContext context, FinancialDebtor? debtor, string? cursor, CancellationToken ct)
        { Calls++; return Task.FromResult(new FinancialResult<FinancialPage<CustomerPaymentSnapshot>>(FinancialStatus.Found, new([Payment(context, unrelated)], null))); }
        public Task<FinancialResult<FinancialPage<ReceivableSnapshot>>> BrowseReceivablesAsync(TenantContext context, FinancialDebtor? debtor, string? cursor, CancellationToken ct)
        { Calls++; return Task.FromResult(new FinancialResult<FinancialPage<ReceivableSnapshot>>(FinancialStatus.Found, new([Receivable(context, unrelated)], null))); }
        public Task<FinancialResult<FinancialPage<CustomerPaymentEvent>>> ReadHistoryAsync(TenantContext context, Guid id, string? cursor, CancellationToken ct)
        {
            Calls++; return Task.FromResult(new FinancialResult<FinancialPage<CustomerPaymentEvent>>(FinancialStatus.Found, new([new(unrelated, Identity(id), 1, CustomerPaymentOperation.Record,
            CustomerPaymentState.Pending, context.AccountId, DateTimeOffset.UtcNow, 1, null, null, null, null)], null)));
        }
        public Task<FinancialResult<DebtorBalance>> ReadBalanceAsync(TenantContext context, FinancialDebtor debtor, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class NonSeekableBody(byte[] bytes) : Stream
    {
        private readonly MemoryStream source = new(bytes);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => source.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }
}
