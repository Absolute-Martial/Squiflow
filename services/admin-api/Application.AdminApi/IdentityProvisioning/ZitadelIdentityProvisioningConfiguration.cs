using Application.AdminApi.Authentication;

namespace Application.AdminApi.IdentityProvisioning;

internal sealed record ZitadelIdentityProvisioningConfiguration(
    Uri ApiUri,
    string ApiToken,
    TimeSpan RequestTimeout)
{
    internal const string SectionName = "IdentityProvisioning:Zitadel";
    private const int MaximumTokenLength = 8192;

    internal static ZitadelIdentityProvisioningConfiguration From(
        IConfiguration configuration,
        AdminOidcAuthenticationConfiguration authentication)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(authentication);

        var section = configuration.GetRequiredSection(SectionName);
        var apiUrl = Required(section, "ApiUrl");
        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var apiUri) ||
            apiUri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(apiUri.Host) ||
            !string.IsNullOrEmpty(apiUri.UserInfo) ||
            !string.IsNullOrEmpty(apiUri.Query) ||
            !string.IsNullOrEmpty(apiUri.Fragment))
        {
            throw new InvalidOperationException(
                $"{SectionName}:ApiUrl must be an absolute HTTPS URL without user information, query or fragment.");
        }

        var authority = new Uri(authentication.Authority, UriKind.Absolute);
        if (!SameOrigin(apiUri, authority))
        {
            throw new InvalidOperationException(
                $"{SectionName}:ApiUrl must use the same origin as Authentication:Authority.");
        }

        var token = Required(section, "ApiToken");
        if (token.Length > MaximumTokenLength || token.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{SectionName}:ApiToken is invalid.");
        }

        var timeoutSeconds =
            int.TryParse(section["RequestTimeoutSeconds"], out var parsed) && parsed is >= 1 and <= 30
                ? parsed
                : throw new InvalidOperationException(
                    $"{SectionName}:RequestTimeoutSeconds must be an integer from 1 through 30.");

        return new ZitadelIdentityProvisioningConfiguration(
            new Uri(apiUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute),
            token,
            TimeSpan.FromSeconds(timeoutSeconds));
    }

    private static string Required(IConfigurationSection section, string name) =>
        section[name] is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{SectionName}:{name} is required.");

    private static bool SameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
        left.Port == right.Port;
}
