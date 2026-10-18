using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessPageTests
{
    [Theory]
    [InlineData("administration", "Tenant administration", "Saved proposal and reconciliation")]
    [InlineData("profile-policy", "Program order policy", "Publish immutable policy")]
    public async Task TenantAdministrationPagesKeepSessionCsrfAndWorkspaceAdmission(string resource, string title, string section)
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        var route = $"/workspace/{factory.Protocol.FirstTenant:D}/{resource}";
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(route)).StatusCode);
        Assert.False((await client.PostAsync(route, JsonContent.Create(new { expectedRevision = 1 }))).IsSuccessStatusCode);
        await LoginAsync(factory, client);
        var html = await client.GetStringAsync(route);
        Assert.Contains($"<h1>{title}</h1>", html, StringComparison.Ordinal); Assert.Contains(section, html, StringComparison.Ordinal);
        Assert.DoesNotContain(Application.Entry.TestSupport.EntryProtocolFixture.AccessToken, html, StringComparison.Ordinal);
        factory.Protocol.DenyWorkspace = true;
        var denied = await client.GetStringAsync(route);
        Assert.Contains("Workspace unavailable", denied, StringComparison.Ordinal); Assert.DoesNotContain(section, denied, StringComparison.Ordinal);
    }
}
