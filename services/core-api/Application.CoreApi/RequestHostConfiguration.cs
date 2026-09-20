namespace Application.CoreApi;

internal static class RequestHostConfiguration
{
    private const int MaximumHostCount = 64;

    internal static void Validate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "AllowedHosts must contain at least one exact host or bounded wildcard host pattern.");
        }

        var hosts = configured.Split(';', StringSplitOptions.None);
        if (hosts.Length == 0 ||
            hosts.Length > MaximumHostCount ||
            hosts.Any(string.IsNullOrEmpty))
        {
            throw new InvalidOperationException(
                $"AllowedHosts must contain between 1 and {MaximumHostCount} host entries.");
        }

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hostEntry in hosts)
        {
            var host = hostEntry.Trim();
            if (!string.Equals(host, hostEntry, StringComparison.Ordinal) ||
                !unique.Add(host) ||
                host == "*" ||
                host.Any(char.IsControl))
            {
                throw new InvalidOperationException(
                    "AllowedHosts contains a wildcard-all, duplicate, whitespace-padded, or control-character entry.");
            }

            var isWildcard = host.StartsWith("*.", StringComparison.Ordinal);
            var exactHost = isWildcard
                ? host[2..]
                : host;
            var hostForValidation = exactHost.Length > 2 &&
                exactHost[0] == '[' &&
                exactHost[^1] == ']'
                    ? exactHost[1..^1]
                    : exactHost;
            var hostType = Uri.CheckHostName(hostForValidation);
            if (exactHost.Length == 0 ||
                exactHost.Length > 253 ||
                hostType == UriHostNameType.Unknown ||
                (isWildcard && hostType != UriHostNameType.Dns))
            {
                throw new InvalidOperationException(
                    "AllowedHosts entries must be exact DNS/IP hosts or left-most-label wildcard DNS hosts without schemes, ports, or paths.");
            }
        }
    }
}
