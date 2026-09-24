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
using Application.Customers;
using Application.CoreApi;
using Application.CoreApi.Authorization;
using Application.IdentityAccess.Postgres;
using Application.IdentityAccess;
using Application.Orders;
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
        var configuration = new BootstrapCacheConfiguration { CacheMaxAgeSeconds = seconds };

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
    private readonly TestTenantOrderAuthorization _orderAuthorization = new();
    private readonly TestTenantCustomerAuthorization _customerAuthorization = new();
    private readonly TestCustomerStore _customers = new();
    private readonly TestOrderDraftStore _orders = new();

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

    public void SetOrderCreateDecision(Guid accountId, Guid tenantId, bool allowed) =>
        _orderAuthorization.SetCreateDecision(accountId, tenantId, allowed);

    public void SetOrderCreateUnavailable(Guid accountId, Guid tenantId) =>
        _orderAuthorization.SetCreateUnavailable(accountId, tenantId);

    public void SetOrderViewDecision(Guid accountId, Guid tenantId, bool allowed) =>
        _orderAuthorization.SetViewDecision(accountId, tenantId, allowed);

    public void SetOrderViewUnavailable(Guid accountId, Guid tenantId) =>
        _orderAuthorization.SetViewUnavailable(accountId, tenantId);

    public void SetOrderAbandonDecision(Guid accountId, Guid tenantId, bool allowed) =>
        _orderAuthorization.SetAbandonDecision(accountId, tenantId, allowed);

    public void SetOrderAbandonUnavailable(Guid accountId, Guid tenantId) =>
        _orderAuthorization.SetAbandonUnavailable(accountId, tenantId);

    public int GetOrderCreateCheckCount(Guid accountId, Guid tenantId) =>
        _orderAuthorization.GetCreateCheckCount(accountId, tenantId);

    public int GetOrderViewCheckCount(Guid accountId, Guid tenantId) =>
        _orderAuthorization.GetViewCheckCount(accountId, tenantId);

    public int GetOrderAbandonCheckCount(Guid accountId, Guid tenantId) =>
        _orderAuthorization.GetAbandonCheckCount(accountId, tenantId);

    public int GetOrderCreateCount(Guid tenantId) => _orders.GetCreateCount(tenantId);

    public void SetCustomerDecision(Guid accountId, Guid tenantId, string operation, bool allowed) =>
        _customerAuthorization.Set(accountId, tenantId, operation, allowed);

    public void SetCustomerUnavailable(Guid accountId, Guid tenantId, string operation) =>
        _customerAuthorization.SetUnavailable(accountId, tenantId, operation);

    public int GetCustomerCheckCount(Guid accountId, Guid tenantId, string operation) =>
        _customerAuthorization.CheckCount(accountId, tenantId, operation);

    public int GetCustomerStoreCallCount(Guid tenantId) => _customers.CallCount(tenantId);

    public int GetOrderFindCount(Guid tenantId) => _orders.GetFindCount(tenantId);

    public int GetOrderListCount(Guid tenantId) => _orders.GetListCount(tenantId);

    public int GetOrderAbandonCount(Guid tenantId) => _orders.GetAbandonCount(tenantId);

    public ListOrderDraftsRequest? GetLastOrderListRequest(Guid tenantId) => _orders.GetLastListRequest(tenantId);

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
            services.RemoveAll<ITenantOrderAuthorization>();
            services.AddSingleton<ITenantOrderAuthorization>(_orderAuthorization);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(_customerAuthorization);
            services.RemoveAll<ICustomerStore>();
            services.AddSingleton<ICustomerStore>(_customers);
            services.RemoveAll<IOrderDraftStore>();
            services.AddSingleton<IOrderDraftStore>(_orders);
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

    private sealed class TestTenantOrderAuthorization : ITenantOrderAuthorization
    {
        private readonly Dictionary<(Guid AccountId, Guid TenantId), bool> _createDecisions = [];
        private readonly Dictionary<(Guid AccountId, Guid TenantId), bool> _viewDecisions = [];
        private readonly Dictionary<(Guid AccountId, Guid TenantId), bool> _abandonDecisions = [];
        private readonly Dictionary<(Guid AccountId, Guid TenantId), int> _createChecks = [];
        private readonly Dictionary<(Guid AccountId, Guid TenantId), int> _viewChecks = [];
        private readonly Dictionary<(Guid AccountId, Guid TenantId), int> _abandonChecks = [];
        private readonly HashSet<(Guid AccountId, Guid TenantId)> _unavailableCreates = [];
        private readonly HashSet<(Guid AccountId, Guid TenantId)> _unavailableViews = [];
        private readonly HashSet<(Guid AccountId, Guid TenantId)> _unavailableAbandons = [];
        private readonly object _gate = new();

        public void SetCreateDecision(Guid accountId, Guid tenantId, bool allowed)
        {
            lock (_gate)
            {
                _createDecisions[(accountId, tenantId)] = allowed;
                _unavailableCreates.Remove((accountId, tenantId));
            }
        }

        public void SetCreateUnavailable(Guid accountId, Guid tenantId)
        {
            lock (_gate)
            {
                _unavailableCreates.Add((accountId, tenantId));
            }
        }

        public void SetViewDecision(Guid accountId, Guid tenantId, bool allowed)
        {
            lock (_gate)
            {
                _viewDecisions[(accountId, tenantId)] = allowed;
                _unavailableViews.Remove((accountId, tenantId));
            }
        }

        public void SetViewUnavailable(Guid accountId, Guid tenantId)
        {
            lock (_gate)
            {
                _unavailableViews.Add((accountId, tenantId));
            }
        }

        public void SetAbandonDecision(Guid accountId, Guid tenantId, bool allowed)
        {
            lock (_gate)
            {
                _abandonDecisions[(accountId, tenantId)] = allowed;
                _unavailableAbandons.Remove((accountId, tenantId));
            }
        }

        public void SetAbandonUnavailable(Guid accountId, Guid tenantId)
        {
            lock (_gate)
            {
                _unavailableAbandons.Add((accountId, tenantId));
            }
        }

        public int GetCreateCheckCount(Guid accountId, Guid tenantId)
        {
            lock (_gate)
            {
                return _createChecks.GetValueOrDefault((accountId, tenantId));
            }
        }

        public int GetViewCheckCount(Guid accountId, Guid tenantId)
        {
            lock (_gate)
            {
                return _viewChecks.GetValueOrDefault((accountId, tenantId));
            }
        }

        public int GetAbandonCheckCount(Guid accountId, Guid tenantId)
        {
            lock (_gate)
            {
                return _abandonChecks.GetValueOrDefault((accountId, tenantId));
            }
        }

        public Task<bool> CanCreateAsync(
            Guid accountId,
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                var key = (accountId, tenantId);
                _createChecks[key] = _createChecks.GetValueOrDefault(key) + 1;
                if (_unavailableCreates.Contains(key))
                {
                    throw new AuthorizationProviderUnavailableException(
                        "Synthetic order authorization provider outage.",
                        new HttpRequestException("Synthetic order authorization provider outage."));
                }

                return Task.FromResult(_createDecisions.GetValueOrDefault(key));
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
                _viewChecks[key] = _viewChecks.GetValueOrDefault(key) + 1;
                if (_unavailableViews.Contains(key))
                {
                    throw new AuthorizationProviderUnavailableException(
                        "Synthetic order view authorization provider outage.",
                        new HttpRequestException("Synthetic order view authorization provider outage."));
                }

                return Task.FromResult(_viewDecisions.GetValueOrDefault(key));
            }
        }

        public Task<bool> CanAbandonAsync(
            Guid accountId,
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                var key = (accountId, tenantId);
                _abandonChecks[key] = _abandonChecks.GetValueOrDefault(key) + 1;
                if (_unavailableAbandons.Contains(key))
                {
                    throw new AuthorizationProviderUnavailableException(
                        "Synthetic order abandon authorization provider outage.",
                        new HttpRequestException("Synthetic order abandon authorization provider outage."));
                }

                return Task.FromResult(_abandonDecisions.GetValueOrDefault(key));
            }
        }
    }

    private sealed class TestOrderDraftStore : IOrderDraftStore
    {
        private readonly Dictionary<(Guid TenantId, Guid AccountId, string Key), Receipt> _receipts = [];
        private readonly Dictionary<(Guid TenantId, Guid AccountId, string Key), AbandonReceipt> _abandonReceipts = [];
        private readonly Dictionary<(Guid TenantId, Guid OrderId), OrderDraftSnapshot> _orders = [];
        private readonly Dictionary<Guid, int> _createCounts = [];
        private readonly Dictionary<Guid, int> _findCounts = [];
        private readonly Dictionary<Guid, int> _listCounts = [];
        private readonly Dictionary<Guid, int> _abandonCounts = [];
        private readonly Dictionary<Guid, ListOrderDraftsRequest> _lastListRequests = [];
        private readonly object _gate = new();

        public int GetCreateCount(Guid tenantId)
        {
            lock (_gate)
            {
                return _createCounts.GetValueOrDefault(tenantId);
            }
        }

        public int GetFindCount(Guid tenantId)
        {
            lock (_gate)
            {
                return _findCounts.GetValueOrDefault(tenantId);
            }
        }

        public int GetListCount(Guid tenantId)
        {
            lock (_gate)
            {
                return _listCounts.GetValueOrDefault(tenantId);
            }
        }

        public int GetAbandonCount(Guid tenantId)
        {
            lock (_gate)
            {
                return _abandonCounts.GetValueOrDefault(tenantId);
            }
        }

        public ListOrderDraftsRequest? GetLastListRequest(Guid tenantId)
        {
            lock (_gate)
            {
                return _lastListRequests.GetValueOrDefault(tenantId);
            }
        }

        public Task<CreateOrderDraftResult> CreateAsync(
            TenantContext tenantContext,
            OrderDraftIntent intent,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                var receiptKey = (tenantContext.TenantId, tenantContext.AccountId, idempotencyKey);
                if (_receipts.TryGetValue(receiptKey, out var receipt))
                {
                    return Task.FromResult(string.Equals(
                        receipt.Fingerprint,
                        intent.Fingerprint,
                        StringComparison.Ordinal)
                        ? new CreateOrderDraftResult(CreateOrderDraftStatus.Replayed, receipt.Order)
                        : new CreateOrderDraftResult(CreateOrderDraftStatus.IdempotencyKeyConflict, null));
                }

                var order = new OrderDraftSnapshot(
                    Guid.CreateVersion7(),
                    tenantContext.TenantId,
                    tenantContext.AccountId,
                    intent.Summary,
                    intent.CurrencyCode,
                    intent.Total,
                    Revision: 1,
                    DateTimeOffset.UtcNow,
                    intent.Lines,
                    CustomerContext: intent.CustomerContext);
                _receipts.Add(receiptKey, new Receipt(intent.Fingerprint, order));
                _orders.Add((tenantContext.TenantId, order.OrderId), order);
                _createCounts[tenantContext.TenantId] = _createCounts.GetValueOrDefault(tenantContext.TenantId) + 1;
                return Task.FromResult(new CreateOrderDraftResult(CreateOrderDraftStatus.Created, order));
            }
        }

        public Task<OrderDraftSnapshot?> FindAsync(
            TenantContext tenantContext,
            Guid orderId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _findCounts[tenantContext.TenantId] = _findCounts.GetValueOrDefault(tenantContext.TenantId) + 1;
                _orders.TryGetValue((tenantContext.TenantId, orderId), out var order);
                return Task.FromResult(order);
            }
        }

        public Task<OrderDraftPage> ListAsync(
            TenantContext tenantContext,
            ListOrderDraftsRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _listCounts[tenantContext.TenantId] = _listCounts.GetValueOrDefault(tenantContext.TenantId) + 1;
                _lastListRequests[tenantContext.TenantId] = request;
                var ordered = _orders.Values
                    .Where(order => order.TenantId == tenantContext.TenantId)
                    .OrderByDescending(order => order.CreatedAt)
                    .ThenByDescending(order => order.OrderId)
                    .Where(order => request.After is null || IsAfter(order, request.After))
                    .ToArray();
                var items = ordered
                    .Take(request.Limit + 1)
                    .Select(order => new OrderDraftListItem(
                        order.OrderId,
                        order.Summary,
                        order.CurrencyCode,
                        order.Total,
                        order.Revision,
                        order.CreatedAt,
                        order.State,
                        order.AbandonedAt,
                        order.CustomerContext))
                    .ToArray();
                var hasNext = items.Length > request.Limit;
                if (hasNext)
                {
                    items = items[..request.Limit];
                }

                var nextCursor = hasNext
                    ? new OrderDraftPageCursor(items[^1].CreatedAt, items[^1].OrderId)
                    : null;
                return Task.FromResult(new OrderDraftPage(items, nextCursor));
            }
        }

        public Task<AbandonOrderDraftResult> AbandonAsync(
            TenantContext tenantContext,
            AbandonOrderDraftRequest request,
            string idempotencyKey,
            string fingerprint,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _abandonCounts[tenantContext.TenantId] = _abandonCounts.GetValueOrDefault(tenantContext.TenantId) + 1;
                var receiptKey = (tenantContext.TenantId, tenantContext.AccountId, idempotencyKey);
                if (_abandonReceipts.TryGetValue(receiptKey, out var receipt))
                {
                    return Task.FromResult(string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
                        ? new AbandonOrderDraftResult(AbandonOrderDraftStatus.Replayed, receipt.Order)
                        : new AbandonOrderDraftResult(AbandonOrderDraftStatus.IdempotencyKeyConflict, null));
                }

                if (!_orders.TryGetValue((tenantContext.TenantId, request.OrderId), out var order))
                {
                    return Task.FromResult(new AbandonOrderDraftResult(AbandonOrderDraftStatus.NotFound, null));
                }

                if (order.State == OrderDraftState.Abandoned)
                {
                    return Task.FromResult(new AbandonOrderDraftResult(AbandonOrderDraftStatus.AlreadyAbandoned, null));
                }

                if (order.Revision != request.ExpectedRevision)
                {
                    return Task.FromResult(new AbandonOrderDraftResult(AbandonOrderDraftStatus.RevisionConflict, null));
                }

                var abandoned = order with
                {
                    State = OrderDraftState.Abandoned,
                    Revision = order.Revision + 1,
                    AbandonedAt = DateTimeOffset.UtcNow,
                    AbandonedByAccountId = tenantContext.AccountId,
                };
                _orders[(tenantContext.TenantId, request.OrderId)] = abandoned;
                _abandonReceipts.Add(receiptKey, new AbandonReceipt(fingerprint, abandoned));
                return Task.FromResult(new AbandonOrderDraftResult(AbandonOrderDraftStatus.Abandoned, abandoned));
            }
        }

        private static bool IsAfter(OrderDraftSnapshot order, OrderDraftPageCursor after) =>
            order.CreatedAt < after.CreatedAt ||
            (order.CreatedAt == after.CreatedAt && order.OrderId.CompareTo(after.OrderId) < 0);

        private sealed record Receipt(string Fingerprint, OrderDraftSnapshot Order);
        private sealed record AbandonReceipt(string Fingerprint, OrderDraftSnapshot Order);
    }
}
