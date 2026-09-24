using Application.Customers;
using Application.Orders;
using Application.Tenancy;
using Xunit;

namespace Application.Orders.Tests;

public sealed class OrderDraftIntentTests
{
    [Fact]
    public void CreateTrimsAndNormalizesInputWhileCalculatingDeterministicLineAndOrderTotals()
    {
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            "  Counter order  ",
            " usd ",
            [
                new OrderDraftLineInput("  Poster printing  ", 2.5m, " ea ", 12.3456m),
                new OrderDraftLineInput("  Design work  ", 3m, " hr ", 1.25m),
            ]));

        Assert.Equal("Counter order", intent.Summary);
        Assert.Equal("USD", intent.CurrencyCode);
        Assert.Equal(30.864m, intent.Lines[0].LineTotal);
        Assert.Equal(3.75m, intent.Lines[1].LineTotal);
        Assert.Equal(34.614m, intent.Total);
        Assert.Equal("Poster printing", intent.Lines[0].Description);
        Assert.Equal("EA", intent.Lines[0].UnitCode);
        Assert.Equal("Design work", intent.Lines[1].Description);
        Assert.Equal("HR", intent.Lines[1].UnitCode);
    }

    [Theory]
    [MemberData(nameof(InvalidDrafts))]
    public void CreateRejectsInvalidDraftInputWithAStableValidationCode(
        CreateOrderDraftRequest request,
        string expectedCode)
    {
        var exception = Assert.Throws<OrderDraftValidationException>(() => OrderDraftIntent.Create(request));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public void CreateProducesTheSameFingerprintForEquivalentNormalizedInputAndChangesItForChangedIntent()
    {
        var normalized = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            "Counter order",
            "USD",
            [new OrderDraftLineInput("Poster printing", 2.5m, "EA", 12.3456m)]));
        var equivalent = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            "  Counter order  ",
            " usd ",
            [new OrderDraftLineInput("  Poster printing  ", 2.5m, " ea ", 12.3456m)]));
        var changed = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            "Counter order",
            "USD",
            [new OrderDraftLineInput("Poster printing", 2.5m, "EA", 12.3455m)]));

        Assert.Equal(normalized.Fingerprint, equivalent.Fingerprint);
        Assert.NotEqual(normalized.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void CreateRejectsEveryNegativeUnitPriceBeforeItCanAffectLineOrOrderTotals()
    {
        var exception = Assert.Throws<OrderDraftValidationException>(() => OrderDraftIntent.Create(
            new CreateOrderDraftRequest(
                "Counter order",
                "USD",
                [
                    new OrderDraftLineInput("Positive line", 1m, "EA", 100m),
                    new OrderDraftLineInput("Negative line", 1m, "EA", -0.0001m),
                ])));

        Assert.Equal("unit_price_invalid", exception.Code);
    }

    [Fact]
    public void CreateAllowsAZeroPricedLine()
    {
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            "Complimentary sample",
            "USD",
            [new OrderDraftLineInput("Sample", 1m, "EA", 0m)]));

        Assert.Equal(0m, intent.Lines[0].UnitPrice);
        Assert.Equal(0m, intent.Lines[0].LineTotal);
        Assert.Equal(0m, intent.Total);
    }

    [Fact]
    public void CreateRoundsLineTotalsToFourPlacesUsingMidpointToEven()
    {
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            "Precise order",
            "USD",
            [new OrderDraftLineInput("Precise line", 0.5m, "EA", 2.4689m)]));

        Assert.Equal(1.2344m, intent.Lines[0].LineTotal);
        Assert.Equal(1.2344m, intent.Total);
    }

    [Theory]
    [MemberData(nameof(MalformedUtf16Drafts))]
    public void CreateRejectsMalformedUtf16BeforeFingerprinting(
        CreateOrderDraftRequest request,
        string expectedCode)
    {
        var exception = Assert.Throws<OrderDraftValidationException>(() => OrderDraftIntent.Create(request));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public async Task ExecuteRejectsMalformedUtf16IdempotencyKeyBeforeCallingTheStore()
    {
        var store = new FailingOrderDraftStore();
        var operation = new CreateOrderDraft(store, new ResolveCustomerOrderContext(new FailingCustomerStore()));
        var context = await new ResolveTenantContext(new ActiveMembershipDirectory())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        var exception = Assert.Throws<OrderDraftValidationException>(() => operation.ExecuteAsync(
                context!,
                new CreateOrderDraftRequest(
                    "Counter order",
                    "USD",
                    [new OrderDraftLineInput("Poster", 1m, "EA", 1m)]),
                "key\uD800",
                CancellationToken.None)
            .GetAwaiter()
            .GetResult());

        Assert.Equal("idempotency_key_invalid", exception.Code);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public void CapabilityRemainsHostProviderAuthorizationAndContainerNeutral()
    {
        var references = typeof(OrderDraftIntent).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("OpenFga", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.StartsWith("Autofac", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name =>
            name.StartsWith("Application.", StringComparison.Ordinal) &&
            name is not ("Application.Tenancy" or "Application.Customers"));
    }

    public static TheoryData<CreateOrderDraftRequest, string> InvalidDrafts => new()
    {
        {
            new CreateOrderDraftRequest("Counter order", "USD", []),
            "lines_invalid"
        },
        {
            new CreateOrderDraftRequest(
                "Counter order",
                "USD",
                Enumerable.Range(1, 101)
                    .Select(index => new OrderDraftLineInput($"Line {index}", 1m, "EA", 1m))
                    .ToArray()),
            "lines_invalid"
        },
        {
            new CreateOrderDraftRequest(
                "Counter order",
                "USD",
                [new OrderDraftLineInput("Poster printing", 1.00001m, "EA", 1m)]),
            "quantity_invalid"
        },
        {
            new CreateOrderDraftRequest(
                "Counter order",
                "USD",
                [new OrderDraftLineInput("Poster printing", -1m, "EA", 1m)]),
            "quantity_invalid"
        },
        {
            new CreateOrderDraftRequest(
                "Counter order",
                "USD",
                [new OrderDraftLineInput("Poster printing", 1m, "EA", -0.0001m)]),
            "unit_price_invalid"
        },
    };

    public static TheoryData<CreateOrderDraftRequest, string> MalformedUtf16Drafts => new()
    {
        {
            new CreateOrderDraftRequest("Counter \uD800order", "USD", [new OrderDraftLineInput("Poster", 1m, "EA", 1m)]),
            "summary_invalid"
        },
        {
            new CreateOrderDraftRequest("Counter order", "USD", [new OrderDraftLineInput("Poster \uDC00", 1m, "EA", 1m)]),
            "line_description_invalid"
        },
        {
            new CreateOrderDraftRequest("Counter order", "US\uD800", [new OrderDraftLineInput("Poster", 1m, "EA", 1m)]),
            "currency_code_invalid"
        },
        {
            new CreateOrderDraftRequest("Counter order", "USD", [new OrderDraftLineInput("Poster", 1m, "E\uD800", 1m)]),
            "unit_code_invalid"
        },
    };

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
            Guid accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(
            Guid accountId,
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FailingOrderDraftStore : IOrderDraftStore
    {
        public Task<ReviseOrderDraftResult> ReviseAsync(
            TenantContext tenantContext, ReviseOrderDraftRequest request, OrderDraftIntent intent,
            string idempotencyKey, string fingerprint, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public bool WasCalled { get; private set; }

        public Task<CreateOrderDraftResult> CreateAsync(
            TenantContext tenantContext,
            OrderDraftIntent intent,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("The store should not be called for malformed input.");
        }

        public Task<OrderDraftSnapshot?> FindAsync(
            TenantContext tenantContext,
            Guid orderId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OrderDraftPage> ListAsync(
            TenantContext tenantContext,
            ListOrderDraftsRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AbandonOrderDraftResult> AbandonAsync(
            TenantContext tenantContext,
            AbandonOrderDraftRequest request,
            string idempotencyKey,
            string fingerprint,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FailingCustomerStore : ICustomerStore
    {
        public Task<CreateCustomerOrganizationResult> CreateOrganizationAsync(
            TenantContext tenantContext, CustomerOrganizationIntent intent, string idempotencyKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CreateCustomerProgramResult> CreateProgramAsync(
            TenantContext tenantContext, CustomerProgramIntent intent, string idempotencyKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerOrganizationSnapshot?> FindOrganizationAsync(
            TenantContext tenantContext, Guid organizationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerProgramSnapshot?> FindProgramAsync(
            TenantContext tenantContext, Guid programId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerOrganizationPage> ListOrganizationsAsync(
            TenantContext tenantContext, ListCustomerOrganizationsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerProgramPage> ListProgramsAsync(
            TenantContext tenantContext, ListCustomerProgramsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerOrderContext?> ResolveOrderContextAsync(
            TenantContext tenantContext, Guid organizationId, Guid? programId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
