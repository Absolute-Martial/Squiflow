using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.CoreApi.ImportExecution;
using Application.Customers;
using Application.ObjectStorage;
using Application.Tenancy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/customers/imports")
        { Content = new StringContent("name\n" + string.Concat(Enumerable.Range(0, 80).Select(index => $"Synthetic Person {index}\n")), Encoding.UTF8, "text/csv") };
        request.Headers.Add("Idempotency-Key", "plan-only");
        using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(80, metadata.RootElement.GetProperty("rowCount").GetInt32());
        Assert.False(metadata.RootElement.TryGetProperty("rows", out _));
        var id = metadata.RootElement.GetProperty("importId").GetGuid();
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
