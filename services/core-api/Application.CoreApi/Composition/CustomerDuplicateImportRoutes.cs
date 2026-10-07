using Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class CustomerDuplicateImportRoutes
{
    internal static void MapCustomerDuplicateImportEndpoints(this WebApplication app)
    {
        Configure(app.MapPost("/api/v1/tenants/{tenantId:guid}/customers/duplicates/search", TenantCustomerDuplicateImportEndpoint.SearchAsync),
            "SearchTenantCustomerDuplicates", EndpointAccess.AuthorizedCustomerDuplicateRead)
            .Produces<CustomerDuplicateSearchResult>(200);
        Configure(app.MapGet("/api/v1/tenants/{tenantId:guid}/customers/individuals/{customerId:guid}/duplicate-resolutions", TenantCustomerDuplicateImportEndpoint.ResolutionsAsync),
            "ReadTenantCustomerDuplicateResolutions", EndpointAccess.AuthorizedCustomerDuplicateRead)
            .Produces<CustomerDuplicateResolutionSnapshot[]>(200);
        Configure(app.MapPost("/api/v1/tenants/{tenantId:guid}/customers/duplicates/resolve", TenantCustomerDuplicateImportEndpoint.ResolveAsync),
            "ResolveTenantCustomerDuplicates", EndpointAccess.AuthorizedCustomerDuplicateResolve)
            .Produces<ResolveCustomerDuplicateResult>(200);
        Configure(app.MapPost("/api/v1/tenants/{tenantId:guid}/customers/duplicates/consolidate", TenantCustomerDuplicateImportEndpoint.ConsolidateAsync),
            "ConsolidateTenantCustomerDuplicates", EndpointAccess.AuthorizedCustomerDuplicateConsolidate)
            .Produces<ConsolidateCustomerDuplicateResult>(200);
        Configure(app.MapPost("/api/v1/tenants/{tenantId:guid}/customers/imports", TenantCustomerDuplicateImportEndpoint.PlanAsync),
            "PlanTenantCustomerImport", EndpointAccess.AuthorizedCustomerImport, CustomerImportCsv.MaxBytes)
            .Produces<CustomerImportPlanResponse>(201).Produces<CustomerImportPlanResponse>(200);
        Configure(app.MapPost("/api/v1/tenants/{tenantId:guid}/customers/imports/{importId:guid}/accept", TenantCustomerDuplicateImportEndpoint.AcceptAsync),
            "AcceptTenantCustomerImport", EndpointAccess.AuthorizedCustomerImport, TenantCustomerDuplicateImportEndpoint.MaximumDecisionBytes)
            .Produces<CustomerImportWorkSnapshot>(202);
        Configure(app.MapPost("/api/v1/tenants/{tenantId:guid}/customers/imports/run-batch", TenantCustomerDuplicateImportEndpoint.RunBatchAsync),
            "RunTenantCustomerImportBatch", EndpointAccess.AuthorizedCustomerImport)
            .Produces<CustomerImportBatchResult>(200);
        Configure(app.MapGet("/api/v1/tenants/{tenantId:guid}/customers/imports/{importId:guid}", TenantCustomerDuplicateImportEndpoint.ReadImportAsync),
            "ReadTenantCustomerImport", EndpointAccess.AuthorizedCustomerImport)
            .Produces<CustomerImportSummary>(200);
        Configure(app.MapGet("/api/v1/tenants/{tenantId:guid}/customers/imports/{importId:guid}/rows", TenantCustomerDuplicateImportEndpoint.RowsAsync),
            "ReadTenantCustomerImportRows", EndpointAccess.AuthorizedCustomerImport)
            .Produces<CustomerImportRowPage>(200);
        Configure(app.MapGet("/api/v1/tenants/{tenantId:guid}/customers/imports/{importId:guid}/source", TenantCustomerDuplicateImportEndpoint.SourceAsync),
            "ReadTenantCustomerImportSource", EndpointAccess.AuthorizedCustomerImport)
            .Produces(200, contentType: "text/csv");
        Configure(app.MapGet("/api/v1/tenants/{tenantId:guid}/customers/import-template", TenantCustomerDuplicateImportEndpoint.TemplateAsync),
            "ReadTenantCustomerImportTemplate", EndpointAccess.AuthorizedCustomerImport)
            .Produces(200, contentType: "text/csv");
    }

    private static RouteHandlerBuilder Configure(RouteHandlerBuilder endpoint, string name, EndpointAccess access,
        long maximumBytes = TenantCustomerEndpoint.MaximumCreateRequestBodyBytes) => endpoint.WithName(name).WithTags("Customers")
        .WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access).RequireAuthorization()
        .WithMetadata(new RequestSizeLimitAttribute(maximumBytes))
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
        .ProducesProblem(413).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504);
}
