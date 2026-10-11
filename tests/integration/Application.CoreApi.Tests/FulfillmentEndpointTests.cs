using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.Fulfillment;
using Application.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class FulfillmentEndpointTests(WhiteLabelApiFactory baseline) : IClassFixture<WhiteLabelApiFactory>
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AuthenticationAndCurrentFulfillmentPermissionRejectBeforeBodyParsingOrStorage()
    {
        var (account, tenant, token, authorization, store) = CreateFixture(work: false);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        var path = OrderPath(tenant, Guid.NewGuid()) + "/complete";

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
        Assert.True(authorization.CheckCount(account, tenant, TenantOrderFulfillmentPermission.RecordWork) > 0);
    }

    [Theory]
    [InlineData("{\"expectedRevision\":1,\"expectedOrderRevision\":2,\"unexpected\":true}")]
    [InlineData("{\"expectedRevision\":1,\"expectedOrderRevision\":2,\"expectedRevision\":3}")]
    [InlineData("{\"expectedOrderRevision\":2}")]
    [InlineData("{\"expectedRevision\":-1,\"expectedOrderRevision\":2}")]
    public async Task StartStrictJsonRejectsDuplicatesUnknownMissingAndInvalidRevisions(string body)
    {
        var (_, tenant, token, authorization, store) = CreateFixture(work: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Post, OrderPath(tenant, Guid.NewGuid()) + "/start", token, body);
        request.Headers.Add("Idempotency-Key", "invalid-start");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("fulfillment_request_invalid", await Code(response));
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task RestartStartForVoidedWorkCarriesCurrentFulfillmentAndOrderRevisions()
    {
        var (_, tenant, token, authorization, store) = CreateFixture(work: true);
        var order = Guid.NewGuid();
        store.CommandResult = new(FulfillmentStoreStatus.Applied, Snapshot(tenant, order));
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Post, OrderPath(tenant, order) + "/start", token,
            "{\"expectedRevision\":2,\"expectedOrderRevision\":7}");
        request.Headers.Add("Idempotency-Key", "restart-after-correction");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, store.Mutation!.ExpectedRevision);
        Assert.Equal(7, store.ExpectedOrderRevision);
    }

    [Fact]
    public async Task MalformedJsonIsRejectedBeforeStorage()
    {
        var (_, tenant, token, authorization, store) = CreateFixture(work: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Post, OrderPath(tenant, Guid.NewGuid()) + "/start", token, "{invalid");
        request.Headers.Add("Idempotency-Key", "malformed-json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task NestedDuplicateFieldsAreRejectedAndChunkedBodyLimitIsEnforcedBeforeStorage()
    {
        var (account, tenant, token, authorization, store) = CreateFixture(correct: true);
        using var host = CreateHost(authorization, store, configuredHighRisk: true);
        using var client = host.CreateClient();
        var path = OrderPath(tenant, Guid.NewGuid()) + "/corrections";
        var elevated = ElevatedToken(account.ToString("D"));
        using (var duplicate = Authorized(HttpMethod.Post, path, elevated,
                   "{\"expectedRevision\":1,\"targetEventId\":\"" + Guid.NewGuid().ToString("D") + "\",\"reason\":\"mistake\",\"replacement\":{\"kind\":\"Handoff\",\"quantity\":1,\"quantity\":2}}"))
        {
            duplicate.Headers.Add("Idempotency-Key", "nested-duplicate");
            using var response = await client.SendAsync(duplicate);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, store.TotalCalls);
        }

        var valid = "{\"expectedRevision\":1,\"targetEventId\":\"" + Guid.NewGuid().ToString("D") + "\",\"reason\":\"mistake\"}";
        var bytes = Encoding.UTF8.GetBytes(valid.PadRight(8193, ' '));
        using var oversized = new HttpRequestMessage(HttpMethod.Post, path);
        oversized.Headers.Authorization = new AuthenticationHeaderValue("Bearer", elevated);
        oversized.Headers.Add("Idempotency-Key", "chunked-oversized");
        oversized.Headers.TransferEncodingChunked = true;
        oversized.Content = new UnknownLengthJsonContent(bytes);
        using var oversizedResponse = await client.SendAsync(oversized);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversizedResponse.StatusCode);
        Assert.Equal("request_too_large", await Code(oversizedResponse));
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task CorrectionRequiresConfiguredRecentStrongTenantWebAuthenticationBeforeReadingBody()
    {
        var (_, tenant, standardToken, authorization, store) = CreateFixture(correct: true);
        using var unconfiguredHost = CreateHost(authorization, store);
        using var unconfiguredClient = unconfiguredHost.CreateClient();
        using var unconfigured = Authorized(HttpMethod.Post, OrderPath(tenant, Guid.NewGuid()) + "/corrections", standardToken, "{");
        unconfigured.Headers.Add("Idempotency-Key", "missing-stepup-config");
        using var unavailable = await unconfiguredClient.SendAsync(unconfigured);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("high_risk_admission_unavailable", await Code(unavailable));
        Assert.Equal(0, store.TotalCalls);

        using var configuredHost = CreateHost(authorization, store, configuredHighRisk: true);
        using var configuredClient = configuredHost.CreateClient();
        using var noStepup = Authorized(HttpMethod.Post, OrderPath(tenant, Guid.NewGuid()) + "/corrections", standardToken, "{");
        noStepup.Headers.Add("Idempotency-Key", "missing-stepup-claim");
        using var denied = await configuredClient.SendAsync(noStepup);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("high_risk_client_required", await Code(denied));
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task SuccessfulReplayAndReadsExposeOnlyFulfillmentFactsWithBoundedResources()
    {
        var (account, tenant, token, authorization, store) = CreateFixture(work: true, handoff: true, view: true);
        var order = Guid.NewGuid();
        store.CommandResult = new(FulfillmentStoreStatus.Replayed, Snapshot(tenant, order));
        store.Work = Snapshot(tenant, order);
        store.Page = new([new(WorkId, order, 3, FulfillmentStage.ReadyForPickup, 4m, 4m, 1m, "each", RecordedAt)], null);
        store.HistoryPage = new(3,
        [
            new(new(EventId, 3, account, RecordedAt, 1, FulfillmentEventKind.Handoff, 1m, "private-recipient-canary", "private-reason-canary"), true)
        ], null);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();

        using (var request = Authorized(HttpMethod.Post, OrderPath(tenant, order) + "/complete", token,
                   "{\"expectedRevision\":2,\"quantity\":1}"))
        {
            request.Headers.Add("Idempotency-Key", "completion-replay");
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(text);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Equal("true", response.Headers.GetValues("Idempotency-Replayed").Single());
            Assert.Equal(tenant, store.Context!.TenantId);
            Assert.Equal(2, store.Mutation!.ExpectedRevision);
            Assert.Null(store.ExpectedOrderRevision);
            Assert.Equal("completion-replay", store.IdempotencyKey);
            Assert.DoesNotContain("private-recipient-canary", text, StringComparison.Ordinal);
            Assert.DoesNotContain("private-reason-canary", text, StringComparison.Ordinal);
            Assert.False(document.RootElement.TryGetProperty("completedQuantity", out _));
        }

        using (var detail = Authorized(HttpMethod.Get, OrderPath(tenant, order), token))
        using (var response = await client.SendAsync(detail))
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("unitPrice", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("currencyCode", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("financial-canary", text, StringComparison.Ordinal);
        }

        using (var inbox = Authorized(HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/fulfillment?limit=50", token))
        using (var response = await client.SendAsync(inbox))
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(50, store.ListRequest!.Limit);
            Assert.DoesNotContain("recipient", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("reason", text, StringComparison.OrdinalIgnoreCase);
        }

        using (var history = Authorized(HttpMethod.Get, OrderPath(tenant, order) + "/history?limit=10&beforeRevision=4", token))
        using (var response = await client.SendAsync(history))
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(new FulfillmentHistoryRequest(10, 4), store.HistoryRequest);
            Assert.Contains("private-recipient-canary", text, StringComparison.Ordinal);
            Assert.Contains("private-reason-canary", text, StringComparison.Ordinal);
            Assert.DoesNotContain("financial-canary", text, StringComparison.Ordinal);
        }
        Assert.Equal(4, store.TotalCalls);
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=51")]
    [InlineData("?limit=1&limit=2")]
    [InlineData("?unknown=x")]
    [InlineData("?after=not-a-cursor")]
    public async Task InboxRejectsUnboundedQueriesAndForeignTenantCursorBeforeStorage(string query)
    {
        var (_, tenant, token, authorization, store) = CreateFixture(view: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/fulfillment{query}", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task InboxCursorIsBoundToTheRequestedTenant()
    {
        var (_, tenant, token, authorization, store) = CreateFixture(view: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        var foreign = Guid.NewGuid();
        var payload = $"{foreign:N}:{RecordedAt.UtcTicks}:{Guid.NewGuid():N}";
        var cursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        using var request = Authorized(HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/fulfillment?after={cursor}", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("fulfillment_cursor_invalid", await Code(response));
        Assert.Equal(0, store.TotalCalls);
    }

    [Theory]
    [InlineData("?limit=11")]
    [InlineData("?limit=1&limit=2")]
    [InlineData("?beforeRevision=0")]
    [InlineData("?unknown=1")]
    public async Task HistoryPageBoundsAndQueryShapeAreEnforcedBeforeStorage(string query)
    {
        var (_, tenant, token, authorization, store) = CreateFixture(view: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Get, OrderPath(tenant, Guid.NewGuid()) + "/history" + query, token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task OpenApiDeclaresProtectedFulfillmentRoutesAndFailureResponses()
    {
        using var client = baseline.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        foreach (var (path, method) in new[]
        {
            ("/api/v1/tenants/{tenantId}/orders/{orderId}/fulfillment/start", "post"),
            ("/api/v1/tenants/{tenantId}/orders/{orderId}/fulfillment/corrections", "post"),
            ("/api/v1/tenants/{tenantId}/orders/{orderId}/fulfillment/history", "get"),
            ("/api/v1/tenants/{tenantId}/fulfillment", "get"),
        })
        {
            var operation = paths.GetProperty(path).GetProperty(method);
            Assert.NotEmpty(operation.GetProperty("security").EnumerateArray());
            foreach (var status in new[] { "200", "400", "401", "403", "404", "409", "413", "429", "500", "503", "504" })
                Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _), $"{method.ToUpperInvariant()} {path} missing {status}");
        }
    }

    private (Guid Account, Guid Tenant, string Token, RecordingAuthorization Authorization, RecordingStore Store) CreateFixture(
        bool work = false, bool handoff = false, bool correct = false, bool view = false)
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var subject = account.ToString("D");
        baseline.Bind(subject, account);
        baseline.AddTenantMembership(account, tenant, "Fulfillment test tenant");
        var authorization = new RecordingAuthorization();
        authorization.Allow(account, tenant, TenantOrderFulfillmentPermission.RecordWork, work);
        authorization.Allow(account, tenant, TenantOrderFulfillmentPermission.RecordHandoff, handoff);
        authorization.Allow(account, tenant, TenantOrderFulfillmentPermission.Correct, correct);
        authorization.Allow(account, tenant, TenantOrderFulfillmentPermission.View, view);
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
            services.RemoveAll<ITenantOrderFulfillmentAuthorization>();
            services.AddSingleton<ITenantOrderFulfillmentAuthorization>(authorization);
            services.RemoveAll<IFulfillmentStore>();
            services.AddSingleton<IFulfillmentStore>(store);
        });
    });

    private string ElevatedToken(string subject) => baseline.CreateToken(subject, extraClaims:
    [
        new Claim("azp", "tenant-web-client"),
        new Claim("acr", "urn:application:assurance:high-risk"),
        new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
    ]);

    private static string OrderPath(Guid tenant, Guid order) => $"/api/v1/tenants/{tenant:D}/orders/{order:D}/fulfillment";

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token, string? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent(body);
        return request;
    }

    private static StringContent JsonContent(string body) => new(body, Encoding.UTF8, "application/json");

    private static FulfillmentSnapshot Snapshot(Guid tenant, Guid order) => new(
        new(tenant, order, 4, Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "each", 0, 4m, true, 1, true, true, false, true),
        WorkId, tenant, order, 3, true, 4m, 1m, null, null, RecordedAt, RecordedAt);

    private static readonly Guid WorkId = Guid.Parse("00000000-0000-0000-0000-000000000021");
    private static readonly Guid EventId = Guid.Parse("00000000-0000-0000-0000-000000000022");

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed class RecordingAuthorization : ITenantOrderFulfillmentAuthorization
    {
        private readonly Dictionary<(Guid Account, Guid Tenant, TenantOrderFulfillmentPermission Permission), bool> _decisions = [];
        private readonly Dictionary<(Guid Account, Guid Tenant, TenantOrderFulfillmentPermission Permission), int> _checks = [];

        public void Allow(Guid account, Guid tenant, TenantOrderFulfillmentPermission permission, bool allowed) =>
            _decisions[(account, tenant, permission)] = allowed;

        public int CheckCount(Guid account, Guid tenant, TenantOrderFulfillmentPermission permission) =>
            _checks.GetValueOrDefault((account, tenant, permission));

        public Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, TenantOrderFulfillmentPermission permission, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = (accountId, tenantId, permission);
            _checks[key] = _checks.GetValueOrDefault(key) + 1;
            return Task.FromResult(_decisions.GetValueOrDefault(key));
        }
    }

    private sealed class RecordingStore : IFulfillmentStore
    {
        public int TotalCalls { get; private set; }
        public TenantContext? Context { get; private set; }
        public FulfillmentMutation? Mutation { get; private set; }
        public long? ExpectedOrderRevision { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public FulfillmentListRequest? ListRequest { get; private set; }
        public FulfillmentHistoryRequest? HistoryRequest { get; private set; }
        public FulfillmentCommandResult CommandResult { get; set; } = new(FulfillmentStoreStatus.Forbidden);
        public FulfillmentSnapshot? Work { get; set; }
        public FulfillmentPage Page { get; set; } = new([], null);
        public FulfillmentHistoryPage? HistoryPage { get; set; }

        public Task<FulfillmentCommandResult> ExecuteAsync(TenantContext context, Guid orderId, FulfillmentMutation mutation,
            long? expectedOrderRevision, string idempotencyKey, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            Mutation = mutation;
            ExpectedOrderRevision = expectedOrderRevision;
            IdempotencyKey = idempotencyKey;
            return Task.FromResult(CommandResult);
        }

        public Task<FulfillmentSnapshot?> FindAsync(TenantContext context, Guid orderId, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            return Task.FromResult(Work);
        }

        public Task<FulfillmentPage> ListAsync(TenantContext context, FulfillmentListRequest request, CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            ListRequest = request;
            return Task.FromResult(Page);
        }

        public Task<FulfillmentHistoryPage?> HistoryAsync(TenantContext context, Guid orderId, FulfillmentHistoryRequest request,
            CancellationToken cancellationToken)
        {
            Record(context, cancellationToken);
            HistoryRequest = request;
            return Task.FromResult(HistoryPage);
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
