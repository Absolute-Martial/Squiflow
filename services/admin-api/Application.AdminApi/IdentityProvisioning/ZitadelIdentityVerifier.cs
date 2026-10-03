using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.AdminApi.Authentication;
using Application.IdentityAccess;

namespace Application.AdminApi.IdentityProvisioning;

internal enum ExternalIdentityVerification
{
    Verified = 1,
    NotFound = 2,
    NotInteractiveUser = 3,
    IssuerMismatch = 4,
}

internal interface IExternalIdentityVerifier
{
    Task<ExternalIdentityVerification> VerifyAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken);
}

internal sealed class ZitadelIdentityVerifier(
    HttpClient client,
    ZitadelIdentityProvisioningConfiguration configuration,
    AdminOidcAuthenticationConfiguration authentication)
    : IExternalIdentityVerifier
{
    private const int MaximumResponseBytes = 64 * 1024;

    public async Task<ExternalIdentityVerification> VerifyAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!string.Equals(identity.Issuer, authentication.Authority, StringComparison.Ordinal))
        {
            return ExternalIdentityVerification.IssuerMismatch;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuration.RequestTimeout);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(configuration.ApiUri, $"v2/users/{Uri.EscapeDataString(identity.Subject)}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ApiToken);
        request.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ExternalIdentityVerification.NotFound;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new AdminIdentityProviderUnavailableException(
                    "The identity provider rejected the administrative user lookup.");
            }

            if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
            {
                throw new AdminIdentityProviderUnavailableException(
                    "The identity provider returned an invalid user lookup response.");
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(timeout.Token)
                .ConfigureAwait(false);
            var body = new byte[MaximumResponseBytes + 1];
            var received = 0;
            while (received < body.Length)
            {
                var count = await stream
                    .ReadAsync(body.AsMemory(received), timeout.Token)
                    .ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                received += count;
            }

            if (received > MaximumResponseBytes)
            {
                throw new AdminIdentityProviderUnavailableException(
                    "The identity provider returned an invalid user lookup response.");
            }

            using var document = JsonDocument.Parse(
                body.AsMemory(0, received),
                new JsonDocumentOptions { MaxDepth = 12 });
            if (!document.RootElement.TryGetProperty("user", out var user) ||
                user.ValueKind != JsonValueKind.Object ||
                !user.TryGetProperty("userId", out var userId) ||
                userId.ValueKind != JsonValueKind.String ||
                !string.Equals(userId.GetString(), identity.Subject, StringComparison.Ordinal))
            {
                throw new AdminIdentityProviderUnavailableException(
                    "The identity provider returned an invalid user lookup response.");
            }

            return user.TryGetProperty("human", out var human) &&
                   human.ValueKind == JsonValueKind.Object
                ? ExternalIdentityVerification.Verified
                : ExternalIdentityVerification.NotInteractiveUser;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdminIdentityProviderUnavailableException(
                "The identity provider user lookup timed out.",
                exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            throw new AdminIdentityProviderUnavailableException(
                "The identity provider user lookup was unavailable.",
                exception);
        }
    }
}

internal sealed class AdminIdentityProviderUnavailableException : Exception
{
    internal AdminIdentityProviderUnavailableException(string message) : base(message)
    {
    }

    internal AdminIdentityProviderUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal static class AdminIdentityProvisioningRegistration
{
    internal static IServiceCollection AddAdminIdentityProvisioning(
        this IServiceCollection services,
        ZitadelIdentityProvisioningConfiguration configuration,
        AdminOidcAuthenticationConfiguration authentication)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(authentication);

        services.AddSingleton(configuration);
        services.AddSingleton(authentication);
        services.AddHttpClient<IExternalIdentityVerifier, ZitadelIdentityVerifier>(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
        });
        return services;
    }
}
