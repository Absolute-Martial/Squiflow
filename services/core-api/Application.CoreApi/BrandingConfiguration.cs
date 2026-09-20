using Application.Branding;

namespace Application.CoreApi;

internal sealed class BrandingConfiguration
{
    public const string SectionName = "Branding";

    public string DisplayName { get; init; } = string.Empty;

    public string ShortName { get; init; } = string.Empty;

    public string LegalName { get; init; } = string.Empty;

    public string ThemeKey { get; init; } = string.Empty;

    public string LogoUrl { get; init; } = string.Empty;

    public string FaviconUrl { get; init; } = string.Empty;

    public string SupportUrl { get; init; } = string.Empty;

    public string PrivacyUrl { get; init; } = string.Empty;

    public string TermsUrl { get; init; } = string.Empty;

    public int? CacheMaxAgeSeconds { get; init; }

    public BrandProfile ToProfile() => BrandProfile.Create(
        DisplayName,
        ShortName,
        LegalName,
        ThemeKey,
        LogoUrl,
        FaviconUrl,
        SupportUrl,
        PrivacyUrl,
        TermsUrl);

    public int GetCacheMaxAgeSeconds()
    {
        if (CacheMaxAgeSeconds is < 0 or > 86_400 || CacheMaxAgeSeconds is null)
        {
            throw new InvalidOperationException(
                $"{SectionName}:CacheMaxAgeSeconds must be an integer between 0 and 86400 seconds.");
        }

        return CacheMaxAgeSeconds.Value;
    }
}
