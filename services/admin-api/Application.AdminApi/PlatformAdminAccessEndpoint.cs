namespace Application.AdminApi;

internal static class PlatformAdminAccessEndpoint
{
    internal static IResult GetAsync(HttpContext context)
    {
        var access = AdminApiPlatformAuthorization.GetRequiredAccess(context);

        return TypedResults.Ok(new PlatformAdminAccessResponse(
            access.PrincipalId,
            access.DeviceId));
    }
}

internal sealed record PlatformAdminAccessResponse(Guid PrincipalId, Guid DeviceId);
