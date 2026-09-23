using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OrderAbandonEndpointTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;
    private readonly HttpClient _client;

    public OrderAbandonEndpointTests(WhiteLabelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AbandonReturnsStateRevisionAndTimestampOnDetailBrowseAndReplay()
    {
        var (accountId, tenantId, token, orderId, createKey) = await CreateDraftAsync();
        _factory.SetOrderAbandonDecision(accountId, tenantId, allowed: true);
        _factory.SetOrderViewDecision(accountId, tenantId, allowed: true);

        using var first = Request(tenantId, orderId, token, "abandon-a", "{\"expectedRevision\":1}");
        using var firstResponse = await _client.SendAsync(first);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal("no-store", firstResponse.Headers.CacheControl?.ToString());
        using var firstBody = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var firstOrder = firstBody.RootElement;
        Assert.Equal("abandoned", firstOrder.GetProperty("state").GetString());
        Assert.Equal(2, firstOrder.GetProperty("revision").GetInt64());
        Assert.True(firstOrder.GetProperty("abandonedAt").GetDateTimeOffset() > DateTimeOffset.MinValue);
        Assert.False(firstOrder.TryGetProperty("abandonedByAccountId", out _));
        Assert.False(firstOrder.TryGetProperty("summary", out _));
        Assert.False(firstOrder.TryGetProperty("currencyCode", out _));
        Assert.False(firstOrder.TryGetProperty("total", out _));
        Assert.False(firstOrder.TryGetProperty("lines", out _));

        using var retry = Request(tenantId, orderId, token, "abandon-a", "{\"expectedRevision\":1}");
        using var replay = await _client.SendAsync(retry);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal("no-store", replay.Headers.CacheControl?.ToString());
        Assert.Equal(await firstResponse.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());

        using var detailRequest = new HttpRequestMessage(HttpMethod.Get, OrderPath(tenantId, orderId));
        detailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var detail = await _client.SendAsync(detailRequest);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailBody = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal("abandoned", detailBody.RootElement.GetProperty("state").GetString());
        Assert.Equal(2, detailBody.RootElement.GetProperty("revision").GetInt64());

        using var browseRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tenants/{tenantId:D}/orders");
        browseRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var browse = await _client.SendAsync(browseRequest);
        Assert.Equal(HttpStatusCode.OK, browse.StatusCode);
        using var browseBody = JsonDocument.Parse(await browse.Content.ReadAsStringAsync());
        var item = browseBody.RootElement.GetProperty("items").EnumerateArray()
            .Single(value => value.GetProperty("orderId").GetGuid() == orderId);
        Assert.Equal("abandoned", item.GetProperty("state").GetString());
        Assert.Equal(2, item.GetProperty("revision").GetInt64());
        Assert.Equal(firstOrder.GetProperty("abandonedAt").GetString(), item.GetProperty("abandonedAt").GetString());
        Assert.Equal(2, _factory.GetOrderAbandonCount(tenantId));

        using var createRetry = CreateRequest(tenantId, token, createKey);
        using var createReplay = await _client.SendAsync(createRetry);
        Assert.Equal(HttpStatusCode.OK, createReplay.StatusCode);
        using var replayBody = JsonDocument.Parse(await createReplay.Content.ReadAsStringAsync());
        Assert.Equal(orderId, replayBody.RootElement.GetProperty("orderId").GetGuid());
        Assert.Equal("draft", replayBody.RootElement.GetProperty("state").GetString());
        Assert.Equal(1, replayBody.RootElement.GetProperty("revision").GetInt64());
        Assert.Equal(JsonValueKind.Null, replayBody.RootElement.GetProperty("abandonedAt").ValueKind);
    }

    [Fact]
    public async Task AbandonPermissionDoesNotGrantPricedDraftRead()
    {
        var (accountId, tenantId, token, orderId, _) = await CreateDraftAsync();
        _factory.SetOrderAbandonDecision(accountId, tenantId, allowed: true);
        using var request = Request(tenantId, orderId, token, "abandon-without-view", "{\"expectedRevision\":1}");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(4, body.RootElement.EnumerateObject().Count());
        Assert.Equal("abandoned", body.RootElement.GetProperty("state").GetString());

        using var read = new HttpRequestMessage(HttpMethod.Get, OrderPath(tenantId, orderId));
        read.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var readResponse = await _client.SendAsync(read);
        Assert.Equal(HttpStatusCode.Forbidden, readResponse.StatusCode);
        Assert.Equal("tenant_permission_denied", await CodeAsync(readResponse));
    }

    [Fact]
    public async Task AbandonRequiresCurrentIdentityMembershipAndDedicatedPermissionBeforeStorage()
    {
        var (accountId, tenantId, token, orderId, _) = await CreateDraftAsync();
        var before = _factory.GetOrderAbandonCount(tenantId);

        using var anonymous = Request(tenantId, orderId, null, "anonymous", "{\"expectedRevision\":1}");
        using var anonymousResponse = await _client.SendAsync(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var anonymousWrongMedia = Request(tenantId, orderId, null, "anonymous-media", "{\"expectedRevision\":1}");
        anonymousWrongMedia.Content!.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var anonymousWrongMediaResponse = await _client.SendAsync(anonymousWrongMedia);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousWrongMediaResponse.StatusCode);

        using var denied = Request(tenantId, orderId, token, "denied", "{\"expectedRevision\":1}");
        using var deniedResponse = await _client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        Assert.Equal("tenant_permission_denied", await CodeAsync(deniedResponse));

        var outsiderId = Guid.NewGuid();
        _factory.Bind("abandon-outsider", outsiderId);
        _factory.SetOrderAbandonDecision(outsiderId, tenantId, allowed: true);
        using var outsider = Request(tenantId, orderId, _factory.CreateToken("abandon-outsider"), "outsider", "{\"expectedRevision\":1}");
        using var outsiderResponse = await _client.SendAsync(outsider);
        Assert.Equal(HttpStatusCode.Forbidden, outsiderResponse.StatusCode);
        Assert.Equal("tenant_access_denied", await CodeAsync(outsiderResponse));
        Assert.Equal(0, _factory.GetOrderAbandonCheckCount(outsiderId, tenantId));
        Assert.Equal(before, _factory.GetOrderAbandonCount(tenantId));
        Assert.Equal(1, _factory.GetOrderAbandonCheckCount(accountId, tenantId));
    }

    [Fact]
    public async Task AuthorizationOutageAndCrossTenantAttemptNeverReachStorage()
    {
        var (accountId, tenantId, token, orderId, _) = await CreateDraftAsync();
        _factory.SetOrderAbandonUnavailable(accountId, tenantId);
        using var outage = Request(tenantId, orderId, token, "outage", "{\"expectedRevision\":1}");
        using var outageResponse = await _client.SendAsync(outage);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, outageResponse.StatusCode);
        Assert.Equal("authorization_unavailable", await CodeAsync(outageResponse));
        Assert.Equal(0, _factory.GetOrderAbandonCount(tenantId));

        var otherTenant = Guid.NewGuid();
        _factory.AddTenantMembership(accountId, otherTenant, "Another Tenant");
        _factory.SetOrderAbandonDecision(accountId, otherTenant, allowed: true);
        using var crossTenant = Request(otherTenant, orderId, token, "cross-tenant", "{\"expectedRevision\":1}");
        using var crossTenantResponse = await _client.SendAsync(crossTenant);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantResponse.StatusCode);
        Assert.Equal("order_not_found", await CodeAsync(crossTenantResponse));
    }

    [Fact]
    public async Task RevisionAlreadyAbandonedAndChangedKeyIntentReturnDistinctConflicts()
    {
        var (accountId, tenantId, token, orderId, _) = await CreateDraftAsync();
        _factory.SetOrderAbandonDecision(accountId, tenantId, allowed: true);
        using var stale = Request(tenantId, orderId, token, "stale", "{\"expectedRevision\":2}");
        using var staleResponse = await _client.SendAsync(stale);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.Equal("revision_conflict", await CodeAsync(staleResponse));

        using var first = Request(tenantId, orderId, token, "same", "{\"expectedRevision\":1}");
        using var firstResponse = await _client.SendAsync(first);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var changed = Request(tenantId, orderId, token, "same", "{\"expectedRevision\":2}");
        using var changedResponse = await _client.SendAsync(changed);
        Assert.Equal(HttpStatusCode.Conflict, changedResponse.StatusCode);
        Assert.Equal("idempotency_key_conflict", await CodeAsync(changedResponse));

        using var second = Request(tenantId, orderId, token, "different", "{\"expectedRevision\":1}");
        using var secondResponse = await _client.SendAsync(second);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("order_already_abandoned", await CodeAsync(secondResponse));
    }

    [Theory]
    [InlineData(null, "{\"expectedRevision\":1}", "idempotency_key_invalid")]
    [InlineData("key", "{}", "expected_revision_invalid")]
    [InlineData("key", "{\"expectedRevision\":0}", "expected_revision_invalid")]
    [InlineData("key", "{\"expectedRevision\":\"invalid\"}", "expected_revision_invalid")]
    [InlineData("key", "{\"expectedRevision\":", "request_invalid")]
    [InlineData("key", "{\"expectedRevision\":\"1\"}", "expected_revision_invalid")]
    [InlineData("key", "{\"expectedRevision\":1.0}", "expected_revision_invalid")]
    [InlineData("key", "{\"expectedRevision\":1,\"expectedRevision\":1}", "expected_revision_invalid")]
    [InlineData("key", "{\"expectedRevision\":1,\"other\":true}", "expected_revision_invalid")]
    [InlineData("key", "null", "expected_revision_invalid")]
    public async Task MalformedRequestsAreRejectedBeforeStorage(string? key, string body, string code)
    {
        var (accountId, tenantId, token, orderId, _) = await CreateDraftAsync();
        _factory.SetOrderAbandonDecision(accountId, tenantId, allowed: true);
        using var request = Request(tenantId, orderId, token, key, body);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await CodeAsync(response));
        Assert.Equal(0, _factory.GetOrderAbandonCount(tenantId));
    }

    [Fact]
    public async Task RepeatedAndOversizedKeysAndOversizedBodiesAreRejected()
    {
        var (accountId, tenantId, token, orderId, _) = await CreateDraftAsync();
        _factory.SetOrderAbandonDecision(accountId, tenantId, allowed: true);

        using var repeated = Request(tenantId, orderId, token, null, "{\"expectedRevision\":1}");
        repeated.Headers.TryAddWithoutValidation("Idempotency-Key", ["first", "second"]);
        using var repeatedResponse = await _client.SendAsync(repeated);
        Assert.Equal(HttpStatusCode.BadRequest, repeatedResponse.StatusCode);
        Assert.Equal("idempotency_key_invalid", await CodeAsync(repeatedResponse));

        using var oversizedKey = Request(tenantId, orderId, token, new string('a', 129), "{\"expectedRevision\":1}");
        using var oversizedKeyResponse = await _client.SendAsync(oversizedKey);
        Assert.Equal(HttpStatusCode.BadRequest, oversizedKeyResponse.StatusCode);
        Assert.Equal("idempotency_key_invalid", await CodeAsync(oversizedKeyResponse));

        using var oversizedBody = Request(tenantId, orderId, token, "large-body",
            "{\"expectedRevision\":1,\"padding\":\"" + new string('a', 1024) + "\"}");
        using var oversizedBodyResponse = await _client.SendAsync(oversizedBody);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversizedBodyResponse.StatusCode);

        using var chunkedBody = Request(tenantId, orderId, token, "large-chunked-body", "{\"expectedRevision\":1}");
        chunkedBody.Content = new UnknownLengthJsonContent(
            "{\"expectedRevision\":1,\"padding\":\"" + new string('a', 1024) + "\"}");
        Assert.Null(chunkedBody.Content.Headers.ContentLength);
        using var chunkedResponse = await _client.SendAsync(chunkedBody);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, chunkedResponse.StatusCode);
        Assert.Equal("request_too_large", await CodeAsync(chunkedResponse));
        Assert.Equal(0, _factory.GetOrderAbandonCount(tenantId));
    }

    [Fact]
    public async Task NonJsonMediaTypeIsRejectedBeforeStorage()
    {
        var (accountId, tenantId, token, orderId, _) = await CreateDraftAsync();
        _factory.SetOrderAbandonDecision(accountId, tenantId, allowed: true);
        using var request = Request(tenantId, orderId, token, "wrong-media", "{\"expectedRevision\":1}");
        request.Content!.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request_invalid", await CodeAsync(response));
        Assert.Equal(0, _factory.GetOrderAbandonCount(tenantId));
    }

    private async Task<(Guid AccountId, Guid TenantId, string Token, Guid OrderId, string CreateKey)> CreateDraftAsync()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var subject = $"abandon-{Guid.NewGuid():N}";
        var token = _factory.CreateToken(subject);
        _factory.Bind(subject, accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Orders Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);
        var createKey = $"create-{Guid.NewGuid():N}";
        using var create = CreateRequest(tenantId, token, createKey);
        using var response = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("draft", body.RootElement.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("abandonedAt").ValueKind);
        return (accountId, tenantId, token, body.RootElement.GetProperty("orderId").GetGuid(), createKey);
    }

    private static HttpRequestMessage CreateRequest(Guid tenantId, string token, string createKey)
    {
        var create = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenantId:D}/orders")
        {
            Content = JsonContent.Create(new
            {
                summary = "Draft",
                currencyCode = "USD",
                lines = new[] { new { description = "Item", quantity = 1m, unitCode = "ea", unitPrice = 2m } },
            }),
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        create.Headers.Add("Idempotency-Key", createKey);
        return create;
    }

    private static HttpRequestMessage Request(Guid tenantId, Guid orderId, string? token, string? key, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{OrderPath(tenantId, orderId)}/abandon")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return request;
    }

    private static string OrderPath(Guid tenantId, Guid orderId) =>
        $"/api/v1/tenants/{tenantId:D}/orders/{orderId:D}";

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("code").GetString();
    }

    private sealed class UnknownLengthJsonContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnknownLengthJsonContent(string json)
        {
            _bytes = System.Text.Encoding.UTF8.GetBytes(json);
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

    }
}
