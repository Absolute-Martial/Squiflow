using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SquiFlow.ApplicationKernel.Authorization;
using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Tenancy;

namespace SquiFlow.Customers.Api;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/customers", async (
            IFeatureSnapshotAccessor features,
            IPermissionEvaluator permissions,
            TenantContext tenantContext,
            CancellationToken cancellationToken) =>
        {
            if (!features.Current.IsEnabled(CustomersModule.Feature))
            {
                return Results.NotFound();
            }

            if (!await permissions.IsAllowedAsync(
                    tenantContext,
                    CustomersModule.ViewPermission,
                    cancellationToken))
            {
                return Results.Forbid();
            }

            return Results.Ok(new
            {
                module = CustomersModule.Id.Value,
                featureRevision = features.Current.Revision,
                items = Array.Empty<object>(),
                note = "Phase 0 exposes no fake customer data."
            });
        })
        .WithName("ListCustomers")
        .WithTags("Customers");

        return endpoints;
    }
}
