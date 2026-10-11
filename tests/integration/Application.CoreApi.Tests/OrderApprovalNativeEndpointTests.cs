extern alias DatabaseMigrator;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using DatabaseMigrator::Application.DatabaseMigrator;
using Application.Orders;
using Application.Orders.Postgres;
using Application.Profiles;
using Application.Profiles.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OpenFga.Sdk.Client;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.CoreApi.Tests;

[Collection(OrderProgramReferenceEndpointGroup.Name)]
public sealed class OrderApprovalNativeEndpointTests
{
    [Fact]
    public async Task ActualHttpApprovalAndAdmissionUseCurrentCentralMembershipPinnedPolicyAndIndependentOpenFgaRights()
    {
        await using var database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithDatabase("approval_http_tests").WithUsername("postgres").WithPassword("local-approval-http-only").Build();
        await database.StartAsync();
        await new MigrationRunner(database.GetConnectionString(), 0x73121109).ApplyAsync(TimeSpan.FromSeconds(10), default);
        await using var server = new ContainerBuilder(new DockerImage(repository: "openfga/openfga", tag: "v1.21.0"))
            .WithCommand("run", "--playground-enabled=false").WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/healthz"))).Build();
        await server.StartAsync();
        using var admin = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.GetMappedPublicPort(8080)}") };
        var storeId = await PostIdAsync(admin, "/stores", new { name = "approval-native-http" }, "id");
        var model = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "OpenFga", "tenant-authorization-model.json"));
        using var modelResponse = await admin.PostAsync($"/stores/{storeId}/authorization-models", new StringContent(model, Encoding.UTF8, "application/json"));
        modelResponse.EnsureSuccessStatusCode();
        using var modelDocument = JsonDocument.Parse(await modelResponse.Content.ReadAsStringAsync());
        var modelId = modelDocument.RootElement.GetProperty("authorization_model_id").GetString()!;
        var configuration = OpenFgaAuthorizationConfiguration.From(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authorization:OpenFga:ApiUrl"] = admin.BaseAddress.AbsoluteUri,
            ["Authorization:OpenFga:StoreId"] = storeId,
            ["Authorization:OpenFga:AuthorizationModelId"] = modelId,
            ["Authorization:OpenFga:RequestTimeoutSeconds"] = "5",
            ["Authorization:OpenFga:CredentialMethod"] = "None",
            ["Authorization:OpenFga:MaximumRetries"] = "0",
            ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
        }).Build());
        using var fga = new OpenFgaClient(configuration.ToClientConfiguration());
        var authorization = new OpenFgaTenantAuthorization(fga, configuration, NullLogger<OpenFgaTenantAuthorization>.Instance);
        var tenant = Guid.NewGuid(); var requester = Guid.NewGuid(); var approver = Guid.NewGuid();
        var organization = Guid.NewGuid(); var program = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = """
                INSERT INTO identity_access.accounts(id,availability,created_at) VALUES(@requester,1,now()),(@approver,1,now());
                INSERT INTO tenancy.tenants(id,display_name,availability,created_at) VALUES(@tenant,'Approval HTTP tenant',1,now());
                INSERT INTO tenancy.memberships(tenant_id,account_id,availability,created_at,revision,is_initial_owner,activated_at)
                    VALUES(@tenant,@requester,1,now(),1,true,now()),(@tenant,@approver,1,now(),1,false,now());
                INSERT INTO customers.organizations(tenant_id,id,display_name,created_by_account_id,created_at) VALUES(@tenant,@organization,'Approval customer',@requester,now());
                INSERT INTO customers.programs(tenant_id,id,organization_id,display_name,created_by_account_id,created_at) VALUES(@tenant,@program,@organization,'Approval program',@requester,now());
                CREATE ROLE approval_http_runtime LOGIN PASSWORD 'local-approval-runtime-only';
                SELECT set_config('app.provision_runtime_role','approval_http_runtime',false);
                """;
            setup.Parameters.AddWithValue("requester", requester); setup.Parameters.AddWithValue("approver", approver);
            setup.Parameters.AddWithValue("tenant", tenant); setup.Parameters.AddWithValue("organization", organization); setup.Parameters.AddWithValue("program", program);
            await setup.ExecuteNonQueryAsync();
        }
        await using (var provision = connection.CreateCommand())
        {
            provision.CommandText = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "deploy/database/grant-core-api-runtime.sql"));
            await provision.ExecuteNonQueryAsync();
        }
        await using (var source = NpgsqlDataSource.Create(database.GetConnectionString()))
        {
            await using var tenantDatabase = TenancyPostgresMigrations.CreateContext(database.GetConnectionString());
            var context = await new ResolveTenantContext(new PostgresTenantMembershipDirectory(tenantDatabase)).ExecuteAsync(requester, tenant, default);
            var profiles = new PostgresProfileStore(source, TimeProvider.System);
            Assert.Equal(ProfileCommandStatus.Edited, (await profiles.EditPolicyAsync(context!, new(0, false, true), 1, "edit", default)).Status);
            var policy = await profiles.PublishPolicyAsync(context!, new(1), 1, "publish-policy", default);
            var profile = await profiles.PublishProfileAsync(new(Guid.NewGuid(), Guid.NewGuid()), new(tenant, 0, policy.PublishedPolicy!.PolicyRevisionId, 1), "publish-profile", default);
            Assert.Equal(ProfileCommandStatus.Activated, (await profiles.ActivateProfileAsync(new(Guid.NewGuid(), Guid.NewGuid()),
                new(tenant, profile.Authority!.Revision, profile.Profile!.ProfileId), "activate-profile", default)).Status);
        }
        await GrantAsync(admin, storeId, modelId, tenant, requester, ["order_creator", "manual_pricer", "order_viewer", "order_committer", "order_approval_submitter", "order_approval_viewer", "order_approval_recoverer"]);
        await GrantAsync(admin, storeId, modelId, tenant, approver, ["order_viewer", "order_approval_decider", "order_approval_viewer"]);
        var runtime = new NpgsqlConnectionStringBuilder(database.GetConnectionString()) { Username = "approval_http_runtime", Password = "local-approval-runtime-only" };
        using var identity = new WhiteLabelApiFactory(); identity.Bind(requester.ToString("D"), requester); identity.Bind(approver.ToString("D"), approver);
        using var host = identity.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("HighRiskActions:TenantWebClientId", "tenant-web-client");
            builder.UseSetting("HighRiskActions:RequiredAcr", "urn:application:assurance:high-risk");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>(); services.AddSingleton(_ => NpgsqlDataSource.Create(runtime.ConnectionString));
                services.RemoveAll<ITenantMembershipDirectory>(); services.AddScoped<ITenantMembershipDirectory, PostgresTenantMembershipDirectory>();
                services.RemoveAll<ITenantOrderAuthorization>(); services.AddSingleton<ITenantOrderAuthorization>(authorization);
                services.RemoveAll<ITenantOrderApprovalAuthorization>(); services.AddSingleton<ITenantOrderApprovalAuthorization>(authorization);
                services.RemoveAll<Application.Customers.ICustomerStore>();
                services.AddScoped<Application.Customers.ICustomerStore, Application.Customers.Postgres.PostgresCustomerStore>();
                services.RemoveAll<IOrderDraftStore>(); services.AddScoped<IOrderDraftStore>(provider => provider.GetRequiredService<PostgresOrderDraftStore>());
            });
        });
        using var client = host.CreateClient(); var requesterToken = identity.CreateToken(requester.ToString("D")); var approverToken = identity.CreateToken(approver.ToString("D"));
        var recentOwnerToken = identity.CreateToken(requester.ToString("D"), extraClaims:
        [
            new("azp", "tenant-web-client"),
            new("acr", "urn:application:assurance:high-risk"),
            new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ]);
        var orders = $"/api/v1/tenants/{tenant:D}/orders";
        var created = await SendAsync(client, HttpMethod.Post, orders, requesterToken, "create", new
        {
            summary = "Native approved Order",
            currencyCode = "USD",
            lines = new[] { new { description = "Approval item", quantity = 2, unitCode = "piece", unitPrice = 12.50m } },
            customerContext = new { organizationId = organization, programId = program },
        }, HttpStatusCode.Created);
        var orderId = created.GetProperty("orderId").GetGuid();
        var denied = await SendAsync(client, HttpMethod.Post, orders + $"/{orderId:D}/commit", requesterToken, "no-approval", new { expectedRevision = 1 }, HttpStatusCode.Conflict);
        Assert.Equal("order_approval_required", denied.GetProperty("code").GetString());
        var waiting = await SendAsync(client, HttpMethod.Get, orders + $"/{orderId:D}/actions", requesterToken, null, null, HttpStatusCode.OK);
        Assert.Equal("order_approval_required", waiting.GetProperty("actions").EnumerateArray().Single(action => action.GetProperty("action").GetString() == "commit").GetProperty("unavailabilityCode").GetString());
        var submitted = await SendAsync(client, HttpMethod.Post, orders + $"/{orderId:D}/approvals", requesterToken, "submit", new { expectedOrderRevision = 1, assignedAccountId = approver }, HttpStatusCode.OK);
        var requestId = submitted.GetProperty("requestId").GetGuid(); var requestPath = $"/api/v1/tenants/{tenant:D}/order-approvals/{requestId:D}";
        var standardRecoveryDenied = await SendAsync(client, HttpMethod.Post, requestPath + "/reassign", requesterToken, "standard-recovery",
            new { expectedRequestRevision = 1, assignedAccountId = approver, reason = "Recovery needs trusted step-up" }, HttpStatusCode.Forbidden);
        Assert.Equal("high_risk_client_required", standardRecoveryDenied.GetProperty("code").GetString());
        var selfReassignmentDenied = await SendAsync(client, HttpMethod.Post, requestPath + "/reassign", recentOwnerToken, "self-reassignment",
            new { expectedRequestRevision = 1, assignedAccountId = requester, reason = "Cannot assign requester" }, HttpStatusCode.Conflict);
        Assert.Equal("approval_self_approval_forbidden", selfReassignmentDenied.GetProperty("code").GetString());
        var reassigned = await SendAsync(client, HttpMethod.Post, requestPath + "/reassign", recentOwnerToken, "owner-reassignment",
            new { expectedRequestRevision = 1, assignedAccountId = approver, reason = "Confirming active approver" }, HttpStatusCode.OK);
        Assert.Equal(2, reassigned.GetProperty("requestRevision").GetInt64());
        var approved = await SendAsync(client, HttpMethod.Post, requestPath + "/approve", approverToken, "approve", new { expectedRequestRevision = 2 }, HttpStatusCode.OK);
        Assert.Equal(3, approved.GetProperty("requestRevision").GetInt64());
        var ready = await SendAsync(client, HttpMethod.Get, orders + $"/{orderId:D}/actions", requesterToken, null, null, HttpStatusCode.OK);
        Assert.True(ready.GetProperty("actions").EnumerateArray().Single(action => action.GetProperty("action").GetString() == "commit").GetProperty("available").GetBoolean());
        await SendAsync(client, HttpMethod.Post, orders + $"/{orderId:D}/commit", requesterToken, "commit", new { expectedRevision = 1 }, HttpStatusCode.OK);
        var read = await SendAsync(client, HttpMethod.Get, orders + $"/{orderId:D}", requesterToken, null, null, HttpStatusCode.OK);
        Assert.Equal(25m, read.GetProperty("total").GetDecimal());
        await using (var suspend = connection.CreateCommand())
        {
            suspend.CommandText = "UPDATE tenancy.memberships SET availability=2,revision=revision+1,suspended_at=now() WHERE tenant_id=@tenant AND account_id=@actor";
            suspend.Parameters.AddWithValue("tenant", tenant); suspend.Parameters.AddWithValue("actor", approver); await suspend.ExecuteNonQueryAsync();
        }
        await SendAsync(client, HttpMethod.Post, requestPath + "/approve", approverToken, "approve", new { expectedRequestRevision = 2 }, HttpStatusCode.Forbidden);
        await SendAsync(client, HttpMethod.Post, orders + $"/{orderId:D}/commit", requesterToken, "commit", new { expectedRevision = 1 }, HttpStatusCode.OK);
    }

    private static async Task<string> PostIdAsync(HttpClient client, string path, object body, string property)
    {
        using var response = await client.PostAsJsonAsync(path, body); response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.GetProperty(property).GetString()!;
    }

    private static async Task GrantAsync(HttpClient client, string storeId, string modelId, Guid tenant, Guid actor, string[] relations)
    {
        using var response = await client.PostAsJsonAsync($"/stores/{storeId}/write", new
        {
            writes = new { tuple_keys = relations.Select(relation => new { user = $"user:{actor:N}", relation, @object = $"tenant:{tenant:N}" }).ToArray() },
            authorization_model_id = modelId,
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> SendAsync(HttpClient client, HttpMethod method, string path, string token, string? key, object? body, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync(); Assert.Equal(expected, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore); using var document = JsonDocument.Parse(text); return document.RootElement.Clone();
    }


    private static string RepositoryRoot()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "Application.slnx"))) return path.FullName;
        throw new InvalidOperationException("Repository source root was not found.");
    }
}
