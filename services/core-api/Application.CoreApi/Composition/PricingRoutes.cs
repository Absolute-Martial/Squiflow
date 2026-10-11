using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class PricingRoutes
{
    internal static void MapPricingEndpoints(this WebApplication app)
    {
        const string root = "/api/v1/tenants/{tenantId:guid}/pricing";
        Configure(app.MapPost(root + "/drafts", TenantPricingEndpoint.CreateDraftAsync), "CreateTenantPriceDraft", EndpointAccess.AuthorizedPricingDraftEdit)
            .Produces<PricingRevisionResponse>(201).Produces<PricingRevisionResponse>(200).ProducesProblem(409);
        Configure(app.MapPost(root + "/revisions/{revisionId:guid}/publish", TenantPricingEndpoint.PublishAsync), "PublishTenantPriceRevision", EndpointAccess.AuthorizedPricingPublish)
            .Produces<PricingRevisionResponse>(200).ProducesProblem(404).ProducesProblem(409);
        Configure(app.MapPost(root + "/revisions/{revisionId:guid}/retire", TenantPricingEndpoint.RetireAsync), "RetireTenantPriceRevision", EndpointAccess.AuthorizedPricingRetire)
            .Produces<PricingRevisionResponse>(200).ProducesProblem(404).ProducesProblem(409);
        Configure(app.MapGet(root + "/revisions/{revisionId:guid}", TenantPricingEndpoint.GetAsync), "ReadTenantPriceRevision", EndpointAccess.AuthorizedPricingRead)
            .Produces<PricingRevisionResponse>(200).ProducesProblem(404);
        Configure(app.MapPost(root + "/resolve", TenantPricingEndpoint.ResolveAsync), "ResolveTenantPrice", EndpointAccess.AuthorizedPricingRead)
            .Produces<PricingResolutionResponse>(200);
        Configure(app.MapPost(root + "/history", TenantPricingEndpoint.HistoryAsync), "ReadTenantPriceHistory", EndpointAccess.AuthorizedPricingRead)
            .Produces<PricingHistoryResponse>(200);
        Configure(app.MapGet(root + "/policy", TenantPricingEndpoint.GetPolicyAsync), "ReadTenantPricingPolicy", EndpointAccess.AuthorizedPricingRead)
            .Produces<Application.Pricing.PricingPolicySnapshot>(200).ProducesProblem(404);
        Configure(app.MapPost(root + "/policy", TenantPricingEndpoint.PublishPolicyAsync), "PublishTenantPricingPolicy", EndpointAccess.AuthorizedPricingPublish)
            .Produces<Application.Pricing.PricingPolicySnapshot>(200).ProducesProblem(409);
    }

    private static RouteHandlerBuilder Configure(RouteHandlerBuilder endpoint, string name, EndpointAccess access) =>
        endpoint.WithName(name).WithTags("Pricing").WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access)
            .WithMetadata(new RequestSizeLimitAttribute(TenantCustomerEndpoint.MaximumCreateRequestBodyBytes))
            .RequireAuthorization().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(413)
            .ProducesProblem(429).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
}
