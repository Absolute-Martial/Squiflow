namespace Application.AdminApi.Authentication;

internal sealed record AdminOidcAuthenticationConfiguration(
    string Authority,
    string Audience,
    TimeSpan BackchannelTimeout,
    TimeSpan ClockSkew)
{
    private const string SectionName = "Authentication";

    internal static AdminOidcAuthenticationConfiguration From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetRequiredSection(SectionName);
        var authority = Required(section, "Authority");
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri) ||
            authorityUri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(authorityUri.Host) ||
            !string.IsNullOrEmpty(authorityUri.UserInfo) ||
            !string.IsNullOrEmpty(authorityUri.Query) ||
            !string.IsNullOrEmpty(authorityUri.Fragment))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Authority must be an absolute HTTPS issuer without user information, query or fragment.");
        }

        var audience = Required(section, "Audience");
        var backchannelTimeoutSeconds = BoundedInt(section, "BackchannelTimeoutSeconds", 1, 30);
        var clockSkewSeconds = BoundedInt(section, "ClockSkewSeconds", 0, 120);
        return new AdminOidcAuthenticationConfiguration(
            authorityUri.AbsoluteUri.TrimEnd('/'),
            audience,
            TimeSpan.FromSeconds(backchannelTimeoutSeconds),
            TimeSpan.FromSeconds(clockSkewSeconds));
    }

    private static string Required(IConfigurationSection section, string name) =>
        section[name] is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{SectionName}:{name} is required.");

    private static int BoundedInt(
        IConfigurationSection section,
        string name,
        int minimum,
        int maximum) =>
        int.TryParse(section[name], out var value) && value >= minimum && value <= maximum
            ? value
            : throw new InvalidOperationException(
                $"{SectionName}:{name} must be an integer from {minimum} through {maximum}.");
}
