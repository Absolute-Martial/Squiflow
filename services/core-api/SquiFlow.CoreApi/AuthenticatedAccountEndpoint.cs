using System.Security.Claims;
using SquiFlow.IdentityAccess;

namespace SquiFlow.CoreApi;

internal static class AuthenticatedAccountEndpoint
{
    public static async Task<IResult> GetAsync(
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var access = await AuthenticatedAccountAccess.ResolveAsync(
            principal,
            resolveAccount,
            cancellationToken);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new AuthenticatedAccountResponse(access.Account!.AccountId));
    }
}

internal sealed record AuthenticatedAccountResponse(Guid AccountId);
