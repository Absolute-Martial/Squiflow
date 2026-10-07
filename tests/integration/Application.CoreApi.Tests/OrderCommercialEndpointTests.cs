using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Catalog;
using Application.CoreApi.Authorization;
using Application.Orders;
using Application.Pricing;
using Application.Tenancy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OrderCommercialEndpointTests
{
    [Fact]
    public async Task CatalogSelectionNeedsNoManualPrivilegeAndRetainsHistoricalFactsAndReplay()
    {
        using var fixture = new OrderCommercialHostFixture();
        using var created = await fixture.SendAsync(HttpMethod.Post, "catalog-priced", fixture.Body(), "create");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(original);
        var id = json.RootElement.GetProperty("orderId").GetGuid();
        Assert.Equal(40m, json.RootElement.GetProperty("total").GetDecimal());
        var facts = json.RootElement.GetProperty("lines")[0].GetProperty("commercialFacts");
        Assert.Equal(fixture.Item, facts.GetProperty("catalog").GetProperty("itemId").GetGuid());
        Assert.False(facts.GetProperty("publishedPrice").TryGetProperty("createdByAccountId", out _));
        Assert.Equal(1, facts.GetProperty("priceSelection").GetProperty("explanation").GetProperty("policyRevision").GetInt64());
        fixture.Prices.Candidates.Clear();
        fixture.Catalog.Selection = new(CatalogLineFactsStatus.ItemRetired, null);
        var reads = fixture.Prices.CandidateReads;
        using var replay = await fixture.SendAsync(HttpMethod.Post, "catalog-priced", fixture.Body(), "create");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(reads, fixture.Prices.CandidateReads);
        using var historical = await fixture.SendAsync(HttpMethod.Get, id.ToString("D"));
        Assert.Equal(original, await historical.Content.ReadAsStringAsync());
        fixture.RevokeCreation();
        using var revoked = await fixture.SendAsync(HttpMethod.Post, "catalog-priced", fixture.Body(), "create");
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.True(revoked.Headers.CacheControl?.NoStore);
        Assert.Equal(1, fixture.Orders.Effects);
    }

    [Fact]
    public async Task CatalogRevisionHasIndependentEditAuthorityAndExpectedRevision()
    {
        using var fixture = new OrderCommercialHostFixture();
        using var created = await fixture.SendAsync(HttpMethod.Post, "catalog-priced", fixture.Body(), "create");
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("orderId").GetGuid();
        using var revised = await fixture.SendAsync(HttpMethod.Put, $"{id:D}/catalog-priced-draft", fixture.Body(expectedRevision: 1), "revise");
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        using var revisedJson = JsonDocument.Parse(await revised.Content.ReadAsStringAsync());
        Assert.Equal(2, revisedJson.RootElement.GetProperty("revision").GetInt64());
        using var stale = await fixture.SendAsync(HttpMethod.Put, $"{id:D}/catalog-priced-draft", fixture.Body(expectedRevision: 1), "stale");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(2, fixture.Orders.Effects);
    }

    [Theory]
    [InlineData("unitPrice", "0")]
    [InlineData("commercialFacts", "{}")]
    [InlineData("catalogFacts", "{}")]
    [InlineData("priceRevision", "1")]
    [InlineData("canOverride", "true")]
    [InlineData("policyRevision", "1")]
    public async Task ClientCannotSupplyServerFactsOrAuthorityEvenWithValidCatalogIdentifiers(string property, string value)
    {
        using var fixture = new OrderCommercialHostFixture();
        var body = $"{{\"summary\":\"Catalog\",\"currencyCode\":\"USD\",\"lines\":[{{\"itemId\":\"{fixture.Item:D}\",\"unitId\":\"{fixture.Unit:D}\",\"quantity\":2,\"{property}\":{value}}}]}}";
        using var response = await fixture.SendJsonAsync(body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.Catalog.Calls);
        Assert.Equal(0, fixture.Prices.CandidateReads);
        Assert.Equal(0, fixture.Orders.Effects);
    }

    [Fact]
    public async Task NestedDuplicatePropertiesAndRevokedPermissionFailBeforeSelection()
    {
        using var fixture = new OrderCommercialHostFixture();
        using var duplicate = await fixture.SendJsonAsync($"{{\"summary\":\"Catalog\",\"currencyCode\":\"USD\",\"lines\":[{{\"itemId\":\"{fixture.Item:D}\",\"ITEMID\":\"{fixture.Item:D}\",\"unitId\":\"{fixture.Unit:D}\",\"quantity\":2}}]}}");
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        fixture.Permissions.View = false;
        using var denied = await fixture.SendJsonAsync("not JSON");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, fixture.Catalog.Calls);
        Assert.Equal(0, fixture.Prices.CandidateReads);
    }

    [Fact]
    public async Task OversizedCatalogOrderBodyIsRejectedBeforeSelection()
    {
        using var fixture = new OrderCommercialHostFixture();
        using var response = await fixture.SendJsonAsync(new string('x', checked((int)TenantOrderEndpoint.MaximumCreateRequestBodyBytes + 1)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, fixture.Catalog.Calls);
        Assert.Equal(0, fixture.Prices.CandidateReads);
        Assert.Equal(0, fixture.Orders.Effects);
    }

    [Fact]
    public async Task OverrideUsesCurrentSeparatePermissionAndReasonWithoutManualPricePermission()
    {
        using var fixture = new OrderCommercialHostFixture();
        using var denied = await fixture.SendAsync(HttpMethod.Post, "catalog-priced", fixture.Body(overridePrice: 22, reason: "special"), "override");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, fixture.Prices.CandidateReads);
        fixture.Permissions.Override = true;
        fixture.Permissions.BeyondUnavailable = true;
        using var missingReason = await fixture.SendAsync(HttpMethod.Post, "catalog-priced", fixture.Body(overridePrice: 22), "reason");
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        using var allowed = await fixture.SendAsync(HttpMethod.Post, "catalog-priced", fixture.Body(overridePrice: 22, reason: "special"), "override");
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        Assert.Equal(0, fixture.Permissions.BeyondChecks);
        Assert.Equal(1, fixture.Orders.Effects);
        fixture.Permissions.Override = false;
        using var revokedReplay = await fixture.SendAsync(HttpMethod.Post, "catalog-priced", fixture.Body(overridePrice: 22, reason: "special"), "override");
        Assert.Equal(HttpStatusCode.Forbidden, revokedReplay.StatusCode);
        Assert.Equal(0, fixture.Permissions.BeyondChecks);
        Assert.Equal(1, fixture.Orders.Effects);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ElevatedOverrideReplayRechecksCurrentAuthorityWithoutRefreshingHistoricalFacts(bool revision, bool unavailable)
    {
        using var fixture = new OrderCommercialHostFixture();
        fixture.Permissions.Override = true;
        fixture.Permissions.Beyond = true;
        using var created = await fixture.SendAsync(HttpMethod.Post, "catalog-priced",
            fixture.Body(overridePrice: 40, reason: "Commercial exception"), "create");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("orderId").GetGuid();
        var method = revision ? HttpMethod.Put : HttpMethod.Post;
        var path = revision ? $"{id:D}/catalog-priced-draft" : "catalog-priced";
        var key = revision ? "revise" : "create";
        var body = fixture.Body(expectedRevision: revision ? 1 : null, overridePrice: 40, reason: "Commercial exception");
        using var original = await fixture.SendAsync(method, path, body, key);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var retained = await original.Content.ReadAsStringAsync();
        var effects = fixture.Orders.Effects;
        fixture.Prices.Candidates.Clear();
        fixture.Catalog.Selection = new(CatalogLineFactsStatus.ItemRetired, null);
        var reads = fixture.Prices.CandidateReads;
        var checks = fixture.Permissions.BeyondChecks;
        fixture.Permissions.Beyond = false;
        fixture.Permissions.BeyondUnavailable = unavailable;

        using var denied = await fixture.SendAsync(method, path, body, key);
        Assert.Equal(unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(denied.Headers.CacheControl?.NoStore);
        Assert.False(denied.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(checks + 1, fixture.Permissions.BeyondChecks);
        Assert.Equal(reads, fixture.Prices.CandidateReads);
        Assert.Equal(effects, fixture.Orders.Effects);

        fixture.Permissions.Beyond = true;
        fixture.Permissions.BeyondUnavailable = false;
        using var recovered = await fixture.SendAsync(method, path, body, key);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal("true", recovered.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(retained, await recovered.Content.ReadAsStringAsync());
        Assert.Equal(reads, fixture.Prices.CandidateReads);
        Assert.Equal(effects, fixture.Orders.Effects);
    }
}

internal sealed class OrderCommercialHostFixture : IDisposable
{
    private readonly WhiteLabelApiFactory _base = new();
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _host;
    private readonly HttpClient _client;
    private readonly string _token;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _account = Guid.NewGuid();
    internal Guid Item { get; } = Guid.NewGuid();
    internal Guid Unit { get; } = Guid.NewGuid();
    internal CatalogHostStore Catalog { get; } = new();
    internal PricingHostPermissions Permissions { get; } = new() { View = true };
    internal PricingHostStore Prices { get; }
    internal CommercialOrderHostStore Orders { get; } = new();
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    internal OrderCommercialHostFixture()
    {
        Catalog.Selection = new(CatalogLineFactsStatus.Available, new(Item, "SKU", Unit, "Catalog item", "EA", "Each", 2m, 4,
            1, 1, new(Unit, Unit, 1, 1, 1), "EA", "Each", 4, 1, 2m));
        Prices = new(_tenant, _account, Now);
        Prices.Candidates.Add(new(_tenant, Guid.NewGuid(), 1, new(Item, "EA", "USD", PriceScope.Default(), Unit), 20,
            new(Now.AddDays(-1)), PricePublicationState.Published, _account, Now.AddDays(-1), Now.AddDays(-1)));
        _host = _base.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICatalogStore>(); services.AddSingleton<ICatalogStore>(Catalog);
            services.RemoveAll<ITenantCatalogAuthorization>(); services.AddSingleton<ITenantCatalogAuthorization>(new CatalogHostAuthorization { CanRead = true });
            services.RemoveAll<ITenantPricingAuthorization>(); services.AddSingleton<ITenantPricingAuthorization>(Permissions);
            services.RemoveAll<IPricingPublicationStore>(); services.AddSingleton<IPricingPublicationStore>(Prices);
            services.RemoveAll<IPricingCandidateReader>(); services.AddSingleton<IPricingCandidateReader>(Prices);
            services.RemoveAll<IPricingPolicyStore>(); services.AddSingleton<IPricingPolicyStore>(Prices);
            services.RemoveAll<IPricingReferenceReader>(); services.AddSingleton<IPricingReferenceReader>(new References(Item, Unit));
            services.RemoveAll<IOrderDraftStore>(); services.AddSingleton<IOrderDraftStore>(Orders);
            services.RemoveAll<IOrderDraftReceiptReader>(); services.AddSingleton<IOrderDraftReceiptReader>(Orders);
            services.RemoveAll<IOrderPricingAuthorityReader>(); services.AddScoped<IOrderPricingAuthorityReader, OrderPricingAuthorityReader>();
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new Clock());
            services.AddScoped<SelectCatalogLineFacts>(); services.AddScoped<PriceResolver>(); services.AddScoped<PricingApplication>();
            services.AddScoped<CatalogOrderDraftApplication>();
        }));
        var subject = Guid.NewGuid().ToString("N");
        _base.Bind(subject, _account); _base.AddTenantMembership(_account, _tenant, "Commercial tenant");
        _base.SetOrderCreateDecision(_account, _tenant, true); _base.SetOrderEditDecision(_account, _tenant, true);
        _base.SetOrderViewDecision(_account, _tenant, true);
        _base.SetOrderManualPriceDecision(_account, _tenant, false);
        _token = _base.CreateToken(subject); _client = _host.CreateClient();
    }
    internal void RevokeCreation() => _base.SetOrderCreateDecision(_account, _tenant, false);
    internal object Body(long? expectedRevision = null, decimal? overridePrice = null, string? reason = null) => new
    {
        summary = "Catalog order",
        currencyCode = "USD",
        expectedRevision,
        lines = new[] { new { itemId = Item, unitId = Unit, quantity = 2m, overridePrice, overrideReason = reason } },
    };
    internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? key = null)
    {
        using var request = Request(method, path, key);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }
    internal async Task<HttpResponseMessage> SendJsonAsync(string json)
    {
        using var request = Request(HttpMethod.Post, "catalog-priced", "test");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.SendAsync(request);
    }
    private HttpRequestMessage Request(HttpMethod method, string path, string? key)
    {
        var request = new HttpRequestMessage(method, $"/api/v1/tenants/{_tenant:D}/orders/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }
    public void Dispose() { _client.Dispose(); _host.Dispose(); _base.Dispose(); }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class References(Guid item, Guid unit) : IPricingReferenceReader
    {
        public Task<string> RequireCompatibleUnitAsync(PricingActorContext actor, Guid itemId, Guid unitId, long? conversionRevision, CancellationToken ct) =>
            itemId == item && unitId == unit && conversionRevision is null ? Task.FromResult("EA") : throw new PricingValidationException("catalog_unit_incompatible", "Incompatible catalog unit.");
        public Task RequireContextAsync(PricingActorContext actor, PriceSelectionContext context, CancellationToken ct) => Task.CompletedTask;
    }
}

// Host-only controlled store; PostgreSQL owns the durability/concurrency proofs.
internal sealed class CommercialOrderHostStore : IOrderDraftStore, IOrderDraftReceiptReader
{
    private readonly Dictionary<string, (string Fingerprint, OrderDraftSnapshot Order)> _creates = [];
    private readonly Dictionary<string, (string Fingerprint, OrderDraftSnapshot Order)> _revisions = [];
    private OrderDraftSnapshot? _order;
    internal int Effects { get; private set; }
    public Task<CreateOrderDraftResult?> FindCreateReceiptAsync(TenantContext context, string key, string fingerprint, CancellationToken ct) =>
        Task.FromResult(_creates.TryGetValue(key, out var retained) ? new CreateOrderDraftResult(retained.Fingerprint == fingerprint ?
            CreateOrderDraftStatus.Replayed : CreateOrderDraftStatus.IdempotencyKeyConflict, retained.Fingerprint == fingerprint ? retained.Order : null) : null);
    public Task<ReviseOrderDraftResult?> FindRevisionReceiptAsync(TenantContext context, string key, string fingerprint, CancellationToken ct) =>
        Task.FromResult(_revisions.TryGetValue(key, out var retained) ? new ReviseOrderDraftResult(retained.Fingerprint == fingerprint ?
            ReviseOrderDraftStatus.Replayed : ReviseOrderDraftStatus.IdempotencyKeyConflict, retained.Fingerprint == fingerprint ? retained.Order : null) : null);
    public Task<CreateOrderDraftResult> CreateAsync(TenantContext context, OrderDraftIntent intent, string key, CancellationToken ct)
    {
        _order = new(Guid.NewGuid(), context.TenantId, context.AccountId, intent.Summary, intent.CurrencyCode, intent.Total,
            1, new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero), intent.Lines, CustomerContext: intent.CustomerContext);
        _creates.Add(key, (intent.Fingerprint, _order)); Effects++;
        return Task.FromResult(new CreateOrderDraftResult(CreateOrderDraftStatus.Created, _order));
    }
    public Task<ReviseOrderDraftResult> ReviseAsync(TenantContext context, ReviseOrderDraftRequest request, OrderDraftIntent intent, string key, string fingerprint, CancellationToken ct)
    {
        if (_order!.Revision != request.ExpectedRevision) return Task.FromResult(new ReviseOrderDraftResult(ReviseOrderDraftStatus.RevisionConflict, null));
        _order = _order with { Summary = intent.Summary, Lines = intent.Lines, Total = intent.Total, Revision = _order.Revision + 1 };
        _revisions.Add(key, (fingerprint, _order)); Effects++;
        return Task.FromResult(new ReviseOrderDraftResult(ReviseOrderDraftStatus.Revised, _order));
    }
    public Task<OrderDraftSnapshot?> FindAsync(TenantContext context, Guid id, CancellationToken ct) => Task.FromResult(_order?.TenantId == context.TenantId && _order.OrderId == id ? _order : null);
    public Task<OrderDraftPage> ListAsync(TenantContext context, ListOrderDraftsRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<AbandonOrderDraftResult> AbandonAsync(TenantContext context, AbandonOrderDraftRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
    public Task<CommitOrderDraftResult> CommitAsync(TenantContext context, CommitOrderDraftRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
}
