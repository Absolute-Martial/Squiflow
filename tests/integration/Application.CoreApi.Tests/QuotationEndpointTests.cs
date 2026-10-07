using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.Catalog;
using Application.Pricing;
using Application.Quotations;
using Application.Tenancy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class QuotationEndpointTests
{
    [Fact]
    public async Task SeparateCreateAndManualPriceRightsPrecedeAnyStoredEffectAndReplayRechecksThem()
    {
        using var fixture = new Fixture();
        fixture.Authority.Denied.Add(QuotationCapability.Create);
        using var forbidden = await fixture.SendAsync(HttpMethod.Post, "", "not JSON", "create");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); Assert.True(forbidden.Headers.CacheControl?.NoStore);
        Assert.Equal(0, fixture.Store.Effects);
        fixture.Authority.Denied.Clear(); fixture.Authority.Denied.Add(QuotationCapability.ManualPricing);
        using var noPrice = await fixture.SendAsync(HttpMethod.Post, "", Fixture.Body(), "create");
        Assert.Equal(HttpStatusCode.Forbidden, noPrice.StatusCode); Assert.Equal(0, fixture.Store.Effects);
        fixture.Authority.Denied.Clear();
        using var created = await fixture.SendAsync(HttpMethod.Post, "", Fixture.Body(), "create");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(original);
        Assert.Equal(20m, document.RootElement.GetProperty("draft").GetProperty("total").GetDecimal());
        Assert.Equal(1, fixture.Store.Effects);
        fixture.Authority.Denied.Add(QuotationCapability.ManualPricing);
        using var revoked = await fixture.SendAsync(HttpMethod.Post, "", Fixture.Body(), "create");
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        fixture.Authority.Denied.Clear();
        using var replay = await fixture.SendAsync(HttpMethod.Post, "", Fixture.Body(), "create");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(1, fixture.Store.Effects);
    }
    [Theory]
    [InlineData("{\"mode\":\"manual\",\"MODE\":\"manual\"}")]
    [InlineData("{\"mode\":\"manual\",\"summary\":\"Offer\",\"currencyCode\":\"USD\",\"validUntil\":\"2099-10-12T12:00:00Z\",\"lines\":null}")]
    [InlineData("{\"mode\":\"manual\",\"summary\":\"Offer\",\"currencyCode\":\"USD\",\"validUntil\":\"2099-10-12T12:00:00Z\",\"lines\":[null]}")]
    [InlineData("{\"mode\":\"manual\",\"summary\":\"Offer\",\"currencyCode\":\"USD\",\"validUntil\":\"2099-10-12T12:00:00\",\"lines\":[{\"quantity\":1,\"description\":\"Item\",\"unitCode\":\"EA\",\"unitPrice\":10}]}")]
    [InlineData("{\"mode\":\"manual\",\"summary\":\"Offer\",\"currencyCode\":\"USD\",\"validUntil\":\"2099-10-12T12:00:00Z\",\"canIssue\":true,\"lines\":[]}")]
    public async Task StrictShapeTimezoneAndServerAuthorityBoundaryRejectUntrustedFields(string body)
    {
        using var fixture = new Fixture();
        using var response = await fixture.SendAsync(HttpMethod.Post, "", body, "invalid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, fixture.Store.Effects);
    }
    [Fact]
    public async Task SizeAndProviderOutageFailBeforeEffectsAndIssueRequiresItsIndependentRight()
    {
        using var fixture = new Fixture();
        using var oversized = await fixture.SendAsync(HttpMethod.Post, "", new string('x', 65537), "large");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        fixture.Authority.Unavailable = true;
        using var unavailable = await fixture.SendAsync(HttpMethod.Post, "", Fixture.Body(), "outage");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode); Assert.True(unavailable.Headers.CacheControl?.NoStore);
        fixture.Authority.Unavailable = false;
        using var created = await fixture.SendAsync(HttpMethod.Post, "", Fixture.Body(), "create");
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("quotationId").GetGuid();
        fixture.Authority.Denied.Add(QuotationCapability.Issue);
        using var denied = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/issue", "not JSON", "issue");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(1, fixture.Store.Effects);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ElevatedCatalogReplayRechecksHistoricalAuthorityWithoutReadingChangedSources(bool unavailable)
    {
        using var fixture = new Fixture();
        var body = fixture.CatalogBody();
        using var created = await fixture.SendAsync(HttpMethod.Post, "", body, "elevated");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var historical = await created.Content.ReadAsStringAsync();
        var offer = fixture.Store.Current!.Draft!;
        var retained = offer.Lines[0].PriceSelection!;
        var forged = offer with
        {
            Lines = [offer.Lines[0] with { PriceSelection = retained with {
            Explanation = retained.Explanation with { Override = retained.Explanation.Override! with { BeyondPolicy = false } } } }]
        };
        Assert.Throws<QuotationValidationException>(() => QuotationRules.Validate(forged));
        fixture.Prices.Candidates.Clear();
        fixture.Catalog.Selection = new(CatalogLineFactsStatus.ItemRetired, null);
        var reads = fixture.Prices.CandidateReads;
        fixture.Authority.Denied.Add(QuotationCapability.OverrideBeyondPolicy);
        fixture.Authority.ElevatedUnavailable = unavailable;
        using var denied = await fixture.SendAsync(HttpMethod.Post, "", body, "elevated");
        Assert.Equal(unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(reads, fixture.Prices.CandidateReads); Assert.Equal(1, fixture.Store.Effects);
        fixture.Authority.Denied.Clear(); fixture.Authority.ElevatedUnavailable = false;
        using var recovered = await fixture.SendAsync(HttpMethod.Post, "", body, "elevated");
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(historical, await recovered.Content.ReadAsStringAsync());
        Assert.Equal(reads, fixture.Prices.CandidateReads); Assert.Equal(1, fixture.Store.Effects);
    }

    [Fact]
    public async Task IssueDetailAndHistoryUseNamedProjectionsAndViewRightsRemainIndependent()
    {
        using var fixture = new Fixture();
        using var created = await fixture.SendAsync(HttpMethod.Post, "", Fixture.Body(), "create");
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("quotationId").GetGuid();
        using var issued = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/issue", "{\"expectedVersion\":1}", "issue");
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        using var result = JsonDocument.Parse(await issued.Content.ReadAsStringAsync());
        var offer = result.RootElement.GetProperty("currentIssued").GetProperty("offer");
        Assert.Equal(20m, offer.GetProperty("total").GetDecimal());
        Assert.Equal("2099-10-12T06:15:00+00:00", offer.GetProperty("validUntil").GetString());
        fixture.Authority.Denied.Add(QuotationCapability.Issue);
        using var detail = await fixture.SendAsync(HttpMethod.Get, $"/{id:D}", "", "read");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.True(detail.Headers.CacheControl?.NoStore);
        using var history = await fixture.SendAsync(HttpMethod.Get, $"/{id:D}/issued?limit=1", "", "history");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var past = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Equal(20m, past.RootElement.GetProperty("items")[0].GetProperty("offer").GetProperty("total").GetDecimal());
        fixture.Authority.Denied.Add(QuotationCapability.View);
        using var denied = await fixture.SendAsync(HttpMethod.Get, $"/{id:D}", "", "denied");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ResponseAndConversionRoutesUseIndependentRightsAndNeverReturnPricesOrLines()
    {
        using var fixture = new Fixture();
        using var created = await fixture.SendAsync(HttpMethod.Post, "", Fixture.Body(), "create");
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("quotationId").GetGuid();
        using var issued = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/issue", "{\"expectedVersion\":1}", "issue");
        using var issuedJson = JsonDocument.Parse(await issued.Content.ReadAsStringAsync());
        var revision = issuedJson.RootElement.GetProperty("currentIssued").GetProperty("revisionId").GetGuid();

        var responseBody = JsonSerializer.Serialize(new { issuedRevisionId = revision, expectedVersion = 2, evidence = "Approved by customer", customerClaim = "buyer@example.test" });
        fixture.Authority.Denied.Add(QuotationCapability.Respond);
        using var responseDenied = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/accept", responseBody, "accept");
        Assert.Equal(HttpStatusCode.Forbidden, responseDenied.StatusCode);
        Assert.Equal(0, fixture.Store.ResponseEffects);
        fixture.Authority.Denied.Remove(QuotationCapability.Respond);

        fixture.Authority.Denied.Add(QuotationCapability.ManualPricing);
        fixture.Authority.Denied.Add(QuotationCapability.Issue);
        fixture.Authority.Denied.Add(QuotationCapability.View);
        using var accepted = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/accept", responseBody, "accept");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var acceptedText = await accepted.Content.ReadAsStringAsync();
        using var acceptedJson = JsonDocument.Parse(acceptedText);
        Assert.Equal("accepted", acceptedJson.RootElement.GetProperty("response").GetProperty("kind").GetString());
        Assert.Equal("buyer@example.test", acceptedJson.RootElement.GetProperty("response").GetProperty("customerClaim").GetString());
        Assert.DoesNotContain("total", acceptedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPrice", acceptedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lines", acceptedText, StringComparison.OrdinalIgnoreCase);

        using var acceptedReplay = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/accept", responseBody, "accept");
        Assert.Equal(HttpStatusCode.OK, acceptedReplay.StatusCode);
        Assert.Equal("true", acceptedReplay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(1, fixture.Store.ResponseEffects);
        fixture.Authority.Denied.Add(QuotationCapability.Respond);
        using var revokedReplay = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/accept", responseBody, "accept");
        Assert.Equal(HttpStatusCode.Forbidden, revokedReplay.StatusCode);
        Assert.Equal(1, fixture.Store.ResponseEffects);
        fixture.Authority.Denied.Remove(QuotationCapability.Respond);

        using var historyDenied = await fixture.SendAsync(HttpMethod.Get, $"/{id:D}/issued/{revision:D}/response", "", "read-response");
        Assert.Equal(HttpStatusCode.Forbidden, historyDenied.StatusCode);
        fixture.Authority.Denied.Remove(QuotationCapability.View);
        using var history = await fixture.SendAsync(HttpMethod.Get, $"/{id:D}/issued/{revision:D}/response", "", "read-response");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.DoesNotContain("total", await history.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        fixture.Authority.Denied.Add(QuotationCapability.Convert);
        using var convertDenied = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/convert",
            JsonSerializer.Serialize(new { issuedRevisionId = revision, expectedVersion = 3 }), "convert");
        Assert.Equal(HttpStatusCode.Forbidden, convertDenied.StatusCode);
        Assert.Equal(1, fixture.Store.ResponseEffects);

        fixture.Authority.Denied.Clear();
        fixture.Authority.Denied.Add(QuotationCapability.ManualPricing);
        fixture.Authority.Denied.Add(QuotationCapability.Issue);
        using var converted = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/convert",
            JsonSerializer.Serialize(new { issuedRevisionId = revision, expectedVersion = 3 }), "convert");
        Assert.Equal(HttpStatusCode.OK, converted.StatusCode);
        var convertedText = await converted.Content.ReadAsStringAsync();
        using var convertedJson = JsonDocument.Parse(convertedText);
        Assert.True(convertedJson.RootElement.TryGetProperty("conversion", out var conversion));
        Assert.True(conversion.TryGetProperty("originalOrder", out var order));
        Assert.True(order.TryGetProperty("orderId", out _));
        Assert.True(order.TryGetProperty("revision", out _));
        Assert.DoesNotContain("total", convertedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPrice", convertedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lines", convertedText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, fixture.Store.ConversionEffects);
        using var convertedReplay = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/convert",
            JsonSerializer.Serialize(new { issuedRevisionId = revision, expectedVersion = 3 }), "convert");
        Assert.Equal(HttpStatusCode.OK, convertedReplay.StatusCode);
        Assert.Equal("true", convertedReplay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(1, fixture.Store.ConversionEffects);
        fixture.Authority.Denied.Add(QuotationCapability.OrderCreate);
        using var orderCreateRevokedReplay = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/convert",
            JsonSerializer.Serialize(new { issuedRevisionId = revision, expectedVersion = 3 }), "convert");
        Assert.Equal(HttpStatusCode.Forbidden, orderCreateRevokedReplay.StatusCode);
        Assert.Equal(1, fixture.Store.ConversionEffects);
    }

    [Fact]
    public async Task ResponseAuthorityOutageIsSafeAndPrecedesTheStore()
    {
        using var fixture = new Fixture();
        fixture.Authority.Unavailable = true;
        using var response = await fixture.SendAsync(HttpMethod.Post, "/00000000-0000-0000-0000-000000000001/accept",
            "{\"issuedRevisionId\":\"00000000-0000-0000-0000-000000000002\",\"expectedVersion\":1,\"evidence\":\"ok\",\"customerClaim\":\"buyer\"}", "outage");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("authorization_unavailable", await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("Synthetic quotation outage", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, fixture.Store.ResponseEffects);
    }

    [Fact]
    public async Task ExpiryUsesItsOwnRightAndRejectsCustomerClaims()
    {
        using var fixture = new Fixture();
        using var created = await fixture.SendAsync(HttpMethod.Post, "", Fixture.Body(), "create");
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("quotationId").GetGuid();
        using var issued = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/issue", "{\"expectedVersion\":1}", "issue");
        using var issuedJson = JsonDocument.Parse(await issued.Content.ReadAsStringAsync());
        var revision = issuedJson.RootElement.GetProperty("currentIssued").GetProperty("revisionId").GetGuid();
        fixture.Authority.Denied.Add(QuotationCapability.Respond);
        var withClaim = JsonSerializer.Serialize(new { issuedRevisionId = revision, expectedVersion = 2, evidence = "Expired notice", customerClaim = "buyer" });
        using var rejectedClaim = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/expire", withClaim, "expire-claim");
        Assert.Equal(HttpStatusCode.BadRequest, rejectedClaim.StatusCode);
        Assert.Equal("response_invalid", await Code(rejectedClaim));
        Assert.Equal(0, fixture.Store.ResponseEffects);
        var withoutClaim = JsonSerializer.Serialize(new { issuedRevisionId = revision, expectedVersion = 2, evidence = "Expiry recorded" });
        using var expired = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/expire", withoutClaim, "expire");
        Assert.Equal(HttpStatusCode.OK, expired.StatusCode);
        using var expiredJson = JsonDocument.Parse(await expired.Content.ReadAsStringAsync());
        Assert.Equal("expired", expiredJson.RootElement.GetProperty("response").GetProperty("kind").GetString());
        Assert.Equal(1, fixture.Store.ResponseEffects);
    }

    [Theory]
    [InlineData("missing_evidence")]
    [InlineData("null_evidence")]
    [InlineData("null_claim")]
    [InlineData("long_evidence")]
    [InlineData("long_claim")]
    public async Task ResponseEvidenceMustBePresentNonNullAndBounded(string invalid)
    {
        using var fixture = new Fixture();
        var fields = new List<string>
        {
            "\"issuedRevisionId\":\"00000000-0000-0000-0000-000000000001\"",
            "\"expectedVersion\":1",
            "\"evidence\":\"ok\"",
            "\"customerClaim\":\"buyer\"",
        };
        switch (invalid)
        {
            case "missing_evidence": fields.RemoveAt(2); break;
            case "null_evidence": fields[2] = "\"evidence\":null"; break;
            case "null_claim": fields[3] = "\"customerClaim\":null"; break;
            case "long_evidence": fields[2] = JsonSerializer.Serialize(new { evidence = new string('e', 2001) })[1..^1]; break;
            case "long_claim": fields[3] = JsonSerializer.Serialize(new { customerClaim = new string('c', 301) })[1..^1]; break;
        }
        var body = $"{{{string.Join(",", fields)}}}";
        using var response = await fixture.SendAsync(HttpMethod.Post, "/00000000-0000-0000-0000-000000000002/accept", body, "bad-evidence");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, fixture.Store.ResponseEffects);
    }

    [Theory]
    [InlineData(false, "revision_conflict", QuotationCommandStatus.RevisionConflict)]
    [InlineData(true, "quotation_not_accepted", QuotationCommandStatus.NotAccepted)]
    public async Task ResponseAndConversionConflictsUseStableSafeCodes(bool conversion, string code, QuotationCommandStatus status)
    {
        using var fixture = new Fixture();
        using var created = await fixture.SendAsync(HttpMethod.Post, "", Fixture.Body(), "create");
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("quotationId").GetGuid();
        using var issued = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/issue", "{\"expectedVersion\":1}", "issue");
        using var issuedJson = JsonDocument.Parse(await issued.Content.ReadAsStringAsync());
        var revision = issuedJson.RootElement.GetProperty("currentIssued").GetProperty("revisionId").GetGuid();
        var body = conversion
            ? JsonSerializer.Serialize(new { issuedRevisionId = revision, expectedVersion = 2 })
            : JsonSerializer.Serialize(new { issuedRevisionId = revision, expectedVersion = 2, evidence = "Rejected", customerClaim = "buyer" });
        if (conversion) fixture.Store.ConvertStatusOverride = status;
        else fixture.Store.RespondStatusOverride = status;
        using var response = await fixture.SendAsync(HttpMethod.Post, $"/{id:D}/{(conversion ? "convert" : "reject")}", body, "conflict");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(code, await Code(response));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, conversion ? fixture.Store.ConversionEffects : fixture.Store.ResponseEffects);
    }

    [Theory]
    [InlineData("{\"issuedRevisionId\":\"00000000-0000-0000-0000-000000000001\",\"expectedVersion\":2,\"evidence\":\"ok\",\"Evidence\":\"duplicate\",\"customerClaim\":\"buyer\"}")]
    [InlineData("{\"issuedRevisionId\":\"00000000-0000-0000-0000-000000000001\",\"expectedVersion\":2,\"evidence\":\"ok\",\"admin\":true,\"customerClaim\":\"buyer\"}")]
    public async Task ResponseRouteRejectsDuplicateAndUnknownFieldsBeforeStore(string body)
    {
        using var fixture = new Fixture();
        using var response = await fixture.SendAsync(HttpMethod.Post, "/00000000-0000-0000-0000-000000000001/accept", body, "invalid-response");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.Store.ResponseEffects);
    }

    [Fact]
    public async Task ResponseRouteRejectsOversizedBodyBeforeStore()
    {
        using var fixture = new Fixture();
        using var response = await fixture.SendAsync(HttpMethod.Post, "/00000000-0000-0000-0000-000000000001/accept", new string('x', 65537), "large-response");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, fixture.Store.ResponseEffects);
    }

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly WhiteLabelApiFactory _base = new();
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _host;
        private readonly HttpClient _client;
        private readonly string _token;
        private readonly Guid _tenant = Guid.NewGuid();
        internal Guid Item { get; } = Guid.NewGuid();
        internal Guid Unit { get; } = Guid.NewGuid();
        internal CatalogHostStore Catalog { get; } = new();
        internal PricingHostStore Prices { get; }
        internal Authority Authority { get; } = new();
        internal ProbeStore Store { get; } = new();
        internal Fixture()
        {
            var account = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            Catalog.Selection = new(CatalogLineFactsStatus.Available, new(Item, "SKU", Unit, "Quoted item", "EA", "Each", 2, 4,
                1, 1, new(Unit, Unit, 1, 1, 1), "EA", "Each", 4, 1, 2));
            Prices = new(_tenant, account, now);
            Prices.Candidates.Add(new(_tenant, Guid.NewGuid(), 1, new(Item, "EA", "USD", PriceScope.Default(), Unit), 20,
                new(now.AddDays(-1)), PricePublicationState.Published, account, now.AddDays(-1), now.AddDays(-1)));

            _host = _base.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IQuotationAuthority>(); services.AddSingleton<IQuotationAuthority>(Authority);
                services.RemoveAll<IQuotationStore>(); services.AddSingleton<IQuotationStore>(Store);
                services.RemoveAll<IQuotationResponseStore>(); services.AddSingleton<IQuotationResponseStore>(Store);
                services.RemoveAll<ICatalogStore>(); services.AddSingleton<ICatalogStore>(Catalog);
                services.RemoveAll<IPricingPublicationStore>(); services.AddSingleton<IPricingPublicationStore>(Prices);
                services.RemoveAll<IPricingCandidateReader>(); services.AddSingleton<IPricingCandidateReader>(Prices);
                services.RemoveAll<IPricingPolicyStore>(); services.AddSingleton<IPricingPolicyStore>(Prices);
                services.RemoveAll<IPricingReferenceReader>(); services.AddSingleton<IPricingReferenceReader>(new References(Item, Unit));
            }));
            var subject = Guid.NewGuid().ToString("N");
            _base.Bind(subject, account); _base.AddTenantMembership(account, _tenant, "Quotation tenant");
            _token = _base.CreateToken(subject); _client = _host.CreateClient();
        }
        internal string CatalogBody() => JsonSerializer.Serialize(new
        {
            mode = "catalog",
            summary = "Offer",
            currencyCode = "USD",
            validUntil = "2099-10-12T12:00:00Z",
            lines = new[] { new { quantity = 2, itemId = Item, unitId = Unit,
                overridePrice = 40, overrideReason = "Commercial exception" } }
        });
        internal static string Body() => JsonSerializer.Serialize(new
        {
            mode = "manual",
            summary = "Offer",
            currencyCode = "USD",
            validUntil = "2099-10-12T12:00:00+05:45",
            lines = new[] { new { quantity = 2, description = "Item", unitCode = "EA", unitPrice = 10 } }
        });
        internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string body, string key)
        {
            using var request = new HttpRequestMessage(method, $"/api/v1/tenants/{_tenant:D}/quotations{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token); request.Headers.Add("Idempotency-Key", key);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return await _client.SendAsync(request);
        }
        public void Dispose() { _client.Dispose(); _host.Dispose(); _base.Dispose(); }
    }
    private sealed class Authority : IQuotationAuthority
    {
        internal HashSet<QuotationCapability> Denied { get; } = [];
        internal bool Unavailable { get; set; }
        internal bool ElevatedUnavailable { get; set; }
        public Task<bool> CheckAsync(TenantContext context, QuotationCapability capability, CancellationToken ct) =>
            (Unavailable || ElevatedUnavailable && capability == QuotationCapability.OverrideBeyondPolicy) ? throw new AuthorizationProviderUnavailableException("Synthetic quotation outage.", new InvalidOperationException()) : Task.FromResult(!Denied.Contains(capability));
    }
    private sealed class References(Guid item, Guid unit) : IPricingReferenceReader
    {
        public Task<string> RequireCompatibleUnitAsync(PricingActorContext actor, Guid itemId, Guid unitId, long? revision, CancellationToken ct) =>
            itemId == item && unitId == unit && revision is null ? Task.FromResult("EA") : throw new InvalidOperationException("Unexpected quote reference.");
        public Task RequireContextAsync(PricingActorContext actor, PriceSelectionContext context, CancellationToken ct) => Task.CompletedTask;
    }
    // This probe proves host/authority flow, not PostgreSQL durability or issue semantics.
    private sealed class ProbeStore : IQuotationStore, IQuotationResponseStore
    {
        private readonly Dictionary<string, QuotationSnapshot> _receipts = [];
        private readonly Dictionary<(string Operation, string Key), (string Fingerprint, QuotationCommandResult Result)> _responseReceipts = [];
        internal int Effects { get; private set; }
        private QuotationSnapshot? _current;
        internal int ResponseEffects { get; private set; }
        internal int ConversionEffects { get; private set; }
        internal QuotationCommandStatus? RespondStatusOverride { get; set; }
        internal QuotationCommandStatus? ConvertStatusOverride { get; set; }
        internal QuotationSnapshot? Current => _current;
        public Task<QuotationCommandResult?> FindReceiptAsync(TenantContext context, string operation, string key, string fingerprint, CancellationToken ct) =>
            Task.FromResult(_receipts.TryGetValue(key, out var snapshot) ? new QuotationCommandResult(QuotationCommandStatus.Replayed, snapshot) : null);
        public Task<QuotationCommandResult> CreateDraftAsync(TenantContext context, QuotationDraftFacts draft, string key, string fingerprint, CancellationToken ct)
        {
            var snapshot = new QuotationSnapshot(Guid.NewGuid(), context.TenantId, context.AccountId, DateTimeOffset.UtcNow, 1, null, draft, null);
            _receipts.Add(key, snapshot); _current = snapshot; Effects++;
            return Task.FromResult(new QuotationCommandResult(QuotationCommandStatus.Created, snapshot));
        }
        public Task<QuotationCommandResult> ReviseDraftAsync(TenantContext context, Guid id, long expectedVersion, QuotationDraftFacts draft, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
        public async Task<QuotationCommandResult> IssueAsync(TenantContext context, Guid id, long expectedVersion, string key, string fingerprint, Func<QuotationDraftFacts, CancellationToken, Task<bool>> guard, CancellationToken ct)
        {
            var draft = _current!.Draft!;
            Assert.True(await guard(draft, ct));
            _current = _current with
            {
                Version = 2,
                Number = 1,
                Draft = null,
                CurrentIssued = new(id, Guid.NewGuid(), context.TenantId, 1, 1, context.AccountId, DateTimeOffset.UtcNow, draft)
            };
            return new(QuotationCommandStatus.Issued, _current);
        }
        public Task<QuotationSnapshot?> FindAsync(TenantContext context, Guid id, CancellationToken ct) => Task.FromResult(_current);
        public Task<QuotationIssuedPage> ListIssuedAsync(TenantContext context, Guid id, long afterRevision, int limit, CancellationToken ct) => Task.FromResult(new QuotationIssuedPage([_current!.CurrentIssued!], null));
        public Task<QuotationCommandResult> RespondAsync(TenantContext context, Guid quotationId, QuotationResponseKind kind,
            QuotationResponseRequest request, string key, string fingerprint, CancellationToken ct)
        {
            var operation = $"respond:{kind}";
            if (_responseReceipts.TryGetValue((operation, key), out var receipt))
                return Task.FromResult(receipt.Fingerprint == fingerprint
                    ? new QuotationCommandResult(QuotationCommandStatus.Replayed, receipt.Result.Quotation)
                    : new QuotationCommandResult(QuotationCommandStatus.IdempotencyKeyConflict, _current));
            if (RespondStatusOverride is { } overridden)
                return Task.FromResult(new QuotationCommandResult(overridden, _current));
            var facts = new QuotationResponseFacts(quotationId, request.IssuedRevisionId, context.TenantId, kind,
                context.AccountId, DateTimeOffset.UtcNow, request.Evidence, request.CustomerClaim);
            _current = _current! with { Version = _current.Version + 1, CurrentResponse = facts };
            ResponseEffects++;
            var result = new QuotationCommandResult(kind switch
            {
                QuotationResponseKind.Accepted => QuotationCommandStatus.Accepted,
                QuotationResponseKind.Rejected => QuotationCommandStatus.Rejected,
                _ => QuotationCommandStatus.Expired,
            }, _current);
            _responseReceipts.Add((operation, key), (fingerprint, result));
            return Task.FromResult(result);
        }
        public Task<QuotationCommandResult> ConvertAsync(TenantContext context, Guid quotationId, QuotationConvertRequest request,
            string key, string fingerprint, CancellationToken ct)
        {
            const string operation = "convert";
            if (_responseReceipts.TryGetValue((operation, key), out var receipt))
                return Task.FromResult(receipt.Fingerprint == fingerprint
                    ? new QuotationCommandResult(QuotationCommandStatus.Replayed, receipt.Result.Quotation)
                    : new QuotationCommandResult(QuotationCommandStatus.IdempotencyKeyConflict, _current));
            if (ConvertStatusOverride is { } overridden)
                return Task.FromResult(new QuotationCommandResult(overridden, _current));
            var order = new Application.Orders.OrderDraftSnapshot(Guid.NewGuid(), context.TenantId, context.AccountId,
                "Private offer", "USD", 20, 1, DateTimeOffset.UtcNow, []);
            var facts = new QuotationConversionFacts(quotationId, request.IssuedRevisionId, context.TenantId,
                context.AccountId, DateTimeOffset.UtcNow, order);
            _current = _current! with { Version = _current.Version + 1, Conversion = facts };
            ConversionEffects++;
            var result = new QuotationCommandResult(QuotationCommandStatus.Converted, _current);
            _responseReceipts.Add((operation, key), (fingerprint, result));
            return Task.FromResult(result);
        }
        public Task<QuotationResponseFacts?> FindResponseAsync(TenantContext context, Guid quotationId, Guid issuedRevisionId, CancellationToken ct) =>
            Task.FromResult(_current?.CurrentResponse);
    }
}
