using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.Orders;
using Application.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OrderDraftHistoryEndpointTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;

    public OrderDraftHistoryEndpointTests(WhiteLabelApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CurrentViewerReceivesHistoricalPricesAndActorWithoutReceiptSecretsAndPermissionIsRechecked()
    {
        var (account, tenant, token) = GrantViewer();
        var order = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var recordedAt = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var snapshot = new OrderDraftSnapshot(order, tenant, actor, "Historical draft", "USD", 20m, 2,
            recordedAt, [new OrderDraftLine(1, "Historical line", 2m, "EA", 10m, 20m)]);
        var store = new RecordingHistoryStore(new OrderDraftHistoryPage(3,
            [new OrderDraftHistoryEntry(OrderDraftChange.Revised, actor, recordedAt, snapshot)], 2));
        using var host = CreateHost(store);
        using var client = host.CreateClient();
        using var request = Request(tenant, order, token, "?limit=1&beforeRevision=3");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, document.RootElement.GetProperty("currentRevision").GetInt64());
        Assert.Equal(2, document.RootElement.GetProperty("nextBeforeRevision").GetInt64());
        var entry = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("revised", entry.GetProperty("change").GetString());
        Assert.Equal(actor, entry.GetProperty("changedByAccountId").GetGuid());
        Assert.Equal(recordedAt, entry.GetProperty("recordedAt").GetDateTimeOffset());
        Assert.Equal(10m, entry.GetProperty("order").GetProperty("lines")[0].GetProperty("unitPrice").GetDecimal());
        Assert.False(entry.TryGetProperty("fingerprint", out _));
        Assert.False(entry.TryGetProperty("idempotencyKey", out _));
        Assert.False(entry.TryGetProperty("responseJson", out _));
        Assert.Equal(tenant, store.Context!.TenantId);
        Assert.Equal(account, store.Context.AccountId);
        Assert.Equal(new GetOrderDraftHistoryRequest(order, 1, 3), store.Request);
        Assert.Equal(1, store.Calls);

        _factory.SetOrderViewDecision(account, tenant, allowed: false);
        using var revoked = Request(tenant, order, token);
        using var denied = await client.SendAsync(revoked);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(denied.Headers.CacheControl?.NoStore);
        Assert.Equal(1, store.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MembershipAndPermissionAreCheckedBeforeInvalidQueryOrHistoryAccess(bool member)
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var subject = account.ToString("D");
        _factory.Bind(subject, account);
        if (member)
            _factory.AddTenantMembership(account, tenant, "History tenant");
        _factory.SetOrderViewDecision(account, tenant, allowed: false);
        var store = new RecordingHistoryStore(null);
        using var host = CreateHost(store);
        using var client = host.CreateClient();
        using var request = Request(tenant, Guid.NewGuid(), _factory.CreateToken(subject), "?limit=invalid");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(member ? "tenant_permission_denied" : "tenant_access_denied", await Code(response));
        Assert.Equal(0, store.Calls);
        Assert.Equal(member ? 1 : 0, _factory.GetOrderViewCheckCount(account, tenant));
    }

    [Fact]
    public async Task AuthorizationOutageFailsClosedBeforeHistoryAccess()
    {
        var (account, tenant, token) = GrantViewer();
        _factory.SetOrderViewUnavailable(account, tenant);
        var store = new RecordingHistoryStore(null);
        using var host = CreateHost(store);
        using var client = host.CreateClient();
        using var request = Request(tenant, Guid.NewGuid(), token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("authorization_unavailable", await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData("?limit=0", "limit_invalid")]
    [InlineData("?limit=11", "limit_invalid")]
    [InlineData("?limit=1&limit=2", "limit_invalid")]
    [InlineData("?limit=2147483648", "limit_invalid")]
    [InlineData("?beforeRevision=0", "history_revision_invalid")]
    [InlineData("?beforeRevision=-1", "history_revision_invalid")]
    [InlineData("?beforeRevision=1&beforeRevision=2", "history_revision_invalid")]
    [InlineData("?beforeRevision=9223372036854775808", "history_revision_invalid")]
    public async Task InvalidHistoryQueryNeverReachesStorage(string query, string code)
    {
        var (_, tenant, token) = GrantViewer();
        var store = new RecordingHistoryStore(null);
        using var host = CreateHost(store);
        using var client = host.CreateClient();
        using var request = Request(tenant, Guid.NewGuid(), token, query);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task MissingOrderReturnsTheSameSafeNotFoundContract()
    {
        var (_, tenant, token) = GrantViewer();
        var store = new RecordingHistoryStore(null);
        using var host = CreateHost(store);
        using var client = host.CreateClient();
        using var request = Request(tenant, Guid.NewGuid(), token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("order_not_found", await Code(response));
        Assert.Equal(5, store.Request!.Limit);
        Assert.Null(store.Request.BeforeRevision);
    }

    [Fact]
    public async Task InvalidPersistedHistoryProducesSafeInternalErrorWithoutItsContents()
    {
        var (_, tenant, token) = GrantViewer();
        var store = new RecordingHistoryStore(null) { Fail = true };
        using var host = CreateHost(store);
        using var client = host.CreateClient();
        using var request = Request(tenant, Guid.NewGuid(), token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("internal_error", await Code(response));
        Assert.DoesNotContain("private-history-payload", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task OpenApiDescribesProtectedBoundedHistoryQueryAndFailureResponses()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/tenants/{tenantId}/orders/{orderId}/history").GetProperty("get");
        Assert.True(operation.TryGetProperty("security", out _));
        var parameters = operation.GetProperty("parameters").EnumerateArray().ToArray();
        var limit = Assert.Single(parameters, parameter => parameter.GetProperty("name").GetString() == "limit").GetProperty("schema");
        Assert.Equal(1, limit.GetProperty("minimum").GetInt32());
        Assert.Equal(10, limit.GetProperty("maximum").GetInt32());
        Assert.Equal(5, limit.GetProperty("default").GetInt32());
        Assert.Contains(parameters, parameter => parameter.GetProperty("name").GetString() == "beforeRevision");
        foreach (var status in new[] { "200", "400", "401", "403", "404", "500", "503", "504" })
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateHost(RecordingHistoryStore store) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOrderDraftHistoryStore>();
            services.AddSingleton<IOrderDraftHistoryStore>(store);
        }));

    private (Guid Account, Guid Tenant, string Token) GrantViewer()
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var subject = account.ToString("D");
        _factory.Bind(subject, account);
        _factory.AddTenantMembership(account, tenant, "History tenant");
        _factory.SetOrderViewDecision(account, tenant, allowed: true);
        return (account, tenant, _factory.CreateToken(subject));
    }

    private static HttpRequestMessage Request(Guid tenant, Guid order, string token, string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/orders/{order:D}/history{query}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed class RecordingHistoryStore(OrderDraftHistoryPage? page) : IOrderDraftHistoryStore
    {
        public int Calls { get; private set; }
        public bool Fail { get; init; }
        public TenantContext? Context { get; private set; }
        public GetOrderDraftHistoryRequest? Request { get; private set; }

        public Task<OrderDraftHistoryPage?> ListHistoryAsync(TenantContext context, GetOrderDraftHistoryRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Context = context;
            Request = request;
            if (Fail)
                throw new InvalidOperationException("private-history-payload");
            return Task.FromResult(page);
        }
    }
}
