using Application.Branding;

namespace Application.ApplicationEntry;

public enum ConnectionStatus { Ready, NotReady, Unavailable, TimedOut, InvalidResponse }

public sealed record PublicApplicationIdentity(
    BrandProfile Brand,
    Uri LogoUrl,
    Uri FaviconUrl,
    Uri SupportUrl,
    Uri PrivacyUrl,
    Uri TermsUrl);

public sealed record ConnectionCheck(ConnectionStatus Status, PublicApplicationIdentity? Application = null)
{
    public string Heading => Status switch
    {
        ConnectionStatus.Ready => "Service available",
        ConnectionStatus.NotReady => "Service is getting ready",
        ConnectionStatus.TimedOut => "Connection timed out",
        ConnectionStatus.InvalidResponse => "Application details unavailable",
        _ => "Unable to connect"
    };

    public string Message => Status switch
    {
        ConnectionStatus.Ready => "The application service is responding. This check does not sign you in or grant workspace access.",
        ConnectionStatus.NotReady => "The service is reachable, but cannot accept work yet. Try checking again shortly.",
        ConnectionStatus.TimedOut => "The service took too long to respond. Check your connection and try again.",
        ConnectionStatus.InvalidResponse => "The service did not return valid application details. Contact your administrator.",
        _ => "Check your connection and service address, then try again."
    };
}
