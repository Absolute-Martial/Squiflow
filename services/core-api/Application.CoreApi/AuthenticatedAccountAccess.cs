using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Application.IdentityAccess;

namespace Application.CoreApi;

internal static class AuthenticatedAccountAccess
{
    public static async Task<AuthenticatedAccountAccessResult> ResolveAsync(
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        CancellationToken cancellationToken)
    {
        var issuer = principal.FindFirstValue("iss");
        var subject = principal.FindFirstValue("sub");

        if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(subject))
        {
            return AuthenticatedAccountAccessResult.Denied(Problem(
                StatusCodes.Status401Unauthorized,
                "authenticated_identity_incomplete",
                "The validated identity did not contain its stable issuer and subject."));
        }

        var binding = await resolveAccount.ExecuteAsync(
            ExternalIdentity.Create(issuer, subject),
            cancellationToken);

        if (binding is null)
        {
            return AuthenticatedAccountAccessResult.Denied(Problem(
                StatusCodes.Status403Forbidden,
                "account_not_bound",
                "The authenticated identity is not bound to an application account."));
        }

        if (binding.Availability != AccountAvailability.Active)
        {
            return AuthenticatedAccountAccessResult.Denied(Problem(
                StatusCodes.Status403Forbidden,
                "account_disabled",
                "The application account is disabled."));
        }

        return AuthenticatedAccountAccessResult.Allowed(binding);
    }

    private static ProblemHttpResult Problem(int status, string code, string detail) =>
        TypedResults.Problem(
            statusCode: status,
            title: "Account access denied.",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}

internal sealed record AuthenticatedAccountAccessResult(
    AccountBinding? Account,
    ProblemHttpResult? Failure)
{
    public static AuthenticatedAccountAccessResult Allowed(AccountBinding account) => new(account, null);

    public static AuthenticatedAccountAccessResult Denied(ProblemHttpResult failure) => new(null, failure);
}
