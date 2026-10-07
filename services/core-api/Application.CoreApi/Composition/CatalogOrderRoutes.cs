using Microsoft.AspNetCore.Mvc;
using Application.Orders;

namespace Application.CoreApi.Composition;

internal static class CatalogOrderRoutes
{
    internal static IServiceCollection AddCatalogOrderIntegration(this IServiceCollection services)
    {
        services.AddScoped<IOrderPricingAuthorityReader, OrderPricingAuthorityReader>();
        services.AddScoped<IOrderCommercialCommitGuard, OrderCommercialCommitGuard>();
        return services;
    }

    internal static void MapCatalogOrderEndpoints(this WebApplication app)
    {
        Configure(app.MapPost("/api/v1/tenants/{tenantId:guid}/orders/catalog-priced", TenantCatalogOrderEndpoint.CreateAsync),
            "CreateTenantCatalogPricedOrderDraft", EndpointAccess.AuthorizedTenantCatalogOrderCreation)
            .Produces<OrderDraftResponse>(201).Produces<OrderDraftResponse>(200);
        Configure(app.MapPut("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/catalog-priced-draft", TenantCatalogOrderEndpoint.ReviseAsync),
            "ReviseTenantCatalogPricedOrderDraft", EndpointAccess.AuthorizedTenantCatalogOrderRevision)
            .Produces<OrderDraftResponse>(200).ProducesProblem(404);
    }

    private static RouteHandlerBuilder Configure(RouteHandlerBuilder route, string name, EndpointAccess access) => route
        .WithName(name).WithTags("Orders").WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access)
        .WithMetadata(new RequestSizeLimitAttribute(TenantOrderEndpoint.MaximumCreateRequestBodyBytes))
        .RequireAuthorization().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409)
        .ProducesProblem(413).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
}
