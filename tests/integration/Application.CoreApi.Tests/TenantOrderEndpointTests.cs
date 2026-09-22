using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class TenantOrderEndpointTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;
    private readonly HttpClient _client;

    public TenantOrderEndpointTests(WhiteLabelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CurrentMemberWithCreatePermissionCanCreateAnOrderDraft()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-create-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Order Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-create-subject"),
            "create-order-1",
            "Business cards");
        using var response = await _client.SendAsync(request);
        var order = await response.Content.ReadFromJsonAsync<OrderDraftContract>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.NotNull(order);
        Assert.Equal("Business cards", order.Summary);
        Assert.Equal("USD", order.CurrencyCode);
        Assert.Equal(25.00m, order.Total);
        Assert.Equal(1, order.Revision);
        Assert.Single(order.Lines);
        Assert.Equal(25.00m, order.Lines[0].LineTotal);
        Assert.Equal(
            $"/api/v1/tenants/{tenantId:D}/orders/{order.OrderId:D}",
            response.Headers.Location?.OriginalString);
        Assert.Equal(1, _factory.GetOrderCreateCheckCount(accountId, tenantId));
        Assert.Equal(1, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task SameIdempotencyKeyWithChangedIntentReturnsConflict()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-idempotency-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Idempotency Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var first = CreateRequest(
            tenantId,
            _factory.CreateToken("order-idempotency-subject"),
            "reused-order-key",
            "Original draft");
        using var firstResponse = await _client.SendAsync(first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var second = CreateRequest(
            tenantId,
            _factory.CreateToken("order-idempotency-subject"),
            "reused-order-key",
            "Changed draft");
        using var secondResponse = await _client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("idempotency_key_conflict", await ReadProblemCodeAsync(secondResponse));
        Assert.Equal(1, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task RepeatingTheSameIdempotencyKeyAndIntentReturnsTheCommittedOrderWithoutAnotherCreate()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-replay-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Replay Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var first = CreateRequest(
            tenantId,
            _factory.CreateToken("order-replay-subject"),
            "replay-order-key",
            "Repeatable draft");
        using var firstResponse = await _client.SendAsync(first);
        var firstOrder = await firstResponse.Content.ReadFromJsonAsync<OrderDraftContract>();

        using var retry = CreateRequest(
            tenantId,
            _factory.CreateToken("order-replay-subject"),
            "replay-order-key",
            "Repeatable draft");
        using var retryResponse = await _client.SendAsync(retry);
        var replayedOrder = await retryResponse.Content.ReadFromJsonAsync<OrderDraftContract>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.NotNull(firstOrder);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal("true", retryResponse.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.NotNull(replayedOrder);
        Assert.Equal(firstOrder.OrderId, replayedOrder.OrderId);
        Assert.Equal(firstOrder.Summary, replayedOrder.Summary);
        Assert.Equal(firstOrder.CurrencyCode, replayedOrder.CurrencyCode);
        Assert.Equal(firstOrder.Total, replayedOrder.Total);
        Assert.Equal(firstOrder.Revision, replayedOrder.Revision);
        Assert.Equal(firstOrder.Lines, replayedOrder.Lines);
        Assert.Equal(1, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task CurrentAccountsInTheSameTenantCanIndependentlyUseTheSameIdempotencyKey()
    {
        var firstAccountId = Guid.NewGuid();
        var secondAccountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("first-order-key-owner", firstAccountId);
        _factory.Bind("second-order-key-owner", secondAccountId);
        _factory.AddTenantMembership(firstAccountId, tenantId, "Shared Tenant");
        _factory.AddTenantMembership(secondAccountId, tenantId, "Shared Tenant");
        _factory.SetOrderCreateDecision(firstAccountId, tenantId, allowed: true);
        _factory.SetOrderCreateDecision(secondAccountId, tenantId, allowed: true);

        using var firstRequest = CreateRequest(
            tenantId,
            _factory.CreateToken("first-order-key-owner"),
            "shared-key",
            "First account draft");
        using var firstResponse = await _client.SendAsync(firstRequest);
        var firstOrder = await firstResponse.Content.ReadFromJsonAsync<OrderDraftContract>();

        using var secondRequest = CreateRequest(
            tenantId,
            _factory.CreateToken("second-order-key-owner"),
            "shared-key",
            "Second account draft");
        using var secondResponse = await _client.SendAsync(secondRequest);
        var secondOrder = await secondResponse.Content.ReadFromJsonAsync<OrderDraftContract>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotNull(firstOrder);
        Assert.NotNull(secondOrder);
        Assert.NotEqual(firstOrder.OrderId, secondOrder.OrderId);
        Assert.Equal(2, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task PermissionDenialPreventsTheOrderStoreFromReceivingTheRequest()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-permission-denied-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Denied Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: false);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-permission-denied-subject"),
            "denied-order-key",
            "Denied draft");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenant_permission_denied", await ReadProblemCodeAsync(response));
        Assert.Equal(1, _factory.GetOrderCreateCheckCount(accountId, tenantId));
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task AuthorizationProviderOutageReturnsASafeServiceUnavailableResponseBeforeStorage()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-provider-outage-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Unavailable Tenant");
        _factory.SetOrderCreateUnavailable(accountId, tenantId);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-provider-outage-subject"),
            "outage-order-key",
            "Unavailable draft");
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("authorization_unavailable", ReadProblemCode(body));
        Assert.DoesNotContain("Synthetic order authorization provider outage.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpRequestException", body, StringComparison.Ordinal);
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task MissingIdempotencyKeyReturnsStableBadRequestWithoutCreatingAnOrder()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-missing-key-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Missing Key Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-missing-key-subject"),
            idempotencyKey: null,
            "Missing key draft");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("idempotency_key_invalid", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task MultipleIdempotencyKeysReturnStableBadRequestWithoutCreatingAnOrder()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-multiple-keys-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Multiple Key Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-multiple-keys-subject"),
            idempotencyKey: null,
            "Multiple keys draft");
        request.Headers.Add("Idempotency-Key", ["first-order-key", "second-order-key"]);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("idempotency_key_invalid", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task OverlengthIdempotencyKeyReturnsStableBadRequestWithoutCreatingAnOrder()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-overlength-key-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Overlength Key Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-overlength-key-subject"),
            new string('x', 129),
            "Overlength key draft");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("idempotency_key_invalid", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task ControlCharacterIdempotencyKeyReturnsStableBadRequestWithoutCreatingAnOrder()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-control-key-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Control Key Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-control-key-subject"),
            idempotencyKey: null,
            "Control key draft");
        Assert.True(request.Headers.TryAddWithoutValidation("Idempotency-Key", "valid\u0001key"));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("idempotency_key_invalid", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task MalformedOrderPayloadReturnsStableBadRequestWithoutCreatingAnOrder()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-malformed-payload-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Malformed Payload Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-malformed-payload-subject"),
            "malformed-payload-key",
            content: new StringContent("{\"summary\":", Encoding.UTF8, "application/json"));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request_invalid", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task NullOrderPayloadReturnsStableBadRequestWithoutCreatingAnOrder()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-null-payload-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Null Payload Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-null-payload-subject"),
            "null-payload-key",
            content: JsonContent.Create<object?>(null));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request_invalid", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task MoreThanOneHundredOrderLinesReturnsStableBadRequestWithoutCreatingAnOrder()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-too-many-lines-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Line Limit Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-too-many-lines-subject"),
            "too-many-lines-key",
            content: JsonContent.Create(new
            {
                summary = "Over line limit",
                currencyCode = "USD",
                lines = Enumerable.Range(1, 101).Select(index => new
                {
                    description = $"Item {index}",
                    quantity = 1m,
                    unitCode = "EA",
                    unitPrice = 1m,
                }),
            }));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("lines_invalid", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task MissingCurrentMembershipPreventsOrderAuthorizationAndStorageAccess()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-nonmember-subject", accountId);
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);

        using var request = CreateRequest(
            tenantId,
            _factory.CreateToken("order-nonmember-subject"),
            "nonmember-key",
            "Unreachable draft");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenant_access_denied", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetOrderCreateCheckCount(accountId, tenantId));
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
    }

    [Fact]
    public async Task AuthorizedTenantCannotRetrieveAnOrderOwnedByAnotherTenant()
    {
        var accountId = Guid.NewGuid();
        var sourceTenantId = Guid.NewGuid();
        var requestingTenantId = Guid.NewGuid();
        _factory.Bind("order-cross-tenant-subject", accountId);
        _factory.AddTenantMembership(accountId, sourceTenantId, "Source Tenant");
        _factory.AddTenantMembership(accountId, requestingTenantId, "Requesting Tenant");
        _factory.SetOrderCreateDecision(accountId, sourceTenantId, allowed: true);
        _factory.SetOrderViewDecision(accountId, requestingTenantId, allowed: true);

        using var create = CreateRequest(
            sourceTenantId,
            _factory.CreateToken("order-cross-tenant-subject"),
            "cross-tenant-create-key",
            "Source order");
        using var createResponse = await _client.SendAsync(create);
        var sourceOrder = await createResponse.Content.ReadFromJsonAsync<OrderDraftContract>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(sourceOrder);

        using var get = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/tenants/{requestingTenantId:D}/orders/{sourceOrder.OrderId:D}");
        get.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _factory.CreateToken("order-cross-tenant-subject"));
        using var getResponse = await _client.SendAsync(get);

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal("order_not_found", await ReadProblemCodeAsync(getResponse));
    }

    [Fact]
    public async Task MissingCurrentMembershipPreventsOrderViewAuthorizationAndStorageAccess()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-view-nonmember-subject", accountId);
        _factory.SetOrderViewDecision(accountId, tenantId, allowed: true);

        using var request = GetRequest(
            tenantId,
            Guid.NewGuid(),
            _factory.CreateToken("order-view-nonmember-subject"));
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenant_access_denied", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetOrderViewCheckCount(accountId, tenantId));
        Assert.Equal(0, _factory.GetOrderFindCount(tenantId));
    }

    [Fact]
    public async Task OrderViewAuthorizationProviderOutageReturnsASafeServiceUnavailableResponseBeforeStorage()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-view-provider-outage-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "View Unavailable Tenant");
        _factory.SetOrderViewUnavailable(accountId, tenantId);

        using var request = GetRequest(
            tenantId,
            Guid.NewGuid(),
            _factory.CreateToken("order-view-provider-outage-subject"));
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("authorization_unavailable", ReadProblemCode(body));
        Assert.DoesNotContain("Synthetic order view authorization provider outage.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpRequestException", body, StringComparison.Ordinal);
        Assert.Equal(0, _factory.GetOrderFindCount(tenantId));
    }

    private static HttpRequestMessage CreateRequest(
        Guid tenantId,
        string token,
        string? idempotencyKey,
        string? summary = null,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/tenants/{tenantId:D}/orders")
        {
            Content = content ?? JsonContent.Create(new
            {
                summary,
                currencyCode = "usd",
                lines = new[]
                {
                    new
                    {
                        description = "Printed item",
                        quantity = 2m,
                        unitCode = "ea",
                        unitPrice = 12.50m,
                    },
                },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return request;
    }

    private static HttpRequestMessage GetRequest(Guid tenantId, Guid orderId, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/tenants/{tenantId:D}/orders/{orderId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        return ReadProblemCode(await response.Content.ReadAsStringAsync());
    }

    private static string? ReadProblemCode(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed record OrderDraftContract(
        Guid OrderId,
        string Summary,
        string CurrencyCode,
        decimal Total,
        long Revision,
        IReadOnlyList<OrderDraftLineContract> Lines);

    private sealed record OrderDraftLineContract(
        int Position,
        string Description,
        decimal Quantity,
        string UnitCode,
        decimal UnitPrice,
        decimal LineTotal);
}
