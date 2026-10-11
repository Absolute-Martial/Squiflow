using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.Orders;
using Application.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OrderApprovalEndpointTests(WhiteLabelApiFactory baseline) : IClassFixture<WhiteLabelApiFactory>
{
    private static readonly string[] CommandFields = ["orderId", "orderRevision", "requestId", "requestRevision", "state"];

    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AuthenticationMembershipAndBothSubmitPermissionsAreRequiredBeforeBodyParsingOrStorage()
    {
        var (account, tenant, token, authorization, store) = await CreateFixture(submit: true, view: false, orderView: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();

        using (var anonymous = new HttpRequestMessage(HttpMethod.Post, SubmitPath(tenant, Guid.NewGuid())))
        {
            anonymous.Content = JsonContent("{");
            using var response = await client.SendAsync(anonymous);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, store.TotalCalls);
        }

        using (var denied = Authorized(HttpMethod.Post, SubmitPath(tenant, Guid.NewGuid()), token, "{"))
        using (var response = await client.SendAsync(denied))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("tenant_permission_denied", await Code(response));
            Assert.Equal(0, store.TotalCalls);
        }

        baseline.RemoveTenantMembership(account, tenant);
        authorization.Allow(account, tenant, TenantOrderApprovalPermission.View, true);
        using var lostMembership = Authorized(HttpMethod.Post, SubmitPath(tenant, Guid.NewGuid()), token, "{");
        using var lostResponse = await client.SendAsync(lostMembership);
        Assert.Equal(HttpStatusCode.Forbidden, lostResponse.StatusCode);
        Assert.Equal("tenant_access_denied", await Code(lostResponse));
        Assert.Equal(0, store.TotalCalls);
    }

    [Theory]
    [InlineData("submit", false, true)]
    [InlineData("submit", true, false)]
    [InlineData("decide", false, true)]
    [InlineData("decide", true, false)]
    public async Task SubmitAndDecisionRequireIndependentApprovalAndOrderViewRights(
        string operation, bool permission, bool orderView)
    {
        var (account, tenant, token, authorization, store) = await CreateFixture(
            submit: operation == "submit" && permission,
            decide: operation == "decide" && permission,
            view: true,
            orderView: orderView);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        var path = operation == "submit"
            ? SubmitPath(tenant, Guid.NewGuid())
            : RequestPath(tenant, Guid.NewGuid()) + "/approve";
        var body = operation == "submit"
            ? "{\"expectedOrderRevision\":1,\"assignedAccountId\":\"" + Guid.NewGuid() + "\"}"
            : "{\"expectedRequestRevision\":1}";
        using var request = Authorized(HttpMethod.Post, path, token, body);
        request.Headers.Add("Idempotency-Key", "approval-policy");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenant_permission_denied", await Code(response));
        Assert.Equal(0, store.TotalCalls);
        Assert.Equal(1, authorization.CheckCount(account, tenant,
            operation == "submit" ? TenantOrderApprovalPermission.Submit : TenantOrderApprovalPermission.Decide));
    }

    [Theory]
    [InlineData("{\"expectedOrderRevision\":1,\"assignedAccountId\":\"{id}\",\"expectedOrderRevision\":2}")]
    [InlineData("{\"expectedOrderRevision\":1,\"assignedAccountId\":\"{id}\",\"extra\":true}")]
    [InlineData("{\"assignedAccountId\":\"{id}\"}")]
    [InlineData("{\"expectedOrderRevision\":1,\"assignedAccountId\":null}")]
    public async Task StrictJsonRejectsDuplicateUnknownMissingAndInvalidRequiredFields(string template)
    {
        var (_, tenant, token, authorization, store) = await CreateFixture(submit: true, view: true, orderView: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        var body = template.Replace("{id}", Guid.NewGuid().ToString("D"), StringComparison.Ordinal);
        using var request = Authorized(HttpMethod.Post, SubmitPath(tenant, Guid.NewGuid()), token, body);
        request.Headers.Add("Idempotency-Key", "approval-invalid-json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("approval_request_invalid", await Code(response));
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task Utf16JsonIsRejectedWithoutDisclosingInputOrCallingStorage()
    {
        var (_, tenant, token, authorization, store) = await CreateFixture(submit: true, view: true, orderView: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        var bytes = Encoding.Unicode.GetBytes("{\"expectedOrderRevision\":1,\"assignedAccountId\":\"" + Guid.NewGuid() + "\"}");
        using var request = new HttpRequestMessage(HttpMethod.Post, SubmitPath(tenant, Guid.NewGuid()));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", "utf16");
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-16" };
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("approval_request_invalid", await Code(response));
        Assert.DoesNotContain("expectedOrderRevision", body, StringComparison.Ordinal);
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task RequestBodyLimitRejectsOversizedChunkedBodyBeforeStorage()
    {
        var (_, tenant, token, authorization, store) = await CreateFixture(submit: true, view: true, orderView: true);
        using var host = CreateHost(authorization, store);
        host.UseKestrel(0);
        using var client = host.CreateClient();
        // Keep the configured synthetic host while binding an ephemeral IPv4 socket.
        client.DefaultRequestHeaders.Host = "localhost";
        var json = "{\"expectedOrderRevision\":1,\"assignedAccountId\":\"" + Guid.NewGuid() + "\"}";
        var bytes = Encoding.UTF8.GetBytes(json.PadRight(8193, ' '));
        using var request = new HttpRequestMessage(HttpMethod.Post, SubmitPath(tenant, Guid.NewGuid()));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", "approval-oversize");
        request.Headers.TransferEncodingChunked = true;
        request.Content = new UnknownLengthJsonContent(bytes);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("request_too_large", await Code(response));
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task SuccessfulCommandReturnsOnlyReceiptSummaryAndMarksReplay()
    {
        var (account, tenant, token, authorization, store) = await CreateFixture(submit: true, view: true, orderView: true);
        var order = Guid.NewGuid();
        var assigned = Guid.NewGuid();
        store.Result = new(OrderApprovalStatus.Replayed, Snapshot(tenant, order, account, assigned));
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Post, SubmitPath(tenant, order), token,
            "{\"expectedOrderRevision\":7,\"assignedAccountId\":\"" + assigned.ToString("D") + "\"}");
        request.Headers.Add("Idempotency-Key", "approval-replay");
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", response.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(1, store.TotalCalls);
        Assert.Equal(account, store.Context!.AccountId);
        Assert.Equal(tenant, store.Context.TenantId);
        Assert.Equal(new SubmitOrderApprovalRequest(order, 7, assigned), store.SubmitRequest);
        Assert.Equal(order, document.RootElement.GetProperty("orderId").GetGuid());
        Assert.Equal(7, document.RootElement.GetProperty("orderRevision").GetInt64());
        Assert.Equal(1, document.RootElement.GetProperty("requestRevision").GetInt64());
        Assert.Equal((int)OrderApprovalState.Pending, document.RootElement.GetProperty("state").GetInt32());
        Assert.Equal(CommandFields,
            document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain("financial-subject-canary", text, StringComparison.Ordinal);
        Assert.DoesNotContain("private-reference-canary", text, StringComparison.Ordinal);
        Assert.DoesNotContain("private-reason-canary", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InboxIsBoundedAndDoesNotExposeFinancialSubjectOrDecisionReason()
    {
        var (_, tenant, token, authorization, store) = await CreateFixture(view: true, orderView: false);
        store.Page = new OrderApprovalPage(
            [new(Guid.NewGuid(), Guid.NewGuid(), 7, 2, OrderApprovalState.Pending, Guid.NewGuid(), Guid.NewGuid(), RecordedAt, false, false)],
            null);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/order-approvals?limit=50", token);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50, store.ListRequest!.Limit);
        Assert.False(store.ListRequest.Recovery);
        Assert.False(document.RootElement.GetProperty("items")[0].TryGetProperty("subject", out _));
        Assert.DoesNotContain("summary", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("externalProgramReference", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reason", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("financial-subject-canary", text, StringComparison.Ordinal);
        Assert.DoesNotContain("private-reference-canary", text, StringComparison.Ordinal);
        Assert.DoesNotContain("private-reason-canary", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=51")]
    [InlineData("?limit=1&limit=2")]
    [InlineData("?recovery=true&unknown=x")]
    [InlineData("?after=not-a-tenant-cursor")]
    public async Task InvalidInboxQueryIsRejectedBeforeStorage(string query)
    {
        var (_, tenant, token, authorization, store) = await CreateFixture(view: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/order-approvals{query}", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(query.Contains("after", StringComparison.Ordinal) ? "approval_cursor_invalid" : "approval_page_invalid", await Code(response));
        Assert.Equal(0, store.TotalCalls);
    }

    [Fact]
    public async Task ApprovalHistoryRejectsOversizePagesAndCursorIsBoundToTheTenant()
    {
        var (_, tenant, token, authorization, store) = await CreateFixture(view: true);
        store.HistoryPage = new OrderApprovalHistoryPage(4, [], null);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var history = Authorized(HttpMethod.Get,
            RequestPath(tenant, Guid.NewGuid()) + "/history?limit=10&beforeRevision=4", token);
        using var historyResponse = await client.SendAsync(history);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.Equal(new ListOrderApprovalHistoryRequest(10, 4), store.HistoryRequest);
        using var tooLarge = Authorized(HttpMethod.Get, RequestPath(tenant, Guid.NewGuid()) + "/history?limit=11", token);
        using var tooLargeResponse = await client.SendAsync(tooLarge);
        Assert.Equal(HttpStatusCode.BadRequest, tooLargeResponse.StatusCode);
        Assert.Equal(1, store.TotalCalls);

        var foreignTenant = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var cursorPayload = $"{foreignTenant:N}:{RecordedAt.UtcTicks}:{requestId:N}";
        var cursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(cursorPayload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        using var inbox = Authorized(HttpMethod.Get,
            $"/api/v1/tenants/{tenant:D}/order-approvals?after={cursor}", token);
        using var inboxResponse = await client.SendAsync(inbox);
        Assert.Equal(HttpStatusCode.BadRequest, inboxResponse.StatusCode);
        Assert.Equal("approval_cursor_invalid", await Code(inboxResponse));
        Assert.Equal(1, store.TotalCalls);
    }

    [Fact]
    public async Task HighRiskRecoveryConfigurationFailurePrecedesBodyReadAndStorage()
    {
        var (account, tenant, token, authorization, store) = await CreateFixture(recover: true, view: true);
        using var host = CreateHost(authorization, store);
        using var client = host.CreateClient();
        using var request = Authorized(HttpMethod.Post,
            RequestPath(tenant, Guid.NewGuid()) + "/recover-cancel", token, "{");
        request.Headers.Add("Idempotency-Key", "approval-recovery");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("high_risk_admission_unavailable", await Code(response));
        Assert.Equal(0, store.TotalCalls);
        Assert.True(authorization.CheckCount(account, tenant, TenantOrderApprovalPermission.Recover) > 0);
    }

    [Fact]
    public async Task OpenApiMarksApprovalOperationsProtectedAndDeclaresBoundedFailureOutcomes()
    {
        using var client = baseline.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        foreach (var (path, method) in new[]
        {
            ("/api/v1/tenants/{tenantId}/orders/{orderId}/approvals", "post"),
            ("/api/v1/tenants/{tenantId}/order-approvals", "get"),
            ("/api/v1/tenants/{tenantId}/order-approvals/{requestId}/history", "get"),
            ("/api/v1/tenants/{tenantId}/order-approvals/{requestId}/approve", "post"),
            ("/api/v1/tenants/{tenantId}/order-approvals/{requestId}/recover-cancel", "post"),
        })
        {
            var operation = paths.GetProperty(path).GetProperty(method);
            Assert.NotEmpty(operation.GetProperty("security").EnumerateArray());
            foreach (var status in new[] { "200", "400", "401", "403", "404", "409", "413", "429", "500", "503", "504" })
                Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _), $"{method.ToUpperInvariant()} {path} missing {status}");
        }
    }

    private async Task<(Guid Account, Guid Tenant, string Token, RecordingAuthorization Authorization, RecordingStore Store)> CreateFixture(
        bool submit = false, bool decide = false, bool view = false, bool recover = false, bool orderView = false)
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var subject = account.ToString("D");
        baseline.Bind(subject, account);
        baseline.AddTenantMembership(account, tenant, "Approval test tenant");
        baseline.SetOrderViewDecision(account, tenant, orderView);
        var authorization = new RecordingAuthorization();
        authorization.Allow(account, tenant, TenantOrderApprovalPermission.Submit, submit);
        authorization.Allow(account, tenant, TenantOrderApprovalPermission.Decide, decide);
        authorization.Allow(account, tenant, TenantOrderApprovalPermission.View, view);
        authorization.Allow(account, tenant, TenantOrderApprovalPermission.Recover, recover);
        return (account, tenant, baseline.CreateToken(subject), authorization, new RecordingStore());
    }

    private WebApplicationFactory<Program> CreateHost(RecordingAuthorization authorization, RecordingStore store) =>
        baseline.WithWebHostBuilder(builder =>
        {
            builder.UseUrls("http://127.0.0.1:0");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITenantOrderApprovalAuthorization>();
                services.AddSingleton<ITenantOrderApprovalAuthorization>(authorization);
                services.RemoveAll<IOrderApprovalStore>();
                services.AddSingleton<IOrderApprovalStore>(store);
            });
        });

    private static string SubmitPath(Guid tenant, Guid order) => $"/api/v1/tenants/{tenant:D}/orders/{order:D}/approvals";
    private static string RequestPath(Guid tenant, Guid request) => $"/api/v1/tenants/{tenant:D}/order-approvals/{request:D}";

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token, string? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent(body);
        return request;
    }

    private static StringContent JsonContent(string body) => new(body, Encoding.UTF8, "application/json");

    private static OrderApprovalSnapshot Snapshot(Guid tenant, Guid order, Guid requester, Guid assigned) =>
        new(Guid.NewGuid(), tenant, order, requester, assigned, 1, OrderApprovalState.Pending, RecordedAt, RecordedAt,
            new(1, new OrderDraftSnapshot(order, tenant, requester, "financial-subject-canary", "USD", 123.45m, 7,
                RecordedAt, [new(1, "line-canary", 1, "EA", 123.45m, 123.45m)], ExternalProgramReference: "private-reference-canary"),
                requester, new string('a', 64)), requester, 1, OrderApprovalChange.Submitted, "private-reason-canary");

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed class RecordingAuthorization : ITenantOrderApprovalAuthorization
    {
        private readonly Dictionary<(Guid Account, Guid Tenant, TenantOrderApprovalPermission Permission), bool> _decisions = [];
        private readonly Dictionary<(Guid Account, Guid Tenant, TenantOrderApprovalPermission Permission), int> _checks = [];

        public void Allow(Guid account, Guid tenant, TenantOrderApprovalPermission permission, bool allowed) =>
            _decisions[(account, tenant, permission)] = allowed;

        public int CheckCount(Guid account, Guid tenant, TenantOrderApprovalPermission permission) =>
            _checks.GetValueOrDefault((account, tenant, permission));

        public Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId, TenantOrderApprovalPermission permission, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = (accountId, tenantId, permission);
            _checks[key] = _checks.GetValueOrDefault(key) + 1;
            return Task.FromResult(_decisions.GetValueOrDefault(key));
        }
    }

    private sealed class RecordingStore : IOrderApprovalStore
    {
        public int TotalCalls { get; private set; }
        public TenantContext? Context { get; private set; }
        public SubmitOrderApprovalRequest? SubmitRequest { get; private set; }
        public ListOrderApprovalsRequest? ListRequest { get; private set; }
        public ListOrderApprovalHistoryRequest? HistoryRequest { get; private set; }
        public OrderApprovalResult Result { get; set; } = new(OrderApprovalStatus.Forbidden, null);
        public OrderApprovalPage Page { get; set; } = new([], null);
        public OrderApprovalHistoryPage? HistoryPage { get; set; }

        public Task<CommitOrderDraftStatus> AssessAdmissionAsync(TenantContext context, Guid orderId, long expectedOrderRevision, CancellationToken ct) =>
            Task.FromResult(CommitOrderDraftStatus.Committed);

        public Task<OrderApprovalResult> SubmitApprovalAsync(TenantContext context, SubmitOrderApprovalRequest request, string key, CancellationToken ct)
        {
            Record(context, ct);
            SubmitRequest = request;
            return Task.FromResult(Result);
        }

        public Task<OrderApprovalResult> DecideApprovalAsync(TenantContext context, DecideOrderApprovalRequest request, string key, CancellationToken ct)
        {
            Record(context, ct);
            return Task.FromResult(Result);
        }

        public Task<OrderApprovalResult> ReassignApprovalAsync(TenantContext context, ReassignOrderApprovalRequest request, string key, CancellationToken ct)
        {
            Record(context, ct);
            return Task.FromResult(Result);
        }

        public Task<OrderApprovalResult> CancelApprovalAsync(TenantContext context, CancelOrderApprovalRequest request, string key, CancellationToken ct)
        {
            Record(context, ct);
            return Task.FromResult(Result);
        }

        public Task<OrderApprovalSnapshot?> FindApprovalAsync(TenantContext context, Guid requestId, CancellationToken ct)
        {
            Record(context, ct);
            return Task.FromResult<OrderApprovalSnapshot?>(null);
        }

        public Task<OrderApprovalPage> ListApprovalsAsync(TenantContext context, ListOrderApprovalsRequest request, CancellationToken ct)
        {
            Record(context, ct);
            ListRequest = request;
            return Task.FromResult(Page);
        }

        public Task<OrderApprovalHistoryPage?> ListApprovalHistoryAsync(TenantContext context, Guid requestId, ListOrderApprovalHistoryRequest request, CancellationToken ct)
        {
            Record(context, ct);
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
