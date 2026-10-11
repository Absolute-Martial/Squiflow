using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class CustomerRepresentativeEndpointTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;
    private readonly HttpClient _client;

    public CustomerRepresentativeEndpointTests(WhiteLabelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RepresentativeManageAndReadAreIndependentReplaySafeAndDoNotLeakContactData()
    {
        var (actor, tenant, token) = Member();
        _factory.SetCustomerDecision(actor, tenant, "createOrganization", true);
        _factory.SetCustomerDecision(actor, tenant, "createIndividual", true);

        var organizations = $"/api/v1/tenants/{tenant:D}/customers/organizations";
        using var organizationCreated = await PostAsync(organizations, token, "org", new { displayName = "Acme" });
        Assert.Equal(HttpStatusCode.Created, organizationCreated.StatusCode);
        using var organizationJson = JsonDocument.Parse(await organizationCreated.Content.ReadAsStringAsync());
        var organizationId = organizationJson.RootElement.GetProperty("organizationId").GetGuid();
        Assert.Equal($"{organizationCreated.RequestMessage!.RequestUri!.AbsolutePath}/{organizationId:D}",
            organizationCreated.Headers.Location!.OriginalString);

        var individuals = $"/api/v1/tenants/{tenant:D}/customers/individuals";
        using var individualCreated = await PostAsync(individuals, token, "person", new
        {
            displayName = "Representative Person",
            email = "private@example.test",
            phone = "+977 12345"
        });
        Assert.Equal(HttpStatusCode.Created, individualCreated.StatusCode);
        using var individualJson = JsonDocument.Parse(await individualCreated.Content.ReadAsStringAsync());
        var individualId = individualJson.RootElement.GetProperty("individualId").GetGuid();
        Assert.Equal($"{individualCreated.RequestMessage!.RequestUri!.AbsolutePath}/{individualId:D}",
            individualCreated.Headers.Location!.OriginalString);

        var representatives = $"{organizations}/{organizationId:D}/representatives";
        using var denied = await PostAsync(representatives, token, "link", new { individualId });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        _factory.SetCustomerDecision(actor, tenant, "manageRepresentatives", true);
        using var linked = await PostAsync(representatives, token, "link", new { individualId });
        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        var linkedBody = await linked.Content.ReadAsStringAsync();
        using var linkedJson = JsonDocument.Parse(linkedBody);
        var representativeId = linkedJson.RootElement.GetProperty("representativeId").GetGuid();
        Assert.Equal($"{linked.RequestMessage!.RequestUri!.AbsolutePath}/{representativeId:D}",
            linked.Headers.Location!.OriginalString);
        Assert.Equal(individualId, linkedJson.RootElement.GetProperty("individualId").GetGuid());
        Assert.False(linkedJson.RootElement.TryGetProperty("displayName", out _));
        Assert.False(linkedJson.RootElement.TryGetProperty("email", out _));
        Assert.False(linkedJson.RootElement.TryGetProperty("phone", out _));

        using var linkReplay = await PostAsync(representatives, token, "link", new { individualId });
        Assert.Equal(HttpStatusCode.OK, linkReplay.StatusCode);
        Assert.Equal(linkedBody, await linkReplay.Content.ReadAsStringAsync());
        Assert.Equal("true", linkReplay.Headers.GetValues("Idempotency-Replayed").Single());

        var relationship = $"{representatives}/{representativeId:D}";
        using var readDenied = await GetAsync(relationship, token);
        Assert.Equal(HttpStatusCode.Forbidden, readDenied.StatusCode);
        _factory.SetCustomerDecision(actor, tenant, "viewRepresentatives", true);
        using var read = await GetAsync(relationship, token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(linkedBody, await read.Content.ReadAsStringAsync());

        _factory.SetCustomerDecision(actor, tenant, "manageRepresentatives", false);
        using var unlinkDenied = await PostAsync(relationship + "/unlink", token, "unlink", new { expectedRevision = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, unlinkDenied.StatusCode);
        _factory.SetCustomerDecision(actor, tenant, "manageRepresentatives", true);
        using var unlinked = await PostAsync(relationship + "/unlink", token, "unlink", new { expectedRevision = 1 });
        Assert.Equal(HttpStatusCode.OK, unlinked.StatusCode);
        var unlinkedBody = await unlinked.Content.ReadAsStringAsync();
        using var unlinkedJson = JsonDocument.Parse(unlinkedBody);
        Assert.Equal("inactive", unlinkedJson.RootElement.GetProperty("availability").GetString());
        Assert.Equal(2, unlinkedJson.RootElement.GetProperty("revision").GetInt64());

        using var unlinkReplay = await PostAsync(relationship + "/unlink", token, "unlink", new { expectedRevision = 1 });
        Assert.Equal(unlinkedBody, await unlinkReplay.Content.ReadAsStringAsync());
        Assert.Equal("true", unlinkReplay.Headers.GetValues("Idempotency-Replayed").Single());
        using var stale = await PostAsync(relationship + "/unlink", token, "stale", new { expectedRevision = 1 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("revision_conflict", await stale.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        _factory.SetCustomerDecision(actor, tenant, "manageRepresentatives", false);
        using var revokedReplay = await PostAsync(relationship + "/unlink", token, "unlink", new { expectedRevision = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, revokedReplay.StatusCode);
    }

    private (Guid Actor, Guid Tenant, string Token) Member()
    {
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var subject = Guid.NewGuid().ToString("N");
        _factory.Bind(subject, actor);
        _factory.AddTenantMembership(actor, tenant, "Representatives tenant");
        return (actor, tenant, _factory.CreateToken(subject));
    }

    private async Task<HttpResponseMessage> PostAsync(string path, string token, string key, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", key);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
