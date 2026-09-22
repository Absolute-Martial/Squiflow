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

        Assert.False(bootstrap.TryGetProperty("security", out _));
        Assert.True(HasOidcRequirement(account));
        Assert.True(HasOidcRequirement(memberships));
        Assert.True(HasOidcRequirement(workspace));
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

    private static bool HasOidcRequirement(JsonElement operation) =>
        operation.GetProperty("security")
            .EnumerateArray()
            .Any(requirement => requirement.TryGetProperty("oidc", out _));
}
