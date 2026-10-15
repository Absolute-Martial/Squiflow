using Application.ApplicationEntry;

namespace Application.Workstation.Authentication;

public sealed record NativeLoginConfiguration(Uri Authority, Uri CoreApiOrigin, string ClientId, string ApiScope)
{
    public static NativeLoginConfiguration? ReadEnvironment()
    {
        var authority = Environment.GetEnvironmentVariable("APPLICATION_OIDC_AUTHORITY");
        var id = Environment.GetEnvironmentVariable("APPLICATION_OIDC_CLIENT_ID");
        var scope = Environment.GetEnvironmentVariable("APPLICATION_OIDC_API_SCOPE");
        if (authority is null && id is null && scope is null) return null;
        if (!ServiceAddress.TryParse(authority, out var issuer) || issuer!.Scheme != "https" ||
            !ServiceAddress.TryParse(Environment.GetEnvironmentVariable("APPLICATION_CORE_API_URL"), out var api) || api!.Scheme != "https" ||
            string.IsNullOrWhiteSpace(id) || id.Length > 200 || id.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(scope) || scope.Length > 512 || scope.Any(char.IsWhiteSpace) || scope.Any(char.IsControl) ||
            scope is "openid" or "offline_access")
            throw new InvalidOperationException("Native login requires exact HTTPS issuer/API origins, a public client identity and one API audience scope.");
        return new(issuer, api, id, scope);
    }
}
