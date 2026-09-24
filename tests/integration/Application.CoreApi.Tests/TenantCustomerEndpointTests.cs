using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Customers;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class TenantCustomerEndpointTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;

    public TenantCustomerEndpointTests(WhiteLabelApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OrganizationAndProgramCreateReplayReadAndBrowseThroughHost()
    {
        var (client, account, tenant) = Client();
        AllowAll(account, tenant);
        var basePath = $"/api/v1/tenants/{tenant:D}/customers/organizations";
        using var create = Post(basePath, "org-one", "Acme");
        using var created = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        var organization = await created.Content.ReadFromJsonAsync<CustomerOrganizationSnapshot>();
        Assert.NotNull(organization);
        using var replay = Post(basePath, "org-one", "Acme");
        using var replayed = await client.SendAsync(replay);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.Equal("true", replayed.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(organization.OrganizationId, (await replayed.Content.ReadFromJsonAsync<CustomerOrganizationSnapshot>())!.OrganizationId);
        using var conflict = Post(basePath, "org-one", "Other");
        using var conflicting = await client.SendAsync(conflict);
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);
        Assert.Contains("idempotency_key_conflict", await conflicting.Content.ReadAsStringAsync());
        var orgPath = $"{basePath}/{organization.OrganizationId:D}";
        using var got = await client.GetAsync(orgPath);
        Assert.Equal(HttpStatusCode.OK, got.StatusCode);
        using var listed = await client.GetAsync(basePath + "?limit=1");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains(organization.OrganizationId.ToString("D"), await listed.Content.ReadAsStringAsync());
        using var createProgram = Post(orgPath + "/programs", "program-one", "Launch");
        using var programCreated = await client.SendAsync(createProgram);
        Assert.Equal(HttpStatusCode.Created, programCreated.StatusCode);
        var program = await programCreated.Content.ReadFromJsonAsync<CustomerProgramSnapshot>();
        Assert.NotNull(program);
        using var programGot = await client.GetAsync(orgPath + $"/programs/{program.ProgramId:D}");
        Assert.Equal(HttpStatusCode.OK, programGot.StatusCode);
        using var programs = await client.GetAsync(orgPath + "/programs?limit=1");
        Assert.Equal(HttpStatusCode.OK, programs.StatusCode);
        Assert.Contains(program.ProgramId.ToString("D"), await programs.Content.ReadAsStringAsync());
        using var nextProgram = Post(orgPath + "/programs", "program-two", "Renewal");
        using var nextCreated = await client.SendAsync(nextProgram);
        Assert.Equal(HttpStatusCode.Created, nextCreated.StatusCode);
        using var firstProgramPage = await client.GetAsync(orgPath + "/programs?limit=1");
        using var page = JsonDocument.Parse(await firstProgramPage.Content.ReadAsStringAsync());
        var cursor = page.RootElement.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));
        using var nextProgramPage = await client.GetAsync(orgPath + $"/programs?limit=1&after={cursor}");
        Assert.Equal(HttpStatusCode.OK, nextProgramPage.StatusCode);
        Assert.Contains("Renewal", await nextProgramPage.Content.ReadAsStringAsync());
        using var anotherOrg = Post(basePath, "org-two", "Other Org");
        using var anotherCreated = await client.SendAsync(anotherOrg);
        var otherOrg = await anotherCreated.Content.ReadFromJsonAsync<CustomerOrganizationSnapshot>();
        Assert.NotNull(otherOrg);
        using var crossParentCursor = await client.GetAsync($"{basePath}/{otherOrg.OrganizationId:D}/programs?after={cursor}");
        Assert.Equal(HttpStatusCode.BadRequest, crossParentCursor.StatusCode);
        using var wrongParent = await client.GetAsync($"{basePath}/{Guid.NewGuid():D}/programs/{program.ProgramId:D}");
        Assert.Equal(HttpStatusCode.NotFound, wrongParent.StatusCode);
        using var unknownParentList = await client.GetAsync($"{basePath}/{Guid.NewGuid():D}/programs");
        Assert.Equal(HttpStatusCode.NotFound, unknownParentList.StatusCode);
        using var otherTenant = await client.GetAsync($"/api/v1/tenants/{Guid.NewGuid():D}/customers/organizations/{organization.OrganizationId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, otherTenant.StatusCode);
    }

    [Fact]
    public async Task MembershipAndEachPermissionAreRequiredBeforeCustomerStoreIsCalled()
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var client = AuthorizedClient(account);
        var path = $"/api/v1/tenants/{tenant:D}/customers/organizations";
        using var noMembership = Post(path, Guid.NewGuid().ToString("N"), "Acme");
        using var deniedMembership = await client.SendAsync(noMembership);
        Assert.Equal(HttpStatusCode.Forbidden, deniedMembership.StatusCode);
        Assert.Equal(0, _factory.GetCustomerCheckCount(account, tenant, "createOrganization"));
        _factory.AddTenantMembership(account, tenant, "Tenant");
        using var noGrant = Post(path, Guid.NewGuid().ToString("N"), "Acme");
        using var deniedGrant = await client.SendAsync(noGrant);
        Assert.Equal(HttpStatusCode.Forbidden, deniedGrant.StatusCode);
        _factory.SetCustomerUnavailable(account, tenant, "createOrganization");
        using var outage = Post(path, Guid.NewGuid().ToString("N"), "Acme");
        using var unavailable = await client.SendAsync(outage);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Contains("authorization_unavailable", await unavailable.Content.ReadAsStringAsync());
        Assert.Equal(0, _factory.GetCustomerStoreCallCount(tenant));
        _factory.SetCustomerDecision(account, tenant, "createOrganization", true);
        using var allowed = Post(path, Guid.NewGuid().ToString("N"), "Acme");
        using var created = await client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var org = await created.Content.ReadFromJsonAsync<CustomerOrganizationSnapshot>();
        using var cannotView = await client.GetAsync($"{path}/{org!.OrganizationId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, cannotView.StatusCode);
        using var cannotCreateProgram = Post($"{path}/{org.OrganizationId:D}/programs", "program-key", "Launch");
        using var programDenied = await client.SendAsync(cannotCreateProgram);
        Assert.Equal(HttpStatusCode.Forbidden, programDenied.StatusCode);
    }

    [Fact]
    public async Task CustomerInputsAndCursorsAreBounded()
    {
        var (client, account, tenant) = Client();
        AllowAll(account, tenant);
        var path = $"/api/v1/tenants/{tenant:D}/customers/organizations";
        using var missingKey = await client.PostAsJsonAsync(path, new { displayName = "Acme" });
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        using var malformed = Post(path, "bad-name", " ");
        using var invalid = await client.SendAsync(malformed);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var large = Post(path, "large-name", new string('x', 5000));
        using var oversized = await client.SendAsync(large);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        using var badPage = await client.GetAsync(path + "?limit=51");
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
        using var badCursor = await client.GetAsync(path + "?after=bad");
        Assert.Equal(HttpStatusCode.BadRequest, badCursor.StatusCode);
        using var duplicateLimit = await client.GetAsync(path + "?limit=1&limit=2");
        Assert.Equal(HttpStatusCode.BadRequest, duplicateLimit.StatusCode);
        using var duplicateAfter = await client.GetAsync(path + "?after=a&after=b");
        Assert.Equal(HttpStatusCode.BadRequest, duplicateAfter.StatusCode);
        using var duplicateKey = Post(path, "one", "Acme");
        duplicateKey.Headers.Add("Idempotency-Key", "two");
        using var rejectedKey = await client.SendAsync(duplicateKey);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedKey.StatusCode);
        using var streamed = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StreamContent(new NonSeekableMemoryStream(System.Text.Encoding.UTF8.GetBytes(
                "{\"displayName\":\"" + new string('x', 5000) + "\"}"))),
        };
        streamed.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        streamed.Headers.TransferEncodingChunked = true;
        streamed.Headers.Add("Idempotency-Key", "streamed-large");
        using var rejectedStream = await client.SendAsync(streamed);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, rejectedStream.StatusCode);
    }

    [Fact]
    public async Task MalformedRequestBodyReadReturnsStableClientError()
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var subject = Guid.NewGuid().ToString("N");
        _factory.Bind(subject, account);
        _factory.AddTenantMembership(account, tenant, "Tenant");
        _factory.SetCustomerDecision(account, tenant, "createOrganization", true);

        var context = await _factory.Server.SendAsync(request =>
        {
            request.Request.Method = HttpMethods.Post;
            request.Request.Path = $"/api/v1/tenants/{tenant:D}/customers/organizations";
            request.Request.ContentType = "application/json";
            request.Request.Headers.Authorization = $"Bearer {_factory.CreateToken(subject)}";
            request.Request.Headers["Idempotency-Key"] = "malformed-body-read";
            request.Request.Body = new BadRequestBodyStream();
        });

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("request_invalid", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, _factory.GetCustomerStoreCallCount(tenant));
    }

    [Fact]
    public async Task OrderCustomerContextIsReturnedInDetailAndListAndMissingContextIsNotFound()
    {
        var (client, account, tenant) = Client();
        AllowAll(account, tenant);
        _factory.SetOrderCreateDecision(account, tenant, true);
        _factory.SetOrderViewDecision(account, tenant, true);
        var organizations = $"/api/v1/tenants/{tenant:D}/customers/organizations";
        using var createOrg = Post(organizations, "order-context-org", "Acme");
        using var createdOrg = await client.SendAsync(createOrg);
        var org = await createdOrg.Content.ReadFromJsonAsync<CustomerOrganizationSnapshot>();
        Assert.NotNull(org);
        var orders = $"/api/v1/tenants/{tenant:D}/orders";
        using var missing = OrderPost(orders, "missing-customer", Guid.NewGuid());
        using var missingResponse = await client.SendAsync(missing);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Contains("customer_context_not_found", await missingResponse.Content.ReadAsStringAsync());
        using var malformed = OrderPost(orders, "malformed-customer", org.OrganizationId);
        malformed.Content = JsonContent.Create(new
        {
            summary = "Customer order",
            currencyCode = "USD",
            lines = new[] { new { description = "Service", quantity = 1, unitCode = "each", unitPrice = 10m } },
            customerContext = new { organizationId = "not-a-guid" },
        });
        using var malformedResponse = await client.SendAsync(malformed);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
        using var emptyContext = OrderPost(orders, "empty-customer", Guid.Empty);
        using var emptyResponse = await client.SendAsync(emptyContext);
        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
        Assert.Contains("customer_context_invalid", await emptyResponse.Content.ReadAsStringAsync());
        using var createOrder = OrderPost(orders, "valid-customer", org.OrganizationId);
        using var createdOrder = await client.SendAsync(createOrder);
        Assert.Equal(HttpStatusCode.Created, createdOrder.StatusCode);
        var detail = await createdOrder.Content.ReadAsStringAsync();
        Assert.Contains(org.OrganizationId.ToString("D"), detail);
        using var listed = await client.GetAsync(orders);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains(org.OrganizationId.ToString("D"), await listed.Content.ReadAsStringAsync());
    }

    private (HttpClient Client, Guid Account, Guid Tenant) Client()
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var client = AuthorizedClient(account);
        _factory.AddTenantMembership(account, tenant, "Tenant");
        return (client, account, tenant);
    }

    private HttpClient AuthorizedClient(Guid account)
    {
        var subject = Guid.NewGuid().ToString("N");
        _factory.Bind(subject, account);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken(subject));
        return client;
    }

    private void AllowAll(Guid account, Guid tenant)
    {
        foreach (var operation in new[] { "createOrganization", "viewOrganizations", "createProgram", "viewPrograms" })
            _factory.SetCustomerDecision(account, tenant, operation, true);
    }

    private static HttpRequestMessage Post(string path, string key, string displayName)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { displayName }) };
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static HttpRequestMessage OrderPost(string path, string key, Guid organizationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new
            {
                summary = "Customer order",
                currencyCode = "USD",
                lines = new[] { new { description = "Service", quantity = 1, unitCode = "each", unitPrice = 10m } },
                customerContext = new { organizationId },
            }),
        };
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private sealed class NonSeekableMemoryStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    private sealed class BadRequestBodyStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new BadHttpRequestException("Malformed request body.", StatusCodes.Status400BadRequest));
    }
}
