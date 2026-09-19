using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SquiFlow.IdentityAccess;
using SquiFlow.Tenancy;
using System.Security.Claims;
using System.Security.Cryptography;
using Xunit;

namespace SquiFlow.CoreApi.Tests;

public sealed class ApplicationBootstrapTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly HttpClient _client;

    public ApplicationBootstrapTests(WhiteLabelApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task BootstrapReturnsOnlyTheConfiguredPublicBrand()
    {
        using var response = await _client.GetAsync("/api/v1/application/bootstrap");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("SquiFlow", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Example Operations", body, StringComparison.Ordinal);
        Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString());
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task BootstrapContractContainsAStableRevision()
    {
        var profile = await _client.GetFromJsonAsync<BootstrapContract>(
            "/api/v1/application/bootstrap");

        Assert.NotNull(profile);
        Assert.Equal("Example Operations", profile.DisplayName);
        Assert.Equal("example-brand", profile.ThemeKey);
        Assert.Equal(24, profile.Revision.Length);
    }

    private sealed record BootstrapContract(string DisplayName, string ThemeKey, string Revision);
}

public sealed class WhiteLabelApiFactory : WebApplicationFactory<Program>
{
    public const string Authority = "https://identity.example.test";
    public const string Audience = "squiflow-core-api";

    private readonly RSA _signingRsa = RSA.Create(2048);
    private readonly RsaSecurityKey _signingKey;
    private readonly TestAccountBindingDirectory _bindings = new();
    private readonly TestTenantMembershipDirectory _memberships = new();

    public WhiteLabelApiFactory()
    {
        _signingKey = new RsaSecurityKey(_signingRsa) { KeyId = "test-signing-key" };
    }

    public void Bind(string subject, Guid accountId, AccountAvailability availability = AccountAvailability.Active) =>
        _bindings.Bind(Authority, subject, new AccountBinding(accountId, availability));

    public void AddTenantMembership(Guid accountId, Guid tenantId, string displayName) =>
        _memberships.Add(accountId, new TenantMembership(tenantId, displayName));

    public string CreateToken(
        string? subject = "subject-42",
        string issuer = Authority,
        string audience = Audience,
        DateTime? expires = null,
        SecurityKey? signingKey = null)
    {
        var claims = subject is null
            ? Array.Empty<Claim>()
            : [new Claim("sub", subject)];
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                signingKey ?? _signingKey,
                SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:Authority", Authority);
        builder.UseSetting("Authentication:Audience", Audience);
        builder.UseSetting("ConnectionStrings:SquiFlow", "Host=unused.example.test;Database=squiflow");
        builder.UseSetting("Branding:DisplayName", "Example Operations");
        builder.UseSetting("Branding:ShortName", "Example");
        builder.UseSetting("Branding:LegalName", "Example Company Ltd.");
        builder.UseSetting("Branding:ThemeKey", "example-brand");
        builder.UseSetting("Branding:LogoUrl", "/tenant-assets/logo.svg");
        builder.UseSetting("Branding:FaviconUrl", "/tenant-assets/favicon.svg");
        builder.UseSetting("Branding:SupportUrl", "https://support.example.test");
        builder.UseSetting("Branding:PrivacyUrl", "https://www.example.test/privacy");
        builder.UseSetting("Branding:TermsUrl", "https://www.example.test/terms");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountBindingDirectory>();
            services.AddSingleton<IAccountBindingDirectory>(_bindings);
            services.RemoveAll<ITenantMembershipDirectory>();
            services.AddSingleton<ITenantMembershipDirectory>(_memberships);
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    var configuration = new OpenIdConnectConfiguration { Issuer = Authority };
                    configuration.SigningKeys.Add(_signingKey);
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _signingRsa.Dispose();
        }
    }

    private sealed class TestAccountBindingDirectory : IAccountBindingDirectory
    {
        private readonly Dictionary<(string Issuer, string Subject), AccountBinding> _bindings = [];
        private readonly object _gate = new();

        public void Bind(string issuer, string subject, AccountBinding binding)
        {
            lock (_gate)
            {
                _bindings[(issuer, subject)] = binding;
            }
        }

        public Task<AccountBinding?> FindAsync(
            ExternalIdentity identity,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _bindings.TryGetValue((identity.Issuer, identity.Subject), out var binding);
                return Task.FromResult(binding);
            }
        }
    }

    private sealed class TestTenantMembershipDirectory : ITenantMembershipDirectory
    {
        private readonly Dictionary<Guid, List<TenantMembership>> _memberships = [];
        private readonly object _gate = new();

        public void Add(Guid accountId, TenantMembership membership)
        {
            lock (_gate)
            {
                if (!_memberships.TryGetValue(accountId, out var values))
                {
                    values = [];
                    _memberships.Add(accountId, values);
                }

                values.Add(membership);
            }
        }

        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
            Guid accountId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<TenantMembership>>(
                    _memberships.TryGetValue(accountId, out var values)
                        ? values.ToArray()
                        : []);
            }
        }

        public Task<bool> IsActiveAsync(
            Guid accountId,
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                return Task.FromResult(
                    _memberships.TryGetValue(accountId, out var values) &&
                    values.Any(value => value.TenantId == tenantId));
            }
        }
    }
}
