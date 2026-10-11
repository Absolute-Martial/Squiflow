using System.Text.Json;
using Application.Catalog;
using Application.Customers;
using Application.Pricing;
using Application.Tenancy;
using Xunit;

namespace Application.Orders.Tests;

public sealed class CatalogOrderDraftTests
{
    // The durable commercial-facts JSON shape the Orders store reads back.
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PublishedSelectionUsesOrdersArithmeticAndRetainsCompleteFrozenFacts()
    {
        var fixture = await Fixture.CreateAsync();
        var result = await fixture.Application.CreateAsync(fixture.Context, fixture.Request, "create", new(false), default);
        var line = Assert.Single(result.Order!.Lines);
        Assert.Equal(2.469m, line.LineTotal);
        Assert.Equal(line.LineTotal, result.Order.Total);
        Assert.Equal("Original catalog name", line.Description);
        var retained = Assert.IsType<OrderCommercialLineFacts>(line.CommercialFacts);
        Assert.Equal(fixture.Price.RevisionId, retained.PublishedPrice.RevisionId);
        Assert.Equal(fixture.Price.PriceId, retained.PriceSelection.Explanation.SelectedPriceId);
        Assert.Equal(1, retained.PriceSelection.Explanation.Policy!.PolicyRevision);
        Assert.Equal(2m, retained.Catalog.BaseQuantity);
        Assert.Equal("Each", retained.Catalog.BaseUnitName);
        OrderCommercialFactsValidation.RequireValid(result.Order);
    }

    [Fact]
    public async Task CatalogCodesRetainTheirFullMeaningWithoutBroadeningManualIngress()
    {
        var fixture = await Fixture.CreateAsync();
        const string code = "UNIT-M2_WITH-LONG-CODE";
        fixture.Catalog.Facts = fixture.Catalog.Facts! with { UnitCode = code };
        fixture.Pricing.UnitCode = code;
        fixture.Pricing.Candidates.Clear();
        fixture.Pricing.Candidates.Add(new(fixture.Context.TenantId, fixture.Price.RevisionId, 1,
            new(fixture.Price.Key.ItemId, code, "USD", PriceScope.Default(), fixture.Price.Key.UnitId),
            fixture.Price.BaseUnitPrice, fixture.Price.Validity, PricePublicationState.Published,
            fixture.Context.AccountId, fixture.Price.CreatedAt, fixture.Price.PublishedAt));
        var result = await fixture.Application.CreateAsync(fixture.Context, fixture.Request, "long-code", new(false), default);
        Assert.Equal(code, result.Order!.Lines[0].UnitCode);
        Assert.Equal(code, result.Order.Lines[0].CommercialFacts!.Catalog.UnitCode);
        Assert.Equal(2.469m, result.Order.Total);
        Assert.Throws<OrderDraftValidationException>(() => OrderDraftIntent.Create(new("Manual", "USD", [new("Manual", 2, code, 1.2345m)])));
        OrderCommercialFactsValidation.RequireValid(result.Order);
    }

    [Fact]
    public async Task ReplayDoesNotReadOrRefreshCurrentCommercialSourcesAndDecimalScaleIsSemantic()
    {
        var fixture = await Fixture.CreateAsync();
        var original = await fixture.Application.CreateAsync(fixture.Context, fixture.Request, "create", new(false), default);
        fixture.Catalog.Facts = null;
        fixture.Pricing.Candidates.Clear();
        var reads = fixture.Catalog.Reads;
        var replay = await fixture.Application.CreateAsync(fixture.Context, fixture.Request with
        { Lines = [fixture.Request.Lines[0] with { Quantity = 2.0000m }] }, "create", new(false), default);
        Assert.Equal(CreateOrderDraftStatus.Replayed, replay.Status);
        Assert.Same(original.Order, replay.Order);
        Assert.Equal(reads, fixture.Catalog.Reads);
        Assert.Equal(CreateOrderDraftStatus.IdempotencyKeyConflict,
            (await fixture.Application.CreateAsync(fixture.Context, fixture.Request with { Summary = "Different" }, "create", new(false), default)).Status);
    }

