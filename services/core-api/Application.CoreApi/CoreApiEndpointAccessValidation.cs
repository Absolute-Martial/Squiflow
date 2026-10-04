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
                throw new InvalidOperationException("Core API route authentication must match its declared access classification.");
            }

            var applicationAuthorization = endpoint.Metadata.GetOrderedMetadata<CoreApiApplicationAuthorizationMetadata>();
            if (classifications[0].RequiresApplicationAuthorization)
            {
                if (!endpoint.RoutePattern.Parameters.Any(parameter =>
                        string.Equals(parameter.Name, "tenantId", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        "Every authorized Core API route must declare the tenantId route boundary.");
                }

                if (applicationAuthorization.Count != 1 ||
                    applicationAuthorization[0].Access != classifications[0].Access ||
                    applicationAuthorization[0].Requirements.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Every authorized Core API route must declare one non-empty application authorization contract.");
                }

                var expected = CoreApiApplicationAuthorizationContract.RequirementsFor(classifications[0].Access);
                if (!applicationAuthorization[0].Requirements.Select(requirement => requirement.GetType())
                    .SequenceEqual(expected.Select(requirement => requirement.GetType())))
                {
                    throw new InvalidOperationException(
                        "Core API route application authorization must match its declared access contract.");
                }
            }
            else if (applicationAuthorization.Count != 0)
            {
                throw new InvalidOperationException("Core API routes without application authorization cannot declare an application authorization requirement.");
            }
        }
    }
}
