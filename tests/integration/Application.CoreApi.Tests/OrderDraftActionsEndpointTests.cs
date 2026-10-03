using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OrderDraftActionsEndpointTests(WhiteLabelApiFactory factory) : IClassFixture<WhiteLabelApiFactory>
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DraftGuidanceUsesDistinctCurrentPermissionsWithoutChangingTheOrder(bool edit, bool abandon)
    {
        var (account, tenant, token, order) = await CreateDraft();
        factory.SetOrderEditDecision(account, tenant, edit);
        factory.SetOrderAbandonDecision(account, tenant, abandon);
        using var client = factory.CreateClient();
        using var request = Request(HttpMethod.Get, tenant, order, token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(order, document.RootElement.GetProperty("orderId").GetGuid());
        Assert.Equal(1, document.RootElement.GetProperty("observedRevision").GetInt64());
        Assert.Equal(3, document.RootElement.GetProperty("actions").GetArrayLength());
        AssertAction(document, "commit", false, "permission_required");
        AssertAction(document, "revise", edit, edit ? null : "permission_required");
        AssertAction(document, "abandon", abandon, abandon ? null : "permission_required");
        Assert.Equal(1, factory.GetOrderEditCheckCount(account, tenant));
        Assert.Equal(1, factory.GetOrderAbandonCheckCount(account, tenant));
        Assert.Equal(edit ? 2 : 1, factory.GetOrderManualPriceCheckCount(account, tenant));
        Assert.Equal(1, factory.GetOrderCreateCount(tenant));
        Assert.Equal(0, factory.GetOrderReviseCount(tenant));
        Assert.Equal(0, factory.GetOrderAbandonCount(tenant));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MembershipAndViewPermissionAreRequiredBeforeReadingOrProbingWritePermissions(bool member)
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        factory.Bind(account.ToString("D"), account);
        if (member)
            factory.AddTenantMembership(account, tenant, "Guidance tenant");
        factory.SetOrderViewDecision(account, tenant, false);
        factory.SetOrderEditDecision(account, tenant, true);
        factory.SetOrderManualPriceDecision(account, tenant, true);
        factory.SetOrderAbandonDecision(account, tenant, true);
        using var client = factory.CreateClient();
        using var request = Request(HttpMethod.Get, tenant, Guid.NewGuid(), factory.CreateToken(account.ToString("D")));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(member ? "tenant_permission_denied" : "tenant_access_denied", await Code(response));
        Assert.Equal(0, factory.GetOrderFindCount(tenant));
        Assert.Equal(0, factory.GetOrderEditCheckCount(account, tenant));
        Assert.Equal(0, factory.GetOrderAbandonCheckCount(account, tenant));
    }

    [Theory]
    [InlineData("view")]
    [InlineData("edit")]
    [InlineData("abandon")]
    [InlineData("pricing")]
    public async Task PermissionProviderOutageReturnsNoPartialGuide(string stage)
    {
        var (account, tenant, token, order) = await CreateDraft();
        factory.SetOrderEditDecision(account, tenant, true);
        factory.SetOrderManualPriceDecision(account, tenant, true);
        factory.SetOrderAbandonDecision(account, tenant, true);
        if (stage == "view") factory.SetOrderViewUnavailable(account, tenant);
        if (stage == "edit") factory.SetOrderEditUnavailable(account, tenant);
        if (stage == "abandon") factory.SetOrderAbandonUnavailable(account, tenant);
        if (stage == "pricing") factory.SetOrderManualPriceUnavailable(account, tenant);
        using var client = factory.CreateClient();
        using var request = Request(HttpMethod.Get, tenant, order, token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("authorization_unavailable", await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("actions", await response.Content.ReadAsStringAsync());
        Assert.Equal(stage == "view" ? 0 : 1, factory.GetOrderFindCount(tenant));
        Assert.Equal(0, factory.GetOrderReviseCount(tenant));
        Assert.Equal(0, factory.GetOrderAbandonCount(tenant));
    }

    [Fact]
    public async Task GuidanceDoesNotGrantCommandAuthorityAndIsRecomputedAfterRevocation()
    {
        var (account, tenant, token, order) = await CreateDraft();
        factory.SetOrderEditDecision(account, tenant, true);
        factory.SetOrderManualPriceDecision(account, tenant, true);
        factory.SetOrderAbandonDecision(account, tenant, true);
        using var client = factory.CreateClient();
        using var initial = Request(HttpMethod.Get, tenant, order, token);
        using var initialResponse = await client.SendAsync(initial);
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);
        factory.SetOrderEditDecision(account, tenant, false);
        using var command = Request(HttpMethod.Put, tenant, order, token, "/draft");
        command.Headers.Add("Idempotency-Key", "revoked-edit");
        command.Content = JsonContent.Create(new { expectedRevision = 1 });
        using var rejected = await client.SendAsync(command);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal(0, factory.GetOrderReviseCount(tenant));
        using var refreshed = Request(HttpMethod.Get, tenant, order, token);
        using var refreshedResponse = await client.SendAsync(refreshed);
        using var document = JsonDocument.Parse(await refreshedResponse.Content.ReadAsStringAsync());
        AssertAction(document, "revise", false, "permission_required");
        AssertAction(document, "abandon", true, null);
    }

    [Fact]
    public async Task StaleGuidanceCannotSkipRevisionChecksAndAbandonedOrdersNeedNoWritePermissionProbe()
    {
        var (account, tenant, token, order) = await CreateDraft();
        factory.SetOrderEditDecision(account, tenant, true);
        factory.SetOrderManualPriceDecision(account, tenant, true);
        factory.SetOrderAbandonDecision(account, tenant, true);
        using var client = factory.CreateClient();
        using var guide = Request(HttpMethod.Get, tenant, order, token);
        using var guided = await client.SendAsync(guide);
        using var original = JsonDocument.Parse(await guided.Content.ReadAsStringAsync());
        var observed = original.RootElement.GetProperty("observedRevision").GetInt64();
        using var edit = Request(HttpMethod.Put, tenant, order, token, "/draft");
        edit.Headers.Add("Idempotency-Key", "edit-after-guide");
        edit.Content = JsonContent.Create(new
        {
            expectedRevision = observed,
            summary = "Revised draft",
            currencyCode = "USD",
            lines = new[] { new { description = "Work", quantity = 2, unitCode = "EA", unitPrice = 10 } }
        });
        using var edited = await client.SendAsync(edit);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using var stale = Request(HttpMethod.Post, tenant, order, token, "/abandon");
        stale.Headers.Add("Idempotency-Key", "stale-abandon");
        stale.Content = JsonContent.Create(new { expectedRevision = observed });
        using var conflicted = await client.SendAsync(stale);
        Assert.Equal(HttpStatusCode.Conflict, conflicted.StatusCode);
        Assert.Equal("revision_conflict", await Code(conflicted));
        using var abandon = Request(HttpMethod.Post, tenant, order, token, "/abandon");
        abandon.Headers.Add("Idempotency-Key", "current-abandon");
        abandon.Content = JsonContent.Create(new { expectedRevision = 2 });
        using var abandoned = await client.SendAsync(abandon);
        Assert.Equal(HttpStatusCode.OK, abandoned.StatusCode);
        var editChecks = factory.GetOrderEditCheckCount(account, tenant);
        var abandonChecks = factory.GetOrderAbandonCheckCount(account, tenant);
        var priceChecks = factory.GetOrderManualPriceCheckCount(account, tenant);
        factory.SetOrderManualPriceUnavailable(account, tenant);
        factory.SetOrderEditUnavailable(account, tenant);
        factory.SetOrderAbandonUnavailable(account, tenant);
        using var terminal = Request(HttpMethod.Get, tenant, order, token);
        using var terminalResponse = await client.SendAsync(terminal);
        Assert.Equal(HttpStatusCode.OK, terminalResponse.StatusCode);
        using var document = JsonDocument.Parse(await terminalResponse.Content.ReadAsStringAsync());
        Assert.Equal(3, document.RootElement.GetProperty("observedRevision").GetInt64());
        AssertAction(document, "revise", false, "order_already_abandoned");
        AssertAction(document, "abandon", false, "order_already_abandoned");
        Assert.Equal(editChecks, factory.GetOrderEditCheckCount(account, tenant));
        Assert.Equal(abandonChecks, factory.GetOrderAbandonCheckCount(account, tenant));
        Assert.Equal(priceChecks, factory.GetOrderManualPriceCheckCount(account, tenant));
    }

    [Fact]
    public async Task ForeignAndMissingOrdersHaveTheSameNotFoundResultWithoutWritePermissionProbes()
    {
        var (account, _, token, order) = await CreateDraft();
        var other = Guid.NewGuid();
        factory.AddTenantMembership(account, other, "Other guidance tenant");
        factory.SetOrderViewDecision(account, other, true);
        using var client = factory.CreateClient();
        foreach (var id in new[] { order, Guid.NewGuid() })
        {
            using var request = Request(HttpMethod.Get, other, id, token);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("order_not_found", await Code(response));
        }
        Assert.Equal(0, factory.GetOrderEditCheckCount(account, other));
        Assert.Equal(0, factory.GetOrderAbandonCheckCount(account, other));
    }

    [Fact]
    public async Task OpenApiDescribesProtectedNonMutatingGuidanceAndSafeFailures()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/tenants/{tenantId}/orders/{orderId}/actions").GetProperty("get");
        Assert.Equal("GetTenantOrderDraftActions", operation.GetProperty("operationId").GetString());
        Assert.NotEmpty(operation.GetProperty("security").EnumerateArray());
        foreach (var status in new[] { "200", "400", "401", "403", "404", "500", "503", "504" })
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        Assert.False(operation.TryGetProperty("requestBody", out _));
    }

    private async Task<(Guid Account, Guid Tenant, string Token, Guid Order)> CreateDraft()
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        factory.Bind(account.ToString("D"), account);
        factory.AddTenantMembership(account, tenant, "Guidance tenant");
        factory.SetOrderCreateDecision(account, tenant, true);
        factory.SetOrderManualPriceDecision(account, tenant, true);
        factory.SetOrderViewDecision(account, tenant, true);
        var token = factory.CreateToken(account.ToString("D"));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", "guidance-draft");
        request.Content = JsonContent.Create(new
        {
            summary = "Guided draft",
            currencyCode = "USD",
            lines = new[] { new { description = "Work", quantity = 1, unitCode = "EA", unitPrice = 10 } }
        });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (account, tenant, token, document.RootElement.GetProperty("orderId").GetGuid());
    }

    private static HttpRequestMessage Request(HttpMethod method, Guid tenant, Guid order, string token, string suffix = "/actions")
    {
        var request = new HttpRequestMessage(method, $"/api/v1/tenants/{tenant:D}/orders/{order:D}{suffix}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static void AssertAction(JsonDocument document, string action, bool available, string? reason)
    {
        var entry = document.RootElement.GetProperty("actions").EnumerateArray()
            .Single(value => value.GetProperty("action").GetString() == action);
        Assert.Equal(available, entry.GetProperty("available").GetBoolean());
        Assert.Equal(reason, entry.GetProperty("unavailabilityCode").GetString());
    }

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
