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

public sealed class OrderFulfillmentEndpointAuthorizationTests
{
    [Fact]
    public void EndpointClassificationsRequireTheirIndependentFulfillmentPermissions()
    {
        AssertRequirement(EndpointAccess.OrderFulfillmentView, TenantOrderFulfillmentPermission.View);
        AssertRequirement(EndpointAccess.OrderFulfillmentWorkRecord, TenantOrderFulfillmentPermission.RecordWork);
        AssertRequirement(EndpointAccess.OrderFulfillmentHandoffRecord, TenantOrderFulfillmentPermission.RecordHandoff);
        AssertRequirement(EndpointAccess.OrderFulfillmentCorrect, TenantOrderFulfillmentPermission.Correct);
    }

    private static void AssertRequirement(
        EndpointAccess access,
        TenantOrderFulfillmentPermission expected)
    {
        var requirements = CoreApiApplicationAuthorizationContract.RequirementsFor(access);
        Assert.Equal(
            [expected],
            requirements.OfType<TenantOrderFulfillmentRequirement>().Select(requirement => requirement.Permission));
    }
}

public sealed class OrderFulfillmentAuthorizationTests : IAsyncLifetime
{
    private const ushort OpenFgaPort = 8080;
    private readonly IContainer _server = new ContainerBuilder(new DockerImage(repository: "openfga/openfga", tag: "v1.21.0"))
        .WithCommand("run", "--playground-enabled=false")
        .WithPortBinding(OpenFgaPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
            request => request.ForPort(OpenFgaPort).ForPath("/healthz")))
        .Build();

    [Fact]
    public async Task FulfillmentPermissionsAreIndependentAndSupportDirectAndTenantRoleGrants()
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
        ITenantOrderFulfillmentAuthorization authorization = new OpenFgaTenantAuthorization(
            client, configuration, NullLogger<OpenFgaTenantAuthorization>.Instance);

        (TenantOrderFulfillmentPermission Permission, string Relation)[] permissions =
        [
            (TenantOrderFulfillmentPermission.View, "order_fulfillment_viewer"),
            (TenantOrderFulfillmentPermission.RecordWork, "work_recorder"),
            (TenantOrderFulfillmentPermission.RecordHandoff, "handoff_recorder"),
            (TenantOrderFulfillmentPermission.Correct, "corrector"),
        ];

        foreach (var (permission, relation) in permissions)
        {
            var accountId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var user = $"user:{accountId:N}";
            var tenant = $"tenant:{tenantId:N}";
            Assert.False(await authorization.IsAllowedAsync(accountId, tenantId, permission, CancellationToken.None));

            await WriteTupleAsync(administrativeClient, storeId, modelId, user, relation, tenant);
            Assert.False(await CheckAsync(client, configuration, user, PermissionRelation(permission), tenant));
            // The host adapter supplies the current-member tuple because its callers first
            // resolve current central membership. Check the pinned FGA model without that
            // contextual tuple to prove persisted member intersection independently.
            await WriteTupleAsync(administrativeClient, storeId, modelId, user, "member", tenant);
            Assert.True(await CheckAsync(client, configuration, user, PermissionRelation(permission), tenant));
            Assert.True(await authorization.IsAllowedAsync(accountId, tenantId, permission, CancellationToken.None));
            foreach (var (otherPermission, _) in permissions.Where(candidate => candidate.Permission != permission))
            {
                Assert.False(await authorization.IsAllowedAsync(accountId, tenantId, otherPermission, CancellationToken.None));
            }
            Assert.False(await authorization.IsAllowedAsync(accountId, Guid.NewGuid(), permission, CancellationToken.None));

            await DeleteTupleAsync(administrativeClient, storeId, modelId, user, relation, tenant);
            Assert.False(await authorization.IsAllowedAsync(accountId, tenantId, permission, CancellationToken.None));
            await DeleteTupleAsync(administrativeClient, storeId, modelId, user, "member", tenant);

            var roleId = Guid.NewGuid();
            var role = $"role:{tenantId:N}_{roleId:N}";
            await WriteTupleAsync(administrativeClient, storeId, modelId,
                $"{role}#assignee", relation, tenant);
            await WriteTupleAsync(administrativeClient, storeId, modelId,
                user, "assignee", role);
            Assert.False(await CheckAsync(client, configuration, user, PermissionRelation(permission), tenant));
            Assert.True(await authorization.IsAllowedAsync(accountId, tenantId, permission, CancellationToken.None));
            await WriteTupleAsync(administrativeClient, storeId, modelId, user, "member", tenant);
            Assert.True(await CheckAsync(client, configuration, user, PermissionRelation(permission), tenant));
            Assert.True(await authorization.IsAllowedAsync(accountId, tenantId, permission, CancellationToken.None));
            foreach (var (otherPermission, _) in permissions.Where(candidate => candidate.Permission != permission))
            {
                Assert.False(await authorization.IsAllowedAsync(accountId, tenantId, otherPermission, CancellationToken.None));
            }
        }
    }

    private static string PermissionRelation(TenantOrderFulfillmentPermission permission) => permission switch
    {
        TenantOrderFulfillmentPermission.View => "can_view_order_fulfillment",
        TenantOrderFulfillmentPermission.RecordWork => "can_record_order_work",
        TenantOrderFulfillmentPermission.RecordHandoff => "can_record_order_handoff",
        TenantOrderFulfillmentPermission.Correct => "can_correct_order_fulfillment",
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
        using var response = await client.PostAsJsonAsync("/stores", new { name = "order-fulfillment-authorization-tests" });
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

    private static Task WriteTupleAsync(
        HttpClient client,
        string storeId,
        string modelId,
        string user,
        string relation,
        string objectId) => WriteTupleAsync(client, storeId, modelId, user, relation, objectId, delete: false);

    private static Task DeleteTupleAsync(
        HttpClient client,
        string storeId,
        string modelId,
        string user,
        string relation,
        string objectId) => WriteTupleAsync(client, storeId, modelId, user, relation, objectId, delete: true);

    private static async Task WriteTupleAsync(
        HttpClient client,
        string storeId,
        string modelId,
        string user,
        string relation,
        string objectId,
        bool delete)
    {
        using var response = delete
            ? await client.PostAsJsonAsync($"/stores/{storeId}/write", new
            {
                deletes = new { tuple_keys = new[] { new { user, relation, @object = objectId } } },
                authorization_model_id = modelId,
            })
            : await client.PostAsJsonAsync($"/stores/{storeId}/write", new
            {
                writes = new { tuple_keys = new[] { new { user, relation, @object = objectId } } },
                authorization_model_id = modelId,
            });
        response.EnsureSuccessStatusCode();
    }

    public Task InitializeAsync() => _server.StartAsync();

    public Task DisposeAsync() => _server.DisposeAsync().AsTask();
}
