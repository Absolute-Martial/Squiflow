using DotNet.Testcontainers.Images;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.CoreApi.Authorization;
using Application.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OpenFgaTenantAuthorizationTests : IAsyncLifetime
{
    private const ushort OpenFgaPort = 8080;
    private readonly IContainer _server = new ContainerBuilder(new DockerImage(repository: "openfga/openfga", tag: "v1.21.0"))
        .WithCommand("run", "--playground-enabled=false")
        .WithPortBinding(OpenFgaPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
            request => request.ForPort(OpenFgaPort).ForPath("/healthz")))
        .Build();

    [Fact]
    public async Task RealServerRequiresPersistedOrderPermissionsAndUsesPinnedModel()
    {
        var apiUrl = $"http://127.0.0.1:{_server.GetMappedPublicPort(OpenFgaPort)}";
        using var administrativeClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        var storeId = await CreateStoreAsync(administrativeClient);
        var modelJson = await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory,
            "OpenFga",
            "tenant-authorization-model.json"));
        var pinnedModelId = await WriteModelAsync(administrativeClient, storeId, modelJson);

        var configuration = OpenFgaAuthorizationConfiguration.From(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:OpenFga:ApiUrl"] = apiUrl,
                ["Authorization:OpenFga:StoreId"] = storeId,
                ["Authorization:OpenFga:AuthorizationModelId"] = pinnedModelId,
                ["Authorization:OpenFga:RequestTimeoutSeconds"] = "5",
                ["Authorization:OpenFga:MaximumRetries"] = "1",
                ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
                ["Authorization:OpenFga:CredentialMethod"] = "None",
            })
            .Build());
        using var client = new OpenFgaClient(configuration.ToClientConfiguration());
        var authorization = new OpenFgaTenantAuthorization(
            client,
            configuration,
            NullLogger<OpenFgaTenantAuthorization>.Instance);
        ITenantOrderAuthorization orderAuthorization = authorization;
        ITenantCustomerAuthorization customerAuthorization = authorization;
        var orderCreatorAccountId = Guid.NewGuid();
        var orderViewerAccountId = Guid.NewGuid();
        var orderAbandonerAccountId = Guid.NewGuid();
        var orderEditorAccountId = Guid.NewGuid();
        var workspaceViewerAccountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var customerAccountId = Guid.NewGuid();

        Assert.False(await customerAuthorization.CanCreateOrganizationAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanViewOrganizationsAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanCreateProgramAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanViewProgramsAsync(customerAccountId, tenantId, CancellationToken.None));
        await WriteTenantRelationAsync(administrativeClient, storeId, pinnedModelId, customerAccountId, tenantId, "organization_creator");
        Assert.True(await customerAuthorization.CanCreateOrganizationAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanViewOrganizationsAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanCreateProgramAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanViewProgramsAsync(customerAccountId, tenantId, CancellationToken.None));
        await WriteTenantRelationAsync(administrativeClient, storeId, pinnedModelId, customerAccountId, tenantId, "organization_viewer");
        Assert.True(await customerAuthorization.CanViewOrganizationsAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanCreateProgramAsync(customerAccountId, tenantId, CancellationToken.None));
        await WriteTenantRelationAsync(administrativeClient, storeId, pinnedModelId, customerAccountId, tenantId, "program_creator");
        Assert.True(await customerAuthorization.CanCreateProgramAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanViewProgramsAsync(customerAccountId, tenantId, CancellationToken.None));
        await WriteTenantRelationAsync(administrativeClient, storeId, pinnedModelId, customerAccountId, tenantId, "program_viewer");
        Assert.True(await customerAuthorization.CanViewProgramsAsync(customerAccountId, tenantId, CancellationToken.None));

        Assert.False(await authorization.CanViewAsync(
            workspaceViewerAccountId,
            tenantId,
            CancellationToken.None));
        await WriteTenantRelationAsync(
            administrativeClient,
            storeId,
            pinnedModelId,
            workspaceViewerAccountId,
            tenantId,
            "workspace_viewer");
        Assert.True(await authorization.CanViewAsync(
            workspaceViewerAccountId,
            tenantId,
            CancellationToken.None));

        Assert.False(await orderAuthorization.CanCreateAsync(
            orderCreatorAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanViewAsync(
            orderCreatorAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanCreateAsync(
            orderViewerAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanViewAsync(
            orderViewerAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanAbandonAsync(
            orderAbandonerAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanEditAsync(
            orderEditorAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanApplyManualPriceAsync(
            orderCreatorAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanApplyManualPriceAsync(
            orderEditorAccountId, tenantId, CancellationToken.None));

        await WriteTenantRelationAsync(
            administrativeClient,
            storeId,
            pinnedModelId,
            orderCreatorAccountId,
            tenantId,
            "order_creator");
        Assert.True(await orderAuthorization.CanCreateAsync(
            orderCreatorAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanViewAsync(
            orderCreatorAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanAbandonAsync(
            orderCreatorAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanEditAsync(
            orderCreatorAccountId, tenantId, CancellationToken.None));

        await WriteTenantRelationAsync(
            administrativeClient,
            storeId,
            pinnedModelId,
            orderViewerAccountId,
            tenantId,
            "order_viewer");
        Assert.False(await orderAuthorization.CanCreateAsync(
            orderViewerAccountId,
            tenantId,
            CancellationToken.None));
        Assert.True(await orderAuthorization.CanViewAsync(
            orderViewerAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanAbandonAsync(
            orderViewerAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanEditAsync(
            orderViewerAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanAbandonAsync(
            orderCreatorAccountId,
            tenantId,
            CancellationToken.None));
        await WriteTenantRelationAsync(
            administrativeClient,
            storeId,
            pinnedModelId,
            orderAbandonerAccountId,
            tenantId,
            "order_abandoner");
        Assert.True(await orderAuthorization.CanAbandonAsync(
            orderAbandonerAccountId,
            tenantId,
            CancellationToken.None));
        Assert.False(await orderAuthorization.CanViewAsync(
            orderAbandonerAccountId,
            tenantId,
            CancellationToken.None));
        await WriteTenantRelationAsync(
            administrativeClient, storeId, pinnedModelId,
            orderEditorAccountId, tenantId, "order_editor");
        Assert.True(await orderAuthorization.CanEditAsync(
            orderEditorAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanCreateAsync(
            orderEditorAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanViewAsync(
            orderEditorAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanAbandonAsync(
            orderEditorAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanApplyManualPriceAsync(
            orderCreatorAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanApplyManualPriceAsync(
            orderEditorAccountId, tenantId, CancellationToken.None));

        var pricingOnlyAccountId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        Assert.False(await orderAuthorization.CanCreateAsync(
            pricingOnlyAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanEditAsync(
            pricingOnlyAccountId, tenantId, CancellationToken.None));
        await WriteTenantRelationAsync(
            administrativeClient, storeId, pinnedModelId,
            pricingOnlyAccountId, tenantId, "manual_pricer");
        Assert.True(await orderAuthorization.CanApplyManualPriceAsync(
            pricingOnlyAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanApplyManualPriceAsync(
            pricingOnlyAccountId, otherTenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanCreateAsync(
            pricingOnlyAccountId, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanEditAsync(
            pricingOnlyAccountId, tenantId, CancellationToken.None));

        var rawCheckWithoutMembership = await client.Check(
            new ClientCheckRequest
            {
                User = $"user:{pricingOnlyAccountId:N}",
                Relation = "can_apply_manual_price",
                Object = $"tenant:{tenantId:N}",
            },
            new ClientCheckOptions
            {
                StoreId = storeId,
                AuthorizationModelId = pinnedModelId,
                Consistency = ConsistencyPreference.HIGHERCONSISTENCY,
            },
            CancellationToken.None);
        Assert.False(rawCheckWithoutMembership.Allowed is true);

        await DeleteTenantRelationAsync(
            administrativeClient, storeId, pinnedModelId,
            pricingOnlyAccountId, tenantId, "manual_pricer");
        Assert.False(await orderAuthorization.CanApplyManualPriceAsync(
            pricingOnlyAccountId, tenantId, CancellationToken.None));
        await WriteTenantRelationAsync(
            administrativeClient, storeId, pinnedModelId,
            pricingOnlyAccountId, tenantId, "manual_pricer");

        Assert.True(await authorization.CanViewAsync(
            workspaceViewerAccountId,
            tenantId,
            CancellationToken.None));

        var committer = Guid.NewGuid();
        Assert.False(await orderAuthorization.CanCommitAsync(committer, tenantId, CancellationToken.None));
        await WriteTenantRelationAsync(administrativeClient, storeId, pinnedModelId, committer, tenantId, "order_committer");
        Assert.True(await orderAuthorization.CanCommitAsync(committer, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanViewAsync(committer, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanEditAsync(committer, tenantId, CancellationToken.None));
        Assert.False(await orderAuthorization.CanApplyManualPriceAsync(committer, tenantId, CancellationToken.None));
        foreach (var relation in new[] { "can_commit_order", "can_create_individual", "can_view_individuals", "can_change_individual_availability" })
        {
            var user = relation == "can_commit_order" ? committer : customerAccountId;
            var raw = await client.Check(new ClientCheckRequest { User = $"user:{user:N}", Relation = relation, Object = $"tenant:{tenantId:N}" },
                new ClientCheckOptions { StoreId = storeId, AuthorizationModelId = pinnedModelId, Consistency = ConsistencyPreference.HIGHERCONSISTENCY }, CancellationToken.None);
            Assert.False(raw.Allowed is true);
        }
        await DeleteTenantRelationAsync(administrativeClient, storeId, pinnedModelId, committer, tenantId, "order_committer");
        Assert.False(await orderAuthorization.CanCommitAsync(committer, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanCreateIndividualAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanViewIndividualsAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanChangeIndividualAvailabilityAsync(customerAccountId, tenantId, CancellationToken.None));
        await WriteTenantRelationAsync(administrativeClient, storeId, pinnedModelId, customerAccountId, tenantId, "individual_creator");
        Assert.True(await customerAuthorization.CanCreateIndividualAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanViewIndividualsAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.False(await customerAuthorization.CanChangeIndividualAvailabilityAsync(customerAccountId, tenantId, CancellationToken.None));
        await WriteTenantRelationAsync(administrativeClient, storeId, pinnedModelId, customerAccountId, tenantId, "individual_viewer");
        await WriteTenantRelationAsync(administrativeClient, storeId, pinnedModelId, customerAccountId, tenantId, "individual_availability_editor");
        Assert.True(await customerAuthorization.CanViewIndividualsAsync(customerAccountId, tenantId, CancellationToken.None));
        Assert.True(await customerAuthorization.CanChangeIndividualAvailabilityAsync(customerAccountId, tenantId, CancellationToken.None));
        await DeleteTenantRelationAsync(administrativeClient, storeId, pinnedModelId, customerAccountId, tenantId, "individual_availability_editor");
        Assert.False(await customerAuthorization.CanChangeIndividualAvailabilityAsync(customerAccountId, tenantId, CancellationToken.None));

        var newerDenyingModel = modelJson
            .Replace("order_creator", "blocked_order_creator", StringComparison.Ordinal)
            .Replace("order_viewer", "blocked_order_viewer", StringComparison.Ordinal);
        newerDenyingModel = newerDenyingModel.Replace(
            "order_abandoner", "blocked_order_abandoner", StringComparison.Ordinal);
        newerDenyingModel = newerDenyingModel.Replace(
            "order_editor", "blocked_order_editor", StringComparison.Ordinal);
        newerDenyingModel = newerDenyingModel.Replace(
            "manual_pricer", "blocked_manual_pricer", StringComparison.Ordinal);
        _ = await WriteModelAsync(administrativeClient, storeId, newerDenyingModel);

        Assert.True(await orderAuthorization.CanCreateAsync(
            orderCreatorAccountId,
            tenantId,
            CancellationToken.None));
        Assert.True(await orderAuthorization.CanViewAsync(
            orderViewerAccountId,
            tenantId,
            CancellationToken.None));
        Assert.True(await orderAuthorization.CanAbandonAsync(
            orderAbandonerAccountId,
            tenantId,
            CancellationToken.None));
        Assert.True(await orderAuthorization.CanEditAsync(
            orderEditorAccountId, tenantId, CancellationToken.None));
        Assert.True(await orderAuthorization.CanApplyManualPriceAsync(
            pricingOnlyAccountId, tenantId, CancellationToken.None));

        var oldModel = JsonNode.Parse(modelJson)!.AsObject();
        var tenantType = oldModel["type_definitions"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(node => node["type"]!.GetValue<string>() == "tenant");
        Assert.True(tenantType["relations"]!.AsObject().Remove("manual_pricer"));
        Assert.True(tenantType["relations"]!.AsObject().Remove("can_apply_manual_price"));
        Assert.True(tenantType["metadata"]!["relations"]!.AsObject().Remove("manual_pricer"));
        Assert.True(tenantType["metadata"]!["relations"]!.AsObject().Remove("can_apply_manual_price"));
        var olderModelId = await WriteModelAsync(administrativeClient, storeId, oldModel.ToJsonString());
        var olderModelConfiguration = OpenFgaAuthorizationConfiguration.From(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:OpenFga:ApiUrl"] = apiUrl,
                ["Authorization:OpenFga:StoreId"] = storeId,
                ["Authorization:OpenFga:AuthorizationModelId"] = olderModelId,
                ["Authorization:OpenFga:RequestTimeoutSeconds"] = "5",
                ["Authorization:OpenFga:MaximumRetries"] = "1",
                ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
                ["Authorization:OpenFga:CredentialMethod"] = "None",
            })
            .Build());
        using var olderModelClient = new OpenFgaClient(olderModelConfiguration.ToClientConfiguration());
        var olderModelAuthorization = new OpenFgaTenantAuthorization(
            olderModelClient,
            olderModelConfiguration,
            NullLogger<OpenFgaTenantAuthorization>.Instance);
        await Assert.ThrowsAsync<AuthorizationProviderUnavailableException>(() =>
            ((ITenantOrderAuthorization)olderModelAuthorization).CanApplyManualPriceAsync(
                pricingOnlyAccountId, tenantId, CancellationToken.None));
    }


    [Fact]
    public async Task AdministrationProviderReconcilesGrantRevokeAndTenantScopedCustomRoleAgainstPinnedModel()
    {
        var apiUrl = $"http://127.0.0.1:{_server.GetMappedPublicPort(OpenFgaPort)}";
        using var administrativeClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        var storeId = await CreateStoreAsync(administrativeClient);
        var modelJson = await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory,
            "OpenFga",
            "tenant-authorization-model.json"));
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
        var provider = new OpenFgaTenantAuthorizationAdministrationProvider(client, configuration);
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var grant = Proposal(
            TenantAuthorizationProposalKind.GrantPermission,
            tenantId,
            actorId,
            targetAccountId: accountId,
            permissionId: "orders.view");
        Assert.Equal(
            TenantAuthorizationProviderOutcome.Applied,
            (await provider.EnsureAsync(grant, [], CancellationToken.None)).Outcome);

        Assert.True(await CheckRelationAsync(
            client, configuration, $"user:{accountId:N}", "order_viewer", $"tenant:{tenantId:N}"));

        var revoke = grant with
        {
            ProposalId = Guid.NewGuid(),
            Kind = TenantAuthorizationProposalKind.RevokePermission,
        };
        Assert.Equal(
            TenantAuthorizationProviderOutcome.Applied,
            (await provider.EnsureAsync(revoke, [], CancellationToken.None)).Outcome);
        Assert.False(await CheckRelationAsync(
            client, configuration, $"user:{accountId:N}", "order_viewer", $"tenant:{tenantId:N}"));

        var createRole = Proposal(
            TenantAuthorizationProposalKind.CreateRole,
            tenantId,
            actorId,
            roleId: roleId,
            roleName: "Order reader",
            requestedPermissions: ["orders.view"]);
        Assert.Equal(
            TenantAuthorizationProviderOutcome.Applied,
            (await provider.EnsureAsync(createRole, [], CancellationToken.None)).Outcome);
        var assign = Proposal(
            TenantAuthorizationProposalKind.AssignRole,
            tenantId,
            actorId,
            targetAccountId: accountId,
            roleId: roleId);
        Assert.Equal(
            TenantAuthorizationProviderOutcome.Applied,
            (await provider.EnsureAsync(assign, [], CancellationToken.None)).Outcome);
        Assert.True(await CheckRelationAsync(
            client, configuration, $"user:{accountId:N}", "order_viewer", $"tenant:{tenantId:N}"));
        Assert.False(await CheckRelationAsync(
            client, configuration, $"user:{accountId:N}", "order_viewer", $"tenant:{otherTenantId:N}"));

        var assignments = new[]
        {
            new TenantRoleAssignment(
                tenantId,
                roleId,
                accountId,
                TenantRoleAssignmentAvailability.Active,
                1,
                DateTimeOffset.UtcNow,
                null),
        };
        var retire = Proposal(
            TenantAuthorizationProposalKind.RetireRole,
            tenantId,
            actorId,
            roleId: roleId,
            expectedRoleRevision: 1,
            appliedPermissions: ["orders.view"]);
        Assert.Equal(
            TenantAuthorizationProviderOutcome.Applied,
            (await provider.EnsureAsync(retire, assignments, CancellationToken.None)).Outcome);
        Assert.False(await CheckRelationAsync(
            client, configuration, $"user:{accountId:N}", "order_viewer", $"tenant:{tenantId:N}"));

        var oldModel = JsonNode.Parse(modelJson)!.AsObject();
        var tenantType = oldModel["type_definitions"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(node => node["type"]!.GetValue<string>() == "tenant");
        Assert.True(tenantType["relations"]!.AsObject().Remove("order_viewer"));
        Assert.True(tenantType["relations"]!.AsObject().Remove("can_view_orders"));
        Assert.True(tenantType["metadata"]!["relations"]!.AsObject().Remove("order_viewer"));
        Assert.True(tenantType["metadata"]!["relations"]!.AsObject().Remove("can_view_orders"));
        var oldModelId = await WriteModelAsync(administrativeClient, storeId, oldModel.ToJsonString());
        var oldConfiguration = OpenFgaAuthorizationConfiguration.From(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:OpenFga:ApiUrl"] = apiUrl,
                ["Authorization:OpenFga:StoreId"] = storeId,
                ["Authorization:OpenFga:AuthorizationModelId"] = oldModelId,
                ["Authorization:OpenFga:RequestTimeoutSeconds"] = "5",
                ["Authorization:OpenFga:MaximumRetries"] = "1",
                ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
                ["Authorization:OpenFga:CredentialMethod"] = "None",
            })
            .Build());
        using var oldClient = new OpenFgaClient(oldConfiguration.ToClientConfiguration());
        var oldProvider = new OpenFgaTenantAuthorizationAdministrationProvider(oldClient, oldConfiguration);
        Assert.Equal(
            TenantAuthorizationProviderOutcome.Failed,
            (await oldProvider.EnsureAsync(grant with { ProposalId = Guid.NewGuid() }, [], CancellationToken.None)).Outcome);
    }

    private static async Task<bool> CheckRelationAsync(
        OpenFgaClient client,
        OpenFgaAuthorizationConfiguration configuration,
        string user,
        string relation,
        string objectId)
    {
        var result = await client.Check(
            new ClientCheckRequest
            {
                User = user,
                Relation = relation,
                Object = objectId,
            },
            new ClientCheckOptions
            {
                StoreId = configuration.StoreId,
                AuthorizationModelId = configuration.AuthorizationModelId,
                Consistency = ConsistencyPreference.HIGHERCONSISTENCY,
            },
            CancellationToken.None);
        return result.Allowed is true;
    }

    private static TenantAuthorizationProposal Proposal(
        TenantAuthorizationProposalKind kind,
        Guid tenantId,
        Guid actorAccountId,
        Guid? targetAccountId = null,
        string? permissionId = null,
        Guid? roleId = null,
        int? expectedRoleRevision = null,
        string? roleName = null,
        string[]? requestedPermissions = null,
        string[]? appliedPermissions = null) =>
        new(
            Guid.NewGuid(),
            tenantId,
            actorAccountId,
            "test-idempotency",
            new string('A', 64),
            kind,
            TenantAuthorizationProposalStatus.Pending,
            1,
            null,
            targetAccountId,
            permissionId,
            roleId,
            expectedRoleRevision,
            roleName,
            requestedPermissions ?? [],
            appliedPermissions ?? [],
            0,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    public Task InitializeAsync() => _server.StartAsync();

    public Task DisposeAsync() => _server.DisposeAsync().AsTask();

    private static async Task<string> CreateStoreAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/stores", new { name = "application-authorization-tests" });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("OpenFGA did not return a store ID.");
    }

    private static async Task<string> WriteModelAsync(
        HttpClient client,
        string storeId,
        string modelJson)
    {
        using var content = new StringContent(modelJson, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"/stores/{storeId}/authorization-models", content);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("authorization_model_id").GetString()
            ?? throw new InvalidOperationException("OpenFGA did not return an authorization model ID.");
    }

    private static async Task WriteTenantRelationAsync(
        HttpClient client,
        string storeId,
        string authorizationModelId,
        Guid accountId,
        Guid tenantId,
        string relation)
    {
        using var response = await client.PostAsJsonAsync(
            $"/stores/{storeId}/write",
            new
            {
                writes = new
                {
                    tuple_keys = new[]
                    {
                        new
                        {
                            user = $"user:{accountId:N}",
                            relation,
                            @object = $"tenant:{tenantId:N}",
                        },
                    },
                },
                authorization_model_id = authorizationModelId,
            });
        response.EnsureSuccessStatusCode();
    }

    private static async Task DeleteTenantRelationAsync(
        HttpClient client,
        string storeId,
        string authorizationModelId,
        Guid accountId,
        Guid tenantId,
        string relation)
    {
        using var response = await client.PostAsJsonAsync(
            $"/stores/{storeId}/write",
            new
            {
                deletes = new
                {
                    tuple_keys = new[]
                    {
                        new
                        {
                            user = $"user:{accountId:N}",
                            relation,
                            @object = $"tenant:{tenantId:N}",
                        },
                    },
                },
                authorization_model_id = authorizationModelId,
            });
        response.EnsureSuccessStatusCode();
    }
}