    [Fact]
    public async Task RevisionRetainsReceiptEvenAfterTheSelectedPriceDisappears()
    {
        var fixture = await Fixture.CreateAsync();
        var created = await fixture.Application.CreateAsync(fixture.Context, fixture.Request, "create", new(false), default);
        var request = new ReviseCatalogOrderDraftRequest(created.Order!.OrderId, 1,
            fixture.Request with { Summary = "Revised selection" });
        var revised = await fixture.Application.ReviseAsync(fixture.Context, request, "revise", new(false), default);
        Assert.Equal(ReviseOrderDraftStatus.Revised, revised.Status);
        Assert.Equal(2, revised.Order!.Revision);
        fixture.Pricing.Candidates.Clear();
        var replay = await fixture.Application.ReviseAsync(fixture.Context, request, "revise", new(false), default);
        Assert.Equal(ReviseOrderDraftStatus.Replayed, replay.Status);
        Assert.Same(revised.Order, replay.Order);
        Assert.Equal(ReviseOrderDraftStatus.IdempotencyKeyConflict,
            (await fixture.Application.ReviseAsync(fixture.Context, request with { ExpectedRevision = 2 }, "revise", new(false), default)).Status);
    }

    [Theory]
    [InlineData(false, false, 1, "discount", "pricing_override_forbidden")]
    [InlineData(true, false, 200, "increase", "price_override_required")]
    public async Task OverridesNeverGainAuthorityFromPriceOrReason(bool canOverride, bool beyond, int price, string reason, string code)
    {
        var fixture = await Fixture.CreateAsync();
        var application = new CatalogOrderDraftApplication(fixture.Orders, fixture.Orders, new(fixture.Catalog),
            fixture.PricingApplication, new(new CustomersPort()), currentPricingAuthority: new Authority(new(true), beyond));
        var request = fixture.Request with { Lines = [fixture.Request.Lines[0] with { OverridePrice = price, OverrideReason = reason }] };
        var error = await Assert.ThrowsAsync<OrderCommercialSelectionException>(() =>
            application.CreateAsync(fixture.Context, request, "override", new(canOverride), default));
        Assert.Equal(code, error.Code);
        Assert.Null(fixture.Orders.Current);
    }

    [Fact]
    public async Task ElevatedOverrideRetainsReasonAndPolicyButMissingReasonOrInvalidQuantityCannotPersist()
    {
        var fixture = await Fixture.CreateAsync(elevatedOverride: true);
        var input = fixture.Request.Lines[0] with { OverridePrice = 200m, OverrideReason = "  approved commercial exception  " };
        var overridden = await fixture.Application.CreateAsync(fixture.Context, fixture.Request with { Lines = [input] }, "override", new(true), default);
        Assert.Equal("approved commercial exception", overridden.Order!.Lines[0].CommercialFacts!.PriceSelection.Explanation.Override!.Reason);
        Assert.True(overridden.Order.Lines[0].CommercialFacts!.PriceSelection.Explanation.Override!.BeyondPolicy);
        await Assert.ThrowsAsync<OrderDraftValidationException>(() => fixture.Application.CreateAsync(fixture.Context,
            fixture.Request with { Lines = [input with { OverrideReason = null }] }, "bad-reason", new(true), default));
        await Assert.ThrowsAsync<OrderDraftValidationException>(() => fixture.Application.CreateAsync(fixture.Context,
            fixture.Request with { Lines = [input with { Quantity = 1.00001m }] }, "bad-quantity", new(true), default));
    }

