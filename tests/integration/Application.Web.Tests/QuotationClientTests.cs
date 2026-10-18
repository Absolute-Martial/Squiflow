using System.Net;
using System.Text;
using System.Text.Json;
using Application.Web.Business;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessClientTests
{
    private static readonly Guid QuoteId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid IssuedId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly QuotationOffer Offer = new("manual", "<script>Offer</script>", "NPR", Organization.CreatedAt.AddDays(5), 25, "Terms", null, null, false,
        [new(1, "Print", 2, "EA", 12.5m, 25, null, null, null, null, null)]);
    private static readonly QuotationRecord Quote = new(QuoteId, Tenant, 1, null, Guid.Parse("40000000-0000-0000-0000-000000000001"), Organization.CreatedAt, Offer, null, null, null);

    [Theory]
    [InlineData("2026-12-31T18:00:00+05:45", true)]
    [InlineData("2026-12-31T18:00:00Z", true)]
    [InlineData("2026-12-31T18:00:00", false)]
    [InlineData("2026-12-31", false)]
    [InlineData("12/31/2026 18:00Z", false)]
    [InlineData("2026-12-31T18:00:00.0000001Z", false)]
    public void QuotationExpiryRequiresExplicitOffsetAndSupportedPrecision(string text, bool valid) => Assert.Equal(valid, QuotationContracts.Instant(text, out _));

    [Fact]
    public void QuotationFactsRejectForeignIdentityUnsupportedStatesAndMixedModes()
    {
        Assert.True(QuotationContracts.Record(Quote, Tenant));
        Assert.False(QuotationContracts.Record(Quote with { TenantId = Guid.NewGuid() }, Tenant));
        Assert.False(QuotationContracts.Record(Quote, Tenant, Guid.NewGuid()));
        Assert.False(QuotationContracts.Offer(Offer with { Lines = [Offer.Lines[0] with { Position = 0 }] }));
        Assert.False(QuotationContracts.Offer(Offer with { Mode = "catalog" }));
        Assert.False(QuotationContracts.Record(Quote with { Draft = null }, Tenant));
        var fact = new QuotationResponseFact(QuoteId, IssuedId, "accepted", Quote.CreatedByAccountId, Organization.CreatedAt, "Recorded evidence", "Customer claim");
        Assert.True(QuotationContracts.Response(fact, QuoteId, IssuedId));
        Assert.False(QuotationContracts.Response(fact with { Kind = "approved" }, QuoteId));
        Assert.False(QuotationContracts.Response(fact, QuoteId, Guid.NewGuid()));
        Assert.False(QuotationContracts.Response(fact with { Kind = "expired" }, QuoteId));
        var link = new QuotationConversion(QuoteId, IssuedId, Quote.CreatedByAccountId, Organization.CreatedAt, new(Guid.NewGuid(), 1));
        Assert.True(QuotationContracts.Command(new(QuoteId, Tenant, 4, fact, link), Tenant, QuoteId, IssuedId, "convert"));
        Assert.False(QuotationContracts.Command(new(QuoteId, Tenant, 4, fact, link), Tenant, QuoteId, Guid.NewGuid(), "convert"));
    }

    [Fact]
    public async Task QuotationReadAndFrozenPutUseOnlyPinnedTenantPathsAndServerCredentials()
    {
        var bodies = new List<string>();
        using var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal("api.example.test", request.RequestUri!.Host);
            Assert.Equal("private-session-token", request.Headers.Authorization?.Parameter);
            if (request.Method == HttpMethod.Get) return Json(Quote);
            Assert.Equal(HttpMethod.Put, request.Method);
            bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            if (bodies.Count == 1) throw new HttpRequestException("private diagnostic");
            var response = Json(Quote with { Version = 2 }); response.Headers.Add("Idempotency-Replayed", "true"); return response;
        });
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        Assert.NotNull((await api.GetAsync<QuotationRecord>(Tenant, $"quotations/{QuoteId:D}", row => QuotationContracts.Record(row, Tenant, QuoteId))).Value);
        var input = new QuotationDraftInput("manual", "Offer", "NPR", "2026-12-31T18:00:00+05:45", [new(2, "Print", "EA", 12.5m)], 1, null, null, null, false);
        var command = BusinessCommand.Create(Tenant, $"quotations/{QuoteId:D}/draft", input, HttpMethod.Put);
        Assert.True((await api.SendAsync<QuotationRecord>(command, row => QuotationContracts.Record(row, Tenant))).Unknown);
        var result = await api.SendAsync<QuotationRecord>(command, row => QuotationContracts.Record(row, Tenant));
        Assert.True(result.Replayed); Assert.Equal(bodies[0], bodies[1]);
        using var json = JsonDocument.Parse(bodies[0]); Assert.Equal(1, json.RootElement.GetProperty("expectedVersion").GetInt64());
        Assert.False(json.RootElement.TryGetProperty("permission", out _));
    }

    [Fact]
    public void QuotationTransportSupportsTheExistingHundredLineContractWithin64KiB()
    {
        var lines = Enumerable.Range(0, 100).Select(_ => new QuotationLineInput(1, new string('x', 300), "EA", 1)).ToArray();
        var input = new QuotationDraftInput("manual", "Offer", "NPR", "2026-12-31T18:00:00Z", lines, null, null, null, null, false);
        var json = JsonSerializer.Serialize(input, JsonSerializerOptions.Web);
        Assert.True(Encoding.UTF8.GetByteCount(json) > BusinessApiClient.MaximumRequestBytes);
        Assert.NotNull(BusinessCommand.Create(Tenant, "quotations", input));
        Assert.Throws<ArgumentException>(() => BusinessCommand.Create(Tenant, "quotations", new { text = new string('x', 65537) }));
        Assert.Throws<ArgumentException>(() => BusinessCommand.Create(Tenant, "orders", input));
    }

    [Fact]
    public async Task QuoteResponseRoutesNeverExposePrivateDiagnosticsAndRejectAnotherRevision()
    {
        var fact = new QuotationResponseFact(QuoteId, Guid.NewGuid(), "accepted", Quote.CreatedByAccountId, Organization.CreatedAt, "Recorded", "Customer");
        using var handler = new Handler((_, _) => Task.FromResult(Json(fact)));
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        var result = await api.GetAsync<QuotationResponseFact>(Tenant, $"quotations/{QuoteId:D}/issued/{IssuedId:D}/response", row => QuotationContracts.Response(row, QuoteId, IssuedId));
        Assert.Equal(BusinessFailure.InvalidResponse, result.Failure); Assert.Null(result.Value);
    }
}
