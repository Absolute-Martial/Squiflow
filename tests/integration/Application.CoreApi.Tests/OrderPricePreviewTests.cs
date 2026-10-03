using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OrderPricePreviewTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;
    private readonly HttpClient _client;
    private const string ValidBody = """
        {"currencyCode":" usd ","lines":[{"description":" Poster ","quantity":0.5,"unitCode":" ea ","unitPrice":2.4689}]}
        """;

    public OrderPricePreviewTests(WhiteLabelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PreviewNeedsNoIdempotencyKeyAndAgreesWithCreationWithoutSavingAnOrder()
    {
        var (accountId, tenantId, token) = GrantAccess();
        using var previewRequest = Request(tenantId, token, ValidBody);
        using var response = await _client.SendAsync(previewRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Idempotency-Replayed"));
        using var preview = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("USD", preview.RootElement.GetProperty("currencyCode").GetString());
        Assert.Equal(1.2344m, preview.RootElement.GetProperty("total").GetDecimal());
        Assert.False(preview.RootElement.TryGetProperty("orderId", out _));
        Assert.Equal(1, _factory.GetOrderCreateCheckCount(accountId, tenantId));
        AssertNoOrderStorage(tenantId);

        using var create = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenantId:D}/orders");
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        create.Headers.Add("Idempotency-Key", "preview-then-create");
        create.Content = JsonContent.Create(new
        {
            summary = "Previewed order",
            currencyCode = " usd ",
            lines = new[] { new { description = " Poster ", quantity = 0.5m, unitCode = " ea ", unitPrice = 2.4689m } },
        });
        using var created = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var order = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(preview.RootElement.GetProperty("currencyCode").GetString(), order.RootElement.GetProperty("currencyCode").GetString());
        Assert.Equal(preview.RootElement.GetProperty("total").GetDecimal(), order.RootElement.GetProperty("total").GetDecimal());
        Assert.Equal(preview.RootElement.GetProperty("lines").GetRawText(), order.RootElement.GetProperty("lines").GetRawText());
        Assert.Equal(1, _factory.GetOrderCreateCount(tenantId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentMembershipAndPermissionAreRequiredBeforeParsingTheBody(bool hasMembership)
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var subject = accountId.ToString("D");
        _factory.Bind(subject, accountId);
        if (hasMembership)
            _factory.AddTenantMembership(accountId, tenantId, "Preview tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: false);
        using var request = Request(tenantId, _factory.CreateToken(subject), "malformed JSON");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(hasMembership ? "tenant_permission_denied" : "tenant_access_denied", await Code(response));
        Assert.Equal(hasMembership ? 1 : 0, _factory.GetOrderCreateCheckCount(accountId, tenantId));
        AssertNoOrderStorage(tenantId);
    }

    [Fact]
    public async Task AuthorizationOutageFailsClosedWithoutExposingProviderDetails()
    {
        var (accountId, tenantId, token) = GrantAccess();
        _factory.SetOrderCreateUnavailable(accountId, tenantId);
        using var request = Request(tenantId, token, ValidBody);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("authorization_unavailable", await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("Synthetic", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        AssertNoOrderStorage(tenantId);
    }

    [Theory]
    [InlineData("null", "request_invalid")]
    [InlineData("{", "request_invalid")]
    [InlineData("{\"currencyCode\":\"USD\",\"lines\":[null]}", "request_invalid")]
    [InlineData("{\"currencyCode\":\"USD\",\"lines\":[]}", "lines_invalid")]
    [InlineData("{\"currencyCode\":\"US\",\"lines\":[]}", "currency_code_invalid")]
    [InlineData("{\"currencyCode\":\"USD\",\"lines\":[{\"description\":\"Poster\",\"quantity\":1,\"unitCode\":\"EA\",\"unitPrice\":-1}]}", "unit_price_invalid")]
    public async Task InvalidPreviewReturnsAStableProblemWithoutSavingAnything(string body, string expectedCode)
    {
        var (_, tenantId, token) = GrantAccess();
        using var request = Request(tenantId, token, body);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        AssertNoOrderStorage(tenantId);
    }

    [Theory]
    [InlineData(false, 65536, HttpStatusCode.OK)]
    [InlineData(false, 65537, HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(true, 65536, HttpStatusCode.OK)]
    [InlineData(true, 65537, HttpStatusCode.RequestEntityTooLarge)]
    public async Task ActualBodySizeIsBoundedEvenWithoutContentLength(bool unknownLength, int bytes, HttpStatusCode expected)
    {
        var (_, tenantId, token) = GrantAccess();
        var body = ValidBody.PadRight(bytes, ' ');
        using var request = Request(tenantId, token, body);
        if (unknownLength)
        {
            request.Content!.Dispose();
            request.Content = new UnknownLengthJsonContent(Encoding.UTF8.GetBytes(body));
            Assert.Null(request.Content.Headers.ContentLength);
        }
        using var response = await _client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        if (expected == HttpStatusCode.RequestEntityTooLarge)
            Assert.Equal("request_too_large", await Code(response));
        AssertNoOrderStorage(tenantId);
    }

    [Fact]
    public async Task ContractDescribesProtectedCalculationWithoutMutationHeaders()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/tenants/{tenantId}/orders/price-preview").GetProperty("post");
        Assert.True(operation.TryGetProperty("security", out _));
        Assert.True(operation.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        if (operation.TryGetProperty("parameters", out var parameters))
            Assert.DoesNotContain(parameters.EnumerateArray(), parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key");
        var responses = operation.GetProperty("responses");
        foreach (var status in new[] { "200", "400", "401", "403", "413", "500", "503", "504" })
            Assert.True(responses.TryGetProperty(status, out _));
        Assert.False(responses.TryGetProperty("201", out _));
    }

    private (Guid AccountId, Guid TenantId, string Token) GrantAccess()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var subject = accountId.ToString("D");
        _factory.Bind(subject, accountId);
        _factory.AddTenantMembership(accountId, tenantId, "Preview tenant");
        _factory.SetOrderCreateDecision(accountId, tenantId, allowed: true);
        _factory.SetOrderManualPriceDecision(accountId, tenantId, true);
        return (accountId, tenantId, _factory.CreateToken(subject));
    }

    private void AssertNoOrderStorage(Guid tenantId)
    {
        Assert.Equal(0, _factory.GetOrderCreateCount(tenantId));
        Assert.Equal(0, _factory.GetOrderReviseCount(tenantId));
        Assert.Equal(0, _factory.GetOrderAbandonCount(tenantId));
        Assert.Equal(0, _factory.GetOrderFindCount(tenantId));
        Assert.Equal(0, _factory.GetOrderListCount(tenantId));
    }

    private static HttpRequestMessage Request(Guid tenantId, string token, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenantId:D}/orders/price-preview");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed class UnknownLengthJsonContent : HttpContent
    {
        private readonly byte[] _body;

        public UnknownLengthJsonContent(byte[] body)
        {
            _body = body;
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_body).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
