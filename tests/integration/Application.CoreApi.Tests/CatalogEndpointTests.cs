using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Catalog;
using Application.CoreApi.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class CatalogEndpointTests
{
    [Fact]
    public async Task IndependentReadAndManagePermissionsPrecedePayloadAndCatalogProvider()
    {
        using var setup = new CatalogHostSetup();
        using var denied = await setup.SendAsync("units", "not-json");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, setup.Store.Calls);
        setup.Authorization.CanManage = true;
        using var created = await setup.SendAsync("units", "{\"code\":\"ea\",\"name\":\"Each\",\"precision\":0}");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("EA", document.RootElement.GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("tenantId", out _));
        var id = document.RootElement.GetProperty("unitId").GetGuid();
        using var readDenied = await setup.SendAsync($"units/{id:D}");
        Assert.Equal(HttpStatusCode.Forbidden, readDenied.StatusCode);
        Assert.Equal(1, setup.Store.Calls);
        setup.Authorization.CanRead = true; setup.Authorization.CanManage = false;
        using var read = await setup.SendAsync($"units/{id:D}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var editDenied = await setup.SendAsync($"units/{id:D}/display", "not-json");
        Assert.Equal(HttpStatusCode.Forbidden, editDenied.StatusCode);
        Assert.Equal(2, setup.Store.Calls);
    }

    [Theory]
    [InlineData("{\"code\":\"EA\",\"name\":\"Each\",\"precision\":0,\"extra\":true}")]
    [InlineData("{\"code\":\"EA\",\"name\":\"First\",\"name\":\"Second\",\"precision\":0}")]
    [InlineData("{\"code\":\"EA\",\"name\":\"Each\",\"precision\":\"0\"}")]
    [InlineData("{\"code\":\"EA\",\"name\":\"Each\"}")]
    [InlineData("{\"code\":\"EA\",\"name\":\"Each\",\"precision\":10}")]
    [InlineData("[]")]
    public async Task StrictBoundedUnitJsonRejectsMalformedFieldsWithoutEffects(string body)
    {
        using var setup = new CatalogHostSetup(); setup.Authorization.CanManage = true;
        using var response = await setup.SendAsync("units", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, setup.Store.Calls);
    }

    [Theory]
    [InlineData("1", "\"nonStock\"")]
    [InlineData("\"Product\"", "\"nonStock\"")]
    [InlineData("\"service\"", "2")]
    [InlineData("\"service\"", "\"unknown\"")]
    public async Task ItemEnumsAreOnlyAllowedStrings(string kind, string stockMode)
    {
        using var setup = new CatalogHostSetup(); setup.Authorization.CanManage = true;
        using var response = await setup.SendAsync("items", $"{{\"code\":null,\"name\":\"Item\",\"description\":null,\"kind\":{kind},\"baseUnitId\":\"{Guid.NewGuid():D}\",\"stockMode\":{stockMode}}}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, setup.Store.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredAndUndeclaredBodyLengthsAreBounded(bool unknownLength)
    {
        using var setup = new CatalogHostSetup(); setup.Authorization.CanManage = true;
        var bytes = Encoding.UTF8.GetBytes(new string(' ', 8193));
        using var request = setup.Request("units", HttpMethod.Post);
        request.Headers.Add("Idempotency-Key", "bounded");
        request.Content = unknownLength ? new UnknownLengthContent(bytes) : new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await setup.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, setup.Store.Calls);
    }

    [Fact]
    public async Task CursorRejectsOtherTenantOrResourceBeforeStore()
    {
        using var setup = new CatalogHostSetup(); setup.Authorization.CanRead = true;
        var id = Guid.NewGuid(); var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        setup.Store.UnitPage = new([], new(now, id));
        using var first = await setup.SendAsync("units?limit=1");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var json = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var cursor = json.RootElement.GetProperty("nextCursor").GetString();
        var before = setup.Store.Calls;
        using var wrongResource = await setup.SendAsync($"items?after={cursor}");
        Assert.Equal(HttpStatusCode.BadRequest, wrongResource.StatusCode);
        var other = Guid.NewGuid(); setup.Baseline.AddTenantMembership(setup.Actor, other, "Other");
        using var wrongTenantRequest = setup.Request("units?after=" + cursor, HttpMethod.Get, other);
        using var wrongTenant = await setup.Client.SendAsync(wrongTenantRequest);
        Assert.Equal(HttpStatusCode.BadRequest, wrongTenant.StatusCode);
        Assert.Equal(before, setup.Store.Calls);
        using var next = await setup.SendAsync($"units?after={cursor}");
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Equal(id, setup.Store.LastUnitPage?.After?.UnitId);
    }

    [Fact]
    public async Task ConversionPublicationReadAndSelectionUseExplicitPinnedFactsWithoutMutationKey()
    {
        using var setup = new CatalogHostSetup(); setup.Authorization.CanManage = true; setup.Authorization.CanRead = true;
        var source = Guid.NewGuid(); var target = Guid.NewGuid(); var item = Guid.NewGuid();
        var path = $"units/{source:D}/conversions/{target:D}";
        using var published = await setup.SendAsync(path, "{\"expectedRevision\":0,\"numerator\":1,\"denominator\":8}");
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        using var read = await setup.SendAsync(path + "?revision=1");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        setup.Store.Selection = new(CatalogLineFactsStatus.Available, new CatalogLineFacts(item, null, source, "Item", "ALT", "Alternate", 1m,
            2, 1, 1, new(source, target, 1, 1m, 8m), "BASE", "Base", 2, 1, 0.12m));
        using var selection = await setup.SendAsync("line-facts", $"{{\"itemId\":\"{item:D}\",\"unitId\":\"{source:D}\",\"quantity\":1,\"conversionRevision\":1}}", key: null);
        Assert.Equal(HttpStatusCode.OK, selection.StatusCode);
        using var selected = JsonDocument.Parse(await selection.Content.ReadAsStringAsync());
        Assert.Equal(0.12m, selected.RootElement.GetProperty("baseQuantity").GetDecimal());
        Assert.Equal(1, setup.Store.LastSelection?.ConversionRevision);
        Assert.False(selection.Headers.Contains("Idempotency-Replayed"));
    }

    [Fact]
    public async Task ManageMutationsReturnOnlyMetadataWhileRetiredHistoryAndSelectionOutcomesAreExplicit()
    {
        using var setup = new CatalogHostSetup(); setup.Authorization.CanManage = true;
        using var unitResponse = await setup.SendAsync("units", "{\"code\":\"EA\",\"name\":\"Each\",\"precision\":2}");
        Assert.Equal(HttpStatusCode.Created, unitResponse.StatusCode);
        var unit = setup.Store.Unit!;
        using var itemResponse = await setup.SendAsync("items", $"{{\"code\":null,\"name\":\"Service\",\"description\":null,\"kind\":\"service\",\"baseUnitId\":\"{unit.UnitId:D}\",\"stockMode\":\"availabilityOnly\"}}");
        Assert.Equal(HttpStatusCode.Created, itemResponse.StatusCode);
        var item = setup.Store.Item!;
        using var itemJson = JsonDocument.Parse(await itemResponse.Content.ReadAsStringAsync());
        Assert.Equal("unavailable", itemJson.RootElement.GetProperty("availability").GetString());
        setup.Store.Item = item with
        {
            Availability = CatalogAvailability.Available,
            Revision = 2,
            AvailabilityChangedAt = DateTimeOffset.UtcNow,
            AvailabilityChangedByAccountId = setup.Actor
        };
        using var availability = await setup.SendAsync($"items/{item.ItemId:D}/availability", "{\"expectedRevision\":1,\"availability\":\"available\"}");
        Assert.Equal(HttpStatusCode.OK, availability.StatusCode);
        using var availabilityJson = JsonDocument.Parse(await availability.Content.ReadAsStringAsync());
        Assert.Equal("available", availabilityJson.RootElement.GetProperty("status").GetString());
        Assert.False(availabilityJson.RootElement.TryGetProperty("name", out _));
        using var unitEdit = await setup.SendAsync($"units/{unit.UnitId:D}/display", "{\"expectedRevision\":1,\"name\":\"Each (display)\"}");
        Assert.Equal(HttpStatusCode.OK, unitEdit.StatusCode);
        using var itemEdit = await setup.SendAsync($"items/{item.ItemId:D}/display", "{\"expectedRevision\":2,\"name\":\"Renamed\",\"description\":null}");
        Assert.Equal(HttpStatusCode.OK, itemEdit.StatusCode);
        setup.Store.Item = (setup.Store.Item ?? throw new InvalidOperationException("The host test item is missing.")) with
        {
            Status = CatalogEntityStatus.Retired,
            Revision = 3,
            RetiredAt = DateTimeOffset.UtcNow,
            RetiredByAccountId = setup.Actor
        };
        using var itemRetire = await setup.SendAsync($"items/{item.ItemId:D}/retire", "{\"expectedRevision\":2}");
        Assert.Equal(HttpStatusCode.OK, itemRetire.StatusCode);
        setup.Store.Unit = unit with
        {
            Status = CatalogEntityStatus.Retired,
            Revision = 2,
            RetiredAt = DateTimeOffset.UtcNow,
            RetiredByAccountId = setup.Actor
        };
        using var unitRetire = await setup.SendAsync($"units/{unit.UnitId:D}/retire", "{\"expectedRevision\":1}");
        Assert.Equal(HttpStatusCode.OK, unitRetire.StatusCode);
        setup.Authorization.CanRead = true;
        using var historical = await setup.SendAsync($"items/{item.ItemId:D}");
        Assert.Equal(HttpStatusCode.OK, historical.StatusCode);
        Assert.Contains("retired", await historical.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        setup.Store.Selection = new(CatalogLineFactsStatus.ItemUnavailable, null);
        using var unavailable = await setup.SendAsync("line-facts", $"{{\"itemId\":\"{item.ItemId:D}\",\"unitId\":\"{unit.UnitId:D}\",\"quantity\":1}}", key: null);
        Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        Assert.Contains("item_unavailable", await unavailable.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorityOutageAndRevocationFailClosedBeforeCatalogProviderAndKeepErrorsSafe()
    {
        using var setup = new CatalogHostSetup(); setup.Authorization.CanManage = true; setup.Authorization.Unavailable = true;
        using var outage = await setup.SendAsync("units", "not-json");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, outage.StatusCode);
        Assert.DoesNotContain("private-canary", await outage.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, setup.Store.Calls);
        setup.Authorization.Unavailable = false;
        using var created = await setup.SendAsync("units", "{\"code\":\"EA\",\"name\":\"Each\",\"precision\":0}");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        setup.Store.UnitCreateStatus = CreateCatalogUnitStatus.Replayed;
        using var replay = await setup.SendAsync("units", "{\"code\":\"EA\",\"name\":\"Each\",\"precision\":0}");
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        setup.Authorization.CanManage = false;
        using var revoked = await setup.SendAsync("units", "{\"code\":\"EA\",\"name\":\"Each\",\"precision\":0}");
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.Equal(2, setup.Store.Calls);
    }

    [Fact]
    public async Task MembershipAndAnonymousDenialPrecedeCatalogChecksAndSafeStoreFailures()
    {
        using var setup = new CatalogHostSetup(); setup.Authorization.CanRead = true;
        using var foreignRequest = setup.Request("units", HttpMethod.Get, Guid.NewGuid());
        using var foreign = await setup.Client.SendAsync(foreignRequest);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(0, setup.Authorization.ReadChecks);
        using var anonymous = await setup.Client.GetAsync($"/api/v1/tenants/{setup.Tenant:D}/catalog/units");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(0, setup.Store.Calls);
        setup.Store.Fail = true;
        using var failure = await setup.SendAsync("units");
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.DoesNotContain("private-canary", await failure.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private sealed class CatalogHostSetup : IDisposable
    {
        internal WhiteLabelApiFactory Baseline { get; } = new();
        internal Guid Actor { get; } = Guid.NewGuid();
        internal Guid Tenant { get; } = Guid.NewGuid();
        internal CatalogHostStore Store { get; } = new();
        internal CatalogHostAuthorization Authorization { get; } = new();
        private readonly WebApplicationFactory<Program> _factory;
        private readonly string _token;
        internal HttpClient Client { get; }

        internal CatalogHostSetup()
        {
            var subject = Guid.NewGuid().ToString("N"); Baseline.Bind(subject, Actor); Baseline.AddTenantMembership(Actor, Tenant, "Catalog");
            _token = Baseline.CreateToken(subject);
            _factory = Baseline.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITenantCatalogAuthorization>(); services.AddSingleton<ITenantCatalogAuthorization>(Authorization);
                services.RemoveAll<ICatalogStore>(); services.AddSingleton<ICatalogStore>(Store);
                services.RemoveAll<ICatalogConversionStore>(); services.AddSingleton<ICatalogConversionStore>(Store);
                services.RemoveAll<ICatalogAvailabilityStore>(); services.AddSingleton<ICatalogAvailabilityStore>(Store);
                services.AddScoped<CreateCatalogUnit>(); services.AddScoped<CreateCatalogItem>();
                services.AddScoped<GetCatalogUnit>(); services.AddScoped<GetCatalogItem>();
                services.AddScoped<ListCatalogUnits>(); services.AddScoped<ListCatalogItems>();
                services.AddScoped<RenameCatalogUnit>(); services.AddScoped<RenameCatalogItem>();
                services.AddScoped<RetireCatalogUnit>(); services.AddScoped<RetireCatalogItem>();
                services.AddScoped<PublishCatalogConversion>(); services.AddScoped<GetCatalogConversion>();
                services.AddScoped<ChangeCatalogAvailability>(); services.AddScoped<SelectCatalogLineFacts>();
            }));
            Client = _factory.CreateClient();
        }

        internal HttpRequestMessage Request(string relative, HttpMethod method, Guid? tenant = null)
        {
            var request = new HttpRequestMessage(method, $"/api/v1/tenants/{tenant ?? Tenant:D}/catalog/{relative}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return request;
        }

        internal async Task<HttpResponseMessage> SendAsync(string relative, string? body = null, string? key = "catalog-key")
        {
            using var request = Request(relative, body is null ? HttpMethod.Get : HttpMethod.Post);
            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                if (key is not null) request.Headers.Add("Idempotency-Key", key);
            }
            return await Client.SendAsync(request);
        }

        public void Dispose() { Client.Dispose(); _factory.Dispose(); Baseline.Dispose(); }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
