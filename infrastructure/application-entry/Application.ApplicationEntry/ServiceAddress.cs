namespace Application.ApplicationEntry;

public static class ServiceAddress
{
    public static bool TryParse(string? value, out Uri? address)
    {
        address = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2_048 ||
            value.Any(char.IsControl) || value.Contains('\\', StringComparison.Ordinal) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var candidate) ||
            candidate.AbsolutePath != "/" || candidate.Query.Length != 0 ||
            candidate.Fragment.Length != 0 || candidate.UserInfo.Length != 0 ||
            string.IsNullOrEmpty(candidate.Host) ||
            !(candidate.Scheme == Uri.UriSchemeHttps ||
              candidate.Scheme == Uri.UriSchemeHttp && candidate.IsLoopback))
        {
            return false;
        }

        address = candidate;
        return true;
    }
}
