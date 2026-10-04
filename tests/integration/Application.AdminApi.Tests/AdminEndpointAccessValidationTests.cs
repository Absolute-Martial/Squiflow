using Application.AdminApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;

namespace Application.AdminApi.Tests;

public sealed class AdminEndpointAccessValidationTests
{
    [Fact]
    public void ClassificationAuthenticationAndPermissionMustAgree()
    {
        var protectedAccess = new AdminEndpointAccessMetadata(AdminEndpointAccess.ProtectedPlatformAdministration);
        var permission = new AdminEndpointPermissionMetadata(PlatformAdminPermission.ReadTenants);
        var publicAccess = new AdminEndpointAccessMetadata(AdminEndpointAccess.PublicHealth);

        Assert.Throws<InvalidOperationException>(() => Validate());
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess, new AuthorizeAttribute()));
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess, permission));
        Assert.Throws<InvalidOperationException>(() => Validate(protectedAccess, permission, new AuthorizeAttribute(), new AllowAnonymousAttribute()));
        Assert.Throws<InvalidOperationException>(() => Validate(publicAccess, permission));
        Assert.Throws<InvalidOperationException>(() => Validate(new AdminEndpointAccessMetadata((AdminEndpointAccess)int.MaxValue)));

        Validate(protectedAccess, permission, new AuthorizeAttribute());
        Validate(publicAccess);
    }

    private static void Validate(params object[] metadata)
    {
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/test"),
            0,
            new EndpointMetadataCollection(metadata),
            "test route");
        AdminApiEndpointAccessValidation.Validate([endpoint]);
    }
}
