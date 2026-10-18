using Application.ApplicationEntry;

namespace Application.Web.Authentication;

public sealed class WebLoginConfiguration(Uri authority, Uri publicOrigin, string clientId, string clientSecret, string apiScope, string? strongAuthenticationAcr = null)
{
    public Uri Authority { get; } = authority;
    public Uri PublicOrigin { get; } = publicOrigin;
    public string ClientId { get; } = clientId;
    public string ClientSecret { get; } = clientSecret;
    public string ApiScope { get; } = apiScope;
    public string? StrongAuthenticationAcr { get; } = strongAuthenticationAcr;
    public static WebLoginConfiguration? Read(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Authentication:Enabled")) return null;
        if (!ServiceAddress.TryParse(configuration["Authentication:Authority"], out var authority) || authority!.Scheme != "https" ||
            !ServiceAddress.TryParse(configuration["Authentication:PublicOrigin"], out var origin) || origin!.Scheme != "https")
            throw new InvalidOperationException("Login requires exact HTTPS identity and Web origins.");
        var id = configuration["Authentication:ClientId"];
        var secret = configuration["Authentication:ClientSecret"];
        var scope = configuration["Authentication:ApiScope"];
        if (string.IsNullOrWhiteSpace(id) || id.Length > 200 || id.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(secret) || secret.Length > 4096 ||
            string.IsNullOrWhiteSpace(scope) || scope.Length > 512 || scope.Any(char.IsWhiteSpace) || scope.Any(char.IsControl) ||
            scope is "openid" or "offline_access")
            throw new InvalidOperationException("Login requires a client identity, deployment secret and one API audience scope.");
        var acr = configuration["Authentication:StrongAuthenticationAcr"];
        if (acr is not null && (acr.Length is < 1 or > 512 || acr.Any(c => !char.IsAscii(c) || char.IsControl(c) || char.IsWhiteSpace(c))))
            throw new InvalidOperationException("Identity verification requires one bounded strong-authentication ACR value.");
        return new(authority, origin, id, secret, scope, acr);
    }
}
