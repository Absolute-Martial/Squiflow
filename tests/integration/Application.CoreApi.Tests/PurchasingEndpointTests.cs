using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.Purchasing;
using Application.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class PurchasingEndpointTests(WhiteLabelApiFactory baseline) : IClassFixture<WhiteLabelApiFactory>
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AuthenticationAndCurrentPurchasingPermissionRejectBeforeBodyParsingOrStorage()
    {
        var (account, tenant, token, authorization, store) = CreateFixture();
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        var path = RequestPath(tenant, Guid.NewGuid());

        using (var anonymous = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent("{") })
        using (var response = await client.SendAsync(anonymous))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, store.TotalCalls);
        }

        using var denied = Authorized(HttpMethod.Post, path, token, "{");
        denied.Headers.Add("Idempotency-Key", "denied-before-body");
        using var deniedResponse = await client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        Assert.Equal("tenant_permission_denied", await Code(deniedResponse));
        Assert.Equal(0, store.TotalCalls);
        Assert.True(authorization.CheckCount(account, tenant, TenantPurchasingPermission.RecordRequest) > 0);
    }

    [Theory]
    [InlineData("{\"expectedRevision\":0,\"supplierId\":\"00000000-0000-0000-0000-000000000001\",\"unitId\":\"00000000-0000-0000-0000-000000000002\",\"description\":\"Print\",\"quantity\":1,\"unexpected\":true}")]
    [InlineData("{\"expectedRevision\":0,\"expectedRevision\":0,\"supplierId\":\"00000000-0000-0000-0000-000000000001\",\"unitId\":\"00000000-0000-0000-0000-000000000002\",\"description\":\"Print\",\"quantity\":1}")]
    [InlineData("{\"expectedRevision\":0,\"supplierId\":\"00000000-0000-0000-0000-000000000001\",\"unitId\":\"00000000-0000-0000-0000-000000000002\",\"description\":\"Print\"}")]
    public async Task RequestStrictJsonRejectsUnknownDuplicateAndMissingRequiredFields(string body)
    {
        var (_, tenant, token, authorization, store) = CreateFixture(request: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Post, RequestPath(tenant, Guid.NewGuid()), token, body);
        request.Headers.Add("Idempotency-Key", "invalid-request");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("purchasing_request_invalid", await Code(response));
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task ChunkedBodyLimitIsEnforcedBeforeStorage()
    {
        var (_, tenant, token, authorization, store) = CreateFixture(manageSupplier: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        var bytes = Encoding.UTF8.GetBytes("{\"expectedRevision\":0,\"name\":\"Printer\"}".PadRight(8193, ' '));
        using var request = new HttpRequestMessage(HttpMethod.Post, SupplierPath(tenant, Guid.NewGuid()));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", "chunked-oversized");
        request.Headers.TransferEncodingChunked = true;
        request.Content = new UnknownLengthJsonContent(bytes);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("request_too_large", await Code(response));
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task CorrectionRequiresConfiguredRecentStrongAuthenticationBeforeReadingBody()
    {
        var (_, tenant, token, authorization, store) = CreateFixture(correct: true);
        var path = RequestPath(tenant, Guid.NewGuid()) + "/corrections";
        using var unconfiguredHost = CreateHost(authorization, store);
        using var unconfiguredClient = unconfiguredHost.CreateClient();
        using var unconfigured = Authorized(HttpMethod.Post, path, token, "{");
        unconfigured.Headers.Add("Idempotency-Key", "missing-stepup-config");
        using var unavailable = await unconfiguredClient.SendAsync(unconfigured);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("high_risk_admission_unavailable", await Code(unavailable));
        Assert.Equal(0, store.TotalCalls);

        using var configuredHost = CreateHost(authorization, store, configuredHighRisk: true);
        using var configuredClient = configuredHost.CreateClient();
        using var noStepup = Authorized(HttpMethod.Post, path, token, "{");
        noStepup.Headers.Add("Idempotency-Key", "missing-stepup-claim");
        using var denied = await configuredClient.SendAsync(noStepup);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("high_risk_client_required", await Code(denied));
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task PaymentRequiresAnExactMethodAndSingleIdempotencyKey()
    {
        var (_, tenant, token, authorization, store) = CreateFixture(payment: true);
        store.PurchaseResult = new(PurchasingStoreStatus.Replayed, Purchase(tenant, PurchaseId));
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        var path = RequestPath(tenant, PurchaseId) + "/payments";

        using (var wrongCase = Authorized(HttpMethod.Post, path, token,
                   $"{{\"expectedRevision\":1,\"receiptId\":\"{ReceiptId:D}\",\"amount\":2.50,\"method\":\"cash\"}}"))
        {
            wrongCase.Headers.Add("Idempotency-Key", "wrong-case-method");
            using var response = await client.SendAsync(wrongCase);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("purchasing_request_invalid", await Code(response));
            Assert.Equal(0, store.TotalCalls);
        }

        using (var missingKey = Authorized(HttpMethod.Post, path, token,
                   $"{{\"expectedRevision\":1,\"receiptId\":\"{ReceiptId:D}\",\"amount\":2.50,\"method\":\"Cash\"}}"))
        {
            using var response = await client.SendAsync(missingKey);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, store.TotalCalls);
        }

        using var multipleKeys = Authorized(HttpMethod.Post, path, token,
            $"{{\"expectedRevision\":1,\"receiptId\":\"{ReceiptId:D}\",\"amount\":2.50,\"method\":\"Cash\"}}");
        multipleKeys.Headers.Add("Idempotency-Key", ["first-key", "second-key"]);
        using var multipleKeyResponse = await client.SendAsync(multipleKeys);
        Assert.Equal(HttpStatusCode.BadRequest, multipleKeyResponse.StatusCode);
        Assert.Equal(0, store.TotalCalls);

        using var valid = Authorized(HttpMethod.Post, path, token,
            $"{{\"expectedRevision\":1,\"receiptId\":\"{ReceiptId:D}\",\"amount\":2.50,\"method\":\"Cash\"}}");
        valid.Headers.Add("Idempotency-Key", "payment-replay");
        using var responseValid = await client.SendAsync(valid);
        using var document = JsonDocument.Parse(await responseValid.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, responseValid.StatusCode);
        Assert.Equal("no-store", responseValid.Headers.CacheControl?.ToString());
        Assert.Equal("true", responseValid.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(2, document.RootElement.EnumerateObject().Count());
        Assert.Equal(PurchaseId.ToString("D"), document.RootElement.GetProperty("id").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("revision").GetInt64());
        Assert.Equal(SupplierPaymentMethod.Cash, store.PurchaseMutation!.Method);
        Assert.Equal("payment-replay", store.IdempotencyKey);
        Assert.Equal(tenant, store.Context!.TenantId);
        Assert.Equal(1, store.PurchaseMutation.ExpectedRevision);
        Assert.DoesNotContain("private-contact-canary", await responseValid.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(1, store.TotalCalls);
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=51")]
    [InlineData("?limit=1&limit=2")]
    [InlineData("?unknown=x")]
    [InlineData("?after=not-a-cursor")]
    public async Task SupplierBrowseRejectsUnboundedQueriesBeforeStorage(string query)
    {
        var (_, tenant, token, authorization, store) = CreateFixture(view: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Get, SupplierListPath(tenant) + query, token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task SupplierCursorIsBoundToTheRequestedTenant()
    {
        var (_, tenant, token, authorization, store) = CreateFixture(view: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        var foreignTenant = Guid.NewGuid();
        var payload = $"1:{foreignTenant:N}:Suppliers:{RecordedAt.UtcTicks}:{Guid.NewGuid():N}";
        var cursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        using var request = Authorized(HttpMethod.Get, SupplierListPath(tenant) + $"?after={cursor}", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("purchasing_cursor_invalid", await Code(response));
        Assert.Equal(0, store.TotalCalls);
    }

    [Theory]
    [InlineData("?limit=11")]
    [InlineData("?limit=1&limit=2")]
    [InlineData("?beforeRevision=0")]
    [InlineData("?unknown=1")]
    public async Task HistoryBoundsAndQueryShapeAreEnforcedBeforeStorage(string query)
    {
        var (_, tenant, token, authorization, store) = CreateFixture(view: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Get, RequestPath(tenant, Guid.NewGuid()) + "/history" + query, token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task OpenApiDeclaresProtectedPurchasingRoutesAndFailureResponses()
    {
        using var client = baseline.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        foreach (var (path, method) in new[]
        {
            ("/api/v1/tenants/{tenantId}/purchasing/suppliers", "get"),
            ("/api/v1/tenants/{tenantId}/purchasing/suppliers/{supplierId}", "post"),
            ("/api/v1/tenants/{tenantId}/purchasing/requests/{purchaseId}/payments", "post"),
            ("/api/v1/tenants/{tenantId}/purchasing/requests/{purchaseId}/corrections", "post"),
            ("/api/v1/tenants/{tenantId}/purchasing/requests/{purchaseId}/history", "get"),
        })
        {
            var operation = paths.GetProperty(path).GetProperty(method);
            Assert.NotEmpty(operation.GetProperty("security").EnumerateArray());
            foreach (var status in new[] { "200", "400", "401", "403", "404", "409", "413", "429", "500", "503", "504" })
                Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _),
                    $"{method.ToUpperInvariant()} {path} missing {status}");
        }
    }

    private (Guid Account, Guid Tenant, string Token, RecordingAuthorization Authorization, RecordingStore Store)
        CreateFixture(bool view = false, bool manageSupplier = false, bool request = false, bool payment = false, bool correct = false)
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var subject = account.ToString("D");
        baseline.Bind(subject, account);
        baseline.AddTenantMembership(account, tenant, "Purchasing test tenant");
        var authorization = new RecordingAuthorization();
        authorization.Allow(account, tenant, TenantPurchasingPermission.View, view);
        authorization.Allow(account, tenant, TenantPurchasingPermission.ManageSupplier, manageSupplier);
        authorization.Allow(account, tenant, TenantPurchasingPermission.RecordRequest, request);
        authorization.Allow(account, tenant, TenantPurchasingPermission.RecordPayment, payment);
        authorization.Allow(account, tenant, TenantPurchasingPermission.Correct, correct);
        return (account, tenant, baseline.CreateToken(subject), authorization, new RecordingStore());
    }

    private WebApplicationFactory<Program> CreateHost(RecordingAuthorization authorization, RecordingStore store,
        bool configuredHighRisk = false) => baseline.WithWebHostBuilder(builder =>
    {
        if (configuredHighRisk)
        {
            builder.UseSetting("HighRiskActions:TenantWebClientId", "tenant-web-client");
            builder.UseSetting("HighRiskActions:RequiredAcr", "urn:application:assurance:high-risk");
        }
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantPurchasingAuthorization>();
            services.AddSingleton<ITenantPurchasingAuthorization>(authorization);
            services.RemoveAll<IPurchasingStore>();
            services.AddSingleton<IPurchasingStore>(store);
        });
    });

    private static string RequestPath(Guid tenant, Guid purchase) =>
        $"/api/v1/tenants/{tenant:D}/purchasing/requests/{purchase:D}";
    private static string SupplierPath(Guid tenant, Guid supplier) =>
        $"/api/v1/tenants/{tenant:D}/purchasing/suppliers/{supplier:D}";
    private static string SupplierListPath(Guid tenant) => $"/api/v1/tenants/{tenant:D}/purchasing/suppliers";

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token, string? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent(body);
        return request;
    }

    private static StringContent JsonContent(string body) => new(body, Encoding.UTF8, "application/json");

    private static SupplierSnapshot Supplier(Guid tenant, Guid supplier) => new(supplier, tenant, 2, "Printer",
        "private-contact-canary", SupplierStatus.Active, ActorId, RecordedAt, ActorId, RecordedAt);

    private static PurchaseSnapshot Purchase(Guid tenant, Guid purchase) => new(purchase, tenant, 2,
        new(SupplierId, tenant, 1, "Printer", "private-contact-canary", SupplierStatus.Active, ActorId, RecordedAt, ActorId, RecordedAt),
        new(tenant, UnitId, "each", 0, 1, true), null, "Printing", 3m,
        Array.Empty<PurchasingReceipt>(), Array.Empty<PurchasingPayment>(), Array.Empty<PurchasingRefund>(), null,
        ActorId, RecordedAt, ActorId, RecordedAt);

    private static readonly Guid ActorId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid SupplierId = Guid.Parse("00000000-0000-0000-0000-000000000012");
    private static readonly Guid UnitId = Guid.Parse("00000000-0000-0000-0000-000000000013");
    private static readonly Guid PurchaseId = Guid.Parse("00000000-0000-0000-0000-000000000014");
    private static readonly Guid ReceiptId = Guid.Parse("00000000-0000-0000-0000-000000000015");

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed class RecordingAuthorization : ITenantPurchasingAuthorization
    {
        private readonly Dictionary<(Guid Account, Guid Tenant, TenantPurchasingPermission Permission), bool> _decisions = [];
        private readonly Dictionary<(Guid Account, Guid Tenant, TenantPurchasingPermission Permission), int> _checks = [];

        public void Allow(Guid account, Guid tenant, TenantPurchasingPermission permission, bool allowed) =>
            _decisions[(account, tenant, permission)] = allowed;

        public int CheckCount(Guid account, Guid tenant, TenantPurchasingPermission permission) =>
            _checks.GetValueOrDefault((account, tenant, permission));

        public Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, TenantPurchasingPermission permission,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = (accountId, tenantId, permission);
            _checks[key] = _checks.GetValueOrDefault(key) + 1;
            return Task.FromResult(_decisions.GetValueOrDefault(key));
        }
    }

    private sealed class RecordingStore : IPurchasingStore
    {
        public int TotalCalls { get; private set; }
        public TenantContext? Context { get; private set; }
        public SupplierMutation? SupplierMutation { get; private set; }
        public PurchasingMutation? PurchaseMutation { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public SupplierCommandResult SupplierResult { get; set; } = new(PurchasingStoreStatus.Forbidden);
        public PurchasingCommandResult PurchaseResult { get; set; } = new(PurchasingStoreStatus.Forbidden);

        public Task<SupplierCommandResult> ExecuteSupplierAsync(TenantContext context, Guid supplierId,
            SupplierMutation mutation, string idempotencyKey, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            SupplierMutation = mutation;
            IdempotencyKey = idempotencyKey;
            return Task.FromResult(SupplierResult);
        }

        public Task<PurchasingCommandResult> ExecutePurchaseAsync(TenantContext context, Guid purchaseId,
            PurchasingMutation mutation, string idempotencyKey, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            PurchaseMutation = mutation;
            IdempotencyKey = idempotencyKey;
            return Task.FromResult(PurchaseResult);
        }

        public Task<SupplierSnapshot?> FindSupplierAsync(TenantContext context, Guid supplierId, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            return Task.FromResult<SupplierSnapshot?>(null);
        }

        public Task<PurchaseSnapshot?> FindPurchaseAsync(TenantContext context, Guid purchaseId, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            return Task.FromResult<PurchaseSnapshot?>(null);
        }

        public Task<SupplierPage> ListSuppliersAsync(TenantContext context, PurchasingListRequest request, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            return Task.FromResult(new SupplierPage([], null));
        }

        public Task<PurchasingPage> ListPurchasesAsync(TenantContext context, PurchasingListRequest request, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            return Task.FromResult(new PurchasingPage([], null));
        }

        public Task<PurchasingHistoryPage?> HistoryAsync(TenantContext context, Guid purchaseId,
            PurchasingHistoryRequest request, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            return Task.FromResult<PurchasingHistoryPage?>(null);
        }

        public Task<SupplierHistoryPage?> SupplierHistoryAsync(TenantContext context, Guid supplierId,
            PurchasingHistoryRequest request, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            return Task.FromResult<SupplierHistoryPage?>(null);
        }

        public Task<SupplierBalance?> SupplierBalanceAsync(TenantContext context, Guid supplierId, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            return Task.FromResult<SupplierBalance?>(null);
        }

        private void Record(TenantContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TotalCalls++;
            Context = context;
        }
    }

    private sealed class UnknownLengthJsonContent : HttpContent
    {
        private readonly byte[] _body;

        public UnknownLengthJsonContent(byte[] body)
        {
            _body = body;
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_body).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
