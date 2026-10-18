using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessPageTests
{
    [Theory]
    [InlineData("quotations")]
    [InlineData("quotations/30000000-0000-0000-0000-000000000001")]
    [InlineData("order-approvals")]
    [InlineData("order-approvals/30000000-0000-0000-0000-000000000001")]
    [InlineData("orders/30000000-0000-0000-0000-000000000001/approval")]
    [InlineData("orders/30000000-0000-0000-0000-000000000001/program-reference")]
    public async Task CommercialContinuationRoutesKeepSessionAndNoHttpMutationBoundaries(string resource)
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        var route = $"/workspace/{factory.Protocol.FirstTenant:D}/{resource}";
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(route)).StatusCode);
        using var forged = await client.PostAsync(route, JsonContent.Create(new { expectedVersion = 1 }));
        Assert.False(forged.IsSuccessStatusCode);
        Assert.Empty(factory.BusinessRequests);
    }

    [Theory]
    [InlineData("quotations", "Create a quotation draft")]
    [InlineData("order-approvals", "Request exact-revision approval")]
    [InlineData("orders/30000000-0000-0000-0000-000000000001/program-reference", "Set or clear supplied reference")]
    public async Task CommercialFormsRemainIndependentAndDeniedWorkspaceClearsThem(string resource, string formHeading)
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        await LoginAsync(factory, client);
        var route = $"/workspace/{factory.Protocol.FirstTenant:D}/{resource}";
        var html = await client.GetStringAsync(route);
        Assert.Contains(formHeading, html, StringComparison.Ordinal);
        Assert.DoesNotContain("test-confidential-secret", html, StringComparison.Ordinal);
        factory.Protocol.DenyWorkspace = true;
        html = await client.GetStringAsync(route);
        Assert.Contains("Workspace unavailable", html, StringComparison.Ordinal);
        Assert.DoesNotContain(formHeading, html, StringComparison.Ordinal);
    }
}
