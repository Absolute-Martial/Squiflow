using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class CustomerRepresentativeRoutes
{
    internal static void MapCustomerRepresentativeEndpoints(this WebApplication app)
    {
        Configure(
                app.MapPost(
                    "/api/v1/tenants/{tenantId:guid}/customers/organizations/{organizationId:guid}/representatives",
                    TenantCustomerRepresentativeEndpoint.LinkAsync),
                "LinkTenantCustomerRepresentative",
                EndpointAccess.AuthorizedCustomerRepresentativeManage)
            .WithSummary("Links an active individual as a representative of an organization or child program.")
            .WithDescription("Representative identity is relationship metadata only; it does not grant operator, approval, login or billing authority.")
            .Produces<CustomerRepresentativeResponse>(201)
            .Produces<CustomerRepresentativeResponse>(200)
            .ProducesProblem(404)
            .ProducesProblem(409);

        Configure(
                app.MapGet(
                    "/api/v1/tenants/{tenantId:guid}/customers/organizations/{organizationId:guid}/representatives/{representativeId:guid}",
                    TenantCustomerRepresentativeEndpoint.GetAsync),
                "GetTenantCustomerRepresentative",
                EndpointAccess.AuthorizedCustomerRepresentativeRead)
            .WithSummary("Reads one representative relationship without disclosing the linked individual's contact fields.")
            .Produces<CustomerRepresentativeResponse>(200)
            .ProducesProblem(404);

        Configure(
                app.MapPost(
                    "/api/v1/tenants/{tenantId:guid}/customers/organizations/{organizationId:guid}/representatives/{representativeId:guid}/unlink",
                    TenantCustomerRepresentativeEndpoint.UnlinkAsync),
                "UnlinkTenantCustomerRepresentative",
                EndpointAccess.AuthorizedCustomerRepresentativeManage)
            .WithSummary("Deactivates a representative relationship using its current revision.")
            .Produces<CustomerRepresentativeResponse>(200)
            .ProducesProblem(404)
            .ProducesProblem(409);
    }

    private static RouteHandlerBuilder Configure(RouteHandlerBuilder endpoint, string name, EndpointAccess access) =>
        endpoint.WithName(name).WithTags("Customers")
            .WithCoreApiAccess(access)
            .WithCoreApiApplicationAuthorization(access)
            .WithMetadata(new RequestSizeLimitAttribute(TenantCustomerEndpoint.MaximumCreateRequestBodyBytes))
            .RequireAuthorization()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(413)
            .ProducesProblem(429).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
}
