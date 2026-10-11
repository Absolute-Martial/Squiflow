extern alias DatabaseMigrator;

using System.Net;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Catalog;
using Application.Catalog.Postgres;
using Application.CoreApi.Authorization;
using Application.Customers;
using Application.Customers.Postgres;
using Application.Orders;
using Application.Orders.Postgres;
using Application.Pricing;
using Application.Profiles;
using Application.Profiles.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using DatabaseMigrator::Application.DatabaseMigrator;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OpenFga.Sdk.Client;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.CoreApi.Tests;

[Collection(OrderProgramReferenceEndpointGroup.Name)]
public sealed class FulfillmentNativeEndpointTests
{
    private readonly ConcurrentQueue<Exception> _unexpectedFailures = new();
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NativeHttpJourneyRetainsCommittedFactsAndProtectsFulfillmentHistoryAcrossRestart(int maximumPoolSize)
    {
        await using var database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithDatabase("fulfillment_http_tests").WithUsername("postgres").WithPassword("local-fulfillment-http-only").Build();
        await database.StartAsync();
        await new MigrationRunner(database.GetConnectionString(), 0x73121109).ApplyAsync(TimeSpan.FromSeconds(10), default);
        await using var server = new ContainerBuilder(new DockerImage(repository: "openfga/openfga", tag: "v1.21.0"))
            .WithCommand("run", "--playground-enabled=false").WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/healthz"))).Build();
        await server.StartAsync();
        using var fgaAdmin = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.GetMappedPublicPort(8080)}") };
        var storeId = await PostIdAsync(fgaAdmin, "/stores", new { name = "fulfillment-native-http" }, "id");
        var model = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "OpenFga", "tenant-authorization-model.json"));
        using var modelResponse = await fgaAdmin.PostAsync($"/stores/{storeId}/authorization-models", new StringContent(model, Encoding.UTF8, "application/json"));
        modelResponse.EnsureSuccessStatusCode();
        using var modelDocument = JsonDocument.Parse(await modelResponse.Content.ReadAsStringAsync());
        var modelId = modelDocument.RootElement.GetProperty("authorization_model_id").GetString()!;
        var fgaConfiguration = OpenFgaAuthorizationConfiguration.From(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authorization:OpenFga:ApiUrl"] = fgaAdmin.BaseAddress.AbsoluteUri,
            ["Authorization:OpenFga:StoreId"] = storeId,
            ["Authorization:OpenFga:AuthorizationModelId"] = modelId,
            ["Authorization:OpenFga:RequestTimeoutSeconds"] = "5",
            ["Authorization:OpenFga:CredentialMethod"] = "None",
            ["Authorization:OpenFga:MaximumRetries"] = "0",
            ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
        }).Build());
        using var fga = new OpenFgaClient(fgaConfiguration.ToClientConfiguration());
        var authorization = new OpenFgaTenantAuthorization(fga, fgaConfiguration, NullLogger<OpenFgaTenantAuthorization>.Instance);

        var tenant = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var operatorAccount = Guid.NewGuid();
        var organization = Guid.NewGuid();
        var program = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = """
                INSERT INTO identity_access.accounts(id,availability,created_at) VALUES(@owner,1,now()),(@operator,1,now());
                INSERT INTO tenancy.tenants(id,display_name,availability,created_at) VALUES(@tenant,'Fulfillment HTTP tenant',1,now());
                INSERT INTO tenancy.memberships(tenant_id,account_id,availability,created_at,revision,is_initial_owner,activated_at)
                    VALUES(@tenant,@owner,1,now(),1,true,now()),(@tenant,@operator,1,now(),1,false,now());
                INSERT INTO customers.organizations(tenant_id,id,display_name,created_by_account_id,created_at)
                    VALUES(@tenant,@organization,'Fulfillment customer',@owner,now());
                INSERT INTO customers.programs(tenant_id,id,organization_id,display_name,created_by_account_id,created_at)
                    VALUES(@tenant,@program,@organization,'Fulfillment program',@owner,now());
                CREATE ROLE fulfillment_http_runtime LOGIN PASSWORD 'local-fulfillment-runtime-only';
                SELECT set_config('app.provision_runtime_role','fulfillment_http_runtime',false);
                """;
            setup.Parameters.AddWithValue("owner", owner);
            setup.Parameters.AddWithValue("operator", operatorAccount);
            setup.Parameters.AddWithValue("tenant", tenant);
            setup.Parameters.AddWithValue("organization", organization);
            setup.Parameters.AddWithValue("program", program);
            await setup.ExecuteNonQueryAsync();
        }
        await using (var grant = connection.CreateCommand())
        {
            grant.CommandText = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "deploy/database/grant-core-api-runtime.sql"));
            await grant.ExecuteNonQueryAsync();
        }

        await using (var source = NpgsqlDataSource.Create(database.GetConnectionString()))
        {
            await using var tenantDatabase = TenancyPostgresMigrations.CreateContext(database.GetConnectionString());
            var context = await new ResolveTenantContext(new PostgresTenantMembershipDirectory(tenantDatabase))
                .ExecuteAsync(owner, tenant, default) ?? throw new InvalidOperationException("The seeded Owner membership was not resolved.");
            var profiles = new PostgresProfileStore(source, TimeProvider.System);
            Assert.Equal(ProfileCommandStatus.Edited,
                (await profiles.EditPolicyAsync(context, new(0, false, false), 1, "edit", default)).Status);
            var policy = await profiles.PublishPolicyAsync(context, new(1), 1, "publish-policy", default);
            var profile = await profiles.PublishProfileAsync(new(Guid.NewGuid(), Guid.NewGuid()),
                new(tenant, 0, policy.PublishedPolicy!.PolicyRevisionId, 1), "publish-profile", default);
            Assert.Equal(ProfileCommandStatus.Activated,
                (await profiles.ActivateProfileAsync(new(Guid.NewGuid(), Guid.NewGuid()),
                    new(tenant, profile.Authority!.Revision, profile.Profile!.ProfileId), "activate-profile", default)).Status);
        }

        await GrantAsync(fgaAdmin, storeId, modelId, tenant, owner,
        [
            "member", "order_creator", "order_committer", "order_viewer", "order_fulfillment_viewer", "work_recorder", "handoff_recorder", "corrector",
            "catalog_editor", "catalog_viewer", "pricing_viewer", "pricing_draft_editor", "pricing_publisher", "manual_pricer"
        ]);
        await GrantAsync(fgaAdmin, storeId, modelId, tenant, operatorAccount,
            ["member", "order_fulfillment_viewer", "work_recorder", "handoff_recorder"]);
        var runtime = new NpgsqlConnectionStringBuilder(database.GetConnectionString())
        { Username = "fulfillment_http_runtime", Password = "local-fulfillment-runtime-only" };
        using var identity = new WhiteLabelApiFactory();
        identity.Bind(owner.ToString("D"), owner);
        identity.Bind(operatorAccount.ToString("D"), operatorAccount);
        var ownerToken = identity.CreateToken(owner.ToString("D"));
        var operatorToken = identity.CreateToken(operatorAccount.ToString("D"));
        var recentOwnerToken = identity.CreateToken(owner.ToString("D"), extraClaims:
        [
            new("azp", "tenant-web-client"),
            new("acr", "urn:application:assurance:high-risk"),
            new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ]);
        // Priced Order admission is prerequisite setup, not qualified for a one-connection pool here.
        var host = CreateHost(identity, runtime.ConnectionString, authorization, 20);
        Guid orderId;
        Guid uncommittedId;
        long uncommittedRevision;
        decimal committedTotal;
        long committedOrderRevision;
        string committedJson;
        try
        {
            using var setupClient = host.CreateClient();
            Assert.Equal(20, host.Services.GetRequiredService<RuntimeDatabaseConfiguration>().MaximumPoolSize);
            Assert.Empty(host.Services.GetServices<ILoggerProvider>());
            var scope = host.Services.CreateAsyncScope();
            await using (scope)
            {
                var context = await scope.ServiceProvider.GetRequiredService<ResolveTenantContext>()
                    .ExecuteAsync(owner, tenant, default) ?? throw new InvalidOperationException("The Owner membership was not resolved through CoreApi.");
                var catalog = scope.ServiceProvider.GetRequiredService<ICatalogStore>();
                var unitResult = await catalog.CreateUnitAsync(context, CatalogUnitIntent.Create(new("EA", "Each", 0)), "unit", default);
                Assert.Equal(CreateCatalogUnitStatus.Created, unitResult.Status);
                var unit = unitResult.Unit ?? throw new InvalidOperationException("Catalog unit creation returned no unit.");
                var itemResult = await catalog.CreateItemAsync(context,
                    CatalogItemIntent.Create(new(null, "In-house service", null, CatalogItemKind.Service, unit.UnitId, CatalogStockMode.NonStock)),
                    "item", default);
                Assert.Equal(CreateCatalogItemStatus.Created, itemResult.Status);
                var item = itemResult.Item ?? throw new InvalidOperationException("Catalog item creation returned no item.");
                var pricing = scope.ServiceProvider.GetRequiredService<PricingApplication>();
                var pricingActor = new PricingActorContext(tenant, owner);
                var pricingPolicy = await scope.ServiceProvider.GetRequiredService<IPricingPolicyStore>()
                    .PublishPolicyAsync(pricingActor, new(0, 0m, 1000m), "publish-pricing-policy", default);
                Assert.Equal(PublishPricingPolicyStatus.Published, pricingPolicy.Status);
                var now = DateTimeOffset.UtcNow;
                var validFrom = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero).AddMinutes(-1);
                var priceDraft = await pricing.CreateDraftAsync(pricingActor,
                    new(item.ItemId, unit.Code, "USD", PriceScope.Default(), 25m, new(validFrom), unit.UnitId), "default-price", default);
                Assert.Equal(CreatePriceDraftStatus.Created, priceDraft.Status);
                var published = await pricing.PublishAsync(pricingActor, new(priceDraft.Price!.RevisionId), "publish-default-price", default);
                Assert.Equal(PublishPriceStatus.Published, published.Status);

                var uncommitted = await SendAsync(setupClient, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders/catalog-priced", ownerToken,
                    "uncommitted-catalog-order", new
                    {
                        summary = "Uncommitted service order",
                        currencyCode = "USD",
                        lines = new[] { new { itemId = item.ItemId, unitId = unit.UnitId, quantity = 4m } },
                        customerContext = new { organizationId = organization, programId = program },
                    }, HttpStatusCode.Created);
                uncommittedId = uncommitted.GetProperty("orderId").GetGuid();
                uncommittedRevision = uncommitted.GetProperty("revision").GetInt64();

                var order = await SendAsync(setupClient, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders/catalog-priced", ownerToken,
                    "create-order", new
                    {
                        summary = "Service order",
                        currencyCode = "USD",
                        lines = new[] { new { itemId = item.ItemId, unitId = unit.UnitId, quantity = 4m } },
                        customerContext = new { organizationId = organization, programId = program },
                    }, HttpStatusCode.Created);
                orderId = order.GetProperty("orderId").GetGuid();
                committedTotal = order.GetProperty("total").GetDecimal();
                Assert.Equal(100m, committedTotal);
                var commit = await SendAsync(setupClient, HttpMethod.Post,
                    $"/api/v1/tenants/{tenant:D}/orders/{orderId:D}/commit", ownerToken, "commit-order",
                    new { expectedRevision = 1 }, HttpStatusCode.OK);
                var committed = await SendAsync(setupClient, HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/orders/{orderId:D}", ownerToken,
                    null, null, HttpStatusCode.OK);
                committedOrderRevision = committed.GetProperty("revision").GetInt64();
                committedJson = committed.GetRawText();
            }

            var manualOrder = await SendAsync(setupClient, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders", ownerToken,
                "create-manual-order", new
                {
                    summary = "Manual service order",
                    currencyCode = "USD",
                    lines = new[] { new { description = "In-house service", quantity = 4m, unitCode = "EA", unitPrice = 25m } },
                    customerContext = new { organizationId = organization, programId = program },
                }, HttpStatusCode.Created);
            var manualOrderId = manualOrder.GetProperty("orderId").GetGuid();
            await SendAsync(setupClient, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders/{manualOrderId:D}/commit", ownerToken,
                "commit-manual-order", new { expectedRevision = manualOrder.GetProperty("revision").GetInt64() }, HttpStatusCode.OK);
            var manualCommittedOrder = await SendAsync(setupClient, HttpMethod.Get,
                $"/api/v1/tenants/{tenant:D}/orders/{manualOrderId:D}", ownerToken, null, null, HttpStatusCode.OK);
            Assert.Equal("committed", manualCommittedOrder.GetProperty("state").GetString());

            setupClient.Dispose();
            await host.DisposeAsync();
            host = CreateHost(identity, runtime.ConnectionString, authorization, maximumPoolSize);
            using var client = host.CreateClient();
            Assert.Equal(maximumPoolSize, host.Services.GetRequiredService<RuntimeDatabaseConfiguration>().MaximumPoolSize);
            Assert.Equal(maximumPoolSize, new NpgsqlConnectionStringBuilder(host.Services.GetRequiredService<NpgsqlDataSource>().ConnectionString).MaxPoolSize);
            Assert.Empty(host.Services.GetServices<ILoggerProvider>());
            var uncommittedStart = await SendAsync(client, HttpMethod.Post,
                $"/api/v1/tenants/{tenant:D}/orders/{uncommittedId:D}/fulfillment/start", operatorToken,
                "start-uncommitted", new { expectedRevision = 0, expectedOrderRevision = uncommittedRevision }, HttpStatusCode.Conflict);
            Assert.Equal("fulfillment_source_ineligible", uncommittedStart.GetProperty("code").GetString());
            var workPath = $"/api/v1/tenants/{tenant:D}/orders/{orderId:D}/fulfillment";
            var staleStart = await SendAsync(client, HttpMethod.Post, workPath + "/start", operatorToken, "start-stale-order-revision",
                new { expectedRevision = 0, expectedOrderRevision = committedOrderRevision + 1 }, HttpStatusCode.Conflict);
            Assert.Equal("order_revision_conflict", staleStart.GetProperty("code").GetString());
            var manualStart = await SendAsync(client, HttpMethod.Post,
                $"/api/v1/tenants/{tenant:D}/orders/{manualOrderId:D}/fulfillment/start", operatorToken,
                "start-manual-order", new { expectedRevision = 0, expectedOrderRevision = manualCommittedOrder.GetProperty("revision").GetInt64() },
                HttpStatusCode.Conflict);
            Assert.Equal("fulfillment_source_ineligible", manualStart.GetProperty("code").GetString());

            var started = await SendAsync(client, HttpMethod.Post, workPath + "/start", operatorToken, "start",
                new { expectedRevision = 0, expectedOrderRevision = committedOrderRevision }, HttpStatusCode.OK);
            Assert.Equal(1, started.GetProperty("revision").GetInt64());
            var race = await Task.WhenAll(
                SendRawAsync(client, HttpMethod.Post, workPath + "/complete", ownerToken, "race-owner",
                    new { expectedRevision = 1, quantity = 1m }),
                SendRawAsync(client, HttpMethod.Post, workPath + "/complete", operatorToken, "race-operator",
                    new { expectedRevision = 1, quantity = 1m }));
            Assert.Single(race, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(race, response => response.StatusCode == HttpStatusCode.Conflict);
            var loser = race.Single(response => response.StatusCode == HttpStatusCode.Conflict);
            using (var conflict = JsonDocument.Parse(await loser.Content.ReadAsStringAsync()))
                Assert.Equal("fulfillment_revision_conflict", conflict.RootElement.GetProperty("code").GetString());
            foreach (var response in race) response.Dispose();
            var afterRace = await SendAsync(client, HttpMethod.Get, workPath, operatorToken, null, null, HttpStatusCode.OK);
            Assert.Equal(1m, afterRace.GetProperty("completedQuantity").GetDecimal());
            var completion = await SendAsync(client, HttpMethod.Post, workPath + "/complete", operatorToken, "complete-second",
                new { expectedRevision = 2, quantity = 1m }, HttpStatusCode.OK);
            Assert.Equal(3, completion.GetProperty("revision").GetInt64());
            var partialPickup = await SendAsync(client, HttpMethod.Post, workPath + "/handoffs", operatorToken, "pickup-partial",
                new { expectedRevision = 3, quantity = 1m, recipient = "Customer representative" }, HttpStatusCode.OK);
            Assert.Equal(1m, (await SendAsync(client, HttpMethod.Get, workPath, operatorToken, null, null, HttpStatusCode.OK))
                .GetProperty("handedOffQuantity").GetDecimal());

            var blocked = await SendAsync(client, HttpMethod.Post, workPath + "/dispositions", operatorToken, "block-work",
                new { expectedRevision = 4, disposition = "Disputed", reason = "Confirming requested service scope" }, HttpStatusCode.OK);
            Assert.Equal("Paused", blocked.GetProperty("stage").GetString());
            var refusedCompletion = await SendAsync(client, HttpMethod.Post, workPath + "/complete", operatorToken, "blocked-completion",
                new { expectedRevision = 5, quantity = 1m }, HttpStatusCode.Conflict);
            Assert.Equal("fulfillment_disposition_active", refusedCompletion.GetProperty("code").GetString());
            var refusedPickup = await SendAsync(client, HttpMethod.Post, workPath + "/handoffs", operatorToken, "blocked-pickup",
                new { expectedRevision = 5, quantity = 1m, recipient = "Customer representative" }, HttpStatusCode.Conflict);
            Assert.Equal("fulfillment_disposition_active", refusedPickup.GetProperty("code").GetString());
            var resolved = await SendAsync(client, HttpMethod.Post, workPath + "/resolve", operatorToken, "resolve-work",
                new { expectedRevision = 5, reason = "Scope confirmed" }, HttpStatusCode.OK);
            Assert.Equal(6, resolved.GetProperty("revision").GetInt64());
            var afterResolve = await SendAsync(client, HttpMethod.Get, workPath, operatorToken, null, null, HttpStatusCode.OK);
            Assert.Equal(2m, afterResolve.GetProperty("completedQuantity").GetDecimal());
            Assert.Equal(1m, afterResolve.GetProperty("handedOffQuantity").GetDecimal());

            var remaining = await SendAsync(client, HttpMethod.Post, workPath + "/complete", operatorToken, "complete-remaining",
                new { expectedRevision = 6, quantity = 2m }, HttpStatusCode.OK);
            Assert.Equal("ReadyForPickup", remaining.GetProperty("stage").GetString());
            var handedOff = await SendAsync(client, HttpMethod.Post, workPath + "/handoffs", operatorToken, "pickup-remaining",
                new { expectedRevision = 7, quantity = 3m, recipient = "Customer representative" }, HttpStatusCode.OK);
            Assert.Equal("HandedOff", handedOff.GetProperty("stage").GetString());
            var history = await SendAsync(client, HttpMethod.Get, workPath + "/history?limit=10", operatorToken, null, null, HttpStatusCode.OK);
            var originalCompletion = history.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("kind").GetString() == "Completion" && item.GetProperty("revision").GetInt64() == 2)
                .GetProperty("eventId").GetString()!;

            var ordinaryCorrection = await SendAsync(client, HttpMethod.Post, workPath + "/corrections", ownerToken, "ordinary-correction",
                new { expectedRevision = 8, targetEventId = originalCompletion, reason = "Correcting recorded entry" }, HttpStatusCode.Forbidden);
            Assert.Equal("high_risk_client_required", ordinaryCorrection.GetProperty("code").GetString());
            var invalidNetCorrection = await SendAsync(client, HttpMethod.Post, workPath + "/corrections", recentOwnerToken, "invalid-net-correction",
                new
                {
                    expectedRevision = 8,
                    targetEventId = originalCompletion,
                    reason = "Correcting recorded entry",
                    replacement = new { kind = "Completion", quantity = 0m },
                }, HttpStatusCode.BadRequest);
            Assert.Equal("fulfillment_quantity_invalid", invalidNetCorrection.GetProperty("code").GetString());
            var invalidVoid = await SendAsync(client, HttpMethod.Post, workPath + "/corrections", recentOwnerToken, "invalid-net-void",
                new { expectedRevision = 8, targetEventId = originalCompletion, reason = "Void would undercut recorded pickup" }, HttpStatusCode.Conflict);
            Assert.Equal("fulfillment_correction_has_dependents", invalidVoid.GetProperty("code").GetString());
            var corrected = await SendAsync(client, HttpMethod.Post, workPath + "/corrections", recentOwnerToken, "valid-correction",
                new
                {
                    expectedRevision = 8,
                    targetEventId = originalCompletion,
                    reason = "Entry was checked against the service sheet",
                    replacement = new { kind = "Completion", quantity = 1m },
                }, HttpStatusCode.OK);
            Assert.Equal(9, corrected.GetProperty("revision").GetInt64());
            var protectedDetail = await SendAsync(client, HttpMethod.Get, workPath, operatorToken, null, null, HttpStatusCode.OK);
            var protectedInbox = await SendAsync(client, HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/fulfillment?limit=50", operatorToken,
                null, null, HttpStatusCode.OK);
            var protectedHistory = await SendAsync(client, HttpMethod.Get, workPath + "/history?limit=10", operatorToken, null, null, HttpStatusCode.OK);
            foreach (var response in new[] { protectedDetail, protectedInbox })
            {
                var text = response.GetRawText();
                Assert.DoesNotContain("Customer representative", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Confirming requested service scope", text, StringComparison.Ordinal);
                Assert.DoesNotContain("reason", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("unitPrice", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("currencyCode", text, StringComparison.OrdinalIgnoreCase);
            }
            Assert.Contains("Customer representative", protectedHistory.GetRawText(), StringComparison.Ordinal);
            Assert.Contains("Confirming requested service scope", protectedHistory.GetRawText(), StringComparison.Ordinal);
            Assert.Contains(originalCompletion, protectedHistory.GetRawText(), StringComparison.Ordinal);
            var retainedCompletion = protectedHistory.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("eventId").GetString() == originalCompletion);
            Assert.False(retainedCompletion.GetProperty("isActive").GetBoolean());
            Assert.Contains(protectedHistory.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("kind").GetString() == "Correction" &&
                item.GetProperty("targetEventId").GetString() == originalCompletion &&
                item.GetProperty("replacement").GetProperty("quantity").GetDecimal() == 1m);
            var recordedHandoff = protectedHistory.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("kind").GetString() == "Handoff" && item.GetProperty("revision").GetInt64() == 8)
                .Clone();
            Assert.Equal(operatorAccount.ToString("D"), recordedHandoff.GetProperty("actorAccountId").GetString());
            Assert.Equal(3m, recordedHandoff.GetProperty("quantity").GetDecimal());
            Assert.True(recordedHandoff.GetProperty("authorizationRevision").GetInt64() > 0);
            Assert.Equal(TimeSpan.Zero, recordedHandoff.GetProperty("recordedAt").GetDateTimeOffset().Offset);
            var pinnedSource = await ReadPinnedSourceAsync(database.GetConnectionString(), tenant, orderId);
            Assert.Equal(committedOrderRevision, pinnedSource.OrderRevision);
            Assert.Equal("EA", pinnedSource.UnitCode);
            foreach (var response in new[] { protectedDetail, protectedInbox, protectedHistory })
            {
                var text = response.GetRawText();
                Assert.DoesNotContain("unitPrice", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("currencyCode", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("USD", text, StringComparison.Ordinal);
            }

            var afterRestart = CreateHost(identity, runtime.ConnectionString, authorization, maximumPoolSize);
            try
            {
                using var restartedClient = afterRestart.CreateClient();
                Assert.Empty(afterRestart.Services.GetServices<ILoggerProvider>());
                var replayResponse = await SendRawAsync(restartedClient, HttpMethod.Post, workPath + "/complete", operatorToken,
                    "complete-remaining", new { expectedRevision = 6, quantity = 2m });
                using (replayResponse)
                {
                    Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
                    Assert.Equal("true", Assert.Single(replayResponse.Headers.GetValues("Idempotency-Replayed")));
                    using var replayDocument = JsonDocument.Parse(await replayResponse.Content.ReadAsStringAsync());
                    Assert.Equal(7, replayDocument.RootElement.GetProperty("revision").GetInt64());
                }
                var durableDetail = await SendAsync(restartedClient, HttpMethod.Get, workPath, operatorToken, null, null, HttpStatusCode.OK);
                Assert.Equal(9, durableDetail.GetProperty("revision").GetInt64());
                Assert.Equal(4m, durableDetail.GetProperty("completedQuantity").GetDecimal());
                Assert.Equal(4m, durableDetail.GetProperty("handedOffQuantity").GetDecimal());
                Assert.Equal(protectedDetail.GetProperty("workId").GetString(), durableDetail.GetProperty("workId").GetString());
                Assert.Equal(orderId.ToString("D"), durableDetail.GetProperty("orderId").GetString());
                Assert.Equal(protectedDetail.GetProperty("unitCode").GetString(), durableDetail.GetProperty("unitCode").GetString());
                var durableHistory = await SendAsync(restartedClient, HttpMethod.Get, workPath + "/history?limit=10", operatorToken,
                    null, null, HttpStatusCode.OK);
                var durableHandoff = durableHistory.GetProperty("items").EnumerateArray()
                    .Single(item => item.GetProperty("eventId").GetString() == recordedHandoff.GetProperty("eventId").GetString());
                Assert.Equal(recordedHandoff.GetRawText(), durableHandoff.GetRawText());
                var durableSource = await ReadPinnedSourceAsync(database.GetConnectionString(), tenant, orderId);
                Assert.Equal(pinnedSource, durableSource);
                var handoffReplay = await SendRawAsync(restartedClient, HttpMethod.Post, workPath + "/handoffs", operatorToken,
                    "pickup-remaining", new { expectedRevision = 7, quantity = 3m, recipient = "Customer representative" });
                using (handoffReplay)
                {
                    Assert.Equal(HttpStatusCode.OK, handoffReplay.StatusCode);
                    Assert.Equal("true", Assert.Single(handoffReplay.Headers.GetValues("Idempotency-Replayed")));
                    using var replayDocument = JsonDocument.Parse(await handoffReplay.Content.ReadAsStringAsync());
                    Assert.Equal(8, replayDocument.RootElement.GetProperty("revision").GetInt64());
                    Assert.Equal("HandedOff", replayDocument.RootElement.GetProperty("stage").GetString());
                }
                var historyAfterHandoffReplay = await SendAsync(restartedClient, HttpMethod.Get, workPath + "/history?limit=10", operatorToken,
                    null, null, HttpStatusCode.OK);
                var retainedHandoffs = historyAfterHandoffReplay.GetProperty("items").EnumerateArray()
                    .Where(item => item.GetProperty("kind").GetString() == "Handoff" && item.GetProperty("revision").GetInt64() == 8)
                    .ToArray();
                Assert.Equal(recordedHandoff.GetProperty("eventId").GetString(), Assert.Single(retainedHandoffs).GetProperty("eventId").GetString());
                var unchangedOrder = await SendAsync(restartedClient, HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/orders/{orderId:D}", ownerToken,
                    null, null, HttpStatusCode.OK);
                Assert.Equal(committedTotal, unchangedOrder.GetProperty("total").GetDecimal());
                Assert.Equal(committedJson, unchangedOrder.GetRawText());
                await DeleteTupleAsync(fgaAdmin, storeId, modelId, tenant, operatorAccount, "work_recorder");
                var revoked = await SendAsync(restartedClient, HttpMethod.Post, workPath + "/complete", operatorToken, "revoked-after-restart",
                    new { expectedRevision = 9, quantity = 1m }, HttpStatusCode.Forbidden);
                Assert.Equal("tenant_permission_denied", revoked.GetProperty("code").GetString());
                await DeleteTupleAsync(fgaAdmin, storeId, modelId, tenant, operatorAccount, "member");
                await using var current = new NpgsqlConnection(database.GetConnectionString());
                await current.OpenAsync();
                await using var suspend = current.CreateCommand();
                suspend.CommandText = "UPDATE tenancy.memberships SET availability=2,revision=revision+1,suspended_at=now() WHERE tenant_id=@tenant AND account_id=@actor";
                suspend.Parameters.AddWithValue("tenant", tenant);
                suspend.Parameters.AddWithValue("actor", operatorAccount);
                await suspend.ExecuteNonQueryAsync();
                var noMembership = await SendAsync(restartedClient, HttpMethod.Get, workPath, operatorToken, null, null, HttpStatusCode.Forbidden);
                Assert.Equal("tenant_access_denied", noMembership.GetProperty("code").GetString());
            }
            finally { await afterRestart.DisposeAsync(); }
        }
        finally { await host.DisposeAsync(); }
    }

    private WebApplicationFactory<Program> CreateHost(WhiteLabelApiFactory identity, string connectionString,
        OpenFgaTenantAuthorization authorization, int maximumPoolSize) => identity.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ConnectionStrings:PrimaryDatabase", connectionString);
        builder.UseSetting("Database:MaximumPoolSize", maximumPoolSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("HighRiskActions:TenantWebClientId", "tenant-web-client");
        builder.UseSetting("HighRiskActions:RequiredAcr", "urn:application:assurance:high-risk");
        builder.ConfigureTestServices(services =>
        {
            services.Insert(0, ServiceDescriptor.Singleton<IExceptionHandler>(new CaptureFailures(_unexpectedFailures)));
            services.RemoveAll<ILoggerProvider>();
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(provider => RuntimeDatabaseConfiguration.From(provider.GetRequiredService<IConfiguration>()).CreateDataSource());
            services.RemoveAll<ITenantMembershipDirectory>();
            services.AddScoped<ITenantMembershipDirectory, PostgresTenantMembershipDirectory>();
            services.RemoveAll<ITenantOrderFulfillmentAuthorization>();
            services.AddSingleton<ITenantOrderFulfillmentAuthorization>(authorization);
            services.RemoveAll<ITenantOrderAuthorization>();
            services.AddSingleton<ITenantOrderAuthorization>(authorization);
            services.RemoveAll<ITenantOrderApprovalAuthorization>();
            services.AddSingleton<ITenantOrderApprovalAuthorization>(authorization);
            services.RemoveAll<ITenantCatalogAuthorization>();
            services.AddSingleton<ITenantCatalogAuthorization>(authorization);
            services.RemoveAll<ITenantPricingAuthorization>();
            services.AddSingleton<ITenantPricingAuthorization>(authorization);
            services.RemoveAll<ICustomerStore>();
            services.AddScoped<ICustomerStore, PostgresCustomerStore>();
            services.RemoveAll<IOrderDraftStore>();
            services.AddScoped<IOrderDraftStore>(provider => provider.GetRequiredService<PostgresOrderDraftStore>());
            services.RemoveAll<ICatalogStore>();
            services.RemoveAll<ICatalogConversionStore>();
            services.RemoveAll<ICatalogAvailabilityStore>();
            services.AddScoped<PostgresCatalogStore>();
            services.AddScoped<ICatalogStore>(provider => provider.GetRequiredService<PostgresCatalogStore>());
            services.AddScoped<ICatalogConversionStore>(provider => provider.GetRequiredService<PostgresCatalogStore>());
            services.AddScoped<ICatalogAvailabilityStore>(provider => provider.GetRequiredService<PostgresCatalogStore>());
        });
    });

    private static async Task<PinnedFulfillmentSource> ReadPinnedSourceAsync(string connectionString, Guid tenantId, Guid orderId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT (source->>'orderRevision')::bigint, (source->>'itemId')::uuid,
                   (source->>'unitId')::uuid, source->>'unitCode', (source->>'linePosition')::integer
            FROM fulfillment.works WHERE tenant_id=@tenant AND order_id=@order
            """;
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("order", orderId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "The persisted fulfillment source was missing.");
        var source = new PinnedFulfillmentSource(reader.GetInt64(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetInt32(4));
        Assert.False(await reader.ReadAsync(), "More than one fulfillment source matched the tenant and order.");
        return source;
    }

    private sealed record PinnedFulfillmentSource(long OrderRevision, Guid ItemId, Guid UnitId, string UnitCode, int LinePosition);

    private async Task<JsonElement> SendAsync(HttpClient client, HttpMethod method, string path, string token,
        string? key, object? body, params HttpStatusCode[] expected)
    {
        using var response = await SendRawAsync(client, method, path, token, key, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(expected.Contains(response.StatusCode), $"{method} {path}: {(int)response.StatusCode}; {text}; {_unexpectedFailures.LastOrDefault()}");
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> SendRawAsync(HttpClient client, HttpMethod method, string path, string token,
        string? key, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<string> PostIdAsync(HttpClient client, string path, object body, string property)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty(property).GetString()!;
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

    private static async Task DeleteTupleAsync(HttpClient client, string storeId, string modelId, Guid tenant, Guid actor, string relation)
    {
        using var response = await client.PostAsJsonAsync($"/stores/{storeId}/write", new
        {
            deletes = new { tuple_keys = new[] { new { user = $"user:{actor:N}", relation, @object = $"tenant:{tenant:N}" } } },
            authorization_model_id = modelId,
        });
        response.EnsureSuccessStatusCode();
    }

    private sealed class CaptureFailures(ConcurrentQueue<Exception> failures) : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
        {
            failures.Enqueue(exception);
            return ValueTask.FromResult(false);
        }
    }

    private static string RepositoryRoot()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "Application.slnx"))) return path.FullName;
        throw new InvalidOperationException("Repository source root was not found.");
    }
}
