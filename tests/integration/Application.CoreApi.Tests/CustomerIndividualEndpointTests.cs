using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class CustomerIndividualEndpointTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;
    private readonly HttpClient _client;
    public CustomerIndividualEndpointTests(WhiteLabelApiFactory factory)
    {
        _factory = factory; _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateReadAvailabilityAndRetriesPreserveHistoricalResultsAndIndependentPermissions()
    {
        var (actor, tenant, token) = Member();
        var path = Path(tenant);
        _factory.SetCustomerDecision(actor, tenant, "createIndividual", true);
        var create = "{\"displayName\":\"Jane Doe\",\"email\":\"jane@example.test\",\"phone\":\"+977 12345\"}";
        using var created = await Send(path, token, "create", create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        var original = await created.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(original);
        var id = json.RootElement.GetProperty("individualId").GetGuid();
        Assert.NotEqual(actor, id);
        Assert.Equal("active", json.RootElement.GetProperty("availability").GetString());
        Assert.False(json.RootElement.TryGetProperty("accountId", out _));
        var recordPath = $"{path}/{id:D}";
        using var readDenied = await Send(recordPath, token);
        Assert.Equal(HttpStatusCode.Forbidden, readDenied.StatusCode);
        _factory.SetCustomerDecision(actor, tenant, "viewIndividuals", true);
        using var read = await Send(recordPath, token);
        Assert.Equal(original, await read.Content.ReadAsStringAsync());
        using var changeDenied = await Send(recordPath + "/availability", token, "inactive", "{\"expectedRevision\":1,\"availability\":\"inactive\"}");
        Assert.Equal(HttpStatusCode.Forbidden, changeDenied.StatusCode);
        _factory.SetCustomerDecision(actor, tenant, "changeIndividualAvailability", true);
        _factory.SetCustomerDecision(actor, tenant, "viewIndividuals", false);
        using var changed = await Send(recordPath + "/availability", token, "inactive", "{\"expectedRevision\":1,\"availability\":\"inactive\"}");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var changedJson = await changed.Content.ReadAsStringAsync();
        using var metadata = JsonDocument.Parse(changedJson);
        Assert.Equal("inactive", metadata.RootElement.GetProperty("availability").GetString());
        Assert.Equal(2, metadata.RootElement.GetProperty("revision").GetInt64());
        Assert.False(metadata.RootElement.TryGetProperty("email", out _));
        Assert.False(metadata.RootElement.TryGetProperty("displayName", out _));
        using var repeated = await Send(recordPath + "/availability", token, "inactive", "{\"expectedRevision\":1,\"availability\":\"inactive\"}");
        Assert.Equal(changedJson, await repeated.Content.ReadAsStringAsync());
        Assert.Equal("true", repeated.Headers.GetValues("Idempotency-Replayed").Single());
        using var stale = await Send(recordPath + "/availability", token, "stale", "{\"expectedRevision\":1,\"availability\":\"active\"}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var restore = await Send(recordPath + "/availability", token, "active", "{\"expectedRevision\":2,\"availability\":\"active\"}");
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        using var oldReplay = await Send(recordPath + "/availability", token, "inactive", "{\"expectedRevision\":1,\"availability\":\"inactive\"}");
        Assert.Equal(changedJson, await oldReplay.Content.ReadAsStringAsync());
        _factory.SetCustomerDecision(actor, tenant, "changeIndividualAvailability", false);
        using var revokedReplay = await Send(recordPath + "/availability", token, "inactive", "{\"expectedRevision\":1,\"availability\":\"inactive\"}");
        Assert.Equal(HttpStatusCode.Forbidden, revokedReplay.StatusCode);
        using var createReplay = await Send(path, token, "create", create);
        Assert.Equal(original, await createReplay.Content.ReadAsStringAsync());
        using var conflict = await Send(path, token, "create", "{\"displayName\":\"Changed name\"}");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task MembershipProviderFailureAndTenantBoundaryPrecedePersistence()
    {
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        _factory.Bind(subject, actor); var token = _factory.CreateToken(subject);
        _factory.SetCustomerDecision(actor, tenant, "createIndividual", true);
        using var absent = await Send(Path(tenant), token, "create", "{\"displayName\":\"Person\"}");
        Assert.Equal(HttpStatusCode.Forbidden, absent.StatusCode);
        Assert.Equal(0, _factory.GetCustomerStoreCallCount(tenant));
        Assert.Equal(0, _factory.GetCustomerCheckCount(actor, tenant, "createIndividual"));
        _factory.AddTenantMembership(actor, tenant, "Tenant");
        _factory.SetCustomerUnavailable(actor, tenant, "createIndividual");
        using var unavailable = await Send(Path(tenant), token, "create", "{\"displayName\":\"Person\"}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal(0, _factory.GetCustomerStoreCallCount(tenant));
        _factory.SetCustomerDecision(actor, tenant, "createIndividual", true);
        using var created = await Send(Path(tenant), token, "create", "{\"displayName\":\"Person\"}");
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("individualId").GetGuid();
        var other = Guid.NewGuid(); _factory.AddTenantMembership(actor, other, "Other");
        _factory.SetCustomerDecision(actor, other, "viewIndividuals", true);
        using var foreign = await Send($"{Path(other)}/{id:D}", token);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var anonymous = await Send(Path(tenant), null, "anonymous", "not-json");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("no-store", anonymous.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("{\"displayName\":\" \"}")]
    [InlineData("{\"displayName\":\"Person\",\"accountId\":\"unexpected\"}")]
    [InlineData("{\"displayName\":\"First\",\"displayName\":\"Second\"}")]
    [InlineData("{\"displayName\":\"Person\",\"email\":\"invalid\"}")]
    [InlineData("{\"displayName\":\"Person\",\"phone\":\"abc\"}")]
    [InlineData("[]")]
    public async Task InvalidCreateDoesNotReachStore(string body)
    {
        var (actor, tenant, token) = Member();
        _factory.SetCustomerDecision(actor, tenant, "createIndividual", true);
        using var response = await Send(Path(tenant), token, "key", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.GetCustomerStoreCallCount(tenant));
    }

    [Theory]
    [InlineData("{\"expectedRevision\":0,\"availability\":\"inactive\"}")]
    [InlineData("{\"expectedRevision\":1,\"availability\":\"unknown\"}")]
    [InlineData("{\"expectedRevision\":1,\"availability\":1}")]
    [InlineData("{\"expectedRevision\":1,\"availability\":\"inactive\",\"availability\":\"active\"}")]
    [InlineData("{\"expectedRevision\":1,\"availability\":\"inactive\",\"email\":\"changed@example.test\"}")]
    public async Task InvalidAvailabilityDoesNotReachStore(string body)
    {
        var (actor, tenant, token) = Member();
        _factory.SetCustomerDecision(actor, tenant, "changeIndividualAvailability", true);
        using var response = await Send($"{Path(tenant)}/{Guid.NewGuid():D}/availability", token, "key", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.GetCustomerStoreCallCount(tenant));
    }

    private (Guid Actor, Guid Tenant, string Token) Member()
    {
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        _factory.Bind(subject, actor); _factory.AddTenantMembership(actor, tenant, "Individuals tenant");
        return (actor, tenant, _factory.CreateToken(subject));
    }

    private async Task<HttpResponseMessage> Send(string path, string? token, string? key = null, string? body = null)
    {
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await _client.SendAsync(request);
    }

    private static string Path(Guid tenant) => $"/api/v1/tenants/{tenant:D}/customers/individuals";
}
