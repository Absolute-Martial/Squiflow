using Application.ApplicationEntry;
using Duende.IdentityModel.OidcClient;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using Duende.IdentityModel.Client;
using System.ComponentModel;

namespace Application.Workstation.Authentication;

public sealed class NativeCredential(string accessToken, DateTimeOffset expiresAt)
{
    public string AccessToken { get; } = accessToken;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
}

public interface INativeSignIn
{
    Uri CoreApiOrigin { get; }
    Task<NativeCredential?> SignInAsync(CancellationToken cancellationToken);
}

public sealed class NativeSignIn(NativeLoginConfiguration configuration, Func<Uri, Task>? openBrowser = null,
    Func<HttpClient>? httpFactory = null) : INativeSignIn
{
    public Uri CoreApiOrigin => configuration.CoreApiOrigin;

    public async Task<NativeCredential?> SignInAsync(CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromMinutes(3));
        using var browser = new LoopbackLoginBrowser(openBrowser);
        using var http = httpFactory?.Invoke() ?? ApplicationEntryClient.CreateHttpClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        http.MaxResponseContentBufferSize = 128 * 1024;
        var nonce = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var options = new OidcClientOptions
        {
            Authority = configuration.Authority.AbsoluteUri.TrimEnd('/'),
            ClientId = configuration.ClientId,
            Scope = "openid " + configuration.ApiScope,
            RedirectUri = browser.CallbackUri.AbsoluteUri,
            Browser = browser,
            LoadProfile = false,
            BrowserTimeout = TimeSpan.FromMinutes(3),
            IdentityTokenValidator = new NativeIdentityTokenValidator(nonce),
            LoggerFactory = NullLoggerFactory.Instance,
            HttpClientFactory = _ => http,
            Policy = new Policy { RequireIdentityTokenSignature = true, ValidateTokenIssuerName = true, ValidSignatureAlgorithms = ["RS256"] },
        };
        try
        {
            var parameters = new Parameters();
            parameters.Add("nonce", nonce);
            var result = await new OidcClient(options).LoginAsync(new LoginRequest { FrontChannelExtraParameters = parameters }, budget.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsError || string.IsNullOrWhiteSpace(result.AccessToken) || result.AccessToken.Length > 16 * 1024 ||
                result.AccessToken.Any(char.IsWhiteSpace) || result.AccessToken.Any(char.IsControl) ||
                result.AccessTokenExpiration <= DateTimeOffset.UtcNow) return null;
            var maximum = DateTimeOffset.UtcNow.AddMinutes(20);
            return new NativeCredential(result.AccessToken, result.AccessTokenExpiration < maximum ? result.AccessTokenExpiration : maximum);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (HttpRequestException) { return null; }
        catch (IOException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (Win32Exception) { return null; }
    }
}
