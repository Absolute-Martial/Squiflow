namespace Application.AdminApi;

internal static class PlatformAdminAccessEndpoint
{
    private const string AuditOperation = "platform_access";

    internal static async Task<IResult> GetAsync(
        HttpContext context,
        PlatformAdminRequestAuthorizer authorizer)
    {
        var authorization = await authorizer
            .AuthorizeAsync(context, AuditOperation, PlatformAdminPermission.Access)
            .ConfigureAwait(false);
        if (authorization.Access is null)
        {
            return authorization.Failure
                ?? throw new InvalidOperationException(
                    "Denied Admin access did not provide a failure result.");
        }

        return TypedResults.Ok(new PlatformAdminAccessResponse(
            authorization.Access.PrincipalId,
            authorization.Access.DeviceId));
    }
}

internal sealed record PlatformAdminAccessResponse(Guid PrincipalId, Guid DeviceId);
