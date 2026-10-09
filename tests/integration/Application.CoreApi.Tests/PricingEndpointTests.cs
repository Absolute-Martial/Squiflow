using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.Pricing;
using Application.Catalog;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class PricingEndpointTests
{
    [Fact]
    public async Task DraftPublishRetireAreIndependentAndRevokedAuthorityBlocksReceiptReplay()
    {
        using var fixture = new PricingHostFixture();
        using var foreign = await fixture.PostAsync("drafts", fixture.DraftBody(), "foreign", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        using var denied = await fixture.PostAsync("drafts", fixture.DraftBody(), "create");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, fixture.Store.Effects);
        fixture.Permissions.Edit = true;
        using var created = await fixture.PostAsync("drafts", fixture.DraftBody(), "create");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var draftJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var revision = draftJson.RootElement.GetProperty("revisionId").GetGuid();
        // The created revision URL is the request pricing root plus the created revision identity.
        Assert.Equal(
            new Uri(created.RequestMessage!.RequestUri!, $"drafts/../revisions/{revision:D}").AbsolutePath,
            created.Headers.Location!.OriginalString);
        using var publishDenied = await fixture.PostAsync($"revisions/{revision:D}/publish", new { }, "publish");
        Assert.Equal(HttpStatusCode.Forbidden, publishDenied.StatusCode);
        fixture.Permissions.Publish = true;
        using var published = await fixture.PostAsync($"revisions/{revision:D}/publish", new { }, "publish");
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var originalPublication = await published.Content.ReadAsStringAsync();
        fixture.Permissions.Publish = false;
        using var revoked = await fixture.PostAsync($"revisions/{revision:D}/publish", new { }, "publish");
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        fixture.Permissions.Retire = true;
        using var retired = await fixture.PostAsync($"revisions/{revision:D}/retire", new { }, "retire");
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        fixture.Permissions.Publish = true;
        using var replay = await fixture.PostAsync($"revisions/{revision:D}/publish", new { }, "publish");
        Assert.Equal(originalPublication, await replay.Content.ReadAsStringAsync());
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(3, fixture.Store.Effects);
        fixture.Permissions.PublishUnavailable = true;
        using var unavailable = await fixture.PostAsync("policy", new { expectedRevision = 0, minimumUnitPrice = 0, maximumUnitPrice = 100 }, "policy");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal(3, fixture.Store.Effects);
        Assert.True(unavailable.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"itemId\":\"not-a-guid\"}")]
    [InlineData("{\"canOverride\":true}")]
    [InlineData("{\"policyRevision\":1}")]
    [InlineData("{\"evaluatedAt\":\"2026-10-06T12:00:00Z\"}")]
    [InlineData("{\"committedAgreementId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}")]
    [InlineData("{\"wholesaleTierId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}")]
    [InlineData("{\"itemId\":null}")]
    [InlineData("{\"itemId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"ITEMID\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}")]
    public async Task MalformedOrClientAuthorityFieldsAreRejectedBeforePriceReads(string json)
    {
        using var fixture = new PricingHostFixture();
        fixture.Permissions.View = true;
        using var response = await fixture.PostJsonAsync("resolve", json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.Store.CandidateReads);
        Assert.Equal(0, fixture.Store.Effects);
    }

    [Fact]
    public async Task CurrentTimeAndPolicyProduceResolvedMissingExpiredAndConflictWithoutClientAuthority()
    {
        using var fixture = new PricingHostFixture();
        fixture.Permissions.View = true;
        using var missing = await fixture.PostAsync("resolve", fixture.SelectionBody());
        Assert.Equal("PriceMissing", await StatusAsync(missing));
        fixture.Store.Candidates.Add(fixture.Revision(12, new(fixture.Now.AddDays(-2), fixture.Now.AddDays(-1))));
        using var expired = await fixture.PostAsync("resolve", fixture.SelectionBody());
        Assert.Equal("PriceExpired", await StatusAsync(expired));
        fixture.Store.Candidates.Clear();
        fixture.Store.Candidates.Add(fixture.Revision(20, new(fixture.Now.AddDays(-1), fixture.Now.AddDays(1))));
        using var resolved = await fixture.PostAsync("resolve", fixture.SelectionBody());
        Assert.Equal("PriceResolved", await StatusAsync(resolved));
        using var json = JsonDocument.Parse(await resolved.Content.ReadAsStringAsync());
        Assert.Equal(fixture.Now, json.RootElement.GetProperty("evaluatedAt").GetDateTimeOffset());
        Assert.Equal(1, json.RootElement.GetProperty("policyRevision").GetInt64());
        fixture.Store.Candidates.Add(fixture.Revision(25, new(fixture.Now.AddDays(-1), fixture.Now.AddDays(1))));
        using var conflict = await fixture.PostAsync("resolve", fixture.SelectionBody());
        Assert.Equal("PriceConflict", await StatusAsync(conflict));
        using var conflictJson = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
        Assert.All(conflictJson.RootElement.GetProperty("candidates").EnumerateArray(), candidate =>
            Assert.Equal(JsonValueKind.Null, candidate.GetProperty("baseUnitPrice").ValueKind));
    }

    [Fact]
    public async Task OverrideRevocationAndOutageFailClosedAndElevationDoesNotRequireApproval()
    {
        using var fixture = new PricingHostFixture();
        fixture.Permissions.View = true;
        fixture.Store.Candidates.Add(fixture.Revision(20, new(fixture.Now.AddDays(-1))));
        using var denied = await fixture.PostAsync("resolve", fixture.SelectionBody(overridePrice: 15, reason: "discount"));
        Assert.Equal("PriceOverrideRequired", await StatusAsync(denied));
        fixture.Permissions.Override = true;
        using var beyondDenied = await fixture.PostAsync("resolve", fixture.SelectionBody(overridePrice: 15, reason: "discount"));
        Assert.Equal("PriceOverrideRequired", await StatusAsync(beyondDenied));
        fixture.Permissions.Beyond = true;
        using var elevated = await fixture.PostAsync("resolve", fixture.SelectionBody(overridePrice: 15, reason: "elevated discount"));
        Assert.Equal("PriceResolved", await StatusAsync(elevated));
        using var missingReason = await fixture.PostAsync("resolve", fixture.SelectionBody(overridePrice: 15));
        Assert.Equal("PriceOverrideRequired", await StatusAsync(missingReason));
        fixture.Permissions.BeyondUnavailable = true;
        var beyondChecks = fixture.Permissions.BeyondChecks;
        using var withinEnvelope = await fixture.PostAsync("resolve", fixture.SelectionBody(overridePrice: 22, reason: "within envelope"));
        Assert.Equal("PriceResolved", await StatusAsync(withinEnvelope));
        Assert.Equal(beyondChecks, fixture.Permissions.BeyondChecks);
        fixture.Permissions.OverrideUnavailable = true;
        var readsBefore = fixture.Store.CandidateReads;
        using var outage = await fixture.PostAsync("resolve", fixture.SelectionBody(overridePrice: 15, reason: "discount"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, outage.StatusCode);
        Assert.Equal(readsBefore, fixture.Store.CandidateReads);
        Assert.Equal(0, fixture.Store.Effects);
    }

    [Fact]
    public async Task PolicyPublicationIsRevisionCheckedRetainedAndCannotBeSuppliedBySelectionClient()
    {
        using var fixture = new PricingHostFixture();
        fixture.Permissions.View = true;
        fixture.Permissions.Publish = true;
        fixture.Store.Candidates.Add(fixture.Revision(20, new(fixture.Now.AddDays(-1))));
        using var selected = await fixture.PostAsync("resolve", fixture.SelectionBody());
        using var retained = JsonDocument.Parse(await selected.Content.ReadAsStringAsync());
        using var policy = await fixture.PostAsync("policy", new { expectedRevision = 1, minimumUnitPrice = 10, maximumUnitPrice = 40, maximumDecreasePercent = 5 }, "policy-2");
        Assert.Equal(HttpStatusCode.OK, policy.StatusCode);
        using var current = await fixture.PostAsync("resolve", fixture.SelectionBody());
        using var currentJson = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        Assert.Equal(1, retained.RootElement.GetProperty("policyRevision").GetInt64());
        Assert.Equal(2, currentJson.RootElement.GetProperty("policyRevision").GetInt64());
        using var stale = await fixture.PostAsync("policy", new { expectedRevision = 1, minimumUnitPrice = 0, maximumUnitPrice = 50 }, "stale");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task UnsupportedScopeIncompatibleUnitForeignContextAndOversizeDoNotCreateEffects()
    {
        using var fixture = new PricingHostFixture();
        fixture.Permissions.Edit = true;
        fixture.Permissions.View = true;
        using var unsupported = await fixture.PostAsync("drafts", fixture.DraftBody(scope: "committedAgreement", target: Guid.NewGuid()), "unsupported");
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        using var guessedTier = await fixture.PostAsync("drafts", fixture.DraftBody(scope: "wholesale", target: Guid.NewGuid()), "tier");
        Assert.Equal(HttpStatusCode.BadRequest, guessedTier.StatusCode);
        using var incompatible = await fixture.PostAsync("drafts", fixture.DraftBody(unit: Guid.NewGuid()), "unit");
        Assert.Equal(HttpStatusCode.BadRequest, incompatible.StatusCode);
        using var customer = await fixture.PostAsync("resolve", fixture.SelectionBody(customer: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, customer.StatusCode);
        using var oversize = await fixture.PostJsonAsync("resolve", "{\"reason\":\"" + new string('x', 5000) + "\"}");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversize.StatusCode);
        Assert.Equal(0, fixture.Store.Effects);
        Assert.Equal(0, fixture.Store.CandidateReads);
    }

    [Fact]
    public async Task AnUnreadableRetainedReceiptIsAServerFaultNotAClientError()
    {
        using var fixture = new PricingHostFixture();
        fixture.Permissions.Edit = true;
        fixture.Store.UnreadableReceiptKey = "unreadable";
        using var response = await fixture.PostAsync("drafts", fixture.DraftBody(), fixture.Store.UnreadableReceiptKey);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var problem = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(problem);
        Assert.Equal("internal_error", json.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("pricing_receipt_version_unsupported", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("stable-unit", problem, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Store.Effects);
    }

    [Fact]
    public async Task PricingProblemTitlesNamePricingAndNeverCustomerResources()
    {
        using var fixture = new PricingHostFixture();
        fixture.Permissions.View = true;
        using var missing = await fixture.GetAsync($"revisions/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(("pricing_revision_not_found", "Price revision not found."), await ProblemAsync(missing));

        fixture.Permissions.Edit = true;
        using var created = await fixture.PostAsync("drafts", fixture.DraftBody(), "shared-key");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var conflicted = await fixture.PostAsync("drafts",
            fixture.DraftBody(baseUnitPrice: 21), "shared-key");
        Assert.Equal(HttpStatusCode.Conflict, conflicted.StatusCode);
        var (conflictCode, conflictTitle) = await ProblemAsync(conflicted);
        Assert.Equal("idempotency_key_conflict", conflictCode);
        Assert.Equal("Idempotency key conflict.", conflictTitle);
        using var conflictProblem = JsonDocument.Parse(await conflicted.Content.ReadAsStringAsync());
        Assert.Contains("pricing request", conflictProblem.RootElement.GetProperty("detail").GetString());

        using var invalid = await fixture.PostAsync("drafts",
            fixture.DraftBody(scope: "committedAgreement", target: Guid.NewGuid()), "unsupported-scope");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var (invalidCode, invalidTitle) = await ProblemAsync(invalid);
        Assert.Equal("pricing_scope_unsupported", invalidCode);
        Assert.Equal("Invalid pricing request.", invalidTitle);

        using var oversize = await fixture.PostJsonAsync("resolve", "{\"reason\":\"" + new string('x', 5000) + "\"}");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversize.StatusCode);
        Assert.Equal(("request_too_large", "Pricing request is too large."), await ProblemAsync(oversize));
    }

    private static async Task<(string Code, string Title)> ProblemAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var title = problem.RootElement.GetProperty("title").GetString()!;
        Assert.DoesNotContain("ustomer", title);
        return (problem.RootElement.GetProperty("code").GetString()!, title);
    }

    private static async Task<string?> StatusAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("status").GetString();
    }
}

internal sealed class PricingHostFixture : IDisposable
{
    private readonly WhiteLabelApiFactory _base = new();
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _host;
    private readonly HttpClient _client;
    private readonly string _token;
    internal Guid Tenant { get; } = Guid.NewGuid();
    internal Guid Account { get; } = Guid.NewGuid();
    internal Guid Item { get; } = Guid.NewGuid();
    internal Guid Unit { get; } = Guid.NewGuid();
    internal DateTimeOffset Now { get; } = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    internal PricingHostPermissions Permissions { get; } = new();
    internal PricingHostStore Store { get; }
    internal PricingCatalogReferences Catalog { get; }

    internal PricingHostFixture(bool realReferences = false)
    {
        Store = new(Tenant, Account, Now);
        Catalog = new(Tenant, Account, Item, Unit, Now);
        _host = _base.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantPricingAuthorization>();
            services.AddSingleton<ITenantPricingAuthorization>(Permissions);
            services.RemoveAll<IPricingPublicationStore>();
            services.RemoveAll<IPricingCandidateReader>();
            services.RemoveAll<IPricingPolicyStore>();
            services.RemoveAll<IPricingReferenceReader>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new PricingHostClock(Now));
            services.AddSingleton<IPricingPublicationStore>(Store);
            services.AddSingleton<IPricingCandidateReader>(Store);
            services.AddSingleton<IPricingPolicyStore>(Store);
            if (realReferences)
            {
                services.RemoveAll<ICatalogStore>();
                services.RemoveAll<ICatalogConversionStore>();
                services.AddSingleton<ICatalogStore>(Catalog);
                services.AddSingleton<ICatalogConversionStore>(Catalog);
                services.AddScoped<GetCatalogItem>();
                services.AddScoped<GetCatalogUnit>();
                services.AddScoped<GetCatalogConversion>();
                services.AddScoped<IPricingReferenceReader, PricingReferenceReader>();
            }
            else services.AddSingleton<IPricingReferenceReader>(new PricingHostReferences(Item, Unit));
            services.AddScoped<PriceResolver>();
            services.AddScoped<PricingApplication>();
        }));
        var subject = Guid.NewGuid().ToString("N");
        _base.Bind(subject, Account);
        _base.AddTenantMembership(Account, Tenant, "Pricing tenant");
        _token = _base.CreateToken(subject);
        _client = _host.CreateClient();
    }

    internal object DraftBody(string scope = "default", Guid? target = null, Guid? unit = null, long? conversionRevision = null, decimal baseUnitPrice = 20) =>
        new
        {
            itemId = Item,
            unitId = unit ?? Unit,
            currencyCode = "USD",
            scope,
            targetId = target,
            baseUnitPrice,
            validFrom = Now.AddDays(-1),
            validTo = Now.AddDays(1),
            conversionRevision
        };
    internal object SelectionBody(decimal? overridePrice = null, string? reason = null, Guid? customer = null, long? conversionRevision = null) =>
        new { itemId = Item, unitId = Unit, currencyCode = "USD", overridePrice, reason, customerId = customer, conversionRevision };
    internal PriceRevision Revision(decimal price, PriceValidity validity) => new(Tenant, Guid.NewGuid(), Store.Candidates.Count + 1,
        new(Item, "EA", "USD", PriceScope.Default(), Unit), price, validity, PricePublicationState.Published, Account, Now.AddDays(-2), Now.AddDays(-1));
    internal async Task<HttpResponseMessage> PostAsync(string path, object body, string? key = null, Guid? tenantId = null)
    {
        using var request = Request(path, key, tenantId);
        request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }
    internal async Task<HttpResponseMessage> PostJsonAsync(string path, string body)
    {
        using var request = Request(path, null);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await _client.SendAsync(request);
    }
    internal async Task<HttpResponseMessage> GetAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tenants/{Tenant:D}/pricing/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return await _client.SendAsync(request);
    }
    private HttpRequestMessage Request(string path, string? key, Guid? tenantId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenantId ?? Tenant:D}/pricing/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }
    public void Dispose() { _client.Dispose(); _host.Dispose(); _base.Dispose(); }
    private sealed class PricingHostClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class PricingHostReferences(Guid item, Guid unit) : IPricingReferenceReader
    {
        public Task<string> RequireCompatibleUnitAsync(PricingActorContext actor, Guid itemId, Guid unitId, long? conversionRevision, CancellationToken ct) =>
            itemId == item && unitId == unit ? Task.FromResult("EA") : throw new PricingValidationException("catalog_unit_incompatible", "Catalog unit is incompatible.");
        public Task RequireContextAsync(PricingActorContext actor, PriceSelectionContext context, CancellationToken ct) =>
            context.CustomerId.HasValue || context.ProgramId.HasValue || context.OrganizationId.HasValue
                ? throw new PricingValidationException("pricing_context_invalid", "Context is not tenant owned.") : Task.CompletedTask;
    }
}

