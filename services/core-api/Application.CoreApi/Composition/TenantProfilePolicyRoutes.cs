using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class TenantProfilePolicyRoutes
{
    internal static void MapTenantProfilePolicyEndpoints(this WebApplication app)
    {
        const string root = "/api/v1/tenants/{tenantId:guid}/profile-policy";
        Configure(app.MapGet(root, TenantProfilePolicyEndpoints.GetAsync),
                "ReadTenantProfilePolicy", EndpointAccess.AuthorizedTenantProfilePolicyView)
            .Produces<Application.Profiles.TenantPolicyState>(StatusCodes.Status200OK);
        Configure(app.MapPut(root, TenantProfilePolicyEndpoints.EditAsync),
                "EditTenantProfilePolicy", EndpointAccess.AuthorizedTenantProfilePolicyEdit)
            .Accepts<EditPolicyPayload>("application/json")
            .Produces<ProfilePolicyCommandResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);
        Configure(app.MapPost(root + "/publish", TenantProfilePolicyEndpoints.PublishAsync),
                "PublishTenantProfilePolicy", EndpointAccess.AuthorizedTenantProfilePolicyPublish)
            .Accepts<PublishPolicyPayload>("application/json")
            .Produces<ProfilePolicyCommandResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static RouteHandlerBuilder Configure(RouteHandlerBuilder endpoint, string name, EndpointAccess access) =>
        endpoint.WithName(name).WithTags("Tenant Profiles").WithCoreApiAccess(access)
            .WithCoreApiApplicationAuthorization(access)
            .WithMetadata(new RequestSizeLimitAttribute(TenantCustomerEndpoint.MaximumCreateRequestBodyBytes))
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
}
