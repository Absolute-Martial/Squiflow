using System.Net;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OpenApiContractTests : IClassFixture<WhiteLabelApiFactory>
{
    private static readonly string DevelopmentCodename = string.Concat("Squi", "Flow");

    private readonly HttpClient _client;

    public OpenApiContractTests(WhiteLabelApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DocumentUsesConfiguredPublicIdentityAndOpenIdConnectAuthority()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(DevelopmentCodename, body, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("Example Operations API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("v1", root.GetProperty("info").GetProperty("version").GetString());

        var oidc = root
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("oidc");
        Assert.Equal("openIdConnect", oidc.GetProperty("type").GetString());
        Assert.Equal(
            $"{WhiteLabelApiFactory.Authority}/.well-known/openid-configuration",
            oidc.GetProperty("openIdConnectUrl").GetString());
    }

    [Fact]
    public async Task SecurityRequirementAppearsOnlyOnProtectedOperations()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        var bootstrap = paths
            .GetProperty("/api/v1/application/bootstrap")
            .GetProperty("get");
        var account = paths
            .GetProperty("/api/v1/account")
            .GetProperty("get");
        var memberships = paths
            .GetProperty("/api/v1/account/tenants")
            .GetProperty("get");
        var workspace = paths
            .GetProperty("/api/v1/tenants/{tenantId}/workspace")
            .GetProperty("get");
        var orders = paths
            .GetProperty("/api/v1/tenants/{tenantId}/orders")
            .GetProperty("get");

        Assert.False(bootstrap.TryGetProperty("security", out _));
        Assert.True(HasOidcRequirement(account));
        Assert.True(HasOidcRequirement(memberships));
        Assert.True(HasOidcRequirement(workspace));
        Assert.True(HasOidcRequirement(orders));
    }

    [Fact]
    public async Task CreateOrderDocumentsIdempotencyAndResponseHeaders()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var createOrder = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/tenants/{tenantId}/orders")
            .GetProperty("post");

        var idempotencyKey = createOrder
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter =>
                parameter.GetProperty("name").GetString() == "Idempotency-Key" &&
                parameter.GetProperty("in").GetString() == "header");
        Assert.True(idempotencyKey.GetProperty("required").GetBoolean());
        Assert.Equal("string", idempotencyKey.GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal(1, idempotencyKey.GetProperty("schema").GetProperty("minLength").GetInt32());
        Assert.Equal(128, idempotencyKey.GetProperty("schema").GetProperty("maxLength").GetInt32());
        Assert.Contains("one value", idempotencyKey.GetProperty("description").GetString());

        var responses = createOrder.GetProperty("responses");
        Assert.True(responses
            .GetProperty("200")
            .GetProperty("headers")
            .TryGetProperty("Idempotency-Replayed", out var replayedHeader));
        Assert.Equal("string", replayedHeader.GetProperty("schema").GetProperty("type").GetString());
        Assert.True(responses
            .GetProperty("201")
            .GetProperty("headers")
            .TryGetProperty("Location", out var locationHeader));
        Assert.Equal("string", locationHeader.GetProperty("schema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task BrowseOrdersDocumentsBoundedOpaquePageParametersAndResponse()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var browseOrders = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/tenants/{tenantId}/orders")
            .GetProperty("get");

        var parameters = browseOrders.GetProperty("parameters").EnumerateArray();
        var limit = parameters.Single(parameter =>
            parameter.GetProperty("name").GetString() == "limit" &&
            parameter.GetProperty("in").GetString() == "query");
        Assert.False(limit.TryGetProperty("required", out var required) && required.GetBoolean());
        Assert.Equal("integer", limit.GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal(1, limit.GetProperty("schema").GetProperty("minimum").GetInt32());
        Assert.Equal(50, limit.GetProperty("schema").GetProperty("maximum").GetInt32());
        Assert.Equal(25, limit.GetProperty("schema").GetProperty("default").GetInt32());

        var after = parameters.Single(parameter =>
            parameter.GetProperty("name").GetString() == "after" &&
            parameter.GetProperty("in").GetString() == "query");
        Assert.Equal("string", after.GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal(128, after.GetProperty("schema").GetProperty("maxLength").GetInt32());
        Assert.Contains("Opaque tenant-bound cursor", after.GetProperty("description").GetString());

        Assert.True(browseOrders.GetProperty("responses").TryGetProperty("200", out var ok));
        var responseSchema = ok.GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        Assert.True(responseSchema.TryGetProperty("$ref", out _));
    }

    [Fact]
    public async Task AbandonOrderDocumentsAuthorizationRevisionBodyAndRetryHeader()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var abandon = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/tenants/{tenantId}/orders/{orderId}/abandon")
            .GetProperty("post");

        Assert.True(HasOidcRequirement(abandon));
        var key = abandon.GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.True(key.GetProperty("required").GetBoolean());
        Assert.Contains("expectedRevision", abandon.GetProperty("description").GetString());
        var requestBody = abandon.GetProperty("requestBody");
        Assert.True(requestBody.GetProperty("required").GetBoolean());
        var bodySchema = requestBody.GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        Assert.Contains(bodySchema.GetProperty("required").EnumerateArray(),
            property => property.GetString() == "expectedRevision");
        Assert.False(bodySchema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("integer", bodySchema.GetProperty("properties")
            .GetProperty("expectedRevision").GetProperty("type").GetString());
        Assert.Equal(1, bodySchema.GetProperty("properties")
            .GetProperty("expectedRevision").GetProperty("minimum").GetInt64());
        var successSchema = abandon.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var successName = successSchema.GetProperty("$ref").GetString()!.Split('/').Last();
        var successProperties = document.RootElement.GetProperty("components")
            .GetProperty("schemas").GetProperty(successName).GetProperty("properties");
        Assert.Equal(4, successProperties.EnumerateObject().Count());
        Assert.False(successProperties.TryGetProperty("summary", out _));
        Assert.Equal("date-time", successProperties.GetProperty("abandonedAt")
            .GetProperty("format").GetString());
        Assert.True(abandon.GetProperty("responses").GetProperty("200")
            .GetProperty("headers").TryGetProperty("Idempotency-Replayed", out _));
        Assert.True(abandon.GetProperty("responses").TryGetProperty("409", out _));
    }

    private static bool HasOidcRequirement(JsonElement operation) =>
        operation.GetProperty("security")
            .EnumerateArray()
            .Any(requirement => requirement.TryGetProperty("oidc", out _));
}
