using System.Security.Claims;
using Application.IdentityAccess;
using Application.Tenancy;

namespace Application.CoreApi;

internal static class TenantMembershipEndpoint
{
    public static async Task<IResult> BrowseAsync(
        Guid? afterTenantId,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        IBoundedTenantMembershipDirectory memberships,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var access = await AuthenticatedAccountAccess.ResolveAsync(principal, resolveAccount, cancellationToken);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        if (afterTenantId == Guid.Empty)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid tenant continuation",
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_tenant_continuation" });
        }

        var page = await memberships.BrowseActiveAsync(access.Account!.AccountId, afterTenantId, cancellationToken);
        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new TenantMembershipPageResponse(
            page.Items.Select(value => new TenantMembershipResponse(value.TenantId, value.DisplayName)).ToArray(),
            page.NextTenantId));
    }

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
internal sealed record TenantMembershipPageResponse(TenantMembershipResponse[] Items, Guid? NextTenantId);
