namespace SquiFlow.CoreApi;

public sealed record OidcAuthenticationConfiguration(string Authority, string Audience)
{
    public const string SectionName = "Authentication";

    public static OidcAuthenticationConfiguration From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetRequiredSection(SectionName);
        var authority = section["Authority"];
        var audience = section["Audience"];

        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        if (authority.Length > 255 ||
            !string.Equals(authority, authority.Trim(), StringComparison.Ordinal) ||
            !Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri) ||
            !string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorityUri.Host) ||
            !string.IsNullOrEmpty(authorityUri.UserInfo) ||
            !string.IsNullOrEmpty(authorityUri.Query) ||
            !string.IsNullOrEmpty(authorityUri.Fragment))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Authority must be an exact absolute HTTPS issuer no longer than 255 characters and without user information, query, or fragment.");
        }

        if (audience.Length > 255 || audience.Any(char.IsControl) ||
            !string.Equals(audience, audience.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Audience must be a nonblank exact value no longer than 255 characters.");
        }

        return new OidcAuthenticationConfiguration(authority, audience);
    }
}
