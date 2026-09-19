using System.Security.Cryptography;
using System.Text;

namespace SquiFlow.Branding;

/// <summary>
/// Public, presentation-safe application identity. It deliberately excludes arbitrary HTML,
/// script and CSS so a branding change cannot become an executable-content boundary.
/// </summary>
public sealed record BrandProfile
{
    private const int NameLimit = 120;
    private const int UrlLimit = 2_048;

    private BrandProfile(
        string displayName,
        string shortName,
        string legalName,
        string themeKey,
        string logoUrl,
        string faviconUrl,
        string supportUrl,
        string privacyUrl,
        string termsUrl)
    {
        DisplayName = displayName;
        ShortName = shortName;
        LegalName = legalName;
        ThemeKey = themeKey;
        LogoUrl = logoUrl;
        FaviconUrl = faviconUrl;
        SupportUrl = supportUrl;
        PrivacyUrl = privacyUrl;
        TermsUrl = termsUrl;
        Revision = CalculateRevision(this);
    }

    public string DisplayName { get; }

    public string ShortName { get; }

    public string LegalName { get; }

    public string ThemeKey { get; }

    public string LogoUrl { get; }

    public string FaviconUrl { get; }

    public string SupportUrl { get; }

    public string PrivacyUrl { get; }

    public string TermsUrl { get; }

    public string Revision { get; }

    public static BrandProfile Create(
        string displayName,
        string shortName,
        string legalName,
        string themeKey,
        string logoUrl,
        string faviconUrl,
        string supportUrl,
        string privacyUrl,
        string termsUrl)
    {
        return new BrandProfile(
            RequiredName(displayName, nameof(displayName)),
            RequiredName(shortName, nameof(shortName)),
            RequiredName(legalName, nameof(legalName)),
            RequiredThemeKey(themeKey),
            RequiredSafeUrl(logoUrl, nameof(logoUrl)),
            RequiredSafeUrl(faviconUrl, nameof(faviconUrl)),
            RequiredSafeUrl(supportUrl, nameof(supportUrl)),
            RequiredSafeUrl(privacyUrl, nameof(privacyUrl)),
            RequiredSafeUrl(termsUrl, nameof(termsUrl)));
    }

    private static string RequiredName(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        var normalized = value.Trim();
        if (normalized.Length > NameLimit || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"{parameterName} must be at most {NameLimit} characters and contain no control characters.",
                parameterName);
        }

        return normalized;
    }

    private static string RequiredThemeKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        if (normalized.Length > 64 || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "themeKey must contain only ASCII letters, digits, '-' or '_' and be at most 64 characters.",
                nameof(value));
        }

        return normalized;
    }

    private static string RequiredSafeUrl(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        var normalized = value.Trim();
        if (normalized.Length > UrlLimit || normalized.Any(char.IsControl))
        {
            throw new ArgumentException($"{parameterName} is not a valid branding URL.", parameterName);
        }

        if (normalized[0] == '/' &&
            !normalized.StartsWith("//", StringComparison.Ordinal))
        {
            return normalized;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException(
                $"{parameterName} must be an application-relative URL or an absolute HTTPS URL without user information.",
                parameterName);
        }

        return uri.AbsoluteUri;
    }

    private static string CalculateRevision(BrandProfile profile)
    {
        var canonical = string.Join('\n',
            profile.DisplayName,
            profile.ShortName,
            profile.LegalName,
            profile.ThemeKey,
            profile.LogoUrl,
            profile.FaviconUrl,
            profile.SupportUrl,
            profile.PrivacyUrl,
            profile.TermsUrl);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(digest.AsSpan(0, 12));
    }
}
