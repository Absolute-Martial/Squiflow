namespace Application.Branding;

/// <summary>
/// Deployment-supplied public identity values. Validation is performed by BrandProfile,
/// which is the presentation-safe contract exposed to clients.
/// </summary>
public sealed class BrandingConfiguration
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
}
