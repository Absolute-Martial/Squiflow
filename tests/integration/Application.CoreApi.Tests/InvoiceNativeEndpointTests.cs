extern alias DatabaseMigrator;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
public sealed class InvoiceNativeEndpointTests
{
    private const string PrivateName = "synthetic-invoice-debtor-canary";
    private const string PrivateDescription = "synthetic-invoice-line-canary";
    private const string PrivateContact = "synthetic-invoice-contact-canary@example.test";
    private readonly ConcurrentQueue<Exception> failures = new();
    private readonly ConcurrentQueue<string> diagnostics = new();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NativeFullInvoiceJourneyPreservesFrozenPricesAndDebtorsWithIndependentCurrentRights(int maximumPoolSize)
    {
        await using var database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithDatabase("invoice_http_tests").WithUsername("postgres").WithPassword("local-invoice-http-only").Build();
        await database.StartAsync();
        await new MigrationRunner(database.GetConnectionString(), 0x73121922).ApplyAsync(TimeSpan.FromSeconds(10), default);
        await using var server = new ContainerBuilder(new DockerImage(repository: "openfga/openfga", tag: "v1.21.0"))
            .WithCommand("run", "--playground-enabled=false").WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/healthz"))).Build();
        await server.StartAsync();
        using var fgaAdmin = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.GetMappedPublicPort(8080)}") };
        var storeId = await PostIdAsync(fgaAdmin, "/stores", new { name = "invoice-native-http" }, "id");
        var model = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "OpenFga", "tenant-authorization-model.json"));
        using var modelResponse = await fgaAdmin.PostAsync($"/stores/{storeId}/authorization-models", new StringContent(model, Encoding.UTF8, "application/json"));
        modelResponse.EnsureSuccessStatusCode();
        using var modelDocument = JsonDocument.Parse(await modelResponse.Content.ReadAsStringAsync());
        var modelId = modelDocument.RootElement.GetProperty("authorization_model_id").GetString()!;
        var configuration = OpenFgaAuthorizationConfiguration.From(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authorization:OpenFga:ApiUrl"] = fgaAdmin.BaseAddress.AbsoluteUri,
            ["Authorization:OpenFga:StoreId"] = storeId,
            ["Authorization:OpenFga:AuthorizationModelId"] = modelId,
            ["Authorization:OpenFga:RequestTimeoutSeconds"] = "5",
            ["Authorization:OpenFga:CredentialMethod"] = "None",
            ["Authorization:OpenFga:MaximumRetries"] = "0",
            ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
        }).Build());
        using var fga = new OpenFgaClient(configuration.ToClientConfiguration());
        var authorization = new OpenFgaTenantAuthorization(fga, configuration, NullLogger<OpenFgaTenantAuthorization>.Instance);
        var tenant = Guid.NewGuid(); var other = Guid.NewGuid(); var owner = Guid.NewGuid();
        var issuer = Guid.NewGuid(); var viewer = Guid.NewGuid(); var roleIssuer = Guid.NewGuid();
        var organization = Guid.NewGuid(); var program = Guid.NewGuid();
        await using var admin = new NpgsqlConnection(database.GetConnectionString());
        await admin.OpenAsync();
        await using (var setup = admin.CreateCommand())
        {
            setup.CommandText = """
                INSERT INTO identity_access.accounts(id,availability,created_at) VALUES(@owner,1,now()),(@issuer,1,now()),(@viewer,1,now()),(@role_issuer,1,now());
                INSERT INTO tenancy.tenants(id,display_name,availability,created_at) VALUES(@tenant,'Invoice HTTP tenant',1,now()),(@other,'Other invoice tenant',1,now());
                INSERT INTO tenancy.memberships(tenant_id,account_id,availability,created_at,revision,is_initial_owner,activated_at)
                    VALUES(@tenant,@owner,1,now(),1,true,now()),(@tenant,@issuer,1,now(),1,false,now()),
                          (@tenant,@viewer,1,now(),1,false,now()),(@tenant,@role_issuer,1,now(),1,false,now()),(@other,@issuer,1,now(),1,true,now());
                INSERT INTO customers.organizations(tenant_id,id,display_name,created_by_account_id,created_at) VALUES(@tenant,@organization,@name,@owner,now());
                INSERT INTO customers.programs(tenant_id,id,organization_id,display_name,created_by_account_id,created_at) VALUES(@tenant,@program,@organization,'Independent program debtor',@owner,now());
                CREATE ROLE invoice_http_runtime LOGIN PASSWORD 'local-invoice-runtime-only';
                SELECT set_config('app.provision_runtime_role','invoice_http_runtime',false);
                """;
            setup.Parameters.AddWithValue("tenant", tenant); setup.Parameters.AddWithValue("other", other);
            setup.Parameters.AddWithValue("owner", owner); setup.Parameters.AddWithValue("issuer", issuer);
            setup.Parameters.AddWithValue("viewer", viewer); setup.Parameters.AddWithValue("role_issuer", roleIssuer);
            setup.Parameters.AddWithValue("organization", organization); setup.Parameters.AddWithValue("program", program);
            setup.Parameters.AddWithValue("name", PrivateName);
            await setup.ExecuteNonQueryAsync();
        }
        await using (var grants = admin.CreateCommand())
        {
            grants.CommandText = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "deploy/database/grant-core-api-runtime.sql"));
            await grants.ExecuteNonQueryAsync();
        }
        await using (var source = NpgsqlDataSource.Create(database.GetConnectionString()))
        {
            await using var tenantDatabase = TenancyPostgresMigrations.CreateContext(database.GetConnectionString());
            var context = await new ResolveTenantContext(new PostgresTenantMembershipDirectory(tenantDatabase)).ExecuteAsync(owner, tenant, default)
                ?? throw new InvalidOperationException("The seeded Owner membership was not resolved.");
            var profiles = new PostgresProfileStore(source, TimeProvider.System);
            Assert.Equal(ProfileCommandStatus.Edited, (await profiles.EditPolicyAsync(context, new(0, false, false), 1, "edit", default)).Status);
            var policy = await profiles.PublishPolicyAsync(context, new(1), 1, "publish-policy", default);
            var profile = await profiles.PublishProfileAsync(new(Guid.NewGuid(), Guid.NewGuid()),
                new(tenant, 0, policy.PublishedPolicy!.PolicyRevisionId, 1), "publish-profile", default);
            Assert.Equal(ProfileCommandStatus.Activated, (await profiles.ActivateProfileAsync(new(Guid.NewGuid(), Guid.NewGuid()),
                new(tenant, profile.Authority!.Revision, profile.Profile!.ProfileId), "activate-profile", default)).Status);
        }
        await GrantAsync(fgaAdmin, storeId, modelId, tenant, owner,
            ["member", "order_creator", "order_committer", "order_viewer", "manual_pricer", "quotation_creator", "quotation_issuer",
                "quotation_responder", "quotation_converter", "individual_creator", "individual_contact_editor", "individual_availability_editor"]);
        await GrantAsync(fgaAdmin, storeId, modelId, tenant, issuer, ["member", "invoice_issuer"]);
        await GrantAsync(fgaAdmin, storeId, modelId, other, issuer, ["member", "invoice_issuer", "invoice_viewer"]);
        var viewerRole = await GrantRoleAsync(fgaAdmin, storeId, modelId, tenant, viewer, "invoice_viewer");
        await GrantRoleAsync(fgaAdmin, storeId, modelId, tenant, roleIssuer, "invoice_issuer");
        var runtime = new NpgsqlConnectionStringBuilder(database.GetConnectionString()) { Username = "invoice_http_runtime", Password = "local-invoice-runtime-only" };
        using var identity = new WhiteLabelApiFactory();
        foreach (var account in new[] { owner, issuer, viewer, roleIssuer }) identity.Bind(account.ToString("D"), account);
        var ownerToken = identity.CreateToken(owner.ToString("D")); var issuerToken = identity.CreateToken(issuer.ToString("D"));
        var viewerToken = identity.CreateToken(viewer.ToString("D")); var roleIssuerToken = identity.CreateToken(roleIssuer.ToString("D"));
        var host = CreateHost(identity, runtime.ConnectionString, authorization, 20);
        try
        {
            Guid orgOrder; Guid quoteOrder; Guid individualOrder; Guid inactiveOrder; Guid draftOrder; Guid usdOrder;
            Guid unattributedOrder; Guid raceOrder; Guid burstOne; Guid burstTwo; Guid individual;
            using (var setupClient = host.CreateClient())
            {
                setupClient.Timeout = TimeSpan.FromSeconds(30);
                // Order commitment remains prerequisite setup at pool20; this regression qualifies invoice paths at pools1/2.
                var customer = await SendAsync(setupClient, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/individuals", ownerToken,
                    "individual", new { displayName = PrivateName, email = PrivateContact, phone = "9800000001" }, HttpStatusCode.Created);
                individual = customer.GetProperty("individualId").GetGuid();
                orgOrder = await PrepareOrderAsync(setupClient, tenant, ownerToken, "org-order", "NPR", organization, null, 2m, 17.25m);
                individualOrder = await PrepareOrderAsync(setupClient, tenant, ownerToken, "individual-order", "NPR", organization, program, 1m, 0m);
                inactiveOrder = await PrepareOrderAsync(setupClient, tenant, ownerToken, "inactive-order", "NPR", organization, program, 1m, 9m);
                draftOrder = await PrepareOrderAsync(setupClient, tenant, ownerToken, "draft-order", "NPR", organization, program, 1m, 1m, false);
                usdOrder = await PrepareOrderAsync(setupClient, tenant, ownerToken, "usd-order", "USD", organization, program, 1m, 5m);
                unattributedOrder = await PrepareOrderAsync(setupClient, tenant, ownerToken, "unattributed-order", "NPR", null, null, 1m, 1m);
                raceOrder = await PrepareOrderAsync(setupClient, tenant, ownerToken, "race-order", "NPR", organization, program, 1m, 13m);
                burstOne = await PrepareOrderAsync(setupClient, tenant, ownerToken, "burst-one", "NPR", organization, program, 1m, 17m);
                burstTwo = await PrepareOrderAsync(setupClient, tenant, ownerToken, "burst-two", "NPR", organization, program, 1m, 19m);
                var validUntil = DateTimeOffset.UtcNow.AddDays(1);
                validUntil = new DateTimeOffset(validUntil.UtcTicks - validUntil.UtcTicks % 10, TimeSpan.Zero);
                var quotation = await SendAsync(setupClient, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/quotations", ownerToken, "quote",
                    new
                    {
                        mode = "manual",
                        summary = "Invoice accepted quotation",
                        currencyCode = "NPR",
                        validUntil = validUntil.ToString("O"),
                        customerContext = new { organizationId = organization, programId = program },
                        lines = new[] { new { quantity = 1.5m, description = PrivateDescription, unitCode = "EA", unitPrice = 1.1111m },
                            new { quantity = 3m, description = "Second service", unitCode = "EA", unitPrice = 2m } }
                    }, HttpStatusCode.Created);
                var quoteId = quotation.GetProperty("quotationId").GetGuid();
                var quotePath = $"/api/v1/tenants/{tenant:D}/quotations/{quoteId:D}";
                var issued = await SendAsync(setupClient, HttpMethod.Post, quotePath + "/issue", ownerToken, "issue-quote", new { expectedVersion = 1 }, HttpStatusCode.OK);
                var revisionId = issued.GetProperty("currentIssued").GetProperty("revisionId").GetGuid();
                await SendAsync(setupClient, HttpMethod.Post, quotePath + "/accept", ownerToken, "accept-quote",
                    new { issuedRevisionId = revisionId, expectedVersion = 2, evidence = "Recorded customer agreement", customerClaim = "Customer accepted" }, HttpStatusCode.OK);
                var converted = await SendAsync(setupClient, HttpMethod.Post, quotePath + "/convert", ownerToken, "convert-quote",
                    new { issuedRevisionId = revisionId, expectedVersion = 3 }, HttpStatusCode.OK);
                quoteOrder = converted.GetProperty("conversion").GetProperty("originalOrder").GetProperty("orderId").GetGuid();
                await SendAsync(setupClient, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders/{quoteOrder:D}/commit", ownerToken,
                    "commit-quoted", new { expectedRevision = 1 }, HttpStatusCode.OK);
            }
            await host.DisposeAsync();
            host = CreateHost(identity, runtime.ConnectionString, authorization, maximumPoolSize);
            using var client = host.CreateClient(); client.Timeout = TimeSpan.FromSeconds(30);
            Assert.Equal(maximumPoolSize, host.Services.GetRequiredService<RuntimeDatabaseConfiguration>().MaximumPoolSize);
            Assert.Equal(maximumPoolSize, new NpgsqlConnectionStringBuilder(host.Services.GetRequiredService<NpgsqlDataSource>().ConnectionString).MaxPoolSize);
            var before = await FinancialSourceAsync(host, issuer, tenant, quoteOrder);
            Assert.Equal(7.6666m, before.Total);

            await SendAsync(client, HttpMethod.Post, OrderPath(tenant, orgOrder), ownerToken, "owner-no-billing",
                new { expectedOrderRevision = 2 }, HttpStatusCode.Forbidden);
            var orgIssue = await SendAsync(client, HttpMethod.Post, OrderPath(tenant, orgOrder), issuerToken, "org-issue",
                new { expectedOrderRevision = 2 }, HttpStatusCode.Created);
            var orgInvoice = CommandId(orgIssue);
            await SendAsync(client, HttpMethod.Get, InvoicePath(tenant, orgInvoice), issuerToken, null, null, HttpStatusCode.Forbidden);
            await SendAsync(client, HttpMethod.Get, InvoicePath(tenant, orgInvoice), ownerToken, null, null, HttpStatusCode.Forbidden);
            var orgRead = await SendAsync(client, HttpMethod.Get, OrderPath(tenant, orgOrder), viewerToken, null, null, HttpStatusCode.OK);
            Assert.Equal(34.5m, orgRead.GetProperty("total").GetDecimal());
            Assert.Equal("Organization", orgRead.GetProperty("debtor").GetProperty("kind").GetString());
            Assert.Equal(1, orgRead.GetProperty("organizationOrdinal").GetInt64());
            AssertDate(orgRead);
            await SendAsync(client, HttpMethod.Post, OrderPath(tenant, orgOrder), viewerToken, "viewer-no-issue", new { expectedOrderRevision = 2 }, HttpStatusCode.Forbidden);
            var programIssue = await SendAsync(client, HttpMethod.Post, OrderPath(tenant, quoteOrder), issuerToken, "program-issue",
                new { expectedOrderRevision = 2, debtor = new { kind = "Program" } }, HttpStatusCode.Created);
            var programInvoice = CommandId(programIssue);
            var programRead = await SendAsync(client, HttpMethod.Get, InvoicePath(tenant, programInvoice), viewerToken, null, null, HttpStatusCode.OK);
            Assert.Equal("Program", programRead.GetProperty("debtor").GetProperty("kind").GetString());
            Assert.Equal(program.ToString("D"), programRead.GetProperty("debtor").GetProperty("programId").GetString());
            Assert.Equal(organization.ToString("D"), programRead.GetProperty("organizationId").GetString());
            Assert.Equal(2, programRead.GetProperty("organizationOrdinal").GetInt64());
            Assert.Equal(before.Total, programRead.GetProperty("total").GetDecimal());
            Assert.Equal(1.6666m, programRead.GetProperty("lines")[0].GetProperty("lineTotal").GetDecimal());
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await FinancialSourceAsync(host, issuer, tenant, quoteOrder)));
            var individualBody = new { expectedOrderRevision = 2, debtor = new { kind = "Individual", individualId = individual } };
            var individualIssue = await SendAsync(client, HttpMethod.Post, OrderPath(tenant, individualOrder), issuerToken, "individual-issue", individualBody, HttpStatusCode.Created);
            var individualInvoice = CommandId(individualIssue);
            var individualRead = await SendAsync(client, HttpMethod.Get, InvoicePath(tenant, individualInvoice), viewerToken, null, null, HttpStatusCode.OK);
            Assert.Equal(0m, individualRead.GetProperty("total").GetDecimal());
            Assert.Equal("Individual", individualRead.GetProperty("debtor").GetProperty("kind").GetString());
            Assert.Equal(3, individualRead.GetProperty("organizationOrdinal").GetInt64());
            Assert.DoesNotContain(PrivateContact, individualRead.GetRawText(), StringComparison.Ordinal);
            await SendAsync(client, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/individuals/{individual:D}/contact", ownerToken, "contact-change",
                new { expectedRevision = 1, displayName = "Renamed individual", email = PrivateContact, phone = "9800000002" }, HttpStatusCode.OK);
            await SendAsync(client, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/individuals/{individual:D}/availability", ownerToken, "inactive",
                new { expectedRevision = 2, availability = "inactive" }, HttpStatusCode.OK);
            var rejected = await SendAsync(client, HttpMethod.Post, OrderPath(tenant, inactiveOrder), issuerToken, "inactive-issue", individualBody, HttpStatusCode.Conflict);
            Assert.Equal("invoice_debtor_ineligible", rejected.GetProperty("code").GetString());
            Assert.Equal(individualRead.GetRawText(), (await SendAsync(client, HttpMethod.Get, InvoicePath(tenant, individualInvoice), viewerToken, null, null, HttpStatusCode.OK)).GetRawText());
            foreach (var (order, revision, code) in new[]
            {
                (draftOrder, 1L, "invoice_order_not_committed"), (usdOrder, 2L, "invoice_currency_unsupported"),
                (unattributedOrder, 2L, "invoice_debtor_ineligible"), (orgOrder, 3L, "invoice_order_revision_conflict"),
            })
            {
                var failure = await SendAsync(client, HttpMethod.Post, OrderPath(tenant, order), issuerToken, "reject-" + order.ToString("N"),
                    new { expectedOrderRevision = revision }, HttpStatusCode.Conflict);
                Assert.Equal(code, failure.GetProperty("code").GetString());
            }
            await SendAsync(client, HttpMethod.Post, OrderPath(other, orgOrder), issuerToken, "wrong-tenant-order", new { expectedOrderRevision = 2 }, HttpStatusCode.NotFound);
            await SendAsync(client, HttpMethod.Get, InvoicePath(other, orgInvoice), issuerToken, null, null, HttpStatusCode.NotFound);
            var race = await Task.WhenAll(
                SendRawAsync(client, HttpMethod.Post, OrderPath(tenant, raceOrder), issuerToken, "race-direct", new { expectedOrderRevision = 2 }),
                SendRawAsync(client, HttpMethod.Post, OrderPath(tenant, raceOrder), roleIssuerToken, "race-role", new { expectedOrderRevision = 2 })).WaitAsync(TimeSpan.FromSeconds(30));
            try
            {
                Assert.Single(race, result => result.StatusCode == HttpStatusCode.Created);
                var losing = Assert.Single(race, result => result.StatusCode == HttpStatusCode.Conflict);
                using var loss = JsonDocument.Parse(await losing.Content.ReadAsStringAsync());
                Assert.Equal("invoice_order_already_invoiced", loss.RootElement.GetProperty("code").GetString());
                foreach (var response in race) { Assert.True(response.Headers.CacheControl?.NoStore); AssertPrivate(await response.Content.ReadAsStringAsync()); }
            }
            finally { foreach (var response in race) response.Dispose(); }
            if (maximumPoolSize == 2)
                await Task.WhenAll(
                    SendAsync(client, HttpMethod.Post, OrderPath(tenant, burstOne), issuerToken, "burst-one", new { expectedOrderRevision = 2 }, HttpStatusCode.Created),
                    SendAsync(client, HttpMethod.Post, OrderPath(tenant, burstTwo), roleIssuerToken, "burst-two", new { expectedOrderRevision = 2 }, HttpStatusCode.Created)).WaitAsync(TimeSpan.FromSeconds(30));

            client.Dispose(); await host.DisposeAsync();
            host = CreateHost(identity, runtime.ConnectionString, authorization, maximumPoolSize);
            using var restarted = host.CreateClient(); restarted.Timeout = TimeSpan.FromSeconds(30);
            using (var replay = await SendRawAsync(restarted, HttpMethod.Post, OrderPath(tenant, individualOrder), issuerToken, "individual-issue", individualBody))
            {
                Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
                Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
                using var replayBody = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
                Assert.Equal(individualInvoice, CommandId(replayBody.RootElement));
            }
            Assert.Equal(individualRead.GetRawText(), (await SendAsync(restarted, HttpMethod.Get, InvoicePath(tenant, individualInvoice), viewerToken, null, null, HttpStatusCode.OK)).GetRawText());
            var changed = await SendAsync(restarted, HttpMethod.Post, OrderPath(tenant, individualOrder), issuerToken, "individual-issue", new { expectedOrderRevision = 2 }, HttpStatusCode.Conflict);
            Assert.Equal("idempotency_key_conflict", changed.GetProperty("code").GetString());
            await DeleteTupleAsync(fgaAdmin, storeId, modelId, tenant, issuer, "invoice_issuer");
            await SendAsync(restarted, HttpMethod.Post, OrderPath(tenant, individualOrder), issuerToken, "individual-issue", individualBody, HttpStatusCode.Forbidden);
            await GrantAsync(fgaAdmin, storeId, modelId, tenant, issuer, ["invoice_issuer"]);
            await SendAsync(restarted, HttpMethod.Post, OrderPath(tenant, individualOrder), issuerToken, "individual-issue", individualBody, HttpStatusCode.OK);
            await ChangeRoleAssignmentAsync(fgaAdmin, storeId, modelId, tenant, viewer, viewerRole, false);
            await SendAsync(restarted, HttpMethod.Get, InvoicePath(tenant, orgInvoice), viewerToken, null, null, HttpStatusCode.Forbidden);
            await ChangeRoleAssignmentAsync(fgaAdmin, storeId, modelId, tenant, viewer, viewerRole, true);
            await using (var suspend = admin.CreateCommand())
            {
                suspend.CommandText = "UPDATE tenancy.memberships SET availability=2,revision=revision+1,suspended_at=now() WHERE tenant_id=@tenant AND account_id=@viewer;";
                suspend.Parameters.AddWithValue("tenant", tenant); suspend.Parameters.AddWithValue("viewer", viewer);
                await suspend.ExecuteNonQueryAsync();
            }
            await SendAsync(restarted, HttpMethod.Get, InvoicePath(tenant, orgInvoice), viewerToken, null, null, HttpStatusCode.Forbidden);
            await using (var activate = admin.CreateCommand())
            {
                activate.CommandText = "UPDATE tenancy.memberships SET availability=1,revision=revision+1,suspended_at=NULL,activated_at=now() WHERE tenant_id=@tenant AND account_id=@viewer;";
                activate.Parameters.AddWithValue("tenant", tenant); activate.Parameters.AddWithValue("viewer", viewer);
                await activate.ExecuteNonQueryAsync();
            }
            await using (var suspend = admin.CreateCommand())
            {
                suspend.CommandText = "UPDATE tenancy.memberships SET availability=2,revision=revision+1,suspended_at=now() WHERE tenant_id=@tenant AND account_id=@issuer;";
                suspend.Parameters.AddWithValue("tenant", tenant); suspend.Parameters.AddWithValue("issuer", issuer);
                await suspend.ExecuteNonQueryAsync();
            }
            await SendAsync(restarted, HttpMethod.Post, OrderPath(tenant, individualOrder), issuerToken, "individual-issue", individualBody, HttpStatusCode.Forbidden);
            await using (var activate = admin.CreateCommand())
            {
                activate.CommandText = "UPDATE tenancy.memberships SET availability=1,revision=revision+1,suspended_at=NULL,activated_at=now() WHERE tenant_id=@tenant AND account_id=@issuer;";
                activate.Parameters.AddWithValue("tenant", tenant); activate.Parameters.AddWithValue("issuer", issuer);
                await activate.ExecuteNonQueryAsync();
            }
            await server.StopAsync();
            await SendAsync(restarted, HttpMethod.Get, InvoicePath(tenant, orgInvoice), viewerToken, null, null, HttpStatusCode.ServiceUnavailable);
            await SendAsync(restarted, HttpMethod.Post, OrderPath(tenant, individualOrder), issuerToken, "individual-issue", individualBody, HttpStatusCode.ServiceUnavailable);
            Assert.Empty(failures);
            Assert.NotEmpty(diagnostics);
            foreach (var diagnostic in diagnostics) AssertPrivate(diagnostic);
        }
        finally { await host.DisposeAsync(); }
    }

    private async Task<Guid> PrepareOrderAsync(HttpClient client, Guid tenant, string token, string key, string currency,
        Guid? organization, Guid? program, decimal quantity, decimal price, bool commit = true)
    {
        var order = await SendAsync(client, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders", token, key,
            new
            {
                summary = "Invoice source order",
                currencyCode = currency,
                lines = new[] { new { description = PrivateDescription, quantity, unitCode = "EA", unitPrice = price } },
                customerContext = organization is null ? null : new { organizationId = organization, programId = program }
            }, HttpStatusCode.Created);
        var id = order.GetProperty("orderId").GetGuid();
        if (commit) await SendAsync(client, HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders/{id:D}/commit", token,
            "commit-" + key, new { expectedRevision = 1 }, HttpStatusCode.OK);
        return id;
    }

    private WebApplicationFactory<Program> CreateHost(WhiteLabelApiFactory identity, string connectionString,
        OpenFgaTenantAuthorization authorization, int pool) => identity.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ConnectionStrings:PrimaryDatabase", connectionString);
        builder.UseSetting("Database:MaximumPoolSize", pool.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureTestServices(services =>
        {
            services.Insert(0, ServiceDescriptor.Singleton<IExceptionHandler>(new CaptureFailures(failures)));
            services.RemoveAll<ILoggerProvider>(); services.AddSingleton<ILoggerProvider>(new CapturedLogs(diagnostics));
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(provider => RuntimeDatabaseConfiguration.From(provider.GetRequiredService<IConfiguration>()).CreateDataSource());
            services.RemoveAll<ITenantMembershipDirectory>(); services.AddScoped<ITenantMembershipDirectory, PostgresTenantMembershipDirectory>();
            services.RemoveAll<OpenFgaTenantAuthorization>(); services.AddSingleton(authorization);
            services.RemoveAll<ITenantInvoiceAuthorization>(); services.AddSingleton<ITenantInvoiceAuthorization>(authorization);
            services.RemoveAll<ITenantOrderAuthorization>(); services.AddSingleton<ITenantOrderAuthorization>(authorization);
            services.RemoveAll<ITenantCustomerAuthorization>(); services.AddSingleton<ITenantCustomerAuthorization>(authorization);
            services.RemoveAll<ICustomerStore>(); services.AddScoped<ICustomerStore, PostgresCustomerStore>();
            services.RemoveAll<ICustomerIndividualStore>(); services.AddScoped<ICustomerIndividualStore, PostgresCustomerStore>();
            services.RemoveAll<ICustomerIndividualContactStore>(); services.AddScoped<ICustomerIndividualContactStore, PostgresCustomerStore>();
            services.RemoveAll<IOrderDraftStore>(); services.AddScoped<IOrderDraftStore>(provider => provider.GetRequiredService<PostgresOrderDraftStore>());
        });
    });

    private static async Task<OrderFinancialSourceFacts> FinancialSourceAsync(WebApplicationFactory<Program> host, Guid actor, Guid tenant, Guid order)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = await scope.ServiceProvider.GetRequiredService<ResolveTenantContext>().ExecuteAsync(actor, tenant, default)
            ?? throw new InvalidOperationException("Invoice source context was not resolved.");
        await using var connection = await scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        return await scope.ServiceProvider.GetRequiredService<PostgresOrderDraftStore>().ReadFinancialSourceInTransactionAsync(connection, transaction, context, order, default)
            ?? throw new InvalidOperationException("Committed financial source is missing.");
    }

    private static Guid CommandId(JsonElement response)
    {
        Assert.Single(response.EnumerateObject()); AssertPrivate(response.GetRawText());
        return response.GetProperty("invoiceId").GetGuid();
    }
    private static void AssertDate(JsonElement response)
    {
        var issued = response.GetProperty("issuedAt").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, issued.Offset);
        Assert.Equal(DateOnly.FromDateTime(issued.UtcDateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), response.GetProperty("businessDate").GetString());
    }
    private static string OrderPath(Guid tenant, Guid order) => $"/api/v1/tenants/{tenant:D}/orders/{order:D}/invoice";
    private static string InvoicePath(Guid tenant, Guid invoice) => $"/api/v1/tenants/{tenant:D}/invoices/{invoice:D}";
    private static void AssertPrivate(string text)
    {
        foreach (var canary in new[] { PrivateName, PrivateDescription, PrivateContact }) Assert.DoesNotContain(canary, text, StringComparison.Ordinal);
    }
    private async Task<JsonElement> SendAsync(HttpClient client, HttpMethod method, string path, string token, string? key, object? body, HttpStatusCode expected)
    {
        using var response = await SendRawAsync(client, method, path, token, key, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{method} {path}: {(int)response.StatusCode}; {text}; {failures.LastOrDefault()}");
        Assert.True(response.Headers.CacheControl?.NoStore);
        if (!response.IsSuccessStatusCode) AssertPrivate(text);
        using var document = JsonDocument.Parse(text); return document.RootElement.Clone();
    }
    private static async Task<HttpResponseMessage> SendRawAsync(HttpClient client, HttpMethod method, string path, string token, string? key, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    private static async Task<string> PostIdAsync(HttpClient client, string path, object body, string property)
    {
        using var response = await client.PostAsJsonAsync(path, body); response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.GetProperty(property).GetString()!;
    }
    private static async Task GrantAsync(HttpClient client, string store, string model, Guid tenant, Guid actor, string[] relations)
    {
        using var response = await client.PostAsJsonAsync($"/stores/{store}/write", new
        {
            writes = new { tuple_keys = relations.Select(relation => new { user = $"user:{actor:N}", relation, @object = $"tenant:{tenant:N}" }).ToArray() },
            authorization_model_id = model,
        }); response.EnsureSuccessStatusCode();
    }
    private static async Task<Guid> GrantRoleAsync(HttpClient client, string store, string model, Guid tenant, Guid actor, string relation)
    {
        var role = Guid.NewGuid();
        using var response = await client.PostAsJsonAsync($"/stores/{store}/write", new
        {
            writes = new
            {
                tuple_keys = new[]
            {
                new { user = $"user:{actor:N}", relation = "assignee", @object = $"role:{tenant:N}_{role:N}" },
                new { user = $"role:{tenant:N}_{role:N}#assignee", relation, @object = $"tenant:{tenant:N}" },
            }
            },
            authorization_model_id = model,
        }); response.EnsureSuccessStatusCode();
        return role;
    }
    private static async Task ChangeRoleAssignmentAsync(HttpClient client, string store, string model, Guid tenant, Guid actor, Guid role, bool grant)
    {
        var tuples = new[] { new { user = $"user:{actor:N}", relation = "assignee", @object = $"role:{tenant:N}_{role:N}" } };
        object body = grant
            ? new { writes = new { tuple_keys = tuples }, authorization_model_id = model }
            : new { deletes = new { tuple_keys = tuples }, authorization_model_id = model };
        using var response = await client.PostAsJsonAsync($"/stores/{store}/write", body);
        response.EnsureSuccessStatusCode();
    }
    private static async Task DeleteTupleAsync(HttpClient client, string store, string model, Guid tenant, Guid actor, string relation)
    {
        using var response = await client.PostAsJsonAsync($"/stores/{store}/write", new
        {
            deletes = new { tuple_keys = new[] { new { user = $"user:{actor:N}", relation, @object = $"tenant:{tenant:N}" } } },
            authorization_model_id = model,
        }); response.EnsureSuccessStatusCode();
    }
    private sealed class CaptureFailures(ConcurrentQueue<Exception> failures) : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
        { failures.Enqueue(error); return ValueTask.FromResult(false); }
    }
    private sealed class CapturedLogs(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Sink(messages);
        public void Dispose() { }
        private sealed class Sink(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
            { messages.Enqueue(formatter(state, error)); if (error is not null) messages.Enqueue(error.ToString()); }
        }
    }
    private static string RepositoryRoot()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "Application.slnx"))) return path.FullName;
        throw new InvalidOperationException("Repository source root was not found.");
    }
}
