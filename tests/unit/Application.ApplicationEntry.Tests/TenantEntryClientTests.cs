using System.Net;
using System.Net.Http.Json;
using Application.ApplicationEntry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Application.ApplicationEntry.Tests;

public sealed class TenantEntryClientTests
{
    private static readonly string[] ExpectedBearers = ["Bearer synthetic-user-a", "Bearer synthetic-user-b", "Bearer synthetic-user-a"];
    [Fact]
    public async Task UsesPerRequestBearerCredentialsAndRejectsAnotherTenantInWorkspaceResponse()
    {
        var seen = new List<string>();
        var requested = Guid.NewGuid();
        await using var server = await StartAsync(async context =>
        {
            seen.Add(context.Request.Headers.Authorization.ToString());
            await context.Response.WriteAsJsonAsync(context.Request.Path == "/api/v1/account"
                ? (object)new { accountId = Guid.NewGuid() }
                : new { tenantId = Guid.NewGuid(), displayName = "Another workspace" });
        });
        using var http = ApplicationEntryClient.CreateHttpClient();
        var client = new TenantEntryClient(http); var origin = new Uri(server.Urls.Single());
        Assert.NotNull((await client.AccountAsync(origin, "synthetic-user-a")).Value);
        Assert.NotNull((await client.AccountAsync(origin, "synthetic-user-b")).Value);
        Assert.Equal(TenantEntryFailure.InvalidResponse, (await client.WorkspaceAsync(origin, "synthetic-user-a", requested)).Failure);
        Assert.Equal(ExpectedBearers, seen);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
    }

    [Theory]
    [InlineData(401, "expired", TenantEntryFailure.SignInRequired)]
    [InlineData(403, "account_disabled", TenantEntryFailure.AccountUnavailable)]
    [InlineData(403, "account_not_bound", TenantEntryFailure.AccountUnavailable)]
    [InlineData(403, "tenant_access_denied", TenantEntryFailure.TenantDenied)]
    [InlineData(503, "private-provider-detail", TenantEntryFailure.Unavailable)]
    public async Task MapsSafeKnownFailuresWithoutDisclosingRemoteDetails(int status, string code, TenantEntryFailure expected)
    {
        await using var server = await StartAsync(async context =>
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { code, detail = "private-provider-detail" }));
        });
        using var http = ApplicationEntryClient.CreateHttpClient();
        var result = await new TenantEntryClient(http).AccountAsync(new(server.Urls.Single()), "synthetic-token");
        Assert.Equal(expected, result.Failure); Assert.Null(result.Value);
        Assert.DoesNotContain("private-provider-detail", TenantEntryMessages.For(result.Failure), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"accountId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"items\":null}")]
    [InlineData("null")]
    public async Task MalformedOrEmptyIdentitiesCannotBecomeAccess(string body)
    {
        await using var server = await StartAsync(async context => { context.Response.ContentType = "application/json"; await context.Response.WriteAsync(body); });
        using var http = ApplicationEntryClient.CreateHttpClient();
        Assert.Equal(TenantEntryFailure.InvalidResponse, (await new TenantEntryClient(http).AccountAsync(new(server.Urls.Single()), "synthetic-token")).Failure);
    }

    [Fact]
    public async Task RejectsOversizedPagesAndBodiesWithoutContentLength()
    {
        await using var server = await StartAsync(async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Query.ContainsKey("afterTenantId"))
            {
                await context.Response.StartAsync();
                await context.Response.WriteAsync(new string('x', TenantEntryClient.MaximumResponseBytes + 1));
            }
            else await context.Response.WriteAsJsonAsync(new { items = Enumerable.Range(0, 51).Select(_ => new { tenantId = Guid.NewGuid(), displayName = "Workspace" }).ToArray(), nextTenantId = (Guid?)null });
        });
        using var http = ApplicationEntryClient.CreateHttpClient(); var client = new TenantEntryClient(http); var origin = new Uri(server.Urls.Single());
        Assert.Equal(TenantEntryFailure.InvalidResponse, (await client.TenantsAsync(origin, "synthetic-token")).Failure);
        Assert.Equal(TenantEntryFailure.InvalidResponse, (await client.TenantsAsync(origin, "synthetic-token", Guid.NewGuid())).Failure);
    }

    [Fact]
    public async Task RedirectDoesNotForwardBearerAndCallerCancellationStaysCancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await StartAsync(async context =>
        {
            if (context.Request.Path == "/api/v1/account") { context.Response.Redirect("https://another.example.test/account"); return; }
            entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
        });
        using var http = ApplicationEntryClient.CreateHttpClient(); var client = new TenantEntryClient(http); var origin = new Uri(server.Urls.Single());
        Assert.Equal(TenantEntryFailure.Unavailable, (await client.AccountAsync(origin, "synthetic-token")).Failure);
        using var cancellation = new CancellationTokenSource();
        var pending = client.TenantsAsync(origin, "synthetic-token", cancellationToken: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private static async Task<WebApplication> StartAsync(Func<HttpContext, Task> action)
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build(); app.Run(context => action(context)); await app.StartAsync(); return app;
    }
}
