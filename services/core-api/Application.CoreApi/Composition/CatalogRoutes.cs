using Application.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class CatalogRoutes
{
    internal static void MapCatalogEndpoints(this WebApplication app)
    {
        const string prefix = "/api/v1/tenants/{tenantId:guid}/catalog";
        Configure(app.MapPost(prefix + "/units", TenantCatalogEndpoint.CreateUnitAsync), "CreateCatalogUnit", EndpointAccess.AuthorizedCatalogManage)
            .Produces<CatalogUnitResponse>(201).Produces<CatalogUnitResponse>(200);
        Configure(app.MapPost(prefix + "/items", TenantCatalogEndpoint.CreateItemAsync), "CreateCatalogItem", EndpointAccess.AuthorizedCatalogManage)
            .Produces<CatalogItemResponse>(201).Produces<CatalogItemResponse>(200);
        Configure(app.MapGet(prefix + "/units", TenantCatalogEndpoint.ListUnitsAsync), "ListCatalogUnits", EndpointAccess.AuthorizedCatalogRead)
            .Produces<CatalogUnitPageResponse>();
        Configure(app.MapGet(prefix + "/items", TenantCatalogEndpoint.ListItemsAsync), "ListCatalogItems", EndpointAccess.AuthorizedCatalogRead)
            .Produces<CatalogItemPageResponse>();
        Configure(app.MapGet(prefix + "/units/{unitId:guid}", TenantCatalogEndpoint.GetUnitAsync), "GetCatalogUnit", EndpointAccess.AuthorizedCatalogRead)
            .Produces<CatalogUnitResponse>();
        Configure(app.MapGet(prefix + "/items/{itemId:guid}", TenantCatalogEndpoint.GetItemAsync), "GetCatalogItem", EndpointAccess.AuthorizedCatalogRead)
            .Produces<CatalogItemResponse>();
        Configure(app.MapPost(prefix + "/units/{unitId:guid}/display", TenantCatalogEndpoint.RenameUnitAsync), "RenameCatalogUnit", EndpointAccess.AuthorizedCatalogManage)
            .Produces<CatalogMutationResponse>();
        Configure(app.MapPost(prefix + "/items/{itemId:guid}/display", TenantCatalogEndpoint.RenameItemAsync), "RenameCatalogItem", EndpointAccess.AuthorizedCatalogManage)
            .Produces<CatalogMutationResponse>();
        Configure(app.MapPost(prefix + "/units/{unitId:guid}/retire", TenantCatalogEndpoint.RetireUnitAsync), "RetireCatalogUnit", EndpointAccess.AuthorizedCatalogManage)
            .Produces<CatalogMutationResponse>();
        Configure(app.MapPost(prefix + "/items/{itemId:guid}/retire", TenantCatalogEndpoint.RetireItemAsync), "RetireCatalogItem", EndpointAccess.AuthorizedCatalogManage)
            .Produces<CatalogMutationResponse>();
        Configure(app.MapPost(prefix + "/items/{itemId:guid}/availability", TenantCatalogEndpoint.ChangeAvailabilityAsync), "ChangeCatalogAvailability", EndpointAccess.AuthorizedCatalogManage)
            .Produces<CatalogMutationResponse>();
        Configure(app.MapPost(prefix + "/units/{sourceUnitId:guid}/conversions/{targetUnitId:guid}", TenantCatalogEndpoint.PublishConversionAsync), "PublishCatalogConversion", EndpointAccess.AuthorizedCatalogManage)
            .Produces<CatalogConversionResponse>();
        Configure(app.MapGet(prefix + "/units/{sourceUnitId:guid}/conversions/{targetUnitId:guid}", TenantCatalogEndpoint.GetConversionAsync), "GetCatalogConversion", EndpointAccess.AuthorizedCatalogRead)
            .Produces<CatalogConversionResponse>();
        Configure(app.MapPost(prefix + "/line-facts", TenantCatalogEndpoint.SelectLineFactsAsync), "SelectCatalogLineFacts", EndpointAccess.AuthorizedCatalogRead)
            .Produces<CatalogLineFacts>();
    }

    private static RouteHandlerBuilder Configure(RouteHandlerBuilder endpoint, string name, EndpointAccess access) =>
        endpoint.WithName(name).WithTags("Catalog")
            .WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access).RequireAuthorization()
            .WithMetadata(new RequestSizeLimitAttribute(TenantCatalogEndpoint.MaximumBodyBytes))
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
            .ProducesProblem(413).ProducesProblem(429).ProducesProblem(500).ProducesProblem(503).ProducesProblem(504);
}
