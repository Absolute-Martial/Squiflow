using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.IdentityAccess;
using Application.IdentityAccess.Postgres;
using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class AccountLifecycleAdmissionTests(WhiteLabelApiFactory baseline) : IClassFixture<WhiteLabelApiFactory>
{
    [Fact]
    public async Task ExistingTokenCannotBypassSuspensionAndReactivationKeepsOnlyCurrentMemberships()
    {
        await using var database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithDatabase("account_lifecycle_admission").WithUsername("postgres").WithPassword("test-only").Build();
        await database.StartAsync();
        await using (var context = IdentityAccessPostgresMigrations.CreateContext(database.GetConnectionString()))
            await context.Database.MigrateAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        var actor = IdentityAdministrationActor.Create(Guid.NewGuid(), Guid.NewGuid());
        var subject = Guid.NewGuid().ToString("N");
        var account = await new PostgresAccountOnboardingStore(source).OnboardAsync(actor,
            AccountOnboardingIntent.Create(ExternalIdentity.Create(WhiteLabelApiFactory.Authority, subject), "onboard"),
            DateTimeOffset.UtcNow, CancellationToken.None);
        var accountId = account.Account!.AccountId;
        var activeTenant = Guid.NewGuid();
        var removedTenant = Guid.NewGuid();
        baseline.AddTenantMembership(accountId, activeTenant, "Active business");
        baseline.AddTenantMembership(accountId, removedTenant, "Removed business");
        using var host = baseline.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(source);
            services.RemoveAll<IAccountBindingDirectory>();
            services.AddScoped<IAccountBindingDirectory, PostgresAccountBindingDirectory>();
        }));
        using var client = host.CreateClient();
        var token = baseline.CreateToken(subject);
        using (var before = await SendAsync("/api/v1/account")) Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var lifecycle = new PostgresAccountLifecycleStore(source);
        var suspend = AccountLifecycleIntent.Create(AccountLifecycleOperation.Suspend, accountId, 1, "Security request", "suspend");
        Assert.Equal(AccountLifecycleStatus.Changed, (await lifecycle.ExecuteAsync(actor, suspend, DateTimeOffset.UtcNow, CancellationToken.None)).Status);
        foreach (var path in new[] { "/api/v1/account", "/api/v1/account/tenants", "/api/v1/account/tenants/page" })
        {
            using var denied = await SendAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal("no-store", denied.Headers.CacheControl?.ToString());
            using var body = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
            Assert.Equal("account_disabled", body.RootElement.GetProperty("code").GetString());
        }
        baseline.RemoveTenantMembership(accountId, removedTenant);
        var reactivate = AccountLifecycleIntent.Create(AccountLifecycleOperation.Reactivate, accountId, 2, "Security restored", "reactivate");
        await lifecycle.ExecuteAsync(actor, reactivate, DateTimeOffset.UtcNow, CancellationToken.None);
        using var restored = await SendAsync("/api/v1/account/tenants");
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        using var memberships = JsonDocument.Parse(await restored.Content.ReadAsStringAsync());
        Assert.Equal(activeTenant, Assert.Single(memberships.RootElement.EnumerateArray()).GetProperty("tenantId").GetGuid());

        async Task<HttpResponseMessage> SendAsync(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await client.SendAsync(request);
        }
    }
}
