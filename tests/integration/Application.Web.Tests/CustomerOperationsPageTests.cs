using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessPageTests
{
    [Theory]
    [InlineData("customers/duplicates", "Customer duplicate review", "Find possible matches")]
    [InlineData("customers/imports", "Customer imports", "Prepare a CSV plan")]
    [InlineData("customers/imports/20000000-0000-0000-0000-000000000001", "Customer imports", "Accept reviewed import work")]
    public async Task CustomerOperationsRequireSessionWorkspaceAndRejectForgedHttpWrites(string resource, string title, string section)
    {
        await using var factory = new BusinessFactory(); using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        var route = $"/workspace/{factory.Protocol.FirstTenant:D}/{resource}";
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(route)).StatusCode);
        Assert.False((await client.PostAsync(route, JsonContent.Create(new { decisions = Array.Empty<object>() }))).IsSuccessStatusCode);
        await LoginAsync(factory, client); var html = await client.GetStringAsync(route);
        Assert.Contains($"<h1>{title}</h1>", html, StringComparison.Ordinal); Assert.Contains(section, html, StringComparison.Ordinal);
        Assert.DoesNotContain(Application.Entry.TestSupport.EntryProtocolFixture.AccessToken, html, StringComparison.Ordinal);
        factory.Protocol.DenyWorkspace = true; var denied = await client.GetStringAsync(route);
        Assert.Contains("Workspace unavailable", denied, StringComparison.Ordinal); Assert.DoesNotContain(section, denied, StringComparison.Ordinal);
    }
}