internal sealed class PricingHostPermissions : ITenantPricingAuthorization
{
    internal bool View { get; set; }
    internal bool Edit { get; set; }
    internal bool Publish { get; set; }
    internal bool Retire { get; set; }
    internal bool Override { get; set; }
    internal bool Beyond { get; set; }
    internal bool PublishUnavailable { get; set; }
    internal bool OverrideUnavailable { get; set; }
    internal bool BeyondUnavailable { get; set; }
    internal int BeyondChecks { get; private set; }
    public Task<bool> CanViewAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(View);
    public Task<bool> CanEditDraftAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(Edit);
    public Task<bool> CanPublishAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Decision(Publish, PublishUnavailable);
    public Task<bool> CanRetireAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(Retire);
    public Task<bool> CanOverrideAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Decision(Override, OverrideUnavailable);
    public Task<bool> CanOverrideBeyondPolicyAsync(Guid accountId, Guid tenantId, CancellationToken ct)
    { BeyondChecks++; return Decision(Beyond, BeyondUnavailable); }
    private static Task<bool> Decision(bool allowed, bool unavailable) => unavailable
        ? throw new AuthorizationProviderUnavailableException("Synthetic pricing authorization outage.", new InvalidOperationException()) : Task.FromResult(allowed);
}

// Pipeline-only controlled port; provider durability is proved in the PostgreSQL suite.
internal sealed class PricingHostStore(Guid tenant, Guid account, DateTimeOffset now) : IPricingPublicationStore, IPricingCandidateReader, IPricingPolicyStore
{
    private readonly Dictionary<Guid, PriceRevision> _prices = [];
    private readonly Dictionary<(string Operation, string Key), (string Fingerprint, object Snapshot)> _receipts = [];
    private PricingPolicySnapshot _policy = new(tenant, new(1, 0, 100, 10, 20), account, now);
    internal List<PriceRevision> Candidates { get; } = [];
    internal int CandidateReads { get; private set; }
    internal int Effects { get; private set; }
    internal string? UnreadableReceiptKey { get; set; }
    public Task<PriceRevision?> FindAsync(PricingActorContext actor, Guid revisionId, CancellationToken ct) => Task.FromResult(_prices.GetValueOrDefault(revisionId));
    public Task<CreatePriceDraftResult> CreateDraftAsync(PricingActorContext actor, CreatePriceDraftRequest request, string key, CancellationToken ct)
    {
        if (key == UnreadableReceiptKey)
            throw new PricingStoredContractException("pricing_receipt_version_unsupported", "This retained receipt predates the immutable stable-unit contract.");
        if (_receipts.TryGetValue(("create", key), out var retained)) return Task.FromResult(new CreatePriceDraftResult(
            retained.Fingerprint == request.Fingerprint ? CreatePriceDraftStatus.Replayed : CreatePriceDraftStatus.IdempotencyKeyConflict,
            retained.Fingerprint == request.Fingerprint ? (PriceRevision)retained.Snapshot : null));
        var price = new PriceRevision(tenant, Guid.NewGuid(), _prices.Count + 1, request.Key, request.BaseUnitPrice, request.Validity,
            PricePublicationState.Draft, account, now, priceId: request.PriceId ?? Guid.NewGuid());
        _prices.Add(price.RevisionId, price);
        _receipts.Add(("create", key), (request.Fingerprint, price));
        Effects++;
        return Task.FromResult(new CreatePriceDraftResult(CreatePriceDraftStatus.Created, price));
    }
    public Task<PublishPriceResult> PublishAsync(PricingActorContext actor, PublishPriceRequest request, string key, CancellationToken ct)
    {
        if (_receipts.TryGetValue(("publish", key), out var retained)) return Task.FromResult(new PublishPriceResult(
            retained.Fingerprint == request.Fingerprint ? PublishPriceStatus.Replayed : PublishPriceStatus.IdempotencyKeyConflict,
            retained.Fingerprint == request.Fingerprint ? (PriceRevision)retained.Snapshot : null));
        var current = _prices.GetValueOrDefault(request.RevisionId);
        if (current is null) return Task.FromResult(new PublishPriceResult(PublishPriceStatus.NotFound, null));
        var price = current.Publish(now);
        _prices[price.RevisionId] = price;
        _receipts.Add(("publish", key), (request.Fingerprint, price));
        Effects++;
        return Task.FromResult(new PublishPriceResult(PublishPriceStatus.Published, price));
    }
    public Task<RetirePriceResult> RetireAsync(PricingActorContext actor, RetirePriceRequest request, string key, CancellationToken ct)
    {
        var price = _prices[request.RevisionId].Retire(now);
        _prices[price.RevisionId] = price;
        Effects++;
        return Task.FromResult(new RetirePriceResult(RetirePriceStatus.Retired, price));
    }
    public Task<IReadOnlyList<PriceRevision>> ListRevisionsAsync(PricingActorContext actor, PriceLookupRequest request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PriceRevision>>(_prices.Values.Take(request.Limit).ToArray());
    public Task<IReadOnlyList<PriceRevision>> GetPublishedCandidatesAsync(PricingActorContext actor, PriceLookupRequest request, CancellationToken ct)
    { CandidateReads++; return Task.FromResult<IReadOnlyList<PriceRevision>>(Candidates.ToArray()); }
    public Task<PricingPolicySnapshot?> GetCurrentPolicyAsync(PricingActorContext actor, CancellationToken ct) => Task.FromResult<PricingPolicySnapshot?>(_policy);
    public Task<PublishPricingPolicyResult> PublishPolicyAsync(PricingActorContext actor, PublishPricingPolicyRequest request, string key, CancellationToken ct)
    {
        if (request.ExpectedRevision != _policy.Policy.PolicyRevision) return Task.FromResult(new PublishPricingPolicyResult(PublishPricingPolicyStatus.RevisionConflict, null));
        _policy = new(tenant, new(request.ExpectedRevision + 1, request.MinimumUnitPrice, request.MaximumUnitPrice,
            request.MaximumDecreasePercent, request.MaximumIncreasePercent), account, now);
        Effects++;
        return Task.FromResult(new PublishPricingPolicyResult(PublishPricingPolicyStatus.Published, _policy));
    }
}
