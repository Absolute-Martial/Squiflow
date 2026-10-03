using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OrderManualPricingPermissionTests(WhiteLabelApiFactory factory) : IClassFixture<WhiteLabelApiFactory>
{
    [Theory]
    [InlineData("create")]
    [InlineData("revise")]
    [InlineData("preview")]
    public async Task PricingDenialPrecedesBodyParsingAndLeavesNoEffect(string operation)
    {
        var (account, tenant, token) = Actor();
        factory.SetOrderCreateDecision(account, tenant, true);
        factory.SetOrderEditDecision(account, tenant, true);
        factory.SetOrderManualPriceDecision(account, tenant, false);
        using var client = factory.CreateClient();
        using var request = Request(operation, tenant, Guid.NewGuid(), token, "malformed JSON");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenant_permission_denied", await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(1, factory.GetOrderManualPriceCheckCount(account, tenant));
        AssertNoEffect(tenant);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("revise")]
    [InlineData("preview")]
    public async Task PricingGrantDoesNotGrantTheBaseOrderPermission(string operation)
    {
        var (account, tenant, token) = Actor();
        factory.SetOrderManualPriceDecision(account, tenant, true);
        using var client = factory.CreateClient();
        using var request = Request(operation, tenant, Guid.NewGuid(), token, "malformed JSON");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, factory.GetOrderManualPriceCheckCount(account, tenant));
        AssertNoEffect(tenant);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("revise")]
    [InlineData("preview")]
    public async Task PricingProviderFailureIsSafeAndCannotCommit(string operation)
    {
        var (account, tenant, token) = Actor();
        factory.SetOrderCreateDecision(account, tenant, true);
        factory.SetOrderEditDecision(account, tenant, true);
        factory.SetOrderManualPriceUnavailable(account, tenant);
        using var client = factory.CreateClient();
        using var request = Request(operation, tenant, Guid.NewGuid(), token, "malformed JSON");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("authorization_unavailable", await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("Synthetic", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        AssertNoEffect(tenant);
    }

    [Fact]
    public async Task MembershipCannotBeReplacedByOrderAndPricingGrants()
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        factory.Bind(account.ToString("D"), account);
        factory.SetOrderCreateDecision(account, tenant, true);
        factory.SetOrderManualPriceDecision(account, tenant, true);
        using var client = factory.CreateClient();
        using var request = Request("create", tenant, Guid.NewGuid(), factory.CreateToken(account.ToString("D")), Draft());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenant_access_denied", await Code(response));
        Assert.Equal(0, factory.GetOrderManualPriceCheckCount(account, tenant));
        AssertNoEffect(tenant);
    }

    [Fact]
    public async Task RevocationDeniesCreateReplayWithoutLosingTheCommittedResult()
    {
        var (account, tenant, token) = Actor();
        factory.SetOrderCreateDecision(account, tenant, true);
        factory.SetOrderManualPriceDecision(account, tenant, true);
        using var client = factory.CreateClient();
        using var first = Request("create", tenant, Guid.NewGuid(), token, Draft());
        using var created = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadAsStringAsync();
        factory.SetOrderManualPriceDecision(account, tenant, false);
        using var retry = Request("create", tenant, Guid.NewGuid(), token, Draft());
        using var denied = await client.SendAsync(retry);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(1, factory.GetOrderCreateCount(tenant));
        factory.SetOrderManualPriceDecision(account, tenant, true);
        using var authorizedRetry = Request("create", tenant, Guid.NewGuid(), token, Draft());
        using var replay = await client.SendAsync(authorizedRetry);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
    }

    [Fact]
    public async Task RevocationDisablesRevisionGuidanceAndExecutionButNotViewingOrAbandonment()
    {
        var (account, tenant, token) = Actor();
        factory.SetOrderCreateDecision(account, tenant, true);
        factory.SetOrderManualPriceDecision(account, tenant, true);
        factory.SetOrderViewDecision(account, tenant, true);
        factory.SetOrderEditDecision(account, tenant, true);
        factory.SetOrderAbandonDecision(account, tenant, true);
        using var client = factory.CreateClient();
        using var create = Request("create", tenant, Guid.NewGuid(), token, Draft());
        using var created = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var snapshot = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var order = snapshot.RootElement.GetProperty("orderId").GetGuid();
        using var initial = Request("actions", tenant, order, token);
        using var guide = await client.SendAsync(initial);
        Assert.Equal(HttpStatusCode.OK, guide.StatusCode);
        using var initialDocument = JsonDocument.Parse(await guide.Content.ReadAsStringAsync());
        Assert.True(initialDocument.RootElement.GetProperty("actions")[0].GetProperty("available").GetBoolean());

        factory.SetOrderManualPriceDecision(account, tenant, false);
        // Even unchanged prices are resubmitted by the full priced replacement route.
        using var edit = Request("revise", tenant, order, token, Draft(expectedRevision: 1));
        using var denied = await client.SendAsync(edit);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, factory.GetOrderReviseCount(tenant));
        using var refreshed = Request("actions", tenant, order, token);
        using var refresh = await client.SendAsync(refreshed);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        using var document = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        var actions = document.RootElement.GetProperty("actions");
        Assert.False(actions[0].GetProperty("available").GetBoolean());
        Assert.Equal("permission_required", actions[0].GetProperty("unavailabilityCode").GetString());
        Assert.True(actions[1].GetProperty("available").GetBoolean());
        using var read = Request("view", tenant, order, token);
        using var viewed = await client.SendAsync(read);
        Assert.Equal(HttpStatusCode.OK, viewed.StatusCode);
        var pricingChecks = factory.GetOrderManualPriceCheckCount(account, tenant);
        using var abandon = Request("abandon", tenant, order, token, """{"expectedRevision":1}""");
        using var abandoned = await client.SendAsync(abandon);
        Assert.Equal(HttpStatusCode.OK, abandoned.StatusCode);
        Assert.Equal(pricingChecks, factory.GetOrderManualPriceCheckCount(account, tenant));
    }

    [Fact]
    public async Task RevisionReplayRechecksPricingAndPreservesItsHistoricalSnapshot()
    {
        var (account, tenant, token) = Actor();
        factory.SetOrderCreateDecision(account, tenant, true);
        factory.SetOrderEditDecision(account, tenant, true);
        factory.SetOrderManualPriceDecision(account, tenant, true);
        using var client = factory.CreateClient();
        using var create = Request("create", tenant, Guid.NewGuid(), token, Draft());
        using var created = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var order = document.RootElement.GetProperty("orderId").GetGuid();
        using var revise = Request("revise", tenant, order, token, Draft(expectedRevision: 1));
        using var revised = await client.SendAsync(revise);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        var original = await revised.Content.ReadAsStringAsync();
        factory.SetOrderManualPriceDecision(account, tenant, false);
        using var retry = Request("revise", tenant, order, token, Draft(expectedRevision: 1));
        using var denied = await client.SendAsync(retry);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(1, factory.GetOrderReviseCount(tenant));
        factory.SetOrderManualPriceDecision(account, tenant, true);
        using var authorized = Request("revise", tenant, order, token, Draft(expectedRevision: 1));
        using var replay = await client.SendAsync(authorized);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
    }

    private (Guid Account, Guid Tenant, string Token) Actor()
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        factory.Bind(account.ToString("D"), account);
        factory.AddTenantMembership(account, tenant, "Pricing tenant");
        return (account, tenant, factory.CreateToken(account.ToString("D")));
    }

    private void AssertNoEffect(Guid tenant)
    {
        Assert.Equal(0, factory.GetOrderCreateCount(tenant));
        Assert.Equal(0, factory.GetOrderReviseCount(tenant));
        Assert.Equal(0, factory.GetOrderAbandonCount(tenant));
        Assert.Equal(0, factory.GetOrderFindCount(tenant));
    }

    private static string Draft(long? expectedRevision = null) => JsonSerializer.Serialize(new
    {
        expectedRevision,
        summary = "Manual price draft",
        currencyCode = "USD",
        lines = new[] { new { description = "Work", quantity = 1, unitCode = "EA", unitPrice = 10 } },
    });

    private static HttpRequestMessage Request(string operation, Guid tenant, Guid order, string token, string? body = null)
    {
        var path = $"/api/v1/tenants/{tenant:D}/orders";
        var (method, suffix) = operation switch
        {
            "create" => (HttpMethod.Post, ""),
            "revise" => (HttpMethod.Put, $"/{order:D}/draft"),
            "preview" => (HttpMethod.Post, "/price-preview"),
            "actions" => (HttpMethod.Get, $"/{order:D}/actions"),
            "view" => (HttpMethod.Get, $"/{order:D}"),
            "abandon" => (HttpMethod.Post, $"/{order:D}/abandon"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        var request = new HttpRequestMessage(method, path + suffix);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (operation is "create" or "revise" or "abandon")
            request.Headers.Add("Idempotency-Key", "pricing-" + operation);
        return request;
    }

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
