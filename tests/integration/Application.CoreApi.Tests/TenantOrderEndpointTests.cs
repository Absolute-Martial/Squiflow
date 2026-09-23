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

    [Fact]
    public async Task CurrentMemberCanBrowseAnEmptyTenantOrderDraftPage()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-browse-empty-subject", accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Empty Browse Tenant");
        _factory.SetOrderViewDecision(accountId, tenantId, allowed: true);

        using var response = await _client.SendAsync(BrowseRequest(
            tenantId,
            _factory.CreateToken("order-browse-empty-subject")));
        var page = await response.Content.ReadFromJsonAsync<OrderDraftPageContract>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
        Assert.Equal(1, _factory.GetOrderViewCheckCount(accountId, tenantId));
        Assert.Equal(1, _factory.GetOrderListCount(tenantId));
        Assert.Equal(25, _factory.GetLastOrderListRequest(tenantId)?.Limit);
        Assert.Null(_factory.GetLastOrderListRequest(tenantId)?.After);
    }

    [Fact]
    public async Task BrowseReturnsBoundedOpaqueCursorPageWithoutCreatorOrLineDetails()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        const string subject = "order-browse-page-subject";
        _factory.Bind(subject, accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Paged Browse Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);
        _factory.SetOrderViewDecision(accountId, tenantId, allowed: true);

        foreach (var summary in new[] { "First draft", "Second draft", "Third draft" })
        {
            using var create = CreateRequest(tenantId, _factory.CreateToken(subject), $"browse-{summary}", summary);
            using var createResponse = await _client.SendAsync(create);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        }

        using var firstResponse = await _client.SendAsync(BrowseRequest(
            tenantId,
            _factory.CreateToken(subject),
            "limit=2"));
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<OrderDraftPageContract>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.All(firstPage.Items, item =>
        {
            Assert.NotEqual(Guid.Empty, item.OrderId);
            Assert.NotEmpty(item.Summary);
            Assert.Equal("USD", item.CurrencyCode);
            Assert.Equal(25.00m, item.Total);
            Assert.Equal(1, item.Revision);
        });
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextCursor));
        Assert.DoesNotContain(".", firstPage.NextCursor!, StringComparison.Ordinal);

        using var nextResponse = await _client.SendAsync(BrowseRequest(
            tenantId,
            _factory.CreateToken(subject),
            $"limit=2&after={firstPage.NextCursor}"));
        var nextPage = await nextResponse.Content.ReadFromJsonAsync<OrderDraftPageContract>();

        Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
        Assert.NotNull(nextPage);
        Assert.Single(nextPage.Items);
        Assert.Null(nextPage.NextCursor);
        Assert.Empty(firstPage.Items.Select(item => item.OrderId).Intersect(nextPage.Items.Select(item => item.OrderId)));
    }

    [Fact]
    public async Task BrowseRejectsMissingMembershipBeforeAuthorizationOrStorage()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _factory.Bind("order-browse-no-membership-subject", accountId);
        _factory.SetOrderViewDecision(accountId, tenantId, allowed: true);

        using var response = await _client.SendAsync(BrowseRequest(
            tenantId,
            _factory.CreateToken("order-browse-no-membership-subject")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenant_access_denied", await ReadProblemCodeAsync(response));
        Assert.Equal(0, _factory.GetOrderViewCheckCount(accountId, tenantId));
        Assert.Equal(0, _factory.GetOrderListCount(tenantId));
    }

    [Fact]
    public async Task BrowseDenialAndAuthorizationOutageDoNotReadStorage()
    {
        var deniedAccountId = Guid.NewGuid();
        var deniedTenantId = Guid.NewGuid();
        _factory.Bind("order-browse-denied-subject", deniedAccountId);
        _factory.AddTenantMembership(deniedAccountId, deniedTenantId, "Denied Browse Tenant");
        _factory.SetOrderViewDecision(deniedAccountId, deniedTenantId, allowed: false);

        using var denied = await _client.SendAsync(BrowseRequest(
            deniedTenantId,
            _factory.CreateToken("order-browse-denied-subject")));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("tenant_permission_denied", await ReadProblemCodeAsync(denied));
        Assert.Equal(0, _factory.GetOrderListCount(deniedTenantId));

        var unavailableAccountId = Guid.NewGuid();
        var unavailableTenantId = Guid.NewGuid();
        _factory.Bind("order-browse-unavailable-subject", unavailableAccountId);
        _factory.AddTenantMembership(unavailableAccountId, unavailableTenantId, "Unavailable Browse Tenant");
        _factory.SetOrderViewUnavailable(unavailableAccountId, unavailableTenantId);

        using var unavailable = await _client.SendAsync(BrowseRequest(
            unavailableTenantId,
            _factory.CreateToken("order-browse-unavailable-subject")));
        var unavailableBody = await unavailable.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("authorization_unavailable", ReadProblemCode(unavailableBody));
        Assert.DoesNotContain("Synthetic order view authorization provider outage.", unavailableBody, StringComparison.Ordinal);
        Assert.Equal(0, _factory.GetOrderListCount(unavailableTenantId));
    }

    [Theory]
    [InlineData("limit=0", "page_size_invalid")]
    [InlineData("limit=51", "page_size_invalid")]
    [InlineData("limit=two", "page_size_invalid")]
    [InlineData("limit=1&limit=2", "page_size_invalid")]
    [InlineData("after=not-a-cursor", "cursor_invalid")]
    [InlineData("after=", "cursor_invalid")]
    [InlineData("after=a&after=b", "cursor_invalid")]
    public async Task BrowseRejectsInvalidOrRepeatedPageParameters(string query, string expectedCode)
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var subject = $"order-browse-query-{Guid.NewGuid():N}";
        _factory.Bind(subject, accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Query Browse Tenant");
        _factory.SetOrderViewDecision(accountId, tenantId, allowed: true);

        using var response = await _client.SendAsync(BrowseRequest(tenantId, _factory.CreateToken(subject), query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, await ReadProblemCodeAsync(response));
        Assert.Equal(1, _factory.GetOrderViewCheckCount(accountId, tenantId));
        Assert.Equal(0, _factory.GetOrderListCount(tenantId));
    }

    [Fact]
    public async Task BrowseRejectsAnUnsupportedOpaqueCursorVersionAfterAuthorization()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        const string subject = "order-browse-unsupported-cursor-subject";
        _factory.Bind(subject, accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Unsupported Cursor Tenant");
        _factory.SetOrderViewDecision(accountId, tenantId, allowed: true);
        var unsupportedCursor = ToBase64Url(
            $"v2:{tenantId:N}:{DateTimeOffset.UtcNow.Ticks}:{Guid.NewGuid():N}");

        using var response = await _client.SendAsync(BrowseRequest(
            tenantId,
            _factory.CreateToken(subject),
            $"after={unsupportedCursor}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("cursor_invalid", await ReadProblemCodeAsync(response));
        Assert.Equal(1, _factory.GetOrderViewCheckCount(accountId, tenantId));
        Assert.Equal(0, _factory.GetOrderListCount(tenantId));
    }

    [Fact]
    public async Task BrowseRejectsAnOversizedCursorBeforeStorage()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        const string subject = "order-browse-oversized-cursor-subject";
        _factory.Bind(subject, accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Oversized Cursor Tenant");
        _factory.SetOrderViewDecision(accountId, tenantId, allowed: true);

        using var response = await _client.SendAsync(BrowseRequest(
            tenantId,
            _factory.CreateToken(subject),
            $"after={new string('a', OrderDraftPageCursorCodec.MaximumEncodedLength + 1)}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("cursor_invalid", await ReadProblemCodeAsync(response));
        Assert.Equal(1, _factory.GetOrderViewCheckCount(accountId, tenantId));
        Assert.Equal(0, _factory.GetOrderListCount(tenantId));
    }

    [Fact]
    public async Task BrowseRejectsAContinuationIssuedForAnotherTenant()
    {
        var accountId = Guid.NewGuid();
        var sourceTenantId = Guid.NewGuid();
        var requestingTenantId = Guid.NewGuid();
        const string subject = "order-browse-foreign-cursor-subject";
        _factory.Bind(subject, accountId);
        _factory.AddTenantMembership(accountId, sourceTenantId, "Cursor Source Tenant");
        _factory.AddTenantMembership(accountId, requestingTenantId, "Cursor Request Tenant");
        _factory.SetOrderCreateDecision(accountId, sourceTenantId, allowed: true);
        _factory.SetOrderViewDecision(accountId, sourceTenantId, allowed: true);
        _factory.SetOrderViewDecision(accountId, requestingTenantId, allowed: true);
        var token = _factory.CreateToken(subject);

        using (var firstCreate = CreateRequest(sourceTenantId, token, "foreign-cursor-order-1", "First source draft"))
        using (var firstResponse = await _client.SendAsync(firstCreate))
        {
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        }

        using (var secondCreate = CreateRequest(sourceTenantId, token, "foreign-cursor-order-2", "Second source draft"))
        using (var secondResponse = await _client.SendAsync(secondCreate))
        {
            Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        }

        using var sourceResponse = await _client.SendAsync(BrowseRequest(sourceTenantId, token, "limit=1"));
        var sourcePage = await sourceResponse.Content.ReadFromJsonAsync<OrderDraftPageContract>();
        Assert.Equal(HttpStatusCode.OK, sourceResponse.StatusCode);
        Assert.NotNull(sourcePage);
        Assert.False(string.IsNullOrWhiteSpace(sourcePage.NextCursor));

        using var response = await _client.SendAsync(BrowseRequest(
            requestingTenantId,
            token,
            $"after={sourcePage.NextCursor}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("cursor_invalid", await ReadProblemCodeAsync(response));
        Assert.Equal(1, _factory.GetOrderViewCheckCount(accountId, requestingTenantId));
        Assert.Equal(0, _factory.GetOrderListCount(requestingTenantId));
    }

    [Fact]
    public async Task BrowseNeverReturnsAnotherTenantOrders()
    {
        var accountId = Guid.NewGuid();
        var sourceTenantId = Guid.NewGuid();
        var requestingTenantId = Guid.NewGuid();
        const string subject = "order-browse-cross-tenant-subject";
        _factory.Bind(subject, accountId);
        _factory.AddTenantMembership(accountId, sourceTenantId, "Source Browse Tenant");
        _factory.AddTenantMembership(accountId, requestingTenantId, "Requesting Browse Tenant");
        _factory.SetOrderCreateDecision(accountId, sourceTenantId, allowed: true);
        _factory.SetOrderViewDecision(accountId, requestingTenantId, allowed: true);

        using var create = CreateRequest(
            sourceTenantId,
            _factory.CreateToken(subject),
            "cross-tenant-browse-order",
            "Source-only draft");
        using var createResponse = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var response = await _client.SendAsync(BrowseRequest(
            requestingTenantId,
            _factory.CreateToken(subject)));
        var page = await response.Content.ReadFromJsonAsync<OrderDraftPageContract>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(1, _factory.GetOrderListCount(requestingTenantId));
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

    private static HttpRequestMessage BrowseRequest(Guid tenantId, string token, string? query = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/tenants/{tenantId:D}/orders{(string.IsNullOrEmpty(query) ? string.Empty : $"?{query}")}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string ToBase64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

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

    private sealed record OrderDraftPageContract(
        IReadOnlyList<OrderDraftListItemContract> Items,
        string? NextCursor);

    private sealed record OrderDraftListItemContract(
        Guid OrderId,
        string Summary,
        string CurrencyCode,
        decimal Total,
        long Revision,
        DateTimeOffset CreatedAt);
}
