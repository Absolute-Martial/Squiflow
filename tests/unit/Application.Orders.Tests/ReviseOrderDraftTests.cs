using Application.Customers;
using Application.Orders;
using Application.Tenancy;
using Xunit;

namespace Application.Orders.Tests;

public sealed class ReviseOrderDraftTests
{
    [Fact]
    public async Task EquivalentReplacementHasStableFingerprintAndCarriesCalculatedIntent()
    {
        var store = new CapturingStore();
        var operation = new ReviseOrderDraft(store, new ResolveCustomerOrderContext(new CustomerStore()));
        var tenant = await ContextAsync();
        var orderId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var request = Request(orderId);

        await operation.ExecuteAsync(tenant, request, " revise-1 ", cancellation.Token);
        var fingerprint = store.Fingerprint;
        Assert.Equal("revise-1", store.Key);
        Assert.Same(tenant, store.Tenant);
        Assert.Equal(cancellation.Token, store.CancellationToken);
        Assert.Equal("Replacement", store.Intent?.Summary);
        Assert.Equal("USD", store.Intent?.CurrencyCode);
        Assert.Equal(5m, store.Intent?.Total);
        Assert.Equal(1, store.Intent?.Lines[0].Position);
        Assert.Matches("^[0-9a-f]{64}$", fingerprint!);

        await operation.ExecuteAsync(tenant, request with
        {
            Summary = "Replacement",
            CurrencyCode = "USD",
        }, "revise-1", cancellation.Token);
        Assert.Equal(fingerprint, store.Fingerprint);

        await operation.ExecuteAsync(tenant, request with { ExpectedRevision = 2 }, "revise-1", cancellation.Token);
        Assert.NotEqual(fingerprint, store.Fingerprint);
        await operation.ExecuteAsync(tenant, request with { Summary = "Other" }, "revise-1", cancellation.Token);
        Assert.NotEqual(fingerprint, store.Fingerprint);
        await operation.ExecuteAsync(tenant, request with { OrderId = Guid.NewGuid() }, "revise-1", cancellation.Token);
        Assert.NotEqual(fingerprint, store.Fingerprint);
    }

