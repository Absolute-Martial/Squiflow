using System.Net;
using System.Text;
using Application.Web.Business;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessClientTests
{
    [Theory]
    [InlineData("authorization/permissions/grant")]
    [InlineData("authorization/roles")]
    [InlineData("authorization/proposals/20000000-0000-0000-0000-000000000001/reconcile")]
    public async Task AcceptedAuthorizationProposalIsNotReportedAsApplied(string resource)
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = System.Net.Http.Json.JsonContent.Create(ProposalFixture()) }));
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        var result = await api.SendAsync<TenantProposalRecord>(BusinessCommand.Create(Tenant, resource, new { }), x => TenantAdministrationContracts.Proposal(x, Tenant));
        Assert.NotNull(result.Value); Assert.True(result.Accepted); Assert.Contains("accepted for processing", result.Message, StringComparison.Ordinal);
        Assert.Equal(1, result.Value.Status);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("profile-policy")]
    [InlineData("authorization/owner-transfer")]
    public async Task UnexpectedAcceptedStatusDoesNotCompleteSynchronousBusinessSave(string resource)
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = System.Net.Http.Json.JsonContent.Create(ProposalFixture()) }));
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        Assert.True((await api.SendAsync<TenantProposalRecord>(BusinessCommand.Create(Tenant, resource, new { }), _ => true)).Unknown);
    }

    [Fact]
    public async Task UnknownOwnerProposalFreezesIntentAndDoesNotMoveToAnotherSession()
    {
        var intents = new List<(string Body, string Key)>();
        using var handler = new Handler(async (request, ct) =>
        {
            intents.Add((await request.Content!.ReadAsStringAsync(ct), request.Headers.GetValues("Idempotency-Key").Single()));
            if (intents.Count == 1) throw new HttpRequestException("private provider diagnostic");
            return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = System.Net.Http.Json.JsonContent.Create(ProposalFixture()) };
        });
        using var http = new HttpClient(handler); var (api, tickets, auth, _) = await ClientAsync(http);
        var payload = new Dictionary<string, object> { ["expectedAuthorizationRevision"] = 7, ["permissionId"] = "orders.view" };
        var command = BusinessCommand.Create(Tenant, "authorization/permissions/grant", payload); payload["expectedAuthorizationRevision"] = 8;
        Assert.True((await api.SendAsync<TenantProposalRecord>(command, x => TenantAdministrationContracts.Proposal(x, Tenant))).Unknown);
        Assert.True((await api.SendAsync<TenantProposalRecord>(command, x => TenantAdministrationContracts.Proposal(x, Tenant))).Accepted);
        Assert.Equal(intents[0], intents[1]); Assert.DoesNotContain(":8", intents[1].Body, StringComparison.Ordinal);
        auth.Principal = await AddTicketAsync(tickets, "replacement-session-token");
        Assert.Equal(BusinessFailure.SignInRequired, (await api.SendAsync<TenantProposalRecord>(command, _ => true)).Failure); Assert.Equal(2, intents.Count);
    }

    [Fact]
    public void TenantProposalRetainsFinishedAndUnresolvedEvidenceWithoutGuessingCompletion()
    {
        var pending = ProposalFixture();
        Assert.True(TenantAdministrationContracts.Proposal(pending, Tenant));
        foreach (var code in new[] { "execution_finished:authorization_model_rejected", "execution_unresolved:provider_timeout", "execution_in_flight" })
            Assert.True(TenantAdministrationContracts.Proposal(pending with { Status = 4, FailureCode = code, AttemptCount = 20 }, Tenant));
        Assert.False(TenantAdministrationContracts.Proposal(pending with { Status = 2 }, Tenant));
        Assert.True(TenantAdministrationContracts.Proposal(pending with { Status = 2, AppliedAuthorizationRevision = 8 }, Tenant));
        Assert.False(TenantAdministrationContracts.Proposal(pending with { TenantId = Guid.NewGuid() }, Tenant));
        Assert.False(TenantAdministrationContracts.Proposal(pending with { AttemptCount = 21 }, Tenant));
        Assert.False(TenantAdministrationContracts.Proposal(pending with { TargetAccountId = null }, Tenant));
        Assert.False(TenantAdministrationContracts.Proposal(pending with { Status = 9 }, Tenant));
    }

    [Fact]
    public async Task LiteralProfilePolicyPublicationAdvancesRevisionAndRequiresBothPolicyFlags()
    {
        var id = Guid.NewGuid(); var actor = Guid.NewGuid();
        var json = $$$"""
            {"status":"PolicyPublished","policy":{"tenantId":"{{{Tenant}}}","revision":2,"requireReferenceForProgramOrders":true,"publishedPolicyRevisionId":"{{{id}}}","requireApprovalForProgramOrders":true},"publishedPolicy":{"policyRevisionId":"{{{id}}}","tenantId":"{{{Tenant}}}","revision":2,"requireReferenceForProgramOrders":true,"publishedByAccountId":"{{{actor}}}","publishedAt":"2026-10-18T10:00:00Z","observedAuthorizationRevision":7,"version":"tenant-policy/v2","requireApprovalForProgramOrders":true}}
            """;
        var bodies = new Queue<string>([json, json.Replace("\"requireApprovalForProgramOrders\":true", "\"otherFlag\":true", StringComparison.Ordinal)]);
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(bodies.Dequeue(), Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        var command = BusinessCommand.Create(Tenant, "profile-policy/publish", new { expectedRevision = 1 });
        var value = await api.SendAsync<PolicyCommandReceipt>(command, x => TenantAdministrationContracts.PolicyReceipt(x, Tenant, true, 1, true, true));
        Assert.NotNull(value.Value); Assert.Equal(2, value.Value.PublishedPolicy!.Revision);
        Assert.False(TenantAdministrationContracts.PolicyReceipt(value.Value with { Policy = value.Value.Policy! with { TenantId = Guid.NewGuid() } }, Tenant, true, 1, true, true));
        Assert.False(TenantAdministrationContracts.PolicyReceipt(value.Value, Tenant, true, 2, true, true));
        Assert.True((await api.SendAsync<PolicyCommandReceipt>(command, x => TenantAdministrationContracts.PolicyReceipt(x, Tenant, true, 1, true, true))).Unknown);
    }

    private static TenantProposalRecord ProposalFixture() => new(Guid.NewGuid(), Tenant, Guid.NewGuid(), "synthetic-request", new string('A', 64),
        1, 1, 7, null, Guid.NewGuid(), "orders.view", null, null, null, [], [], 0, null, DateTimeOffset.Parse("2026-10-18T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-10-18T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
}
