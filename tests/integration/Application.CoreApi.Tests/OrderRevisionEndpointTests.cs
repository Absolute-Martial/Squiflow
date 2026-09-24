using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OrderRevisionEndpointTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;
    private readonly HttpClient _client;

    public OrderRevisionEndpointTests(WhiteLabelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RevisionReplacesTheWholeDraftAndReplaysTheCommittedSnapshot()
    {
        var (accountId, tenantId, token, orderId) = await CreateDraftAsync();
        _factory.SetOrderEditDecision(accountId, tenantId, true);
        _factory.SetCustomerDecision(accountId, tenantId, "createOrganization", true);
        _factory.SetCustomerDecision(accountId, tenantId, "createProgram", true);
        var organizations = $"/api/v1/tenants/{tenantId:D}/customers/organizations";
        using var createOrganization = CustomerRequest(organizations, token, "Organization");
        using var createdOrganization = await _client.SendAsync(createOrganization);
        Assert.Equal(HttpStatusCode.Created, createdOrganization.StatusCode);
        var organizationId = (await createdOrganization.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("organizationId").GetGuid();
        using var createProgram = CustomerRequest($"{organizations}/{organizationId:D}/programs", token, "Program");
        using var createdProgram = await _client.SendAsync(createProgram);
        Assert.Equal(HttpStatusCode.Created, createdProgram.StatusCode);
        var programId = (await createdProgram.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("programId").GetGuid();
        var body = Payload(1, "Updated", new { organizationId, programId });

        using var first = await _client.SendAsync(Request(tenantId, orderId, token, "edit-1", body));
        using var revised = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("no-store", first.Headers.CacheControl?.ToString());
        Assert.Equal(2, revised.RootElement.GetProperty("revision").GetInt64());
        Assert.Equal("Updated", revised.RootElement.GetProperty("summary").GetString());
        Assert.Equal(organizationId, revised.RootElement.GetProperty("customerContext").GetProperty("organizationId").GetGuid());
        Assert.Equal(programId, revised.RootElement.GetProperty("customerContext").GetProperty("programId").GetGuid());
        Assert.Equal(1, _factory.GetOrderReviseCount(tenantId));

        using var replay = await _client.SendAsync(Request(tenantId, orderId, token, "edit-1", body));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(2, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt64());

        using var stale = await _client.SendAsync(Request(tenantId, orderId, token, "edit-stale", body));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("revision_conflict", await CodeAsync(stale));

        using var changedKey = await _client.SendAsync(Request(tenantId, orderId, token, "edit-1", Payload(1, "Changed")));
        Assert.Equal(HttpStatusCode.Conflict, changedKey.StatusCode);
        Assert.Equal("idempotency_key_conflict", await CodeAsync(changedKey));
    }

    [Fact]
    public async Task PermissionAndMembershipAreSeparateAndProviderOutageFailsClosed()
    {
        var (accountId, tenantId, token, orderId) = await CreateDraftAsync();
        var before = _factory.GetOrderReviseCount(tenantId);
        using var denied = await _client.SendAsync(Request(tenantId, orderId, token, "denied", Payload(1)));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("tenant_permission_denied", await CodeAsync(denied));
        Assert.Equal(before, _factory.GetOrderReviseCount(tenantId));

        _factory.SetOrderEditUnavailable(accountId, tenantId);
        using var outage = await _client.SendAsync(Request(tenantId, orderId, token, "outage", Payload(1)));
        var outageBody = await outage.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, outage.StatusCode);
        Assert.Equal("authorization_unavailable", Code(outageBody));
        Assert.DoesNotContain("Synthetic", outageBody, StringComparison.Ordinal);
        Assert.Equal(before, _factory.GetOrderReviseCount(tenantId));

        var outsider = Guid.NewGuid();
        var outsiderSubject = $"edit-outsider-{Guid.NewGuid():N}";
        _factory.Bind(outsiderSubject, outsider);
        _factory.SetOrderEditDecision(outsider, tenantId, true);
        using var noMembership = await _client.SendAsync(Request(
            tenantId, orderId, _factory.CreateToken(outsiderSubject), "outsider", Payload(1)));
        Assert.Equal(HttpStatusCode.Forbidden, noMembership.StatusCode);
        Assert.Equal("tenant_access_denied", await CodeAsync(noMembership));
        Assert.Equal(0, _factory.GetOrderEditCheckCount(outsider, tenantId));
        Assert.Equal(before, _factory.GetOrderReviseCount(tenantId));
    }

    [Theory]
    [InlineData(null, "{\"expectedRevision\":1}", "idempotency_key_invalid")]
    [InlineData("key", "{", "request_invalid")]
    [InlineData("key", "{\"expectedRevision\":0,\"summary\":\"Updated\",\"currencyCode\":\"USD\",\"lines\":[]}", "expected_revision_invalid")]
    public async Task InvalidRequestsFailBeforeTheStore(string? key, string body, string code)
    {
        var (accountId, tenantId, token, orderId) = await CreateDraftAsync();
        _factory.SetOrderEditDecision(accountId, tenantId, true);
        using var response = await _client.SendAsync(Request(tenantId, orderId, token, key, body));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await CodeAsync(response));
        Assert.Equal(0, _factory.GetOrderReviseCount(tenantId));
    }

    [Fact]
    public async Task OversizedBodyAndUnknownOrderDoNotMutate()
    {
        var (accountId, tenantId, token, orderId) = await CreateDraftAsync();
        _factory.SetOrderEditDecision(accountId, tenantId, true);
        using var huge = await _client.SendAsync(Request(
            tenantId, orderId, token, "huge", Payload(1, new string('a', 65536))));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, huge.StatusCode);
        using var missing = await _client.SendAsync(Request(
            tenantId, Guid.NewGuid(), token, "missing", Payload(1)));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("order_not_found", await CodeAsync(missing));
    }

    private async Task<(Guid AccountId, Guid TenantId, string Token, Guid OrderId)> CreateDraftAsync()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var subject = $"edit-{Guid.NewGuid():N}";
        var token = _factory.CreateToken(subject);
        _factory.Bind(subject, accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Order Edit Tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, true);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenantId:D}/orders")
        {
            Content = JsonContent.Create(new
            {
                summary = "Original",
                currencyCode = "USD",
                lines = new[] { new { description = "Item", quantity = 1m, unitCode = "ea", unitPrice = 5m } },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", $"create-{Guid.NewGuid():N}");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (accountId, tenantId, token, order.GetProperty("orderId").GetGuid());
    }

    private static HttpRequestMessage Request(Guid tenantId, Guid orderId, string token, string? key, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/v1/tenants/{tenantId:D}/orders/{orderId:D}/draft")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return request;
    }

    private static HttpRequestMessage CustomerRequest(string path, string token, string displayName)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { displayName }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return request;
    }

    private static string Payload(long revision, string summary = "Updated", object? customerContext = null) =>
        JsonSerializer.Serialize(new
        {
            expectedRevision = revision,
            summary,
            currencyCode = "USD",
            lines = new[] { new { description = "New item", quantity = 2m, unitCode = "ea", unitPrice = 7m } },
            customerContext,
        });

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        Code(await response.Content.ReadAsStringAsync());

    private static string? Code(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("code").GetString();
    }
}
