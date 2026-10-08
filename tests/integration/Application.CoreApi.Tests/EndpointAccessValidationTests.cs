using System.Net;
using System.Security.Claims;
using System.Reflection;
using Application.CoreApi.Authorization;
using Application.CoreApi;
using Application.Tenancy;
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
        Assert.Throws<InvalidOperationException>(() => ValidateTenant(authorizedAccess, new AuthorizeAttribute(),
            new CoreApiApplicationAuthorizationMetadata(
                EndpointAccess.AuthorizedTenantOrderRead,
                CoreApiApplicationAuthorizationContract.RequirementsFor(EndpointAccess.AuthorizedTenantOrderRead))));
        Assert.Throws<InvalidOperationException>(() => ValidateTenant(authorizedAccess, new AuthorizeAttribute(),
            new CoreApiApplicationAuthorizationMetadata(
                EndpointAccess.AuthorizedTenantWorkspace,
                [ViewOrdersRequirement.Instance])));
        Assert.Throws<InvalidOperationException>(() => Validate(
            authorizedAccess,
            new AuthorizeAttribute(),
            new CoreApiApplicationAuthorizationMetadata(
                EndpointAccess.AuthorizedTenantWorkspace,
                CoreApiApplicationAuthorizationContract.RequirementsFor(EndpointAccess.AuthorizedTenantWorkspace))));
        ValidateTenant(authorizedAccess, new AuthorizeAttribute(),
            new CoreApiApplicationAuthorizationMetadata(
                EndpointAccess.AuthorizedTenantWorkspace,
                CoreApiApplicationAuthorizationContract.RequirementsFor(EndpointAccess.AuthorizedTenantWorkspace)));
    }


    [Fact]
    public async Task RuntimeAuthorizationUsesTheValidatedEndpointDeclaration()
    {
        var access = EndpointAccess.AuthorizedTenantOrderCreation;
        var declaration = new CoreApiApplicationAuthorizationMetadata(
            access,
            CoreApiApplicationAuthorizationContract.RequirementsFor(access));
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/test"),
            0,
            new EndpointMetadataCollection(
                new EndpointAccessMetadata(access),
                declaration,
                new AuthorizeAttribute()),
            "test route");
        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);
        var authorization = new RecordingAuthorizationService();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test")], "test"));

        var failure = await CoreApiDeclaredAuthorization.AuthorizeAsync(
            context,
            principal,
            new object(),
            authorization,
            "denied");

        Assert.Null(failure);
        Assert.Equal(
            [typeof(CreateOrderRequirement), typeof(ApplyManualPriceRequirement)],
            authorization.RequirementTypes);
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

    [Fact]
    public void EveryApplicationAuthorizedEndpointAccessIsMappedInBothTheRequirementAndResourceSwitches()
    {
        // The requirement table and the middleware's resource table are two independent switches
        // over EndpointAccess. CoreApiEndpointAccessValidation compares the requirement table with
        // the declared metadata, so it cannot observe a classification the resource table never
        // handles; that gap would otherwise surface only as a runtime failure on the endpoint
        // that owns the classification. Public and merely authenticated classifications are
        // excluded because they never reach application authorization.
        var createResource = typeof(CoreApiApplicationAuthorizationMiddleware)
            .GetMethod("CreateResource", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(createResource);
        var tenantContext = ResolveTenantContext();
        var authorized = Enum.GetValues<EndpointAccess>()
            .Where(access => new EndpointAccessMetadata(access).RequiresApplicationAuthorization)
            .ToArray();
        Assert.NotEmpty(authorized);

        foreach (var access in authorized)
        {
            Assert.NotEmpty(CoreApiApplicationAuthorizationContract.RequirementsFor(access));
            Assert.NotNull(createResource.Invoke(null, [access, tenantContext, CancellationToken.None]));
        }
    }

    private static TenantContext ResolveTenantContext() =>
        new ResolveTenantContext(new ActiveMembershipDirectory())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None)
            .GetAwaiter()
            .GetResult()!;

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
            Guid requestedAccountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(
            Guid requestedAccountId, Guid requestedTenantId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private static void Validate(params object[] metadata)
        => ValidatePattern("/test", metadata);

    private static void ValidateTenant(params object[] metadata)
        => ValidatePattern("/test/{tenantId:guid}", metadata);

    private static void ValidatePattern(string pattern, params object[] metadata)
    {
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask, RoutePatternFactory.Parse(pattern), 0,
            new EndpointMetadataCollection(metadata), "test route");
        CoreApiEndpointAccessValidation.Validate([endpoint]);
    }

    private sealed class RecordingAuthorizationService : IAuthorizationService
    {
        internal Type[] RequirementTypes { get; private set; } = [];

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            // Declared requirements are authorized sequentially so evaluation can stop at the first
            // denial. Accumulate every requirement the runtime actually asked about, in order,
            // rather than only the most recent call.
            RequirementTypes = [.. RequirementTypes, .. requirements.Select(requirement => requirement.GetType())];
            return Task.FromResult(AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            string policyName) =>
            throw new NotSupportedException();
    }
}
