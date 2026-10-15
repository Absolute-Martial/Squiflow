using System.Security.Claims;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Results;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Application.Workstation.Authentication;

public sealed class NativeIdentityTokenValidator(string expectedNonce) : IIdentityTokenValidator
{
    public async Task<IdentityTokenValidationResult> ValidateAsync(string identityToken, OidcClientOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(identityToken) || identityToken.Length > 16 * 1024)
            return new() { Error = "invalid_identity" };
        var keys = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(JsonSerializer.Serialize(options.ProviderInformation.KeySet));
        var handler = new JsonWebTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = 16 * 1024 };
        var result = await handler.ValidateTokenAsync(identityToken, new TokenValidationParameters
        {
            ValidIssuer = options.Authority.TrimEnd('/'),
            ValidAudience = options.ClientId,
            IssuerSigningKeys = keys.GetSigningKeys(),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            IncludeTokenOnFailedValidation = false,
            LogValidationExceptions = false,
        }).ConfigureAwait(false);
        if (!result.IsValid || result.ClaimsIdentity.FindAll("sub").Count() != 1 ||
            string.IsNullOrWhiteSpace(result.ClaimsIdentity.FindFirst("sub")?.Value) ||
            result.ClaimsIdentity.FindAll("nonce").Count() != 1 ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedNonce),
                Encoding.UTF8.GetBytes(result.ClaimsIdentity.FindFirst("nonce")?.Value ?? "")))
            return new() { Error = "invalid_identity" };
        return new() { User = new ClaimsPrincipal(result.ClaimsIdentity), SignatureAlgorithm = SecurityAlgorithms.RsaSha256 };
    }
}
