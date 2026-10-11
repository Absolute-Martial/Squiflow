using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.Tenancy;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Model;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OrderApprovalEndpointAuthorizationTests
{
    [Fact]
    public void EndpointClassificationsRequireTheDeclaredIndependentPermissions()
    {
        var submit = CoreApiApplicationAuthorizationContract.RequirementsFor(EndpointAccess.OrderApprovalSubmit);
        Assert.Equal(
            [TenantOrderApprovalPermission.Submit, TenantOrderApprovalPermission.View],
            submit.OfType<TenantOrderApprovalRequirement>().Select(requirement => requirement.Permission));
        Assert.Contains(submit, requirement => requirement is ViewOrdersRequirement);

        var view = CoreApiApplicationAuthorizationContract.RequirementsFor(EndpointAccess.OrderApprovalView);
        Assert.Equal(
            [TenantOrderApprovalPermission.View],
            view.OfType<TenantOrderApprovalRequirement>().Select(requirement => requirement.Permission));

        var decide = CoreApiApplicationAuthorizationContract.RequirementsFor(EndpointAccess.OrderApprovalDecide);
        Assert.Equal(
            [TenantOrderApprovalPermission.Decide, TenantOrderApprovalPermission.View],
            decide.OfType<TenantOrderApprovalRequirement>().Select(requirement => requirement.Permission));
        Assert.Contains(decide, requirement => requirement is ViewOrdersRequirement);

        var recover = CoreApiApplicationAuthorizationContract.RequirementsFor(EndpointAccess.OrderApprovalRecover);
        Assert.Equal(
            [TenantOrderApprovalPermission.Recover],
            recover.OfType<TenantOrderApprovalRequirement>().Select(requirement => requirement.Permission));
    }
}