    [Fact]
    public async Task RevalidationRejectsPublicationOrPolicyChangesWithoutRepricingAndAllowsLabelRename()
    {
        var fixture = await Fixture.CreateAsync();
        var created = (await fixture.Application.CreateAsync(fixture.Context, fixture.Request, "create", new(false), default)).Order!;
        var guard = new OrderCommercialCommitGuard(new(fixture.Catalog), fixture.PricingApplication, new Authority());
        fixture.Catalog.Facts = fixture.Catalog.Facts! with { ItemName = "Renamed", ItemRevision = 2 };
        Assert.True(await CompatibleAsync(guard, fixture.Context, created));
        Assert.Equal("Original catalog name", created.Lines[0].Description);
        fixture.Pricing.Policy = new(2, 0, 100);
        Assert.False(await CompatibleAsync(guard, fixture.Context, created));
        fixture.Pricing.Policy = new(1, 0, 100);
        fixture.Pricing.Candidates.Clear();
        fixture.Pricing.Candidates.Add(new(fixture.Context.TenantId, Guid.NewGuid(), 2, fixture.Price.Key,
            fixture.Price.BaseUnitPrice, fixture.Price.Validity, PricePublicationState.Published,
            fixture.Context.AccountId, Fixture.Now, Fixture.Now));
        Assert.False(await CompatibleAsync(guard, fixture.Context, created));
        Assert.Equal(fixture.Price.RevisionId, created.Lines[0].CommercialFacts!.PublishedPrice.RevisionId);
    }

    [Fact]
    public async Task MutableCreationUsesCanonicalCustomerButReplayNeverRedirectsFrozenContext()
    {
        var fixture = await Fixture.CreateAsync();
        var requested = Guid.NewGuid(); var canonical = Guid.NewGuid();
        var directory = new CanonicalCustomers(fixture.Context, canonical);
        var app = new CatalogOrderDraftApplication(fixture.Orders, fixture.Orders, new(fixture.Catalog),
            fixture.PricingApplication, new(new CustomersPort()), directory);
        var request = fixture.Request with { CustomerId = requested };
        var created = await app.CreateAsync(fixture.Context, request, "canonical", new(false), default);
        Assert.Equal(canonical, created.Order!.Lines[0].CommercialFacts!.PriceSelection.Explanation.Context.CustomerId);
        directory.CurrentId = Guid.NewGuid();
        var replay = await app.CreateAsync(fixture.Context, request, "canonical", new(false), default);
        Assert.Equal(CreateOrderDraftStatus.Replayed, replay.Status);
        Assert.Equal(canonical, replay.Order!.Lines[0].CommercialFacts!.PriceSelection.Explanation.Context.CustomerId);
        Assert.Equal(1, directory.Reads);
        Assert.Equal(requested, request.CustomerId);
    }

    [Fact]
    public async Task RetiredOrUnavailableCatalogSelectionIsIncompatibleWithoutRefreshingRetainedFacts()
    {
        var fixture = await Fixture.CreateAsync();
        var created = (await fixture.Application.CreateAsync(fixture.Context, fixture.Request, "create", new(false), default)).Order!;
        fixture.Catalog.Facts = null;
        var guard = new OrderCommercialCommitGuard(new(fixture.Catalog), fixture.PricingApplication, new Authority());
        Assert.False(await CompatibleAsync(guard, fixture.Context, created));
        Assert.Equal("Original catalog name", created.Lines[0].CommercialFacts!.Catalog.ItemName);
    }

