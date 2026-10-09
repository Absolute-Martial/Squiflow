extern alias DatabaseMigrator;

using System.Collections.Concurrent;
using System.Net;
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
using Application.Profiles;
using Application.Profiles.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using DatabaseMigrator::Application.DatabaseMigrator;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
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
public sealed class PurchasingNativeEndpointTests
{
    private const string SensitiveContact = "synthetic-contact-canary@example.test";
    private const string SensitiveDescription = "synthetic-service-description-canary";
    private const string SensitiveReference = "synthetic-bank-reference-canary";
    private const string SensitiveReason = "synthetic-correction-reason-canary";
    private readonly ConcurrentQueue<Exception> _unexpectedFailures = new();
    private readonly ConcurrentQueue<string> _diagnostics = new();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NativeSupplierServiceJourneyPreservesPricesAndRechecksIndependentAuthorityAfterRestart(int maximumPoolSize)
    {
        await using var database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithDatabase("purchasing_http_tests").WithUsername("postgres").WithPassword("local-purchasing-http-only").Build();
        await database.StartAsync();
        await new MigrationRunner(database.GetConnectionString(), 0x73121610).ApplyAsync(TimeSpan.FromSeconds(10), default);
        await using var server = new ContainerBuilder(new DockerImage(repository: "openfga/openfga", tag: "v1.21.0"))
            .WithCommand("run", "--playground-enabled=false").WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/healthz"))).Build();
        await server.StartAsync();
        using var fgaAdmin = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.GetMappedPublicPort(8080)}") };
        var storeId = await PostIdAsync(fgaAdmin, "/stores", new { name = "purchasing-native-http" }, "id");
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
        var otherTenant = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var recorder = Guid.NewGuid();
        var organization = Guid.NewGuid();
        var program = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = """
                INSERT INTO identity_access.accounts(id,availability,created_at) VALUES(@owner,1,now()),(@recorder,1,now());
                INSERT INTO tenancy.tenants(id,display_name,availability,created_at)
                    VALUES(@tenant,'Purchasing HTTP tenant',1,now()),(@other,'Other purchasing tenant',1,now());
                INSERT INTO tenancy.memberships(tenant_id,account_id,availability,created_at,revision,is_initial_owner,activated_at)
                    VALUES(@tenant,@owner,1,now(),1,true,now()),(@tenant,@recorder,1,now(),1,false,now()),(@other,@owner,1,now(),1,true,now());
                INSERT INTO customers.organizations(tenant_id,id,display_name,created_by_account_id,created_at)
                    VALUES(@tenant,@organization,'Purchasing customer',@owner,now());
                INSERT INTO customers.programs(tenant_id,id,organization_id,display_name,created_by_account_id,created_at)
                    VALUES(@tenant,@program,@organization,'Purchasing customer program',@owner,now());
                CREATE ROLE purchasing_http_runtime LOGIN PASSWORD 'local-purchasing-runtime-only';
                SELECT set_config('app.provision_runtime_role','purchasing_http_runtime',false);
                """;
            setup.Parameters.AddWithValue("owner", owner);
            setup.Parameters.AddWithValue("recorder", recorder);
            setup.Parameters.AddWithValue("tenant", tenant);
            setup.Parameters.AddWithValue("other", otherTenant);
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
            Assert.Equal(ProfileCommandStatus.Edited, (await profiles.EditPolicyAsync(context, new(0, false, false), 1, "edit", default)).Status);
            var policy = await profiles.PublishPolicyAsync(context, new(1), 1, "publish-policy", default);
            var profile = await profiles.PublishProfileAsync(new(Guid.NewGuid(), Guid.NewGuid()),
                new(tenant, 0, policy.PublishedPolicy!.PolicyRevisionId, 1), "publish-profile", default);
            Assert.Equal(ProfileCommandStatus.Activated, (await profiles.ActivateProfileAsync(new(Guid.NewGuid(), Guid.NewGuid()),
                new(tenant, profile.Authority!.Revision, profile.Profile!.ProfileId), "activate-profile", default)).Status);
        }
        await GrantAsync(fgaAdmin, storeId, modelId, tenant, owner,
            ["member", "catalog_editor", "catalog_viewer", "order_creator", "order_committer", "order_viewer", "manual_pricer"]);
        await GrantAsync(fgaAdmin, storeId, modelId, otherTenant, owner, ["member", "supplier_viewer"]);
        await GrantAsync(fgaAdmin, storeId, modelId, tenant, recorder, ["member", "purchase_receiver"]);
        await GrantRequestRoleAsync(fgaAdmin, storeId, modelId, tenant, recorder, Guid.NewGuid());
        var runtime = new NpgsqlConnectionStringBuilder(database.GetConnectionString())
        { Username = "purchasing_http_runtime", Password = "local-purchasing-runtime-only" };
        using var identity = new WhiteLabelApiFactory();
        identity.Bind(owner.ToString("D"), owner);
        identity.Bind(recorder.ToString("D"), recorder);
        var ownerToken = identity.CreateToken(owner.ToString("D"));
        var recorderToken = identity.CreateToken(recorder.ToString("D"));
        var recentOwnerToken = ElevatedToken(identity, owner);
        var recentRecorderToken = ElevatedToken(identity, recorder);
        var host = CreateHost(identity, runtime.ConnectionString, authorization, maximumPoolSize);
        try
        {
            using var client = host.CreateClient();
            Assert.Equal(maximumPoolSize, host.Services.GetRequiredService<RuntimeDatabaseConfiguration>().MaximumPoolSize);
            Assert.Equal(maximumPoolSize, new NpgsqlConnectionStringBuilder(host.Services.GetRequiredService<NpgsqlDataSource>().ConnectionString).MaxPoolSize);
            var basePath = $"/api/v1/tenants/{tenant:D}/purchasing";
            var supplier = Guid.NewGuid();
            var supplierPath = $"{basePath}/suppliers/{supplier:D}";
            var supplierBody = new { expectedRevision = 0, name = "Informal printing supplier", contact = SensitiveContact };
            await SendAsync(client, HttpMethod.Post, supplierPath, ownerToken, "no-automatic-owner-grant", supplierBody, HttpStatusCode.Forbidden);
            await GrantAsync(fgaAdmin, storeId, modelId, tenant, owner,
                ["supplier_viewer", "supplier_editor", "purchase_request_recorder", "purchase_receiver", "supplier_payer", "purchasing_corrector"]);
            var createdSupplier = await SendAsync(client, HttpMethod.Post, supplierPath, ownerToken, "supplier", supplierBody, HttpStatusCode.OK);
            AssertCommandOnly(createdSupplier, supplier, 1);

            Guid unitId;
            Guid otherUnitId;
            await GrantAsync(fgaAdmin, storeId, modelId, otherTenant, owner,
                ["catalog_editor", "catalog_viewer", "supplier_editor", "purchase_request_recorder"]);
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var context = await scope.ServiceProvider.GetRequiredService<ResolveTenantContext>()
                    .ExecuteAsync(owner, tenant, default) ?? throw new InvalidOperationException("CoreApi did not resolve the Owner membership.");
                var unit = await scope.ServiceProvider.GetRequiredService<ICatalogStore>().CreateUnitAsync(context,
                    CatalogUnitIntent.Create(new("EA", "Each", 0)), "unit", default);
                Assert.Equal(CreateCatalogUnitStatus.Created, unit.Status);
                unitId = unit.Unit!.UnitId;
                var otherContext = await scope.ServiceProvider.GetRequiredService<ResolveTenantContext>()
                    .ExecuteAsync(owner, otherTenant, default) ?? throw new InvalidOperationException("CoreApi did not resolve the other tenant membership.");
                var otherUnit = await scope.ServiceProvider.GetRequiredService<ICatalogStore>().CreateUnitAsync(otherContext,
                    CatalogUnitIntent.Create(new("EA", "Each", 0)), "unit", default);
                Assert.Equal(CreateCatalogUnitStatus.Created, otherUnit.Status);
                otherUnitId = otherUnit.Unit!.UnitId;
            }
            if (maximumPoolSize == 2)
            {
                var firstBurstSupplier = Guid.NewGuid();
                var secondBurstSupplier = Guid.NewGuid();
                var suppliers = await Task.WhenAll(
                    SendAsync(client, HttpMethod.Post, $"{basePath}/suppliers/{firstBurstSupplier:D}", ownerToken, "burst-supplier-one", supplierBody, HttpStatusCode.OK),
                    SendAsync(client, HttpMethod.Post, $"{basePath}/suppliers/{secondBurstSupplier:D}", ownerToken, "burst-supplier-two", supplierBody, HttpStatusCode.OK))
                    .WaitAsync(TimeSpan.FromSeconds(30));
                AssertCommandOnly(suppliers[0], firstBurstSupplier, 1);
                AssertCommandOnly(suppliers[1], secondBurstSupplier, 1);
                var firstBurstPurchase = Guid.NewGuid();
                var secondBurstPurchase = Guid.NewGuid();
                var purchases = await Task.WhenAll(
                    SendAsync(client, HttpMethod.Post, $"{basePath}/requests/{firstBurstPurchase:D}", ownerToken, "burst-request-one",
                        new { expectedRevision = 0, supplierId = firstBurstSupplier, unitId, description = SensitiveDescription, quantity = 1m }, HttpStatusCode.OK),
                    SendAsync(client, HttpMethod.Post, $"{basePath}/requests/{secondBurstPurchase:D}", ownerToken, "burst-request-two",
                        new { expectedRevision = 0, supplierId = secondBurstSupplier, unitId, description = SensitiveDescription, quantity = 1m }, HttpStatusCode.OK))
                    .WaitAsync(TimeSpan.FromSeconds(30));
                AssertCommandOnly(purchases[0], firstBurstPurchase, 1);
                AssertCommandOnly(purchases[1], secondBurstPurchase, 1);
            }
            var purchase = Guid.NewGuid();
            var purchasePath = $"{basePath}/requests/{purchase:D}";
            var informal = new { expectedRevision = 0, supplierId = supplier, unitId, description = SensitiveDescription, quantity = 4m };
            AssertCommandOnly(await SendAsync(client, HttpMethod.Post, purchasePath, recorderToken, "phone-request", informal, HttpStatusCode.OK), purchase, 1);
            await SendAsync(client, HttpMethod.Get, purchasePath, recorderToken, null, null, HttpStatusCode.Forbidden);
            await SendAsync(client, HttpMethod.Post, supplierPath + "/revise", recorderToken, "no-supplier-management",
                new { expectedRevision = 1, name = "Changed", contact = SensitiveContact }, HttpStatusCode.Forbidden);
            await SendAsync(client, HttpMethod.Post, purchasePath + "/receipts", recorderToken, "fractional-each",
                new { expectedRevision = 1, quantity = 0.5m, cost = 100m }, HttpStatusCode.BadRequest);
            await SendAsync(client, HttpMethod.Post, purchasePath + "/receipts", recorderToken, "fractional-money",
                new { expectedRevision = 1, quantity = 1m, cost = 100.001m }, HttpStatusCode.BadRequest);
            var firstReceipt = new { expectedRevision = 1, quantity = 2m, cost = 1000m };
            AssertCommandOnly(await SendAsync(client, HttpMethod.Post, purchasePath + "/receipts", recorderToken,
                "first-receipt", firstReceipt, HttpStatusCode.OK), purchase, 2);
            var read = await SendAsync(client, HttpMethod.Get, purchasePath, ownerToken, null, null, HttpStatusCode.OK);
            Assert.Equal(JsonValueKind.Null, read.GetProperty("order").ValueKind);
            Assert.Equal(1000m, read.GetProperty("outstandingPayable").GetDecimal());
            var receiptId = Assert.Single(read.GetProperty("receipts").EnumerateArray()).GetProperty("entryId").GetString()!;
            var paymentBody = new { expectedRevision = 2, receiptId, amount = 500m, method = "Bank", reference = SensitiveReference };
            await SendAsync(client, HttpMethod.Post, purchasePath + "/payments", recorderToken, "no-payment-permission", paymentBody, HttpStatusCode.Forbidden);
            await GrantAsync(fgaAdmin, storeId, modelId, tenant, recorder, ["supplier_payer", "supplier_viewer", "purchasing_corrector"]);
            AssertCommandOnly(await SendAsync(client, HttpMethod.Post, purchasePath + "/payments", recorderToken,
                "first-payment", paymentBody, HttpStatusCode.OK), purchase, 3);
            await SendAsync(client, HttpMethod.Post, purchasePath + "/payments", recorderToken, "over-payment",
                new { expectedRevision = 3, receiptId, amount = 500.01m, method = "Cash" }, HttpStatusCode.Conflict);
            read = await SendAsync(client, HttpMethod.Get, purchasePath, recorderToken, null, null, HttpStatusCode.OK);
            var paymentId = Assert.Single(read.GetProperty("payments").EnumerateArray()).GetProperty("entryId").GetString()!;
            var correctedCost = new
            {
                expectedRevision = 3,
                targetEventId = receiptId,
                reason = SensitiveReason,
                replacement = new { kind = "Receipt", quantity = 2m, amount = 400m },
            };
            await SendAsync(client, HttpMethod.Post, purchasePath + "/corrections", ownerToken, "ordinary-owner-correction", correctedCost, HttpStatusCode.Forbidden);
            await SendAsync(client, HttpMethod.Post, purchasePath + "/corrections", recentRecorderToken, "non-owner-correction", correctedCost, HttpStatusCode.Forbidden);
            await SendAsync(client, HttpMethod.Post, purchasePath + "/corrections", recentOwnerToken, "paid-cost-cannot-fall", correctedCost, HttpStatusCode.Conflict);
            AssertCommandOnly(await SendAsync(client, HttpMethod.Post, purchasePath + "/refunds", recorderToken, "actual-refund",
                new { expectedRevision = 3, paymentId, amount = 100m, method = "Bank", reference = SensitiveReference }, HttpStatusCode.OK), purchase, 4);
            await SendAsync(client, HttpMethod.Post, purchasePath + "/refunds", recorderToken, "over-refund",
                new { expectedRevision = 4, paymentId, amount = 400.01m, method = "Cash" }, HttpStatusCode.Conflict);
            AssertCommandOnly(await SendAsync(client, HttpMethod.Post, purchasePath + "/corrections", recentOwnerToken, "correct-cost-after-refund",
                new
                {
                    expectedRevision = 4,
                    targetEventId = receiptId,
                    reason = SensitiveReason,
                    replacement = new { kind = "Receipt", quantity = 2m, amount = 400m },
                }, HttpStatusCode.OK), purchase, 5);
            AssertCommandOnly(await SendAsync(client, HttpMethod.Post, purchasePath + "/receipts", recorderToken, "second-receipt",
                new { expectedRevision = 5, quantity = 1m, cost = 150m }, HttpStatusCode.OK), purchase, 6);
            read = await SendAsync(client, HttpMethod.Get, purchasePath, ownerToken, null, null, HttpStatusCode.OK);
            var secondReceiptId = read.GetProperty("receipts").EnumerateArray().Single(x => x.GetProperty("entryId").GetString() != receiptId).GetProperty("entryId").GetString()!;
            AssertCommandOnly(await SendAsync(client, HttpMethod.Post, purchasePath + "/payments", recorderToken, "cash-payment",
                new { expectedRevision = 6, receiptId = secondReceiptId, amount = 75m, method = "Cash" }, HttpStatusCode.OK), purchase, 7);
            AssertCommandOnly(await SendAsync(client, HttpMethod.Post, purchasePath + "/cancel", recorderToken, "cancel-unreceived",
                new { expectedRevision = 7, reason = SensitiveReason }, HttpStatusCode.OK), purchase, 8);
            await SendAsync(client, HttpMethod.Post, purchasePath + "/receipts", recorderToken, "after-cancel",
                new { expectedRevision = 8, quantity = 1m, cost = 10m }, HttpStatusCode.Conflict);
            await SendAsync(client, HttpMethod.Post, purchasePath + "/receipts", recorderToken, "first-receipt",
                new { expectedRevision = 1, quantity = 2m, cost = 999m }, HttpStatusCode.Conflict);
            var balance = await SendAsync(client, HttpMethod.Get, supplierPath + "/balance", ownerToken, null, null, HttpStatusCode.OK);
            AssertBalance(balance);
            var history = await SendAsync(client, HttpMethod.Get, purchasePath + "/history?limit=10", recorderToken, null, null, HttpStatusCode.OK);
            Assert.Equal(8, history.GetProperty("observedRevision").GetInt64());
            var retainedReceipt = history.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("eventId").GetString() == receiptId);
            Assert.Equal(1000m, retainedReceipt.GetProperty("facts").GetProperty("amount").GetDecimal());
            Assert.Equal(recorder.ToString("D"), retainedReceipt.GetProperty("actorAccountId").GetString());
            Assert.True(retainedReceipt.GetProperty("authorizationRevision").GetInt64() > 0);
            Assert.Equal(TimeSpan.Zero, retainedReceipt.GetProperty("recordedAt").GetDateTimeOffset().Offset);
            Assert.Contains(history.GetProperty("items").EnumerateArray(), x => x.GetProperty("kind").GetString() == "Correction" &&
                x.GetProperty("targetEventId").GetString() == receiptId && x.GetProperty("replacement").GetProperty("amount").GetDecimal() == 400m);
            await SendAsync(client, HttpMethod.Get, purchasePath + "/history?limit=11", ownerToken, null, null, HttpStatusCode.BadRequest);
            await SendAsync(client, HttpMethod.Get, $"/api/v1/tenants/{otherTenant:D}/purchasing/requests/{purchase:D}", ownerToken, null, null, HttpStatusCode.NotFound);

            var order = await SendAsync(client, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders", ownerToken, "selling-order",
                new
                {
                    summary = "Service sold to the customer",
                    currencyCode = "USD",
                    lines = new[] { new { description = "Sold service", quantity = 4m, unitCode = "EA", unitPrice = 25m } },
                    customerContext = new { organizationId = organization, programId = program },
                }, HttpStatusCode.Created);
            var orderId = order.GetProperty("orderId").GetGuid();
            var linkedPurchase = Guid.NewGuid();
            var linkedPath = $"{basePath}/requests/{linkedPurchase:D}";
            await SendAsync(client, HttpMethod.Post, linkedPath, ownerToken, "draft-order-link",
                new
                {
                    expectedRevision = 0,
                    supplierId = supplier,
                    unitId,
                    description = "Linked printing service",
                    quantity = 1m,
                    order = new { orderId, linePosition = 1, orderRevision = 1 }
                }, HttpStatusCode.NotFound);
            await SendAsync(client, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders/{orderId:D}/commit", ownerToken, "commit-selling-order",
                new { expectedRevision = 1 }, HttpStatusCode.OK);
            var committed = await SendAsync(client, HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/orders/{orderId:D}", ownerToken, null, null, HttpStatusCode.OK);
            Assert.Equal(100m, committed.GetProperty("total").GetDecimal());
            var committedRevision = committed.GetProperty("revision").GetInt64();
            await SendAsync(client, HttpMethod.Post, linkedPath, ownerToken, "stale-order-link",
                new
                {
                    expectedRevision = 0,
                    supplierId = supplier,
                    unitId,
                    description = "Linked printing service",
                    quantity = 1m,
                    order = new { orderId, linePosition = 1, orderRevision = committedRevision + 1 }
                }, HttpStatusCode.NotFound);
            await SendAsync(client, HttpMethod.Post, linkedPath, ownerToken, "invalid-order-line",
                new
                {
                    expectedRevision = 0,
                    supplierId = supplier,
                    unitId,
                    description = "Linked printing service",
                    quantity = 1m,
                    order = new { orderId, linePosition = 2, orderRevision = committedRevision }
                }, HttpStatusCode.NotFound);
            AssertCommandOnly(await SendAsync(client, HttpMethod.Post, linkedPath, ownerToken, "committed-order-link",
                new
                {
                    expectedRevision = 0,
                    supplierId = supplier,
                    unitId,
                    description = "Linked printing service",
                    quantity = 1m,
                    order = new { orderId, linePosition = 1, orderRevision = committedRevision }
                }, HttpStatusCode.OK), linkedPurchase, 1);
            var linkedRead = await SendAsync(client, HttpMethod.Get, linkedPath, ownerToken, null, null, HttpStatusCode.OK);
            Assert.Equal(orderId, linkedRead.GetProperty("order").GetProperty("orderId").GetGuid());
            Assert.Equal(committedRevision, linkedRead.GetProperty("order").GetProperty("orderRevision").GetInt64());
            Assert.DoesNotContain("unitPrice", linkedRead.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("customerContext", linkedRead.GetRawText(), StringComparison.OrdinalIgnoreCase);
            var otherSupplier = Guid.NewGuid();
            await SendAsync(client, HttpMethod.Post, $"/api/v1/tenants/{otherTenant:D}/purchasing/suppliers/{otherSupplier:D}", ownerToken,
                "other-supplier", supplierBody, HttpStatusCode.OK);
            await SendAsync(client, HttpMethod.Post, $"/api/v1/tenants/{otherTenant:D}/purchasing/requests/{Guid.NewGuid():D}", ownerToken,
                "cross-tenant-order-link", new
                {
                    expectedRevision = 0,
                    supplierId = otherSupplier,
                    unitId = otherUnitId,
                    description = SensitiveDescription,
                    quantity = 1m,
                    order = new { orderId, linePosition = 1, orderRevision = committedRevision }
                }, HttpStatusCode.NotFound);

            await host.DisposeAsync();
            var restarted = CreateHost(identity, runtime.ConnectionString, authorization, maximumPoolSize);
            try
            {
                using var restartedClient = restarted.CreateClient();
                using (var replay = await SendRawAsync(restartedClient, HttpMethod.Post, purchasePath + "/receipts", recorderToken, "first-receipt", firstReceipt))
                {
                    Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
                    Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
                    using var document = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
                    AssertCommandOnly(document.RootElement, purchase, 2);
                }
                var durable = await SendAsync(restartedClient, HttpMethod.Get, purchasePath, recorderToken, null, null, HttpStatusCode.OK);
                Assert.Equal(8, durable.GetProperty("revision").GetInt64());
                Assert.Equal(3m, durable.GetProperty("receivedQuantity").GetDecimal());
                Assert.Equal(1m, durable.GetProperty("cancelledQuantity").GetDecimal());
                Assert.Equal(0m, durable.GetProperty("remainingQuantity").GetDecimal());
                Assert.Equal(history.GetRawText(), (await SendAsync(restartedClient, HttpMethod.Get, purchasePath + "/history?limit=10", recorderToken,
                    null, null, HttpStatusCode.OK)).GetRawText());
                AssertBalance(await SendAsync(restartedClient, HttpMethod.Get, supplierPath + "/balance", ownerToken, null, null, HttpStatusCode.OK));
                Assert.Equal(committed.GetRawText(), (await SendAsync(restartedClient, HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/orders/{orderId:D}", ownerToken,
                    null, null, HttpStatusCode.OK)).GetRawText());
                await DeleteTupleAsync(fgaAdmin, storeId, modelId, tenant, recorder, "purchase_receiver");
                await SendAsync(restartedClient, HttpMethod.Post, purchasePath + "/receipts", recorderToken, "first-receipt", firstReceipt, HttpStatusCode.Forbidden);
                await DeleteTupleAsync(fgaAdmin, storeId, modelId, tenant, recorder, "member");
                await SendAsync(restartedClient, HttpMethod.Get, purchasePath, recorderToken, null, null, HttpStatusCode.OK);
                await using var suspend = connection.CreateCommand();
                suspend.CommandText = "UPDATE tenancy.memberships SET availability=2,revision=revision+1,suspended_at=now() WHERE tenant_id=@tenant AND account_id=@actor";
                suspend.Parameters.AddWithValue("tenant", tenant);
                suspend.Parameters.AddWithValue("actor", recorder);
                await suspend.ExecuteNonQueryAsync();
                var noMembership = await SendAsync(restartedClient, HttpMethod.Get, purchasePath, recorderToken, null, null, HttpStatusCode.Forbidden);
                Assert.Equal("tenant_access_denied", noMembership.GetProperty("code").GetString());
                Assert.Empty(_unexpectedFailures);
                Assert.NotEmpty(_diagnostics);
                Assert.All(_diagnostics, AssertPrivate);
            }
            finally { await restarted.DisposeAsync(); }
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
            services.AddSingleton<ILoggerProvider>(new CapturedLogs(_diagnostics));
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(provider => RuntimeDatabaseConfiguration.From(provider.GetRequiredService<IConfiguration>()).CreateDataSource());
            services.RemoveAll<ITenantMembershipDirectory>();
            services.AddScoped<ITenantMembershipDirectory, PostgresTenantMembershipDirectory>();
            services.RemoveAll<ITenantPurchasingAuthorization>();
            services.AddSingleton<ITenantPurchasingAuthorization>(authorization);
            services.RemoveAll<ITenantOrderAuthorization>();
            services.AddSingleton<ITenantOrderAuthorization>(authorization);
            services.RemoveAll<ITenantOrderApprovalAuthorization>();
            services.AddSingleton<ITenantOrderApprovalAuthorization>(authorization);
            services.RemoveAll<ITenantCatalogAuthorization>();
            services.AddSingleton<ITenantCatalogAuthorization>(authorization);
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

    private static string ElevatedToken(WhiteLabelApiFactory identity, Guid account) => identity.CreateToken(account.ToString("D"), extraClaims:
    [
        new("azp", "tenant-web-client"),
        new("acr", "urn:application:assurance:high-risk"),
        new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
    ]);

    private static void AssertCommandOnly(JsonElement body, Guid id, long revision)
    {
        Assert.Equal(id.ToString("D"), body.GetProperty("id").GetString());
        Assert.Equal(revision, body.GetProperty("revision").GetInt64());
        Assert.Equal(2, body.EnumerateObject().Count());
        AssertPrivate(body.GetRawText());
    }

    private static void AssertBalance(JsonElement balance)
    {
        Assert.Equal("NPR", balance.GetProperty("currency").GetString());
        Assert.Equal(550m, balance.GetProperty("totalCost").GetDecimal());
        Assert.Equal(575m, balance.GetProperty("paidAmount").GetDecimal());
        Assert.Equal(100m, balance.GetProperty("refundedAmount").GetDecimal());
        Assert.Equal(75m, balance.GetProperty("outstandingPayable").GetDecimal());
    }

    private static void AssertPrivate(string text)
    {
        foreach (var canary in new[] { SensitiveContact, SensitiveDescription, SensitiveReference, SensitiveReason })
            Assert.DoesNotContain(canary, text, StringComparison.Ordinal);
    }

    private async Task<JsonElement> SendAsync(HttpClient client, HttpMethod method, string path, string token,
        string? key, object? body, HttpStatusCode expected)
    {
        using var response = await SendRawAsync(client, method, path, token, key, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{method} {path}: {(int)response.StatusCode}; {text}; {_unexpectedFailures.LastOrDefault()}");
        Assert.True(response.Headers.CacheControl?.NoStore);
        if (!response.IsSuccessStatusCode) AssertPrivate(text);
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

    private static async Task GrantRequestRoleAsync(HttpClient client, string storeId, string modelId, Guid tenant, Guid actor, Guid role)
    {
        using var response = await client.PostAsJsonAsync($"/stores/{storeId}/write", new
        {
            writes = new
            {
                tuple_keys = new[]
            {
                new { user = $"user:{actor:N}", relation = "assignee", @object = $"role:{tenant:N}_{role:N}" },
                new { user = $"role:{tenant:N}_{role:N}#assignee", relation = "purchase_request_recorder", @object = $"tenant:{tenant:N}" },
            }
            },
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

    private sealed class CapturedLogs(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Sink(messages);
        public void Dispose() { }

        private sealed class Sink(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
                if (exception is not null) messages.Enqueue(exception.ToString());
            }
        }
    }

    private static string RepositoryRoot()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "Application.slnx"))) return path.FullName;
        throw new InvalidOperationException("Repository source root was not found.");
    }
}
