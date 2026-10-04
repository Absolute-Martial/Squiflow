using System.Security.Claims;
using Application.CoreApi.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi;

internal static class CoreApiDeclaredAuthorization
{
    private static readonly object MiddlewareAuthorizedAccessKey = new();

    internal static async Task<IResult?> AuthorizeAsync(
        HttpContext context,
        ClaimsPrincipal principal,
        object resource,
        IAuthorizationService authorization,
        string deniedDetail)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNullOrWhiteSpace(deniedDetail);

        var declarations = context.GetEndpoint()?.Metadata
            .GetOrderedMetadata<CoreApiApplicationAuthorizationMetadata>();
        if (declarations is null || declarations.Count != 1 || declarations[0].Requirements.Count == 0)
        {
            throw new InvalidOperationException(
                "Authorized Core API execution requires exactly one validated application-authorization declaration.");
        }

        if (context.Items.TryGetValue(MiddlewareAuthorizedAccessKey, out var admitted) &&
            admitted is EndpointAccess admittedAccess)
        {
            if (admittedAccess != declarations[0].Access)
            {
                throw new InvalidOperationException(
                    "Core API application authorization middleware admitted a different access contract.");
            }

            return null;
        }

        AuthorizationResult? denied = null;
        try
        {
            // Evaluate declared requirements in order and stop at the first failure. A combined
            // policy would run every handler, so a caller who lacks the base order permission
            // would still trigger the manual-pricing provider check. Sequential evaluation keeps
            // the narrow permission the route actually depends on as the only one consulted.
            foreach (var requirement in declarations[0].Requirements)
            {
                var result = await authorization.AuthorizeAsync(principal, resource, requirement).ConfigureAwait(false);
                if (!result.Succeeded)
                {
                    denied = result;
                    break;
                }
            }
        }
        catch (AuthorizationProviderUnavailableException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Authorization is temporarily unavailable.",
                detail: "The request could not be authorized safely.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "authorization_unavailable",
                });
        }

        return denied is null
            ? null
            : TenantRequestAccess.Problem("tenant_permission_denied", deniedDetail);
    }

    internal static void MarkMiddlewareAuthorized(HttpContext context, EndpointAccess access)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Items.ContainsKey(MiddlewareAuthorizedAccessKey))
        {
            throw new InvalidOperationException("Core API application authorization middleware executed more than once.");
        }

        context.Items[MiddlewareAuthorizedAccessKey] = access;
    }
}