    [Theory]
    [InlineData("precision")]
    [InlineData("conversion")]
    [InlineData("base-quantity")]
    [InlineData("arithmetic")]
    public async Task ComparisonRejectsChangedCatalogMeaningEvenWhenPriceIdentityIsUnchanged(string change)
    {
        var fixture = await Fixture.CreateAsync();
        var created = (await fixture.Application.CreateAsync(fixture.Context, fixture.Request, "create", new(false), default)).Order!;
        var facts = fixture.Catalog.Facts!;
        fixture.Catalog.Facts = change switch
        {
            "precision" => facts with { UnitPrecision = 3 },
            "conversion" => facts with { Conversion = facts.Conversion with { Revision = 2 } },
            "base-quantity" => facts with { BaseQuantity = 3 },
            "arithmetic" => facts with { QuantityArithmeticVersion = 2 },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
        var guard = new OrderCommercialCommitGuard(new(fixture.Catalog), fixture.PricingApplication, new Authority());
        Assert.False(await CompatibleAsync(guard, fixture.Context, created));
        Assert.Equal(facts, created.Lines[0].CommercialFacts!.Catalog);
    }

    [Theory]
    [InlineData(1, true, false, true)]
    [InlineData(1, false, false, false)]
    [InlineData(200, true, true, true)]
    [InlineData(200, true, false, false)]
    public async Task ComparisonRechecksCurrentOverrideAndBeyondPolicyAuthority(int price, bool canOverride, bool beyond, bool compatible)
    {
        var fixture = await Fixture.CreateAsync(elevatedOverride: true);
        var request = fixture.Request with { Lines = [fixture.Request.Lines[0] with { OverridePrice = price, OverrideReason = "Commercial exception" }] };
        var created = (await fixture.Application.CreateAsync(fixture.Context, request, "create", new(true), default)).Order!;
        var guard = new OrderCommercialCommitGuard(new(fixture.Catalog), fixture.PricingApplication, new Authority(new(canOverride), beyond));
        Assert.Equal(compatible, await CompatibleAsync(guard, fixture.Context, created));
        Assert.Equal(price, created.Lines[0].UnitPrice);
    }

    [Theory]
    [InlineData("quantityArithmeticVersion")]
    [InlineData("quantityRounding")]
    public async Task RetainedFactsMissingDurableQuantityArithmeticAreRejectedInsteadOfReadAsVersionOne(string absentField)
    {
        var fixture = await Fixture.CreateAsync();
        var created = (await fixture.Application.CreateAsync(fixture.Context, fixture.Request, "create", new(false), default)).Order!;
        OrderCommercialFactsValidation.RequireValid(created);

        // Reproduce a durable payload that lost an arithmetic field. Catalog requires
        // both, so an absent one must not read back as the value the engine produced.
        var payload = JsonSerializer.SerializeToNode(created, SnapshotJsonOptions)!;
        payload["lines"]![0]!["commercialFacts"]!["catalog"]!.AsObject().Remove(absentField);
        var truncated = JsonSerializer.Deserialize<OrderDraftSnapshot>(
            payload.ToJsonString(SnapshotJsonOptions), SnapshotJsonOptions)!;
        var retained = created.Lines[0].CommercialFacts!.Catalog;
        var catalog = truncated.Lines[0].CommercialFacts!.Catalog;
        if (absentField == "quantityArithmeticVersion")
            Assert.NotEqual(retained.QuantityArithmeticVersion, catalog.QuantityArithmeticVersion);
        else
            Assert.NotEqual(retained.QuantityRounding, catalog.QuantityRounding);
        Assert.Throws<InvalidOperationException>(() => OrderCommercialFactsValidation.RequireValid(truncated));
    }

    // Mirrors the production commit sequence exactly: external current authority is
    // resolved first, then the pinned comparison consumes that decision.
    private static async Task<bool> CompatibleAsync(OrderCommercialCommitGuard guard, TenantContext context, OrderDraftSnapshot order)
    {
        var authority = await guard.ResolveCurrentAuthorityAsync(context, order, default);
        return await guard.IsCompatibleAsync(context, order, authority, default);
    }

    [Theory]
    [InlineData(CustomerIndividualAvailability.Inactive, false)]
    [InlineData(CustomerIndividualAvailability.Active, true)]
    public async Task InactiveOrRedirectedCanonicalCustomerIsABusinessRejectionNotAnInternalFault(
        CustomerIndividualAvailability availability, bool redirected)
    {
        // An individual made inactive, or forwarded to a survivor, is an ordinary current
        // business state. It must reach the caller as the same pricing/customer-invalid
        // business outcome the sibling pricing context check uses, never as an
        // InvalidOperationException that a host maps to HTTP 500.
        var fixture = await Fixture.CreateAsync();
        var directory = new CanonicalCustomers(fixture.Context, Guid.NewGuid())
        {
            Availability = availability,
            RedirectTargetIndividualId = redirected ? Guid.NewGuid() : null,
        };
        var app = new CatalogOrderDraftApplication(fixture.Orders, fixture.Orders, new(fixture.Catalog),
            fixture.PricingApplication, new(new CustomersPort()), directory);
        var request = fixture.Request with { CustomerId = Guid.NewGuid() };
        var error = await Assert.ThrowsAsync<OrderCommercialSelectionException>(() =>
            app.CreateAsync(fixture.Context, request, "canonical", new(false), default));
        Assert.Equal("pricing_customer_invalid", error.Code);
        Assert.Equal(1, directory.Reads);
        Assert.Null(fixture.Orders.Current);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ImpossibleCanonicalDirectoryFactsStillFailAsInternalFaults(bool emptyIdentity)
    {
        // Ownership and identity of a returned survivor are not business states: a foreign
        // tenant or an empty identity can only mean the directory itself is inconsistent.
        var fixture = await Fixture.CreateAsync();
        var directory = new CanonicalCustomers(fixture.Context, emptyIdentity ? Guid.Empty : Guid.NewGuid())
        {
            ReportedTenantId = emptyIdentity ? fixture.Context.TenantId : Guid.NewGuid(),
        };
        var app = new CatalogOrderDraftApplication(fixture.Orders, fixture.Orders, new(fixture.Catalog),
            fixture.PricingApplication, new(new CustomersPort()), directory);
        var request = fixture.Request with { CustomerId = Guid.NewGuid() };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.CreateAsync(fixture.Context, request, "canonical", new(false), default));
        Assert.Equal(1, directory.Reads);
        Assert.Null(fixture.Orders.Current);
    }

    private sealed class Fixture
    {
        internal static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        internal required TenantContext Context { get; init; }
        internal required PriceRevision Price { get; init; }
        internal required CatalogOrderDraftRequest Request { get; init; }
        internal required CatalogPort Catalog { get; init; }
        internal required PricingPort Pricing { get; init; }
        internal required PricingApplication PricingApplication { get; init; }
        internal required CatalogOrderDraftApplication Application { get; init; }
        internal OrderPort Orders { get; init; } = new();
        internal static async Task<Fixture> CreateAsync(bool elevatedOverride = false)
        {
            var context = (await new ResolveTenantContext(new Membership()).ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), default))!;
            var item = Guid.NewGuid(); var unit = Guid.NewGuid();
            var catalog = new CatalogPort
            {
                Facts = new(item, "ITEM", unit, "Original catalog name", "EA", "Each", 2, 4, 1, 1,
                    new(unit, unit, 1, 1m, 1m), QuantityArithmetic.Version1, QuantityArithmetic.Version1Rounding,
                    "EA", "Each", 4, 1, 2m)
            };
            var price = new PriceRevision(context.TenantId, Guid.NewGuid(), 1, new(item, "EA", "USD", PriceScope.Default(), unit),
                1.2345m, new(Now.AddDays(-1)), PricePublicationState.Published, context.AccountId, Now.AddDays(-1), Now.AddDays(-1));
            var pricing = new PricingPort(context); pricing.Candidates.Add(price);
            var app = new PricingApplication(pricing, new(pricing), pricing, pricing, new Clock());
            var orders = new OrderPort();
            return new()
            {
                Context = context,
                Price = price,
                Catalog = catalog,
                Pricing = pricing,
                PricingApplication = app,
                Request = new("Catalog order", "USD", [new(item, unit, 2m)]),
                Orders = orders,
                Application = new(orders, orders, new(catalog), app, new(new CustomersPort()),
                    currentPricingAuthority: elevatedOverride ? new Authority(new OrderPricingAuthority(true), true) : null)
            };
        }
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Fixture.Now; }
    private sealed class Membership : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Task.FromResult(true);
    }
    private sealed class Authority(OrderPricingAuthority? current = null, bool canBeyond = false) : IOrderPricingAuthorityReader
    {
        public Task<OrderPricingAuthority> ReadAsync(TenantContext context, CancellationToken ct) =>
            Task.FromResult(current ?? new OrderPricingAuthority(false));
        public Task<bool> CanOverrideBeyondPolicyAsync(TenantContext context, CancellationToken ct) =>
            Task.FromResult(canBeyond);
    }
    private sealed class CanonicalCustomers(TenantContext context, Guid canonical) : ICustomerCanonicalDirectory
    {
        internal Guid CurrentId { get; set; } = canonical;
        internal Guid ReportedTenantId { get; set; } = context.TenantId;
        internal CustomerIndividualAvailability Availability { get; set; } = CustomerIndividualAvailability.Active;
        // Remaining CustomerIndividualSnapshot members keep their declared defaults.
        internal Guid? RedirectTargetIndividualId { get; set; }
        internal int Reads { get; private set; }
        public Task<CustomerIndividualSnapshot?> ResolveCurrentCustomerAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult<CustomerIndividualSnapshot?>(new(CurrentId, ReportedTenantId, "Canonical", null, null,
                Availability, 1, context.AccountId, Fixture.Now, null, null, RedirectTargetIndividualId: RedirectTargetIndividualId));
        }
    }
    private sealed class OrderPort : IOrderDraftStore, IOrderDraftReceiptReader
    {
        private readonly Dictionary<string, (string Fingerprint, OrderDraftSnapshot Order)> _creates = [];
        private readonly Dictionary<string, (string Fingerprint, OrderDraftSnapshot Order)> _revisions = [];
        internal OrderDraftSnapshot? Current { get; private set; }
        public Task<CreateOrderDraftResult?> FindCreateReceiptAsync(TenantContext context, string key, string fingerprint, CancellationToken ct) =>
            Task.FromResult(_creates.TryGetValue(key, out var entry) ? new CreateOrderDraftResult(entry.Fingerprint == fingerprint ?
                CreateOrderDraftStatus.Replayed : CreateOrderDraftStatus.IdempotencyKeyConflict, entry.Fingerprint == fingerprint ? entry.Order : null) : null);
        public Task<ReviseOrderDraftResult?> FindRevisionReceiptAsync(TenantContext context, string key, string fingerprint, CancellationToken ct) =>
            Task.FromResult(_revisions.TryGetValue(key, out var entry) ? new ReviseOrderDraftResult(entry.Fingerprint == fingerprint ?
                ReviseOrderDraftStatus.Replayed : ReviseOrderDraftStatus.IdempotencyKeyConflict, entry.Fingerprint == fingerprint ? entry.Order : null) : null);
        public Task<CreateOrderDraftResult> CreateAsync(TenantContext context, OrderDraftIntent intent, string key, CancellationToken ct)
        {
            Current = new(Guid.NewGuid(), context.TenantId, context.AccountId, intent.Summary, intent.CurrencyCode, intent.Total,
                1, Fixture.Now, intent.Lines, CustomerContext: intent.CustomerContext);
            _creates.Add(key, (intent.Fingerprint, Current)); return Task.FromResult(new CreateOrderDraftResult(CreateOrderDraftStatus.Created, Current));
        }
        public Task<ReviseOrderDraftResult> ReviseAsync(TenantContext context, ReviseOrderDraftRequest request, OrderDraftIntent intent, string key, string fingerprint, CancellationToken ct)
        {
            Current = Current! with { Summary = intent.Summary, Lines = intent.Lines, Total = intent.Total, Revision = Current!.Revision + 1 };
            _revisions.Add(key, (fingerprint, Current)); return Task.FromResult(new ReviseOrderDraftResult(ReviseOrderDraftStatus.Revised, Current));
        }
        public Task<OrderDraftSnapshot?> FindAsync(TenantContext context, Guid id, CancellationToken ct) => Task.FromResult(Current);
        public Task<OrderDraftPage> ListAsync(TenantContext context, ListOrderDraftsRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<AbandonOrderDraftResult> AbandonAsync(TenantContext context, AbandonOrderDraftRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
        public Task<CommitOrderDraftResult> CommitAsync(TenantContext context, CommitOrderDraftRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class PricingPort(TenantContext context) : IPricingPublicationStore, IPricingCandidateReader, IPricingPolicyStore, IPricingReferenceReader
    {
        internal List<PriceRevision> Candidates { get; } = [];
        internal PricingOverridePolicy Policy { get; set; } = new(1, 0, 100);
        internal string UnitCode { get; set; } = "EA";
        public Task<IReadOnlyList<PriceRevision>> GetPublishedCandidatesAsync(PricingActorContext actor, PriceLookupRequest request, CancellationToken ct) => Task.FromResult<IReadOnlyList<PriceRevision>>(Candidates.ToArray());
        public Task<PricingPolicySnapshot?> GetCurrentPolicyAsync(PricingActorContext actor, CancellationToken ct) => Task.FromResult<PricingPolicySnapshot?>(new(context.TenantId, Policy, context.AccountId, Fixture.Now));
        public Task<string> RequireCompatibleUnitAsync(PricingActorContext actor, Guid item, Guid unit, long? conversion, CancellationToken ct) => Task.FromResult(UnitCode);
        public Task RequireContextAsync(PricingActorContext actor, PriceSelectionContext priceContext, CancellationToken ct) => Task.CompletedTask;
        public Task<PriceRevision?> FindAsync(PricingActorContext actor, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<CreatePriceDraftResult> CreateDraftAsync(PricingActorContext actor, CreatePriceDraftRequest request, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<PublishPriceResult> PublishAsync(PricingActorContext actor, PublishPriceRequest request, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<RetirePriceResult> RetireAsync(PricingActorContext actor, RetirePriceRequest request, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PriceRevision>> ListRevisionsAsync(PricingActorContext actor, PriceLookupRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<PublishPricingPolicyResult> PublishPolicyAsync(PricingActorContext actor, PublishPricingPolicyRequest request, string key, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class CustomersPort : ICustomerStore
    {
        public Task<CustomerOrderContext?> ResolveOrderContextAsync(TenantContext context, Guid org, Guid? program, CancellationToken ct) => Task.FromResult<CustomerOrderContext?>(new(org, program));
        public Task<CreateCustomerOrganizationResult> CreateOrganizationAsync(TenantContext context, CustomerOrganizationIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<CreateCustomerProgramResult> CreateProgramAsync(TenantContext context, CustomerProgramIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<CustomerOrganizationSnapshot?> FindOrganizationAsync(TenantContext context, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<CustomerProgramSnapshot?> FindProgramAsync(TenantContext context, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<CustomerOrganizationPage> ListOrganizationsAsync(TenantContext context, ListCustomerOrganizationsRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<CustomerProgramPage> ListProgramsAsync(TenantContext context, ListCustomerProgramsRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class CatalogPort : ICatalogStore
    {
        internal CatalogLineFacts? Facts { get; set; }
        internal int Reads { get; private set; }
        public Task<CatalogLineFactsResult> SelectLineFactsAsync(TenantContext context, CatalogLineSelection selection, CancellationToken ct)
        { Reads++; return Task.FromResult(new CatalogLineFactsResult(Facts is null ? CatalogLineFactsStatus.ItemRetired : CatalogLineFactsStatus.Available, Facts)); }
        public Task<CreateCatalogUnitResult> CreateUnitAsync(TenantContext context, CatalogUnitIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<CreateCatalogItemResult> CreateItemAsync(TenantContext context, CatalogItemIntent intent, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<CatalogUnitSnapshot?> FindUnitAsync(TenantContext context, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<CatalogItemSnapshot?> FindItemAsync(TenantContext context, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<CatalogUnitPage> ListUnitsAsync(TenantContext context, ListCatalogUnitsRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<CatalogItemPage> ListItemsAsync(TenantContext context, ListCatalogItemsRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<RenameCatalogUnitResult> RenameUnitAsync(TenantContext context, RenameCatalogUnitRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
        public Task<RenameCatalogItemResult> RenameItemAsync(TenantContext context, RenameCatalogItemRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
        public Task<RetireCatalogUnitResult> RetireUnitAsync(TenantContext context, RetireCatalogUnitRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
        public Task<RetireCatalogItemResult> RetireItemAsync(TenantContext context, RetireCatalogItemRequest request, string key, string fingerprint, CancellationToken ct) => throw new NotSupportedException();
    }
}
