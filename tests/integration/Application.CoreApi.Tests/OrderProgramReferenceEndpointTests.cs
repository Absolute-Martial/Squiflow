using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Customers;
using Application.Orders;
using Xunit;

namespace Application.CoreApi.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OrderProgramReferenceEndpointGroup : ICollectionFixture<WhiteLabelApiFactory>
{
    public const string Name = "CoreApi Order program reference";
}

[Collection(OrderProgramReferenceEndpointGroup.Name)]
public sealed class OrderProgramReferenceEndpointTests(WhiteLabelApiFactory factory)
{
    private static readonly string[] ResponseFields = ["orderId", "revision", "externalProgramReference", "programPolicy"];
    [Fact]
    public async Task EditGrantAloneChangesOnlyTheReferenceProjectionAndReplayIsMarked()
    {
        var (accountId, tenantId, token) = Actor();
        var orderId = Guid.NewGuid();
        factory.AddTenantMembership(accountId, tenantId, "Reference tenant");
        factory.SetOrderEditDecision(accountId, tenantId, true);
        factory.SetOrderManualPriceDecision(accountId, tenantId, false);
        factory.SetOrderCreateDecision(accountId, tenantId, false);
        factory.SetOrderCommitDecision(accountId, tenantId, false);

        var order = Snapshot(accountId, tenantId, orderId, "PO-742");
        factory.SetOrderProgramReferenceResult(
            new SetOrderProgramReferenceResult(SetOrderProgramReferenceStatus.Updated, order));
        var initialCalls = factory.GetOrderProgramReferenceCallCount;

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Request(
            tenantId, orderId, token, "reference-change", "{\"expectedRevision\":1,\"externalProgramReference\":\"  PO-742  \"}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(initialCalls + 1, factory.GetOrderProgramReferenceCallCount);
        Assert.Equal("PO-742", factory.GetLastProgramReferenceRequest?.ExternalProgramReference);
        Assert.Equal(1, factory.GetOrderEditCheckCount(accountId, tenantId));
        Assert.Equal(0, factory.GetOrderManualPriceCheckCount(accountId, tenantId));
        Assert.Equal(0, factory.GetOrderCreateCheckCount(accountId, tenantId));
        Assert.Equal(0, factory.GetOrderCommitCheckCount(accountId, tenantId));

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(
            ResponseFields,
            root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(orderId, root.GetProperty("orderId").GetGuid());
        Assert.Equal(2, root.GetProperty("revision").GetInt64());
        Assert.Equal("PO-742", root.GetProperty("externalProgramReference").GetString());
        Assert.DoesNotContain("Confidential", body, StringComparison.Ordinal);
        Assert.DoesNotContain("USD", body, StringComparison.Ordinal);
        Assert.DoesNotContain("987.65", body, StringComparison.Ordinal);
        Assert.DoesNotContain("123.45", body, StringComparison.Ordinal);

        factory.SetOrderProgramReferenceResult(
            new SetOrderProgramReferenceResult(SetOrderProgramReferenceStatus.Replayed, order));
        using var replay = await client.SendAsync(Request(
            tenantId, orderId, token, "reference-change", "{\"expectedRevision\":1,\"externalProgramReference\":\"PO-742\"}"));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        Assert.Equal(initialCalls + 2, factory.GetOrderProgramReferenceCallCount);
    }

    [Fact]
    public async Task DenialAndMissingMembershipStopBeforeTheProgramReferenceStore()
    {
        var (accountId, tenantId, token) = Actor();
        factory.AddTenantMembership(accountId, tenantId, "Reference tenant");
        factory.SetOrderEditDecision(accountId, tenantId, false);
        factory.SetOrderManualPriceDecision(accountId, tenantId, true);
        var initialCalls = factory.GetOrderProgramReferenceCallCount;

        using var client = factory.CreateClient();
        using var denied = await client.SendAsync(Request(tenantId, Guid.NewGuid(), token, "denied", "malformed"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(denied.Headers.CacheControl?.NoStore);
        Assert.Equal(initialCalls, factory.GetOrderProgramReferenceCallCount);
        Assert.Equal(1, factory.GetOrderEditCheckCount(accountId, tenantId));
        Assert.Equal(0, factory.GetOrderManualPriceCheckCount(accountId, tenantId));

        var unjoined = Actor();
        factory.SetOrderEditDecision(unjoined.AccountId, unjoined.TenantId, true);
        using var noMembership = await client.SendAsync(Request(
            unjoined.TenantId, Guid.NewGuid(), unjoined.Token, "no-membership", "malformed"));
        Assert.Equal(HttpStatusCode.Forbidden, noMembership.StatusCode);
        Assert.Equal("tenant_access_denied", await ProblemCode(noMembership));
        Assert.Equal(initialCalls, factory.GetOrderProgramReferenceCallCount);
        Assert.Equal(0, factory.GetOrderEditCheckCount(unjoined.AccountId, unjoined.TenantId));
    }

    [Fact]
    public async Task MembershipRevocationBlocksReplayBeforeTheStore()
    {
        var (accountId, tenantId, token) = Actor();
        var orderId = Guid.NewGuid();
        factory.AddTenantMembership(accountId, tenantId, "Reference tenant");
        factory.SetOrderEditDecision(accountId, tenantId, true);
        factory.SetOrderProgramReferenceResult(
            new SetOrderProgramReferenceResult(
                SetOrderProgramReferenceStatus.Updated,
                Snapshot(accountId, tenantId, orderId, "PO-742")));
        var initialCalls = factory.GetOrderProgramReferenceCallCount;
        using var client = factory.CreateClient();

        using var first = await client.SendAsync(Request(
            tenantId, orderId, token, "same-reference", "{\"expectedRevision\":1,\"externalProgramReference\":\"PO-742\"}"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(initialCalls + 1, factory.GetOrderProgramReferenceCallCount);

        factory.RemoveTenantMembership(accountId, tenantId);
        factory.SetOrderProgramReferenceResult(
            new SetOrderProgramReferenceResult(
                SetOrderProgramReferenceStatus.Replayed,
                Snapshot(accountId, tenantId, orderId, "PO-742")));
        using var replay = await client.SendAsync(Request(
            tenantId, orderId, token, "same-reference", "{\"expectedRevision\":1,\"externalProgramReference\":\"PO-742\"}"));
        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
        Assert.Equal("tenant_access_denied", await ProblemCode(replay));
        Assert.Equal(initialCalls + 1, factory.GetOrderProgramReferenceCallCount);
    }

    [Theory]
    [InlineData("{\"expectedRevision\":1}")]
    [InlineData("{\"expectedRevision\":1,\"externalProgramReference\":\"PO-1\",\"extra\":true}")]
    [InlineData("{\"expectedRevision\":1,\"expectedRevision\":1,\"externalProgramReference\":\"PO-1\"}")]
    [InlineData("{\"expectedRevision\":1,\"externalProgramReference\":\"before\\u0001after\"}")]
    [InlineData("{\"expectedRevision\":1,\"externalProgramReference\":\"\\uD800\"}")]
    [InlineData("{\"expectedRevision\":1,\"externalProgramReference\":\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789\"}")]
    public async Task InvalidReferencePayloadsAreBoundedAndDoNotReachTheStore(string json)
    {
        var (accountId, tenantId, token) = Actor();
        factory.AddTenantMembership(accountId, tenantId, "Reference tenant");
        factory.SetOrderEditDecision(accountId, tenantId, true);
        factory.SetOrderManualPriceDecision(accountId, tenantId, false);
        var initialCalls = factory.GetOrderProgramReferenceCallCount;

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Request(tenantId, Guid.NewGuid(), token, Guid.NewGuid().ToString("N"), json));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("order_program_reference_invalid", await ProblemCode(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(initialCalls, factory.GetOrderProgramReferenceCallCount);
    }

    [Fact]
    public async Task OversizedReferencePayloadIsRejectedBeforeTheStore()
    {
        var (accountId, tenantId, token) = Actor();
        factory.AddTenantMembership(accountId, tenantId, "Reference tenant");
        factory.SetOrderEditDecision(accountId, tenantId, true);
        var initialCalls = factory.GetOrderProgramReferenceCallCount;

        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Request(
            tenantId, Guid.NewGuid(), token, "oversized", new string(' ', 4097)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("request_too_large", await ProblemCode(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(initialCalls, factory.GetOrderProgramReferenceCallCount);
    }

    private (Guid AccountId, Guid TenantId, string Token) Actor()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var subject = accountId.ToString("D");
        factory.Bind(subject, accountId);
        return (accountId, tenantId, factory.CreateToken(subject));
    }

    private static HttpRequestMessage Request(Guid tenantId, Guid orderId, string token, string key, string body)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/tenants/{tenantId:D}/orders/{orderId:D}/program-reference")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static async Task<string?> ProblemCode(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static OrderDraftSnapshot Snapshot(Guid accountId, Guid tenantId, Guid orderId, string reference) =>
        new(
            orderId,
            tenantId,
            accountId,
            "Confidential order summary",
            "USD",
            987.65m,
            2,
            DateTimeOffset.UnixEpoch,
            [new OrderDraftLine(1, "Confidential item", 1m, "each", 123.45m, 123.45m)],
            CustomerContext: new CustomerOrderContext(Guid.NewGuid(), Guid.NewGuid()),
            ProgramPolicy: new OrderProgramPolicyFacts(Guid.NewGuid(), Guid.NewGuid(), true),
            ExternalProgramReference: reference);
}
