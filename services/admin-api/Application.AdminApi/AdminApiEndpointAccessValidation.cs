using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace Application.AdminApi;

internal static class AdminApiEndpointAccessValidation
{
    internal static void ValidateAdminApiEndpointAccess(this WebApplication application)
    {
        var sources = ((IEndpointRouteBuilder)application).DataSources;
        Validate(sources.SelectMany(source => source.Endpoints));
    }

    internal static void Validate(IEnumerable<Endpoint> endpoints)
    {
        foreach (var endpoint in endpoints.OfType<RouteEndpoint>())
        {
            var classifications = endpoint.Metadata.GetOrderedMetadata<AdminEndpointAccessMetadata>();
            if (classifications.Count != 1 || !Enum.IsDefined(classifications[0].Access))
                throw new InvalidOperationException("Every Admin API route must have exactly one recognized access classification.");

            var requiresAuthentication = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
            var allowsAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var permissions = endpoint.Metadata.GetOrderedMetadata<AdminEndpointPermissionMetadata>();
            if (classifications[0].IsProtected != requiresAuthentication ||
                (classifications[0].IsProtected && allowsAnonymous))
                throw new InvalidOperationException("Admin API route authentication must match its declared access classification.");

            if (classifications[0].IsProtected)
            {
                if (permissions.Count != 1 || !Enum.IsDefined(permissions[0].Permission))
                    throw new InvalidOperationException("Every protected Admin API route must declare exactly one recognized platform permission.");
            }
            else if (permissions.Count != 0)
            {
                throw new InvalidOperationException("Public Admin API routes cannot declare platform permissions.");
            }
        }
    }
}
