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

    private static bool HasOidcRequirement(JsonElement operation) =>
        operation.GetProperty("security")
            .EnumerateArray()
            .Any(requirement => requirement.TryGetProperty("oidc", out _));
}
