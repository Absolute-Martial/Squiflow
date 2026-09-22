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
using Application.Branding;
using Application.CoreApi;
using Application.CoreApi.Authorization;
using Application.IdentityAccess.Postgres;
using Application.IdentityAccess;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using System.Security.Claims;
using System.Security.Cryptography;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class ApplicationBootstrapTests : IClassFixture<WhiteLabelApiFactory>
{
    private static readonly string[] ActiveSourceRoots = ["modules", "services", "tests"];
    private static readonly string DevelopmentCodename = string.Concat("Squi", "Flow");

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
        Assert.DoesNotContain(DevelopmentCodename, body, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void MissingDeploymentBrandHasNoCodenameFallback()
    {
        var configuration = new BrandingConfiguration();

        var exception = Assert.Throws<ArgumentException>(() => configuration.ToProfile());

        Assert.Equal("displayName", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(86401)]
    public void InvalidBootstrapCachePolicyFailsConfiguration(int? seconds)
    {
        var configuration = new BrandingConfiguration { CacheMaxAgeSeconds = seconds };

        Assert.Throws<InvalidOperationException>(() => configuration.GetCacheMaxAgeSeconds());
    }

    [Fact]
    public void CheckedInRuntimeConfigurationContainsNoDevelopmentCodename()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appSettings = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "services",
            "core-api",
            "Application.CoreApi",
            "appsettings.json"));

        Assert.DoesNotContain(DevelopmentCodename, appSettings, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActiveBuildAndAssemblyIdentitiesContainNoDevelopmentCodename()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoots = ActiveSourceRoots
            .Select(name => new DirectoryInfo(Path.Combine(repositoryRoot.FullName, name)));
        var identityPaths = sourceRoots
            .SelectMany(root => root.EnumerateDirectories("*", SearchOption.AllDirectories)
                .Select(directory => directory.Name)
                .Concat(root.EnumerateFiles("*.csproj", SearchOption.AllDirectories)
                    .Select(project => project.Name)))
            .Append("Application.slnx");

        Assert.DoesNotContain(identityPaths, identity =>
            identity.Contains(DevelopmentCodename, StringComparison.OrdinalIgnoreCase));

        var activeIdentityFiles = sourceRoots
            .SelectMany(root => root.EnumerateFiles("*", SearchOption.AllDirectories))
            .Where(file => file.Extension is ".cs" or ".csproj" or ".json")
            .Where(file => !IsGeneratedBuildPath(file.FullName))
            .Append(new FileInfo(Path.Combine(repositoryRoot.FullName, "Application.slnx")));
        Assert.DoesNotContain(activeIdentityFiles, file =>
            File.ReadAllText(file.FullName)
                .Contains(DevelopmentCodename, StringComparison.OrdinalIgnoreCase));

        var assemblyNames = new[]
        {
            typeof(BrandProfile).Assembly.GetName().Name,
            typeof(AccountBinding).Assembly.GetName().Name,
            typeof(IdentityAccessDbContext).Assembly.GetName().Name,
            typeof(TenantContext).Assembly.GetName().Name,
            typeof(TenancyDbContext).Assembly.GetName().Name,
            typeof(Program).Assembly.GetName().Name,
            typeof(ApplicationBootstrapTests).Assembly.GetName().Name,
        };

        Assert.DoesNotContain(assemblyNames, name =>
            name is null || name.Contains(DevelopmentCodename, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGeneratedBuildPath(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Application.slnx")))
            {
                return current;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed record BootstrapContract(string DisplayName, string ThemeKey, string Revision);
}

public sealed class WhiteLabelApiFactory : WebApplicationFactory<Program>
{
    public const string Authority = "https://identity.example.test";
    public const string Audience = "application-core-api";

    private readonly RSA _signingRsa = RSA.Create(2048);
    private readonly RsaSecurityKey _signingKey;
    private readonly TestAccountBindingDirectory _bindings = new();
    private readonly TestTenantMembershipDirectory _memberships = new();
    private readonly TestTenantWorkspaceAuthorization _workspaceAuthorization = new();

    public WhiteLabelApiFactory()
    {
        _signingKey = new RsaSecurityKey(_signingRsa) { KeyId = "test-signing-key" };
    }

    public void Bind(string subject, Guid accountId, AccountAvailability availability = AccountAvailability.Active) =>
        _bindings.Bind(Authority, subject, new AccountBinding(accountId, availability));

    public void AddTenantMembership(Guid accountId, Guid tenantId, string displayName) =>
        _memberships.Add(accountId, new TenantMembership(tenantId, displayName));

    public void SetWorkspaceDecision(Guid accountId, Guid tenantId, bool allowed) =>
        _workspaceAuthorization.SetDecision(accountId, tenantId, allowed);

    public void SetWorkspaceUnavailable(Guid accountId, Guid tenantId) =>
        _workspaceAuthorization.SetUnavailable(accountId, tenantId);

    public int GetWorkspaceCheckCount(Guid accountId, Guid tenantId) =>
        _workspaceAuthorization.GetCheckCount(accountId, tenantId);

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
        builder.UseSetting("Authorization:OpenFga:ApiUrl", "http://localhost:8080");
        builder.UseSetting("Authorization:OpenFga:StoreId", "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        builder.UseSetting("Authorization:OpenFga:AuthorizationModelId", "01ARZ3NDEKTSV4RRFFQ69G5FAW");
        builder.UseSetting("Authorization:OpenFga:RequestTimeoutSeconds", "3");
        builder.UseSetting("Authorization:OpenFga:CredentialMethod", "None");
        builder.UseSetting("AllowedHosts", "localhost");
        builder.UseSetting("ConnectionStrings:PrimaryDatabase", "Host=unused.example.test;Database=application");
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
            services.RemoveAll<ITenantWorkspaceAuthorization>();
            services.AddSingleton<ITenantWorkspaceAuthorization>(_workspaceAuthorization);
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

    private sealed class TestTenantWorkspaceAuthorization : ITenantWorkspaceAuthorization
    {
        private readonly Dictionary<(Guid AccountId, Guid TenantId), Decision> _decisions = [];
        private readonly Dictionary<(Guid AccountId, Guid TenantId), int> _checks = [];
        private readonly object _gate = new();

        public void SetDecision(Guid accountId, Guid tenantId, bool allowed)
        {
            lock (_gate)
            {
                _decisions[(accountId, tenantId)] = new Decision(allowed, Unavailable: false);
            }
        }

        public void SetUnavailable(Guid accountId, Guid tenantId)
        {
            lock (_gate)
            {
                _decisions[(accountId, tenantId)] = new Decision(Allowed: false, Unavailable: true);
            }
        }

        public int GetCheckCount(Guid accountId, Guid tenantId)
        {
            lock (_gate)
            {
                return _checks.GetValueOrDefault((accountId, tenantId));
            }
        }

        public Task<bool> CanViewAsync(
            Guid accountId,
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                var key = (accountId, tenantId);
                _checks[key] = _checks.GetValueOrDefault(key) + 1;
                var decision = _decisions.GetValueOrDefault(key);
                if (decision.Unavailable)
                {
                    throw new AuthorizationProviderUnavailableException(
                        "Synthetic provider outage.",
                        new HttpRequestException("Synthetic provider outage."));
                }

                return Task.FromResult(decision.Allowed);
            }
        }

        private readonly record struct Decision(bool Allowed, bool Unavailable);
    }
}
