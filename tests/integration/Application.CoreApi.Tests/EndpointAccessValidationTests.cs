using System.Net;
using Application.CoreApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class EndpointAccessValidationTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;

    public EndpointAccessValidationTests(WhiteLabelApiFactory factory) => _factory = factory;

    [Fact]
    public void MissingDuplicatedAndUnknownClassificationsFailClosed()
    {
        var protectedAccess = new EndpointAccessMetadata(EndpointAccess.AuthenticatedAccount);
        Assert.Throws<InvalidOperationException>(() => Validate());
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess, protectedAccess, new AuthorizeAttribute()));
        Assert.Throws<InvalidOperationException>(() => Validate(new EndpointAccessMetadata((EndpointAccess)int.MaxValue)));
    }

    [Fact]
    public void AuthenticationAndAnonymousOverridesMustMatchClassification()
    {
        var protectedAccess = new EndpointAccessMetadata(EndpointAccess.AuthenticatedAccount);
        var publicAccess = new EndpointAccessMetadata(EndpointAccess.PublicLiveness);
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess));
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess, new AuthorizeAttribute(), new AllowAnonymousAttribute()));
        Assert.Throws<InvalidOperationException>(() => Validate(publicAccess, new AuthorizeAttribute()));

        Validate(protectedAccess, new AuthorizeAttribute());
        Validate(publicAccess);

        var authorizedAccess = new EndpointAccessMetadata(EndpointAccess.AuthorizedTenantWorkspace);
        Assert.Throws<InvalidOperationException>(() => Validate(authorizedAccess, new AuthorizeAttribute()));
        Assert.Throws<InvalidOperationException>(() => Validate(authorizedAccess, new AuthorizeAttribute(),
            new CoreApiApplicationAuthorizationMetadata(EndpointAccess.AuthorizedTenantOrderRead)));
        Validate(authorizedAccess, new AuthorizeAttribute(),
            new CoreApiApplicationAuthorizationMetadata(EndpointAccess.AuthorizedTenantWorkspace));
    }

    [Fact]
    public async Task EveryProtectedRouteRejectsAnonymousRequestsAndForgedAdminHeaders()
    {
        using var client = _factory.CreateClient();
        var endpoints = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<EndpointAccessMetadata>()!.IsProtected)
            .ToArray();
        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            var path = endpoint.RoutePattern.RawText!;
            foreach (var parameter in endpoint.RoutePattern.Parameters)
            {
                var token = "{" + parameter.Name + ":guid}";
                path = path.Replace(token, "11111111-1111-1111-1111-111111111111", StringComparison.Ordinal);
            }

            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                request.Headers.Add("X-Platform-Admin", "true");
                request.Headers.Add("X-Admin-Device", "registered");
                request.Headers.Add("Tailscale-User-Login", "operator@example.test");
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.True(response.Headers.CacheControl?.NoStore);
            }
        }
    }

    private static void Validate(params object[] metadata)
    {
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask, RoutePatternFactory.Parse("/test"), 0,
            new EndpointMetadataCollection(metadata), "test route");
        CoreApiEndpointAccessValidation.Validate([endpoint]);
    }
}