    [Theory]
    [InlineData(0, "expected_revision_invalid")]
    [InlineData(-1, "expected_revision_invalid")]
    public async Task InvalidRevisionNeverCallsStore(long revision, string code)
    {
        var store = new CapturingStore();
        var operation = new ReviseOrderDraft(store, new ResolveCustomerOrderContext(new CustomerStore()));
        var tenant = await ContextAsync();
        var error = await Assert.ThrowsAsync<OrderDraftValidationException>(() => operation.ExecuteAsync(
            tenant, Request(Guid.NewGuid()) with { ExpectedRevision = revision },
            "revise-1", CancellationToken.None));
        Assert.Equal(code, error.Code);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task InvalidOrderKeyAndContentNeverCallStore()
    {
        var store = new CapturingStore();
        var operation = new ReviseOrderDraft(store, new ResolveCustomerOrderContext(new CustomerStore()));
        var tenant = await ContextAsync();
        var invalidOrder = await Assert.ThrowsAsync<OrderDraftValidationException>(() =>
            operation.ExecuteAsync(tenant, Request(Guid.Empty), "revise-1", CancellationToken.None));
        var invalidKey = await Assert.ThrowsAsync<OrderDraftValidationException>(() =>
            operation.ExecuteAsync(tenant, Request(Guid.NewGuid()), "key\uD800", CancellationToken.None));
        var invalidLines = await Assert.ThrowsAsync<OrderDraftValidationException>(() =>
            operation.ExecuteAsync(tenant, Request(Guid.NewGuid()) with { Lines = [] },
                "revise-1", CancellationToken.None));

        Assert.Equal("order_id_invalid", invalidOrder.Code);
        Assert.Equal("idempotency_key_invalid", invalidKey.Code);
        Assert.Equal("lines_invalid", invalidLines.Code);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task MissingOrWrongParentContextRejectsRevisionBeforePersistence()
    {
        var store = new CapturingStore();
        var customers = new CustomerStore();
        var operation = new ReviseOrderDraft(store, new ResolveCustomerOrderContext(customers));
        var tenant = await ContextAsync();
        var context = new CustomerOrderContext(Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<CustomerOrderContextNotFoundException>(() => operation.ExecuteAsync(
            tenant, Request(Guid.NewGuid()) with { CustomerContext = context },
            "revise-1", CancellationToken.None));
        Assert.Equal(context, customers.SeenContext);
        Assert.Equal(tenant.TenantId, customers.SeenTenantId);
        Assert.False(store.WasCalled);

        customers.Resolved = context;
        await operation.ExecuteAsync(tenant, Request(Guid.NewGuid()) with { CustomerContext = context },
            "revise-1", CancellationToken.None);
        Assert.Equal(context, store.Intent?.CustomerContext);
    }

    [Theory]
    [InlineData(OrderDraftState.Draft, 1, 1, ReviseOrderDraftStatus.Revised)]
    [InlineData(OrderDraftState.Draft, 2, 1, ReviseOrderDraftStatus.RevisionConflict)]
    [InlineData(OrderDraftState.Abandoned, 2, 1, ReviseOrderDraftStatus.AlreadyAbandoned)]
    [InlineData(OrderDraftState.Abandoned, 2, 2, ReviseOrderDraftStatus.AlreadyAbandoned)]
    public void OnlyCurrentDraftRevisionCanBeReplaced(
        OrderDraftState state, long currentRevision, long expectedRevision,
        ReviseOrderDraftStatus expected) =>
        Assert.Equal(expected, OrderDraftLifecycle.AssessRevise(state, currentRevision, expectedRevision));

    private static ReviseOrderDraftRequest Request(Guid orderId) => new(
        orderId, 1, " Replacement ", " usd ",
        [new OrderDraftLineInput("Print", 2m, "ea", 2.5m)]);

    private static async Task<TenantContext> ContextAsync() =>
        (await new ResolveTenantContext(new ActiveMembershipDirectory())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None))!;

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
            Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(
            Guid accountId, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class CapturingStore : IOrderDraftStore
    {
        public bool WasCalled { get; private set; }
        public TenantContext? Tenant { get; private set; }
        public OrderDraftIntent? Intent { get; private set; }
        public string? Key { get; private set; }
        public string? Fingerprint { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<ReviseOrderDraftResult> ReviseAsync(
            TenantContext tenantContext, ReviseOrderDraftRequest request, OrderDraftIntent intent,
            string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
        {
            WasCalled = true;
            Tenant = tenantContext;
            Intent = intent;
            Key = idempotencyKey;
            Fingerprint = fingerprint;
            CancellationToken = cancellationToken;
            return Task.FromResult(new ReviseOrderDraftResult(ReviseOrderDraftStatus.NotFound, null));
        }

        public Task<CreateOrderDraftResult> CreateAsync(
            TenantContext tenantContext, OrderDraftIntent intent, string idempotencyKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDraftSnapshot?> FindAsync(
            TenantContext tenantContext, Guid orderId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDraftPage> ListAsync(
            TenantContext tenantContext, ListOrderDraftsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AbandonOrderDraftResult> AbandonAsync(
            TenantContext tenantContext, AbandonOrderDraftRequest request,
            string idempotencyKey, string fingerprint,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CustomerStore : ICustomerStore
    {
        public CustomerOrderContext? Resolved { get; set; }
        public CustomerOrderContext? SeenContext { get; private set; }
        public Guid SeenTenantId { get; private set; }
        public Task<CustomerOrderContext?> ResolveOrderContextAsync(
            TenantContext tenantContext, Guid organizationId, Guid? programId,
            CancellationToken cancellationToken)
        {
            SeenTenantId = tenantContext.TenantId;
            SeenContext = new CustomerOrderContext(organizationId, programId);
            return Task.FromResult(Resolved);
        }

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
    }
}
