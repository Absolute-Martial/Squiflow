using System.Security.Claims;
using Application.IdentityAccess;
using Application.Tenancy;

namespace Application.CoreApi;

internal static class TenantMembershipEndpoint
{
    public static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ITenantMembershipDirectory memberships,
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

        var activeMemberships = await memberships.ListActiveAsync(
            access.Account!.AccountId,
            cancellationToken);

        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(activeMemberships
            .Select(membership => new TenantMembershipResponse(
                membership.TenantId,
                membership.DisplayName))
            .ToArray());
    }
}

internal sealed record TenantMembershipResponse(Guid TenantId, string DisplayName);
