using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessPageTests
{
    [Theory]
    [InlineData("fulfillment", "Fulfillment", "Record service progress")]
    [InlineData("purchasing", "Purchasing", "Supplier records")]
    public async Task OperationsRoutesUseExistingSessionWorkspaceBoundaryAndSeparateCommandForms(string resource, string title, string command)
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        var route = $"/workspace/{factory.Protocol.FirstTenant:D}/{resource}";
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(route)).StatusCode);
        using var forged = await client.PostAsync(route, JsonContent.Create(new { expectedRevision = 1 }));
        Assert.False(forged.IsSuccessStatusCode);
        await LoginAsync(factory, client);
        var html = await client.GetStringAsync(route);
        Assert.Contains($"<h1>{title}</h1>", html, StringComparison.Ordinal);
        Assert.Contains(command, html, StringComparison.Ordinal);
        Assert.Contains("Each action checks its own current permission", html, StringComparison.Ordinal);
        Assert.Contains("/auth/verify-identity", html, StringComparison.Ordinal);
        Assert.DoesNotContain(Application.Entry.TestSupport.EntryProtocolFixture.AccessToken, html, StringComparison.Ordinal);
        factory.Protocol.DenyWorkspace = true;
        var denied = await client.GetStringAsync(route);
        Assert.Contains("Workspace unavailable", denied, StringComparison.Ordinal);
        Assert.DoesNotContain(command, denied, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fulfillment/orders/20000000-0000-0000-0000-000000000001")]
    [InlineData("purchasing/suppliers/20000000-0000-0000-0000-000000000001")]
    [InlineData("purchasing/requests/20000000-0000-0000-0000-000000000001")]
    public async Task OperationsDetailRoutesRequireExistingSession(string resource)
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync($"/workspace/{factory.Protocol.FirstTenant:D}/{resource}")).StatusCode);
    }
}
