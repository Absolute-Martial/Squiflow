using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.CoreApi.Health;
using Application.CoreApi.ImportExecution;
using Application.CoreApi.Storage;
using Application.Customers;
using Application.ObjectStorage;
using Application.Tenancy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class CustomerDuplicateImportEndpointTests
{
    [Theory]
    [InlineData("imports", "text/csv")]
    [InlineData("duplicates/search", "application/json")]
    [InlineData("duplicates/resolve", "application/json")]
    [InlineData("duplicates/consolidate", "application/json")]
    public async Task DenialPrecedesParsingAndAnyCapabilityEffects(string path, string contentType)
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor);
        factory.AddTenantMembership(actor, tenant, "Import security tenant");
        var store = new NeverCalledCustomerImportStore();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportStore>();
            services.AddSingleton<ICustomerImportStore>(store);
            services.RemoveAll<ICustomerImportAuthority>();
            services.AddSingleton<ICustomerImportAuthority>(new DeniedImportAuthority());
        }));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/{path}")
        { Content = new StringContent("not a valid payload", Encoding.UTF8, contentType) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(subject));
        request.Headers.Add("Idempotency-Key", "denied-before-parse");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task ImportPlanReadRequiresCurrentTenantMembershipBeforeLookup()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor);
        var store = new NeverCalledCustomerImportStore();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportStore>(); services.AddSingleton<ICustomerImportStore>(store);
        }));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/tenants/{tenant:D}/customers/imports/{Guid.NewGuid():D}/rows?limit=10000");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(subject));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task PlanResponseIsBoundedMetadataAndRowsRejectAnOversizedPage()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var store = new PlanningEndpointStore();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportStore>(); services.AddSingleton<ICustomerImportStore>(store);
            services.RemoveAll<ITenantCustomerAuthorization>(); services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
            services.RemoveAll<ICustomerImportAuthority>(); services.AddSingleton<ICustomerImportAuthority>(new DeniedImportAuthority());
        }));
        using var client = configured.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));
        var importPath = $"/api/v1/tenants/{tenant:D}/customers/imports";
        using var request = new HttpRequestMessage(HttpMethod.Post, importPath)
        { Content = new StringContent("name\n" + string.Concat(Enumerable.Range(0, 80).Select(index => $"Synthetic Person {index}\n")), Encoding.UTF8, "text/csv") };
        request.Headers.Add("Idempotency-Key", "plan-only");
        using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(80, metadata.RootElement.GetProperty("rowCount").GetInt32());
        Assert.False(metadata.RootElement.TryGetProperty("rows", out _));
        var id = metadata.RootElement.GetProperty("importId").GetGuid();
        // The created import URL is the requested import resource plus the created identity.
        Assert.Equal($"{request.RequestUri!.AbsolutePath}/{id:D}", response.Headers.Location!.OriginalString);
        using var invalidPage = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/imports/{id:D}/rows?limit=10000");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode); Assert.Equal(0, store.ReadCalls);
        using var page = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/imports/{id:D}/rows?limit=25");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var rows = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        Assert.Equal(25, rows.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task InvalidRetentionElectionIsRejectedBeforePlanning()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var store = new NeverCalledCustomerImportStore();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportStore>(); services.AddSingleton<ICustomerImportStore>(store);
            services.RemoveAll<ITenantCustomerAuthorization>(); services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
        }));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/imports")
        { Content = new StringContent("name\nNot retained\n", Encoding.UTF8, "text/csv") };
        request.Headers.Authorization = new("Bearer", factory.CreateToken(subject));
        request.Headers.Add("Idempotency-Key", "invalid-retention");
        request.Headers.Add("X-Tenant-Import-Retention", "permanent");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("import_retention_invalid", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData(1, 503)] // staged
    [InlineData(2, 503)] // quarantined
    [InlineData(4, 503)] // unavailable
    [InlineData(5, 503)] // orphaned
    [InlineData(6, 404)] // retired
    [InlineData(7, 503)] // retirement-pending
    public async Task NonAvailableSourceStatesNeverOpenObjectStoreBytes(int rawState, int expectedStatus)
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        var importId = Guid.NewGuid();
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var source = new SourceEndpointStore(Source(rawState));
        var objectStore = new CountingObjectStore();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportSourceStore>(); services.AddSingleton<ICustomerImportSourceStore>(source);
            services.RemoveAll<IObjectStore>(); services.AddSingleton<IObjectStore>(objectStore);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedTimeProvider(SourceNow));
        }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/imports/{importId:D}/source");

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        Assert.Equal(1, source.Reads);
        Assert.Equal(0, objectStore.Reads);
    }

    [Fact]
    public async Task SourceReadRequiresCurrentTenantMembershipBeforeMetadataOrProvider()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor);
        var source = new SourceEndpointStore(Source((int)CustomerImportSourceState.Available));
        var objectStore = new CountingObjectStore();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportSourceStore>(); services.AddSingleton<ICustomerImportSourceStore>(source);
            services.RemoveAll<IObjectStore>(); services.AddSingleton<IObjectStore>(objectStore);
        }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/imports/{Guid.NewGuid():D}/source");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, source.Reads);
        Assert.Equal(0, objectStore.Reads);
    }

    [Fact]
    public async Task SourceReadRequiresImportPermissionBeforeMetadataOrProvider()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var source = new SourceEndpointStore(Source((int)CustomerImportSourceState.Available));
        var objectStore = new CountingObjectStore();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportSourceStore>(); services.AddSingleton<ICustomerImportSourceStore>(source);
            services.RemoveAll<IObjectStore>(); services.AddSingleton<IObjectStore>(objectStore);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: false));
        }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/imports/{Guid.NewGuid():D}/source");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, source.Reads);
        Assert.Equal(0, objectStore.Reads);
    }

    [Fact]
    public async Task ExpiredAvailableSourceIsBlockedBeforeObjectStoreRead()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        var importId = Guid.NewGuid();
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var source = new SourceEndpointStore(Source((int)CustomerImportSourceState.Available,
            expiresAt: SourceNow.AddMinutes(-1)));
        var objectStore = new CountingObjectStore();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportSourceStore>(); services.AddSingleton<ICustomerImportSourceStore>(source);
            services.RemoveAll<IObjectStore>(); services.AddSingleton<IObjectStore>(objectStore);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedTimeProvider(SourceNow));
        }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/imports/{importId:D}/source");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("import_source_unavailable", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(1, source.Reads);
        Assert.Equal(0, objectStore.Reads);
    }

    [Fact]
    public async Task AvailableSourceStreamsOnlyAfterMetadataAuthorization()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        var importId = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes("name\nAvailable source\n");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var source = new SourceEndpointStore(Source((int)CustomerImportSourceState.Available, bytes.Length));
        var objectStore = new CountingObjectStore(new ObjectStoreReadResult(
            ObjectStoreReadOutcome.Opened, new MemoryStream(bytes),
            new(ObjectStoreKey.Create("imports/source.csv"), bytes.Length, new string('a', 64), "text/csv; charset=utf-8")));
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportSourceStore>(); services.AddSingleton<ICustomerImportSourceStore>(source);
            services.RemoveAll<IObjectStore>(); services.AddSingleton<IObjectStore>(objectStore);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedTimeProvider(SourceNow));
        }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/imports/{importId:D}/source");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(1, source.Reads);
        Assert.Equal(1, objectStore.Reads);
    }

    [Fact]
    public async Task ArchivedAvailableSourceWithoutExpiryCanBeRead()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        var importId = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes("name\nArchived source\n");
        var content = new TrackingStream(bytes);
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var source = new SourceEndpointStore(Source((int)CustomerImportSourceState.Available, bytes.Length,
            CustomerImportRetention.TenantArchived));
        var objectStore = new CountingObjectStore(new ObjectStoreReadResult(
            ObjectStoreReadOutcome.Opened, content,
            new(ObjectStoreKey.Create("imports/source.csv"), bytes.Length, new string('a', 64), "text/csv; charset=utf-8")));
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportSourceStore>(); services.AddSingleton<ICustomerImportSourceStore>(source);
            services.RemoveAll<IObjectStore>(); services.AddSingleton<IObjectStore>(objectStore);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedTimeProvider(SourceNow));
        }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/imports/{importId:D}/source");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.True(content.Disposed);
        Assert.Equal(1, source.Reads);
        Assert.Equal(1, objectStore.Reads);
    }

    [Theory]
    [InlineData(ObjectStoreReadOutcome.NotFound)]
    [InlineData(ObjectStoreReadOutcome.PermissionDenied)]
    [InlineData(ObjectStoreReadOutcome.Unavailable)]
    [InlineData(ObjectStoreReadOutcome.TimedOut)]
    [InlineData(ObjectStoreReadOutcome.Corrupt)]
    public async Task ProviderReadFailuresReturnNoStore503AndDisposeSuppliedContent(ObjectStoreReadOutcome outcome)
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        var importId = Guid.NewGuid();
        var content = new TrackingStream(Encoding.UTF8.GetBytes("name\nProvider failure\n"));
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var source = new SourceEndpointStore(Source((int)CustomerImportSourceState.Available));
        var objectStore = new CountingObjectStore(new ObjectStoreReadResult(outcome, content));
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportSourceStore>(); services.AddSingleton<ICustomerImportSourceStore>(source);
            services.RemoveAll<IObjectStore>(); services.AddSingleton<IObjectStore>(objectStore);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedTimeProvider(SourceNow));
        }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/imports/{importId:D}/source");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("import_source_unavailable", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.True(content.Disposed);
        Assert.Equal(1, source.Reads);
        Assert.Equal(1, objectStore.Reads);
    }

    [Fact]
    public async Task AcceptedWorkPointsAtTheAcceptedImportResource()
    {
        using var factory = new WhiteLabelApiFactory(); var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var store = new AcceptingImportStore();
        var current = await factory.Server.Services.GetRequiredService<ResolveTenantContext>()
            .ExecuteAsync(actor, tenant, CancellationToken.None);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CustomerImportExecutionState>();
            var execution = new CustomerImportExecutionState(); execution.SetAccepting(true);
            services.AddSingleton(execution);
            services.RemoveAll<ICustomerImportStore>(); services.AddSingleton<ICustomerImportStore>(store);
            services.RemoveAll<ITenantCustomerAuthorization>(); services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
            services.RemoveAll<ICustomerImportAuthority>(); services.AddSingleton<ICustomerImportAuthority>(new AllowedImportAuthority(current!));
        }));
        using var client = configured.CreateClient();
        var importPath = $"/api/v1/tenants/{tenant:D}/customers/imports";
        var importId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{importPath}/{importId:D}/accept")
        { Content = JsonContent.Create(new { decisions = Array.Empty<object>() }) };
        request.Headers.Authorization = new("Bearer", factory.CreateToken(subject)); request.Headers.Add("Idempotency-Key", "accept-work");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        // The accepted work URL is the requested import resource, not the accept action.
        Assert.Equal($"{new Uri(request.RequestUri!, "../").AbsolutePath}{importId:D}",
            response.Headers.Location!.OriginalString);
        Assert.Equal(importId, store.Requests.Single().ImportId);
    }

    [Fact]
    public async Task AcceptanceRechecksNeutralCurrentAuthorityBeforeDurableWork()
    {
        using var factory = new WhiteLabelApiFactory(); var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var store = new NeverCalledCustomerImportStore(); var authority = new DeniedImportAuthority();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CustomerImportExecutionState>();
            var execution = new CustomerImportExecutionState(); execution.SetAccepting(true);
            services.AddSingleton(execution);
            services.RemoveAll<ICustomerImportStore>(); services.AddSingleton<ICustomerImportStore>(store);
            services.RemoveAll<ITenantCustomerAuthorization>(); services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
            services.RemoveAll<ICustomerImportAuthority>(); services.AddSingleton<ICustomerImportAuthority>(authority);
        }));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/imports/{Guid.NewGuid():D}/accept")
        { Content = JsonContent.Create(new { decisions = Array.Empty<object>() }) };
        request.Headers.Authorization = new("Bearer", factory.CreateToken(subject)); request.Headers.Add("Idempotency-Key", "accept-denied");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(1, authority.Checks); Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task DisabledExecutorRejectsAcceptanceBeforeParsingOrDurableEffects()
    {
        using var factory = new WhiteLabelApiFactory(); var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var store = new NeverCalledCustomerImportStore();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportStore>(); services.AddSingleton<ICustomerImportStore>(store);
            services.RemoveAll<ITenantCustomerAuthorization>(); services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
        }));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/imports/{Guid.NewGuid():D}/accept")
        { Content = new StringContent("not JSON", Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new("Bearer", factory.CreateToken(subject));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, store.Calls);
        Assert.Contains("import_executor_unavailable", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // Object storage is not the deleter. Raw expiring import PII is deleted only by the hosted
    // customer import executor, so enabling one without the other must fail composition loudly
    // rather than leave readiness green over an unbounded retention exposure.
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void ObjectStorageCompositionRequiresTheImportExecutorThatDeletesRawSources(
        bool objectStorageEnabled, bool executionEnabled, bool invalid)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:PrimaryDatabase"] = "Host=localhost;Database=composition_fixture",
            ["Database:ConnectionMode"] = "Direct",
            ["Database:MaximumPoolSize"] = "20",
            ["Database:MinimumPoolSize"] = "0",
            ["Database:ConnectionIdleLifetimeSeconds"] = "300",
            ["Database:ConnectionPruningIntervalSeconds"] = "10",
            ["Database:ConnectionLifetimeSeconds"] = "3600",
            ["Database:CommandTimeoutSeconds"] = "15",
            ["CustomerImports:Execution:Enabled"] = executionEnabled ? "true" : "false",
        };
        if (objectStorageEnabled)
        {
            values["ObjectStorage:Enabled"] = "true";
            values["ObjectStorage:Endpoint"] = "https://s3.hf.co";
            values["ObjectStorage:Namespace"] = "example-namespace";
            values["ObjectStorage:Bucket"] = "example-bucket";
            values["ObjectStorage:AccessKeyId"] = "example-access-key";
            values["ObjectStorage:SecretAccessKey"] = "example-secret-key";
            values["ObjectStorage:ProviderScope"] = "tenant";
            values["ObjectStorage:MaximumRetainedBytes"] = "1048576";
            values["ObjectStorage:RequestTimeoutSeconds"] = "30";
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var objectStorage = HuggingFaceObjectStoreConfiguration.From(configuration);
        var services = new ServiceCollection();

        if (invalid)
        {
            var error = Assert.Throws<InvalidOperationException>(() => services.AddObjectStorage(objectStorage)
                .AddCustomerImportExecution(configuration, RuntimeDatabaseConfiguration.From(configuration), objectStorage));
            Assert.Contains("CustomerImports:Execution:Enabled", error.Message, StringComparison.Ordinal);
            return;
        }

        services.AddObjectStorage(objectStorage)
            .AddCustomerImportExecution(configuration, RuntimeDatabaseConfiguration.From(configuration), objectStorage);
    }

    [Theory]
    [InlineData(false, false, HealthStatus.Healthy)]
    [InlineData(true, false, HealthStatus.Unhealthy)]
    [InlineData(true, true, HealthStatus.Healthy)]
    public async Task ImportExecutionReadinessReportsTheDeleterAdmissionStateOnlyWhenEnabled(
        bool enabled, bool accepting, HealthStatus expected)
    {
        var state = new CustomerImportExecutionState();
        state.SetAccepting(accepting);
        var check = new CustomerImportExecutionReadinessCheck(new(enabled, TimeSpan.FromSeconds(5), 10, 25, TimeSpan.FromSeconds(20)), state);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task RunBatchRefusesWorkWhileTheExecutorDoesNotAdmitItBeforeParsingOrClaiming()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var context = ActiveContext(tenant, actor);
        var work = new BatchEndpointWorkStore(context);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CustomerImportExecutionState>();
            var execution = new CustomerImportExecutionState(); execution.SetAccepting(false);
            services.AddSingleton(execution);
            services.RemoveAll<ICustomerImportWorkStore>(); services.AddSingleton<ICustomerImportWorkStore>(work);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
        }));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/imports/run-batch")
        { Content = new StringContent("not JSON", Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("import_executor_unavailable", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, work.Claims);
    }

    [Fact]
    public async Task RunBatchReportsAuthorityDeniedWithoutProcessingAnyRow()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var context = ActiveContext(tenant, actor);
        var work = new BatchEndpointWorkStore(context);
        var authority = new DeniedImportAuthority();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CustomerImportExecutionState>();
            var execution = new CustomerImportExecutionState(); execution.SetAccepting(true);
            services.AddSingleton(execution);
            services.RemoveAll<ICustomerImportWorkStore>(); services.AddSingleton<ICustomerImportWorkStore>(work);
            services.RemoveAll<ICustomerImportAuthority>(); services.AddSingleton<ICustomerImportAuthority>(authority);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
        }));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/imports/run-batch")
        { Content = JsonContent.Create(new { limit = 25 }) };
        request.Headers.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, work.Claims);
        Assert.Equal(1, authority.Checks);
        Assert.Equal(0, work.Processed);
    }

    [Fact]
    public async Task RunBatchRunsOneBoundedBatchWhileTheExecutorAdmitsWork()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var context = ActiveContext(tenant, actor);
        var work = new BatchEndpointWorkStore(context);
        var authority = new GrantedImportAuthority(context);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CustomerImportExecutionState>();
            var execution = new CustomerImportExecutionState(); execution.SetAccepting(true);
            services.AddSingleton(execution);
            services.RemoveAll<ICustomerImportWorkStore>(); services.AddSingleton<ICustomerImportWorkStore>(work);
            services.RemoveAll<ICustomerImportAuthority>(); services.AddSingleton<ICustomerImportAuthority>(authority);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
        }));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/imports/run-batch")
        { Content = JsonContent.Create(new { limit = 25 }) };
        request.Headers.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(1, work.Claims);
        Assert.Equal(1, work.Processed);
        Assert.Equal(1, work.Released);
        Assert.Equal(work.WorkId, work.ClaimedWorkId);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(work.WorkId, body.RootElement.GetProperty("workId").GetGuid());
        Assert.Equal((int)CustomerImportBatchStatus.Completed, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(0, body.RootElement.GetProperty("rowsProcessed").GetInt32());
    }

    // A provider outage is a dependency failure, not an internal error: both import execution
    // entry points declare 503 and must never fall through to the generic 500 handler.
    [Fact]
    public async Task AuthorizationProviderOutageOnAcceptReturnsServiceUnavailableNotInternalError()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var store = new NeverCalledCustomerImportStore();
        var authority = new UnavailableImportAuthority();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CustomerImportExecutionState>();
            var execution = new CustomerImportExecutionState(); execution.SetAccepting(true);
            services.AddSingleton(execution);
            services.RemoveAll<ICustomerImportStore>(); services.AddSingleton<ICustomerImportStore>(store);
            services.RemoveAll<ICustomerImportAuthority>(); services.AddSingleton<ICustomerImportAuthority>(authority);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
        }));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/tenants/{tenant:D}/customers/imports/{Guid.NewGuid():D}/accept")
        { Content = JsonContent.Create(new { decisions = Array.Empty<object>() }) };
        request.Headers.Authorization = new("Bearer", factory.CreateToken(subject));
        request.Headers.Add("Idempotency-Key", "accept-authorization-outage");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("authorization_unavailable", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(1, authority.Checks);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task AuthorizationProviderOutageOnRunBatchReturnsServiceUnavailableNotInternalError()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var context = ActiveContext(tenant, actor);
        var work = new BatchEndpointWorkStore(context);
        var authority = new UnavailableImportAuthority();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CustomerImportExecutionState>();
            var execution = new CustomerImportExecutionState(); execution.SetAccepting(true);
            services.AddSingleton(execution);
            services.RemoveAll<ICustomerImportWorkStore>(); services.AddSingleton<ICustomerImportWorkStore>(work);
            services.RemoveAll<ICustomerImportAuthority>(); services.AddSingleton<ICustomerImportAuthority>(authority);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
        }));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/imports/run-batch")
        { Content = JsonContent.Create(new { limit = 25 }) };
        request.Headers.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("authorization_unavailable", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(1, authority.Checks);
        Assert.Equal(0, work.Processed);
    }

    [Fact]
    public async Task ImportTemplateIsRefusedWithoutCurrentImportPermission()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: false));
        }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/import-template");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain(CustomerImportCsv.Template, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportTemplateReturnsTheCanonicalCsvToCurrentImportAuthority()
    {
        using var factory = new WhiteLabelApiFactory();
        var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Import tenant");
        var work = new BatchEndpointWorkStore(ActiveContext(tenant, actor));
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerImportWorkStore>(); services.AddSingleton<ICustomerImportWorkStore>(work);
            services.RemoveAll<ITenantCustomerAuthorization>();
            services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(importAllowed: true));
        }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));

        using var response = await client.GetAsync($"/api/v1/tenants/{tenant:D}/customers/import-template");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(CustomerImportCsv.Template, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, work.Claims);
    }

    [Fact]
    public async Task ResolvePermissionCannotReachTheConsolidationCommand()
    {
        using var factory = new WhiteLabelApiFactory(); var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Duplicate tenant");
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantCustomerAuthorization>(); services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(resolveAllowed: true));
        }));
        using var client = configured.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));
        using var refused = await client.PostAsJsonAsync($"/api/v1/tenants/{tenant:D}/customers/duplicates/consolidate", new { });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/duplicates/resolve")
        { Content = JsonContent.Create(new { customerId = Guid.NewGuid(), otherCustomerId = Guid.NewGuid(), expectedCustomerRevision = 1, expectedOtherRevision = 1, outcome = "ConsolidateInto" }) };
        request.Headers.Add("Idempotency-Key", "resolve-no-consolidate");
        using var invalid = await client.SendAsync(request); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private const long BatchAuthorityRevision = 12;
    [Fact]
    public async Task ResolveRefusalOnARedirectedPairIsAConflictAndRecordsNothing()
    {
        using var factory = new WhiteLabelApiFactory(); var actor = Guid.NewGuid(); var tenant = Guid.NewGuid(); var subject = Guid.NewGuid().ToString("N");
        factory.Bind(subject, actor); factory.AddTenantMembership(actor, tenant, "Duplicate tenant");
        var store = new RedirectedPairResolutionStore();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantCustomerAuthorization>(); services.AddSingleton<ITenantCustomerAuthorization>(new EndpointCustomerAuthority(resolveAllowed: true));
            services.RemoveAll<ICustomerDuplicateResolutionStore>(); services.AddSingleton<ICustomerDuplicateResolutionStore>(store);
        }));
        using var client = configured.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(subject));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/duplicates/resolve")
        { Content = JsonContent.Create(new { customerId = Guid.NewGuid(), otherCustomerId = Guid.NewGuid(), expectedCustomerRevision = 1, expectedOtherRevision = 1, outcome = "KeepSeparate" }) };
        request.Headers.Add("Idempotency-Key", "resolve-redirected");
        using var refused = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("AlreadyRedirected", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(1, store.Calls);
    }

    private sealed class RedirectedPairResolutionStore : ICustomerDuplicateResolutionStore
    {
        public int Calls { get; private set; }
        public Task<ResolveCustomerDuplicateResult> ResolveDuplicateAsync(TenantContext context,
            ResolveCustomerDuplicateRequest request, string idempotencyKey, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(new ResolveCustomerDuplicateResult(ResolveCustomerDuplicateStatus.AlreadyRedirected, null)); }
    }

    private sealed class DeniedImportAuthority : ICustomerImportAuthority
    {
        public int Checks { get; private set; }
        public Task<CustomerImportAuthoritySnapshot?> CheckAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        { Checks++; return Task.FromResult<CustomerImportAuthoritySnapshot?>(null); }
    }

    private sealed class UnavailableImportAuthority : ICustomerImportAuthority
    {
        public int Checks { get; private set; }
        public Task<CustomerImportAuthoritySnapshot?> CheckAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        { Checks++; throw new AuthorizationProviderUnavailableException("Synthetic outage.", new HttpRequestException("Synthetic outage.")); }
    }

    private sealed class GrantedImportAuthority(TenantContext context) : ICustomerImportAuthority
    {
        public int Checks { get; private set; }
        public Task<CustomerImportAuthoritySnapshot?> CheckAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        { Checks++; return Task.FromResult<CustomerImportAuthoritySnapshot?>(new(context, BatchAuthorityRevision)); }
    }

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);
    }

    private static TenantContext ActiveContext(Guid tenantId, Guid accountId) =>
        new ResolveTenantContext(new ActiveMembershipDirectory())
            .ExecuteAsync(accountId, tenantId, CancellationToken.None).Result!;

    // Records exactly which durable effects the protected batch route caused.
    private sealed class BatchEndpointWorkStore(TenantContext context) : ICustomerImportWorkStore
    {
        public int Claims, Processed, Released;
        public Guid WorkId { get; } = Guid.NewGuid();
        public Guid? ClaimedWorkId { get; private set; }

        public Task<CustomerImportClaim?> ClaimImportAsync(Guid tenantId, Guid workerId, TimeSpan lease, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Claims++;
            ClaimedWorkId = WorkId;
            return Task.FromResult<CustomerImportClaim?>(new(tenantId, WorkId, Guid.NewGuid(), context.AccountId,
                BatchAuthorityRevision, workerId, 1, DateTimeOffset.UtcNow + lease));
        }

        public Task<CustomerImportRowExecutionStatus> ProcessNextImportRowAsync(CustomerImportClaim claim,
            TenantContext currentContext, CancellationToken cancellationToken)
        {
            Processed++;
            return Task.FromResult(CustomerImportRowExecutionStatus.Complete);
        }

        public Task ReleaseImportClaimAsync(CustomerImportClaim claim, bool authorityDenied, CancellationToken cancellationToken)
        {
            Released++;
            return Task.CompletedTask;
        }
    private sealed class AllowedImportAuthority(TenantContext current) : ICustomerImportAuthority
    {
        public Task<CustomerImportAuthoritySnapshot?> CheckAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<CustomerImportAuthoritySnapshot?>(
                tenantId == current.TenantId && accountId == current.AccountId
                    ? new(current, 1) : null);
    }

    private sealed class AcceptingImportStore : ICustomerImportStore
    {
        public List<ExecuteCustomerImportRequest> Requests { get; } = [];
        public Task<ExecuteCustomerImportResult> ExecuteImportAsync(TenantContext context, ExecuteCustomerImportRequest request,
            string idempotencyKey, long authorizationRevision, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new ExecuteCustomerImportResult(
                new(Guid.NewGuid(), request.ImportId, CustomerImportWorkStatus.Accepted, 0, 0,
                    new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero), null, null), []));
        }
        private static T Fail<T>() => throw new InvalidOperationException("This import store double accepts work only.");
        public Task<CreateCustomerImportResult> CreateImportPlanAsync(TenantContext context, CustomerImportPlan plan,
            string idempotencyKey, CancellationToken cancellationToken) => Fail<Task<CreateCustomerImportResult>>();
        public Task<CustomerImportSummary?> ReadImportSummaryAsync(TenantContext context, Guid importId, CancellationToken cancellationToken) => Fail<Task<CustomerImportSummary?>>();
        public Task<CustomerImportRowPage> ReadImportRowsPageAsync(TenantContext context, Guid importId, int afterRowNumber,
            int limit, CancellationToken cancellationToken) => Fail<Task<CustomerImportRowPage>>();
    }

    private sealed class EndpointCustomerAuthority(bool importAllowed = false, bool resolveAllowed = false) : ITenantCustomerAuthorization
    {
        public Task<bool> CanCreateOrganizationAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanViewOrganizationsAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanCreateProgramAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanViewProgramsAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanCreateIndividualAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanViewIndividualsAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanChangeIndividualAvailabilityAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanEditIndividualContactAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanViewRepresentativesAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanManageRepresentativesAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanResolveCustomerDuplicatesAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(resolveAllowed);
        public Task<bool> CanConsolidateCustomerDuplicatesAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CanImportCustomersAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(importAllowed);
    }

    private sealed class PlanningEndpointStore : ICustomerImportStore
    {
        private CustomerImportPlan? _plan;
        public int ReadCalls { get; private set; }
        public Task<CreateCustomerImportResult> CreateImportPlanAsync(TenantContext context, CustomerImportPlan plan, string idempotencyKey, CancellationToken cancellationToken)
        { _plan = plan; return Task.FromResult(new CreateCustomerImportResult(plan.ImportId, true, false, false, plan)); }
        public Task<ExecuteCustomerImportResult> ExecuteImportAsync(TenantContext context, ExecuteCustomerImportRequest request, string idempotencyKey, long authorizationRevision, CancellationToken cancellationToken) => throw new InvalidOperationException("Planning does not accept work.");
        public Task<CustomerImportSummary?> ReadImportSummaryAsync(TenantContext context, Guid importId, CancellationToken cancellationToken)
        { ReadCalls++; return Task.FromResult(_plan is null ? null : new CustomerImportSummary(importId, _plan.ContractVersion, _plan.ManifestHash, _plan.Rows.Count, _plan.PendingRowCount, 0, 0, _plan.RejectedRowCount, 0, null)); }
        public Task<CustomerImportRowPage> ReadImportRowsPageAsync(TenantContext context, Guid importId, int afterRowNumber, int limit, CancellationToken cancellationToken)
        { ReadCalls++; return Task.FromResult(new CustomerImportRowPage(_plan!.Rows.Where(row => row.RowNumber > afterRowNumber).Take(limit).ToArray(), afterRowNumber + limit + 1)); }
    }

    private static readonly DateTimeOffset SourceNow = new(2040, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static CustomerImportSourceSnapshot Source(int rawState, long byteLength = 0,
        CustomerImportRetention retention = CustomerImportRetention.DefaultSevenDays,
        DateTimeOffset? expiresAt = null) => new(
        "imports/source.csv", "test/provider", byteLength, new string('a', 64),
        "text/csv; charset=utf-8", (CustomerImportSourceState)rawState,
        retention, SourceNow.AddDays(-1),
        expiresAt ?? (retention == CustomerImportRetention.DefaultSevenDays ? SourceNow.AddDays(6) : null), null);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Disposed = true;
            return base.DisposeAsync();
        }
    }

    private sealed class SourceEndpointStore(CustomerImportSourceSnapshot snapshot) : ICustomerImportSourceStore
    {
        public int Reads { get; private set; }

        public Task<CustomerImportSourceSnapshot?> ReadSourceAsync(TenantContext context, Guid importId,
            CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<CustomerImportSourceSnapshot?>(snapshot);
        }

        public Task<CustomerImportSourceLease> BeginSourceAsync(TenantContext context, CustomerImportPlan plan,
            string idempotencyKey, CustomerImportRetention retention, CustomerImportSourcePolicy policy,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CreateCustomerImportResult> CompleteSourceAsync(TenantContext context, CustomerImportPlan plan,
            string idempotencyKey, CustomerImportRetention retention, CustomerImportSourcePolicy policy,
            CustomerImportSourceLease lease, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task MarkSourceFailureAsync(TenantContext context, CustomerImportSourceLease lease,
            CustomerImportSourceFailureKind failureKind, string failureCode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CustomerImportSourceRetirementLease?> ClaimExpiredSourceRetirementAsync(TenantContext context,
            DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task CompleteSourceRetirementAsync(TenantContext context, CustomerImportSourceRetirementLease lease,
            bool deleted, string? failureCode, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CountingObjectStore(ObjectStoreReadResult? result = null) : IObjectStore
    {
        private readonly ObjectStoreReadResult _result = result ?? new(ObjectStoreReadOutcome.Unavailable);
        public int Reads { get; private set; }

        public Task<ObjectStorePutResult> PutAsync(ObjectStorePutRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ObjectStoreReadResult> OpenReadAsync(ObjectStoreReadRequest request, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(_result);
        }

        public Task<ObjectStoreMetadataResult> GetMetadataAsync(ObjectStoreKey key, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ObjectStoreDeleteResult> DeleteAsync(ObjectStoreKey key, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    // This double asserts admission ordering only; PostgreSQL tests own durable business behavior.
    private sealed class NeverCalledCustomerImportStore : ICustomerImportStore
    {
        public int Calls { get; private set; }
        private T Fail<T>() { Calls++; throw new InvalidOperationException("Denied request reached the capability."); }
        public Task<CreateCustomerImportResult> CreateImportPlanAsync(TenantContext context, CustomerImportPlan plan,
            string idempotencyKey, CancellationToken cancellationToken) => Fail<Task<CreateCustomerImportResult>>();
        public Task<ExecuteCustomerImportResult> ExecuteImportAsync(TenantContext context, ExecuteCustomerImportRequest request,
            string idempotencyKey, long authorizationRevision, CancellationToken cancellationToken) => Fail<Task<ExecuteCustomerImportResult>>();
        public Task<CustomerImportSummary?> ReadImportSummaryAsync(TenantContext context, Guid importId, CancellationToken cancellationToken) => Fail<Task<CustomerImportSummary?>>();
        public Task<CustomerImportRowPage> ReadImportRowsPageAsync(TenantContext context, Guid importId, int afterRowNumber,
            int limit, CancellationToken cancellationToken) => Fail<Task<CustomerImportRowPage>>();
    }
}
