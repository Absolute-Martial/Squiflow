using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Application.Entry.TestSupport;

internal sealed class EntryProtocolFixture : HttpMessageHandler
{
    internal const string Issuer = "https://identity.example.test";
    internal const string ApiScope = "urn:zitadel:iam:org:project:id:tenant-access-test:aud";
    internal const string AccessToken = "synthetic-access-token-never-render-this";
    private static readonly object Discovery = new
    {
        issuer = Issuer,
        authorization_endpoint = Issuer + "/authorize",
        token_endpoint = Issuer + "/token",
        jwks_uri = Issuer + "/keys",
        response_types_supported = new[] { "code" },
        response_modes_supported = new[] { "query" },
        grant_types_supported = new[] { "authorization_code" },
        token_endpoint_auth_methods_supported = new[] { "none", "client_secret_post" },
        id_token_signing_alg_values_supported = new[] { "RS256" },
        code_challenge_methods_supported = new[] { "S256" },
        scopes_supported = new[] { "openid", ApiScope },
    };
    private readonly RSA rsa = RSA.Create(2048);
    internal string ClientId { get; set; } = "entry-client";
    internal string? Nonce { get; set; }
    internal string? Challenge { get; set; }
    internal bool InvalidNonce { get; set; }
    internal bool InvalidAudience { get; set; }
    internal bool InvalidSignature { get; set; }
    internal bool PkceVerified { get; private set; }
    internal bool ClientSecretSeen { get; private set; }
    internal Guid Account { get; } = Guid.NewGuid();
    internal Guid FirstTenant { get; } = Guid.NewGuid();
    internal Guid SecondTenant { get; } = Guid.NewGuid();
    internal bool DenyWorkspace { get; set; }
    internal bool NoMemberships { get; set; }
    internal bool DisabledAccount { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/.well-known/openid-configuration")
            return Json(Discovery);
        if (path == "/keys")
        {
            var key = rsa.ExportParameters(false);
            return Json(new { keys = new[] { new { kty = "RSA", use = "sig", alg = "RS256", kid = "test-signing", n = WebEncoders.Base64UrlEncode(key.Modulus!), e = WebEncoders.Base64UrlEncode(key.Exponent!) } } });
        }
        if (path == "/token")
        {
            var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
            ClientSecretSeen = form.ContainsKey("client_secret");
            var verifier = form["code_verifier"].ToString();
            PkceVerified = verifier.Length >= 43 && WebEncoders.Base64UrlEncode(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier))) == Challenge;
            if (!PkceVerified) return Json(new { error = "invalid_grant" }, HttpStatusCode.BadRequest);
            using var bad = RSA.Create(2048);
            var key = new RsaSecurityKey(InvalidSignature ? bad : rsa) { KeyId = "test-signing" };
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = InvalidAudience ? "another-client" : ClientId,
                Expires = DateTime.UtcNow.AddMinutes(10),
                IssuedAt = DateTime.UtcNow,
                Claims = new Dictionary<string, object> { ["sub"] = "synthetic-human", ["nonce"] = InvalidNonce ? "wrong-nonce" : Nonce! },
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
            });
            return Json(new { access_token = AccessToken, token_type = "Bearer", expires_in = 600, id_token = token });
        }
        if (path.StartsWith("/api/", StringComparison.Ordinal))
        {
            if (request.Headers.Authorization?.Parameter != AccessToken) return new(HttpStatusCode.Unauthorized);
            if (DisabledAccount) return Problem("account_disabled");
            if (path == "/api/v1/account") return Json(new { accountId = Account });
            if (path == "/api/v1/account/tenants/page") return Json(new
            {
                items = NoMemberships ? [] : new[] { new { tenantId = FirstTenant, displayName = "North workspace" }, new { tenantId = SecondTenant, displayName = "<script>South workspace</script>" } },
                nextTenantId = (Guid?)null,
            });
            if (path.EndsWith("/workspace", StringComparison.Ordinal))
            {
                if (DenyWorkspace) return Problem("tenant_access_denied");
                var id = Guid.Parse(path.Split('/')[4]);
                return Json(new { tenantId = id, displayName = id == FirstTenant ? "North workspace" : "South workspace" });
            }
        }
        if (path == "/health/ready") return new(HttpStatusCode.OK);
        if (path == "/api/v1/application/bootstrap") throw new InvalidOperationException("Handle public branding in the host fixture.");
        return new(HttpStatusCode.NotFound);
    }

    internal void ObserveAuthorization(Uri uri)
    {
        var query = QueryHelpers.ParseQuery(uri.Query);
        Nonce = query["nonce"]; Challenge = query["code_challenge"];
    }
    private static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = JsonContent.Create(value) };
    private static HttpResponseMessage Problem(string code) => new(HttpStatusCode.Forbidden) { Content = JsonContent.Create(new { code }, mediaType: new("application/problem+json")) };
    protected override void Dispose(bool disposing) { if (disposing) rsa.Dispose(); base.Dispose(disposing); }
}
