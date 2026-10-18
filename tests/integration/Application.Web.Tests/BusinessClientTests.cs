using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Web.Authentication;
using Application.Web.Business;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessClientTests
{
    private static readonly Guid Tenant = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly OrganizationRecord Organization = new(Guid.Parse("20000000-0000-0000-0000-000000000001"), Tenant, "Fieldwork client", DateTimeOffset.Parse("2026-10-15T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public async Task LostResponseCanReplayOnlyTheSameFrozenIntentAndSession()
    {
        var calls = new List<(string Body, string Key)>();
        using var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal($"https://api.example.test/api/v1/tenants/{Tenant:D}/customers/organizations", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("private-session-token", request.Headers.Authorization.Parameter);
            calls.Add((await request.Content!.ReadAsStringAsync(ct), request.Headers.GetValues("Idempotency-Key").Single()));
            if (calls.Count == 1) throw new HttpRequestException("private provider diagnostic");
            var response = Json(Organization); response.Headers.Add("Idempotency-Replayed", "true"); return response;
        });
        using var http = new HttpClient(handler);
        var (api, tickets, authentication, _) = await ClientAsync(http);
        var mutable = new Dictionary<string, string> { ["displayName"] = "Fieldwork client" };
        var command = BusinessCommand.Create(Tenant, "customers/organizations", mutable);
        mutable["displayName"] = "Changed after save started";
        var lost = await api.SendAsync<OrganizationRecord>(command, row => CustomerContracts.Organization(row, Tenant));
        Assert.True(lost.Unknown); Assert.DoesNotContain("private provider diagnostic", lost.Message, StringComparison.Ordinal);
        var replay = await api.SendAsync<OrganizationRecord>(command, row => CustomerContracts.Organization(row, Tenant));
        Assert.Equal(Organization, replay.Value); Assert.True(replay.Replayed);
        Assert.Equal(calls[0], calls[1]); Assert.DoesNotContain("Changed after", calls[0].Body, StringComparison.Ordinal);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
        authentication.Principal = await AddTicketAsync(tickets, "different-account-token");
        Assert.Equal(BusinessFailure.SignInRequired, (await api.SendAsync<OrganizationRecord>(command, _ => true)).Failure);
        Assert.Equal(2, calls.Count);
    }

    [Theory]
    [InlineData(401, BusinessFailure.SignInRequired)]
    [InlineData(403, BusinessFailure.Denied)]
    [InlineData(404, BusinessFailure.NotFound)]
    [InlineData(400, BusinessFailure.InvalidRequest)]
    [InlineData(409, BusinessFailure.Conflict)]
    [InlineData(500, BusinessFailure.Unknown)]
    [InlineData(503, BusinessFailure.Unknown)]
    [InlineData(302, BusinessFailure.Unknown)]
    public async Task MutationFailuresUseSafeMessagesAndNeverFollowResponseLocations(int status, BusinessFailure failure)
    {
        var calls = 0;
        using var handler = new Handler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("private bearer and SQL diagnostic") };
            response.Headers.Location = new Uri("https://other.example.test/steal");
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        var result = await api.SendAsync<OrganizationRecord>(BusinessCommand.Create(Tenant, "customers/organizations", new { displayName = "Fieldwork client" }), _ => true);
        Assert.Equal(failure, result.Failure); Assert.Null(result.Value); Assert.Equal(1, calls);
        Assert.DoesNotContain("SQL", result.Message, StringComparison.Ordinal); Assert.DoesNotContain("bearer", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TenantAndParentMismatchesAreRejectedForReadsAndUncertainSaves()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(Organization with { TenantId = Guid.NewGuid() })));
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        Assert.Equal(BusinessFailure.InvalidResponse, (await api.GetAsync<OrganizationRecord>(Tenant, "customers/organizations/" + Organization.OrganizationId, row => CustomerContracts.Organization(row, Tenant, Organization.OrganizationId))).Failure);
        Assert.True((await api.SendAsync<OrganizationRecord>(BusinessCommand.Create(Tenant, "customers/organizations", new { displayName = "Fieldwork client" }), row => CustomerContracts.Organization(row, Tenant))).Unknown);
        var program = new ProgramRecord(Guid.NewGuid(), Tenant, Guid.NewGuid(), "Program", Organization.CreatedAt);
        Assert.False(CustomerContracts.Program(program, Tenant, Organization.OrganizationId));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"items\":null}")]
    [InlineData("{\"items\":[null]}")]
    [InlineData("{\"items\":[],\"nextCursor\":\"https://evil.example\"}")]
    [InlineData("{\"items\":[],\"nextCursor\":\"loop\"}")]
    public async Task MalformedPagesCannotBecomeBusinessState(string body)
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        var result = await api.GetAsync<OrganizationPage>(Tenant, "customers/organizations?limit=25", page =>
            BusinessValidation.Page(page.Items, page.NextCursor, row => CustomerContracts.Organization(row, Tenant), row => row.OrganizationId));
        Assert.Equal(BusinessFailure.InvalidResponse, result.Failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResponseBoundsApplyWithAndWithoutContentLength(bool knownLength)
    {
        using var handler = new Handler((_, _) =>
        {
            var bytes = Encoding.UTF8.GetBytes(new string(' ', BusinessApiClient.MaximumResponseBytes + 1));
            HttpContent content = knownLength ? new ByteArrayContent(bytes) : new ChunkedContent(bytes);
            content.Headers.ContentType = new("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        Assert.Equal(BusinessFailure.InvalidResponse, (await api.GetAsync<OrganizationRecord>(Tenant, "customers/organizations", _ => true)).Failure);
        Assert.True((await api.SendAsync<OrganizationRecord>(BusinessCommand.Create(Tenant, "customers/organizations", new { displayName = "Bounded" }), _ => true)).Unknown);
    }

    [Fact]
    public async Task CurrentTicketIsRequiredAndRevocationDiscardsReadData()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var handler = new Handler(async (_, ct) => { calls++; entered.SetResult(); await release.Task.WaitAsync(ct); return Json(Organization); });
        using var http = new HttpClient(handler);
        var (api, tickets, _, key) = await ClientAsync(http);
        var pending = api.GetAsync<OrganizationRecord>(Tenant, "customers/organizations", _ => true);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await tickets.RemoveAsync(key); release.SetResult();
        Assert.Equal(BusinessFailure.SignInRequired, (await pending).Failure);
        Assert.Equal(BusinessFailure.SignInRequired, (await api.GetAsync<OrganizationRecord>(Tenant, "customers/organizations", _ => true)).Failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ReadOnlyPostQueriesDoNotCarryMutationKeysOrSuggestASave()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method); Assert.False(request.Headers.Contains("Idempotency-Key"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        var result = await api.QueryAsync<PriceResolution>(Tenant, "pricing/resolve", new { itemId = Guid.NewGuid() }, _ => true);
        Assert.Equal(BusinessFailure.Unavailable, result.Failure); Assert.False(result.Unknown);
    }

    [Fact]
    public async Task CancellationAndDeadlineHaveDifferentOutcomes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (_, ct) => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); return Json(Organization); });
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        using var caller = new CancellationTokenSource();
        var pending = api.GetAsync<OrganizationRecord>(Tenant, "customers/organizations", _ => true, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        using var deadlineHandler = new Handler((_, _) => throw new TaskCanceledException());
        using var deadlineHttp = new HttpClient(deadlineHandler);
        var (deadlineApi, _, _, _) = await ClientAsync(deadlineHttp);
        Assert.Equal(BusinessFailure.TimedOut, (await deadlineApi.GetAsync<OrganizationRecord>(Tenant, "customers/organizations", _ => true)).Failure);
        Assert.True((await deadlineApi.SendAsync<OrganizationRecord>(BusinessCommand.Create(Tenant, "customers/organizations", new { displayName = "Retry" }), _ => true)).Unknown);
    }

    [Theory]
    [InlineData("https://other.example.test/customers/")]
    [InlineData("customers/../../account")]
    [InlineData("catalog/%2e%2e/%2e%2e/account")]
    [InlineData("customers/organizations\r\nX-Token: bad")]
    [InlineData("customers//organizations")]
    public void RejectsUnsafePathsBeforeCredentialsCanBeSent(string resource) =>
        Assert.Throws<ArgumentException>(() => BusinessCommand.Create(Tenant, resource, new { }));

    [Fact]
    public void LargeCommandsAndEmptyTenantsAreRejectedBeforeDispatch()
    {
        Assert.Throws<ArgumentException>(() => BusinessCommand.Create(Tenant, "customers/organizations", new { displayName = new string('x', BusinessApiClient.MaximumRequestBytes) }));
        Assert.Throws<ArgumentException>(() => BusinessCommand.Create(Guid.Empty, "customers/organizations", new { }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignOutDuringBodyReadCannotPublishBusinessData(bool mutation)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var content = new DelayedContent(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Organization, System.Text.Json.JsonSerializerOptions.Web), entered, release);
        content.Headers.ContentType = new("application/json");
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var http = new HttpClient(handler);
        var (api, tickets, _, key) = await ClientAsync(http);
        var pending = mutation
            ? api.SendAsync<OrganizationRecord>(BusinessCommand.Create(Tenant, "customers/organizations", new { displayName = "Fieldwork" }), row => CustomerContracts.Organization(row, Tenant))
            : api.GetAsync<OrganizationRecord>(Tenant, "customers/organizations", row => CustomerContracts.Organization(row, Tenant));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await tickets.RemoveAsync(key); release.SetResult();
        var result = await pending;
        Assert.Null(result.Value);
        Assert.Equal(mutation ? BusinessFailure.Unknown : BusinessFailure.SignInRequired, result.Failure);
    }

    [Fact]
    public async Task LiteralCatalogAndPricingWireFixturesRetainCaseAndRequiredFacts()
    {
        const string unit = """
            {"unitId":"20000000-0000-0000-0000-000000000001","code":"EA","name":"Each","precision":0,"status":"active","revision":1,"createdAt":"2026-10-15T00:00:00Z","retiredAt":null}
            """;
        const string price = """
            {"priceId":"20000000-0000-0000-0000-000000000002","revisionId":"20000000-0000-0000-0000-000000000003","revision":1,"itemId":"20000000-0000-0000-0000-000000000004","unitId":"20000000-0000-0000-0000-000000000001","unitCode":"EA","currencyCode":"NPR","scope":"Default","targetId":null,"baseUnitPrice":12.5,"validFrom":"2026-10-15T00:00:00Z","validTo":null,"state":"Draft","createdAt":"2026-10-15T00:00:00Z","publishedAt":null,"retiredAt":null,"conversionRevision":null}
            """;
        var bodies = new Queue<string>([unit, price, unit.Replace("\"precision\":0,", "", StringComparison.Ordinal), price.Replace("Draft", "draft", StringComparison.Ordinal)]);
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(bodies.Dequeue(), Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        Assert.NotNull((await api.GetAsync<CatalogUnit>(Tenant, "catalog/units", row => CatalogPricingContracts.Unit(row))).Value);
        Assert.NotNull((await api.GetAsync<PriceRevision>(Tenant, "pricing/revisions", row => CatalogPricingContracts.Price(row))).Value);
        Assert.Equal(BusinessFailure.InvalidResponse, (await api.GetAsync<CatalogUnit>(Tenant, "catalog/units", row => CatalogPricingContracts.Unit(row))).Failure);
        Assert.Equal(BusinessFailure.InvalidResponse, (await api.GetAsync<PriceRevision>(Tenant, "pricing/revisions", row => CatalogPricingContracts.Price(row))).Failure);
    }

    private sealed class DelayedContent(byte[] bytes, TaskCompletionSource entered, TaskCompletionSource release) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            entered.SetResult(); await release.Task; await stream.WriteAsync(bytes);
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    [Fact]
    public async Task DraftReplacementPinsPutMethodAndExactBodyAcrossUnknownRetry()
    {
        var calls = new List<string>();
        using var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            calls.Add(await request.Content!.ReadAsStringAsync(ct));
            if (calls.Count == 1) throw new HttpRequestException();
            return Json(Organization);
        });
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        var command = BusinessCommand.Create(Tenant, "orders/20000000-0000-0000-0000-000000000001/draft", new { expectedRevision = 7, summary = "Replacement" }, HttpMethod.Put);
        Assert.True((await api.SendAsync<OrganizationRecord>(command, _ => true)).Unknown);
        Assert.NotNull((await api.SendAsync<OrganizationRecord>(command, _ => true)).Value);
        Assert.Equal(calls[0], calls[1]);
        Assert.Throws<ArgumentException>(() => BusinessCommand.Create(Tenant, "orders", new { }, HttpMethod.Delete));
    }

    [Fact]
    public void OrderResponsesRejectIncompleteOrForeignResourceFacts()
    {
        var line = new OrderLine(1, "Printing", 2, "EA", 12.5m, 25);
        var row = new OrderRecord(Organization.OrganizationId, "Order", "NPR", 25, 1, Organization.CreatedAt, [line], "draft", null, null, null, null);
        Assert.True(OrderContracts.Record(row, row.OrderId));
        Assert.False(OrderContracts.Record(row, Guid.NewGuid()));
        Assert.False(OrderContracts.Record(row with { Lines = [line with { Position = 0 }] }));
        Assert.False(OrderContracts.Record(row with { State = "committed" }));
        Assert.False(OrderContracts.Record(row with { CustomerContext = new(Guid.Empty, null) }));
        Assert.False(OrderContracts.Record(row with { QuotationOrigin = new(Guid.Empty, Guid.NewGuid(), 1, 1) }));
    }

    [Fact]
    public async Task DeniedRetryCannotEraseAnEarlierUnknownSaveEnvelope()
    {
        var calls = 0;
        using var handler = new Handler((_, _) =>
        {
            calls++;
            if (calls == 1) throw new HttpRequestException();
            return Task.FromResult(calls == 2 ? new HttpResponseMessage(HttpStatusCode.Forbidden) : Json(Organization));
        });
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        using var page = new RecoveryPage();
        page.Configure(api);
        Assert.Null(await page.SaveAsync());
        var envelope = page.Recovery;
        Assert.NotNull(envelope);
        Assert.Null(await page.SaveAsync());
        Assert.Same(envelope, page.Recovery);
        Assert.Contains("earlier save outcome is still unknown", page.Feedback, StringComparison.Ordinal);
        Assert.NotNull(await page.SaveAsync());
        Assert.Null(page.Recovery);
    }

    private sealed class RecoveryPage : BusinessPage
    {
        internal BusinessCommand? Recovery => PendingCommand;
        internal string Feedback => Message;
        internal void Configure(BusinessApiClient api) { Api = api; TenantId = Tenant; }
        internal Task<OrganizationRecord?> SaveAsync() => SaveAsync<OrganizationRecord>("customers/organizations", new { displayName = "Fieldwork" }, row => CustomerContracts.Organization(row, Tenant), CancellationToken.None);
    }

    private static async Task<(BusinessApiClient Api, WebSessionTickets Tickets, TestAuthentication Authentication, string Key)> ClientAsync(HttpClient http)
    {
        var tickets = new WebSessionTickets(TimeProvider.System);
        var principal = await AddTicketAsync(tickets, "private-session-token");
        var authentication = new TestAuthentication(principal);
        return (new(http, new Uri("https://api.example.test"), authentication, tickets), tickets, authentication, principal.FindFirst(WebSessionTickets.SessionClaim)!.Value);
    }
    private static async Task<ClaimsPrincipal> AddTicketAsync(WebSessionTickets tickets, string token)
    {
        var session = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(WebSessionTickets.SessionClaim, session)], "test-session"));
        var properties = new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) };
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = token }]);
        await tickets.StoreAsync(new(principal, properties, "Cookies")); return principal;
    }
    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private sealed class TestAuthentication(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        internal ClaimsPrincipal Principal { get; set; } = principal;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Principal));
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private sealed class ChunkedContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
