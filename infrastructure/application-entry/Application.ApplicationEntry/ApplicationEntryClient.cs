using System.Net;
using System.Text.Json;
using Application.Branding;

namespace Application.ApplicationEntry;

public sealed class ApplicationEntryClient(HttpClient client)
{
    public const int MaximumBootstrapBytes = 16_384;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 16 };

    public static HttpClient CreateHttpClient() => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        MaxConnectionsPerServer = 2,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public async Task<ConnectionCheck> CheckAsync(Uri serviceAddress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceAddress);
        if (!serviceAddress.IsAbsoluteUri || !ServiceAddress.TryParse(serviceAddress.AbsoluteUri, out _))
        {
            throw new ArgumentException("Use an HTTPS service address, or HTTP on localhost, without a path or credentials.", nameof(serviceAddress));
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            using var bootstrap = await client.GetAsync(new Uri(serviceAddress, "api/v1/application/bootstrap"),
                HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false);
            if (bootstrap.StatusCode != HttpStatusCode.OK)
            {
                return new(ConnectionStatus.Unavailable);
            }

            if (!string.Equals(bootstrap.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                bootstrap.Content.Headers.ContentLength > MaximumBootstrapBytes)
            {
                return new(ConnectionStatus.InvalidResponse);
            }

            await using var stream = await bootstrap.Content.ReadAsStreamAsync(budget.Token).ConfigureAwait(false);
            var bytes = new byte[MaximumBootstrapBytes + 1];
            var length = 0;
            while (length < bytes.Length)
            {
                var count = await stream.ReadAsync(bytes.AsMemory(length), budget.Token).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                length += count;
            }

            if (length > MaximumBootstrapBytes)
            {
                return new(ConnectionStatus.InvalidResponse);
            }

            var document = JsonSerializer.Deserialize<BootstrapDocument>(bytes.AsSpan(0, length), JsonOptions);
            if (document is null)
            {
                return new(ConnectionStatus.InvalidResponse);
            }

            var brand = BrandProfile.Create(document.DisplayName!, document.ShortName!, document.LegalName!,
                document.ThemeKey!, document.LogoUrl!, document.FaviconUrl!, document.SupportUrl!,
                document.PrivacyUrl!, document.TermsUrl!);
            if (brand.Revision != document.Revision)
            {
                return new(ConnectionStatus.InvalidResponse);
            }

            var identity = new PublicApplicationIdentity(brand,
                ResolveLink(serviceAddress, brand.LogoUrl), ResolveLink(serviceAddress, brand.FaviconUrl),
                ResolveLink(serviceAddress, brand.SupportUrl), ResolveLink(serviceAddress, brand.PrivacyUrl),
                ResolveLink(serviceAddress, brand.TermsUrl));
            using var readiness = await client.GetAsync(new Uri(serviceAddress, "health/ready"),
                HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false);
            return readiness.StatusCode switch
            {
                HttpStatusCode.OK => new(ConnectionStatus.Ready, identity),
                HttpStatusCode.ServiceUnavailable => new(ConnectionStatus.NotReady, identity),
                _ => new(ConnectionStatus.Unavailable)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ConnectionStatus.TimedOut);
        }
        catch (HttpRequestException)
        {
            return new(ConnectionStatus.Unavailable);
        }
        catch (IOException)
        {
            return new(ConnectionStatus.Unavailable);
        }
        catch (JsonException)
        {
            return new(ConnectionStatus.InvalidResponse);
        }
        catch (ArgumentException)
        {
            return new(ConnectionStatus.InvalidResponse);
        }
    }

    private static Uri ResolveLink(Uri serviceAddress, string value)
    {
        if (value.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid application link.");
        }

        var link = new Uri(serviceAddress, value);
        if (link.UserInfo.Length != 0 ||
            !(link.Scheme == Uri.UriSchemeHttps ||
              link.Scheme == Uri.UriSchemeHttp && serviceAddress.IsLoopback && link.Authority == serviceAddress.Authority))
        {
            throw new ArgumentException("Invalid application link.");
        }

        return link;
    }

    private sealed record BootstrapDocument(
        string? DisplayName, string? ShortName, string? LegalName, string? ThemeKey,
        string? LogoUrl, string? FaviconUrl, string? SupportUrl, string? PrivacyUrl,
        string? TermsUrl, string? Revision);
}
