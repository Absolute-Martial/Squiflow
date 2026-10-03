using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace Application.CoreApi;

internal static class CoreApiEndpointAccessValidation
{
    internal static void ValidateCoreApiEndpointAccess(this WebApplication application)
    {
        var sources = ((IEndpointRouteBuilder)application).DataSources;
        Validate(sources.SelectMany(source => source.Endpoints));
    }

    internal static void Validate(IEnumerable<Endpoint> endpoints)
    {
        foreach (var endpoint in endpoints.OfType<RouteEndpoint>())
        {
            var classifications = endpoint.Metadata.GetOrderedMetadata<EndpointAccessMetadata>();
            if (classifications.Count != 1 || !Enum.IsDefined(classifications[0].Access))
            {
                throw new InvalidOperationException("Every Core API route must have exactly one recognized access classification.");
            }

            var requiresAuthorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
            var allowsAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            if (classifications[0].IsProtected != requiresAuthorization ||
                (classifications[0].IsProtected && allowsAnonymous))
            {
                throw new InvalidOperationException("Core API route authorization must match its declared access classification.");
            }
        }
    }
}
