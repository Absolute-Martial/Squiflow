using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class CustomerIndividualRoutes
{
    internal static void MapCustomerIndividualEndpoints(this WebApplication app)
    {
        Configure(app.MapPost("/api/v1/tenants/{tenantId:guid}/customers/individuals", TenantCustomerIndividualEndpoint.CreateAsync),
            "CreateTenantCustomerIndividual", EndpointAccess.AuthorizedCustomerIndividualCreation)
            .WithSummary("Creates a customer individual billing record independently of login accounts.")
            .Produces<CustomerIndividualResponse>(201).Produces<CustomerIndividualResponse>(200)
            .ProducesProblem(409);
        Configure(app.MapGet("/api/v1/tenants/{tenantId:guid}/customers/individuals/{individualId:guid}", TenantCustomerIndividualEndpoint.GetAsync),
            "GetTenantCustomerIndividual", EndpointAccess.AuthorizedCustomerIndividualRead)
            .WithSummary("Reads one tenant-owned individual customer record.")
            .Produces<CustomerIndividualResponse>(200).ProducesProblem(404);
        Configure(app.MapPost("/api/v1/tenants/{tenantId:guid}/customers/individuals/{individualId:guid}/availability", TenantCustomerIndividualEndpoint.ChangeAvailabilityAsync),
            "ChangeTenantCustomerIndividualAvailability", EndpointAccess.AuthorizedCustomerIndividualAvailability)
            .WithSummary("Changes individual availability using a current revision and Idempotency-Key.")
            .WithDescription("Requires can_change_individual_availability independently of contact-reading permission. Accepted states are active and inactive; an exact retry returns retained transition metadata. No debtor assignment is performed.")
            .Produces<IndividualAvailabilityResponse>(200).ProducesProblem(404).ProducesProblem(409);
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