public sealed class OrderApprovalAuthorizationTests : IAsyncLifetime
{
    private const ushort OpenFgaPort = 8080;
    private readonly IContainer _server = new ContainerBuilder(new DockerImage(repository: "openfga/openfga", tag: "v1.21.0"))
        .WithCommand("run", "--playground-enabled=false")
        .WithPortBinding(OpenFgaPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
            request => request.ForPort(OpenFgaPort).ForPath("/healthz")))
        .Build();

    [Fact]
    public async Task ApprovalPermissionsAreIndependentAndSupportDirectAndTenantRoleGrants()
    {
        var apiUrl = $"http://127.0.0.1:{_server.GetMappedPublicPort(OpenFgaPort)}";
        using var administrativeClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        var storeId = await CreateStoreAsync(administrativeClient);
        var modelJson = await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "OpenFga", "tenant-authorization-model.json"));
        var modelId = await WriteModelAsync(administrativeClient, storeId, modelJson);
        var configuration = OpenFgaAuthorizationConfiguration.From(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:OpenFga:ApiUrl"] = apiUrl,
                ["Authorization:OpenFga:StoreId"] = storeId,
                ["Authorization:OpenFga:AuthorizationModelId"] = modelId,
                ["Authorization:OpenFga:RequestTimeoutSeconds"] = "5",
                ["Authorization:OpenFga:MaximumRetries"] = "1",
                ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
                ["Authorization:OpenFga:CredentialMethod"] = "None",
            })
            .Build());
        using var client = new OpenFgaClient(configuration.ToClientConfiguration());
        ITenantOrderApprovalAuthorization authorization = new OpenFgaTenantAuthorization(
            client, configuration, NullLogger<OpenFgaTenantAuthorization>.Instance);

        (TenantOrderApprovalPermission Permission, string Relation)[] permissions =
        [
            (TenantOrderApprovalPermission.Submit, "order_approval_submitter"),
            (TenantOrderApprovalPermission.View, "order_approval_viewer"),
            (TenantOrderApprovalPermission.Decide, "order_approval_decider"),
            (TenantOrderApprovalPermission.Recover, "order_approval_recoverer"),
        ];

        foreach (var (permission, relation) in permissions)
        {
            var accountId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            Assert.False(await authorization.IsAllowedAsync(accountId, tenantId, permission, CancellationToken.None));

            var user = $"user:{accountId:N}";
            var tenant = $"tenant:{tenantId:N}";
            await WriteTupleAsync(administrativeClient, storeId, modelId, user, "member", tenant);
            await WriteTupleAsync(administrativeClient, storeId, modelId, user, "order_viewer", tenant);
            foreach (var (approvalPermission, _) in permissions)
            {
                Assert.False(await authorization.IsAllowedAsync(
                    accountId, tenantId, approvalPermission, CancellationToken.None));
            }
            await DeleteTupleAsync(administrativeClient, storeId, modelId, user, "member", tenant);

            await WriteTupleAsync(administrativeClient, storeId, modelId, user, relation, tenant);
            Assert.False(await CheckAsync(client, configuration, user, PermissionRelation(permission), tenant));
            await WriteTupleAsync(administrativeClient, storeId, modelId, user, "member", tenant);
            Assert.True(await CheckAsync(client, configuration, user, PermissionRelation(permission), tenant));
            Assert.True(await authorization.IsAllowedAsync(accountId, tenantId, permission, CancellationToken.None));
            foreach (var (otherPermission, _) in permissions.Where(candidate => candidate.Permission != permission))
            {
                Assert.False(await authorization.IsAllowedAsync(
                    accountId, tenantId, otherPermission, CancellationToken.None));
            }

            await DeleteTupleAsync(administrativeClient, storeId, modelId, user, relation, tenant);
            Assert.False(await authorization.IsAllowedAsync(accountId, tenantId, permission, CancellationToken.None));

            var roleId = Guid.NewGuid();
            var role = $"role:{tenantId:N}_{roleId:N}";
            await WriteTupleAsync(administrativeClient, storeId, modelId,
                $"{role}#assignee", relation, tenant);
            await WriteTupleAsync(administrativeClient, storeId, modelId,
                user, "assignee", role);
            Assert.True(await authorization.IsAllowedAsync(accountId, tenantId, permission, CancellationToken.None));
            foreach (var (otherPermission, _) in permissions.Where(candidate => candidate.Permission != permission))
            {
                Assert.False(await authorization.IsAllowedAsync(
                    accountId, tenantId, otherPermission, CancellationToken.None));
            }
            Assert.False(await authorization.IsAllowedAsync(accountId, Guid.NewGuid(), permission, CancellationToken.None));
        }
    }

    private static string PermissionRelation(TenantOrderApprovalPermission permission) => permission switch
    {
        TenantOrderApprovalPermission.Submit => "can_submit_order_approval",
        TenantOrderApprovalPermission.View => "can_view_order_approvals",
        TenantOrderApprovalPermission.Decide => "can_decide_order_approval",
        TenantOrderApprovalPermission.Recover => "can_recover_order_approval",
        _ => throw new ArgumentOutOfRangeException(nameof(permission)),
    };

    private static async Task<bool> CheckAsync(
        OpenFgaClient client,
        OpenFgaAuthorizationConfiguration configuration,
        string user,
        string relation,
        string objectId)
    {
        var result = await client.Check(
            new ClientCheckRequest { User = user, Relation = relation, Object = objectId },
            new ClientCheckOptions
            {
                StoreId = configuration.StoreId,
                AuthorizationModelId = configuration.AuthorizationModelId,
                Consistency = ConsistencyPreference.HIGHERCONSISTENCY,
            },
            CancellationToken.None);
        return result.Allowed is true;
    }

    private static async Task<string> CreateStoreAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/stores", new { name = "order-approval-authorization-tests" });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("OpenFGA did not return a store ID.");
    }

    private static async Task<string> WriteModelAsync(HttpClient client, string storeId, string modelJson)
    {
        using var content = new StringContent(modelJson, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"/stores/{storeId}/authorization-models", content);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("authorization_model_id").GetString()
            ?? throw new InvalidOperationException("OpenFGA did not return an authorization model ID.");
    }

    private static async Task WriteTupleAsync(
        HttpClient client,
        string storeId,
        string modelId,
        string user,
        string relation,
        string objectId)
    {
        using var response = await client.PostAsJsonAsync($"/stores/{storeId}/write", new
        {
            writes = new { tuple_keys = new[] { new { user, relation, @object = objectId } } },
            authorization_model_id = modelId,
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task DeleteTupleAsync(
        HttpClient client,
        string storeId,
        string modelId,
        string user,
        string relation,
        string objectId)
    {
        using var response = await client.PostAsJsonAsync($"/stores/{storeId}/write", new
        {
            deletes = new { tuple_keys = new[] { new { user, relation, @object = objectId } } },
            authorization_model_id = modelId,
        });
        response.EnsureSuccessStatusCode();
    }

    public Task InitializeAsync() => _server.StartAsync();

    public Task DisposeAsync() => _server.DisposeAsync().AsTask();
}
