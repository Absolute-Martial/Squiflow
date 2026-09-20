namespace Application.IdentityAccess;

/// <summary>
/// Stable external authentication identity. Mutable profile fields such as email and display
/// name are deliberately excluded from account identity.
/// </summary>
public sealed record ExternalIdentity
{
    private const int ComponentLimit = 255;

    private ExternalIdentity(string issuer, string subject)
    {
        Issuer = issuer;
        Subject = subject;
    }

    public string Issuer { get; }

    public string Subject { get; }

    public static ExternalIdentity Create(string issuer, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var exactIssuer = issuer;
        var exactSubject = subject;

        if (!string.Equals(exactIssuer, exactIssuer.Trim(), StringComparison.Ordinal) ||
            exactIssuer.Length > ComponentLimit ||
            !Uri.TryCreate(exactIssuer, UriKind.Absolute, out var issuerUri) ||
            !string.Equals(issuerUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(issuerUri.Host) ||
            !string.IsNullOrEmpty(issuerUri.UserInfo) ||
            !string.IsNullOrEmpty(issuerUri.Query) ||
            !string.IsNullOrEmpty(issuerUri.Fragment))
        {
            throw new ArgumentException(
                $"Issuer must be an absolute HTTPS issuer no longer than {ComponentLimit} characters.",
                nameof(issuer));
        }

        if (exactSubject.Length > ComponentLimit || exactSubject.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Subject must be no longer than {ComponentLimit} characters and contain no control characters.",
                nameof(subject));
        }

        return new ExternalIdentity(exactIssuer, exactSubject);
    }
}
